using System;
using TopViewDefense.Combat;
using TopViewDefense.Map;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 적 유닛 런타임 컴포넌트. 흐름장(<see cref="Pathfinder"/>)을 따라 기지로 이동하고,
    /// 체력·피격·사망·기지 도달을 처리한다. 종류별 차이는 <see cref="EnemyData"/>가 주입한다.
    ///
    /// 이동 원리(문서 4.7 흐름장):
    /// - 매 프레임 자기 월드 위치를 셀 좌표로 변환(WorldToGrid) → 그 셀의 "다음 셀"(TryGetNextStep)로 이동.
    /// - 셀 좌표는 월드에 고정되어 있고 회전은 '타일 내용'만 순열하므로, 위치→셀 변환만으로
    ///   회전/장애물 파괴 후 재계산된 흐름장을 자동으로 따라간다(적 쪽에 별도 처리 불필요).
    ///
    /// 결합 최소화: 기지/경제와 직접 엮이지 않고 <see cref="OnReachedBase"/>/<see cref="OnDied"/>
    /// 이벤트만 발행한다. 배선은 <see cref="EnemyManager"/>가 담당.
    /// </summary>
    [DisallowMultipleComponent]
    public class Enemy : MonoBehaviour, IDamageable
    {
        /// <summary>기지 도달 시(피격을 입히기 직전). 인자는 자신.</summary>
        public event Action<Enemy> OnReachedBase;

        /// <summary>체력이 0이 되어 사망 시(보상 드랍 지점). 인자는 자신.</summary>
        public event Action<Enemy> OnDied;

        [Tooltip("셀 중심으로부터 이 거리 안이면 도착으로 간주(연출용, 로직은 셀 단위).")]
        [SerializeField] private float faceTurnSpeed = 12f;

        public EnemyData Data { get; private set; }
        public float CurrentHp { get; private set; }
        public bool IsDead { get; private set; }
        public Vector3 Position => transform.position;

        /// <summary>남은 보호막 충전 수. 1 이상이면 다음 피격 1회를 완전히 무시한다.</summary>
        public int ShieldCharges { get; private set; }

        // 프리즈 감속 상태(외부에서 부여받는 피동 상태 — AddShield와 같은 계층).
        private float _slowMultiplier = 1f; // 1 = 감속 없음.
        private float _slowUntil;           // Time.time 기준 만료 시각.

        // 파이어 도트 상태.
        private float _dotPerSecond;
        private float _dotUntil;            // Time.time 기준 만료 시각.
        private float _dotTickTimer;        // 다음 초당 틱까지 남은 시간.
        private DamageType _dotType = DamageType.Fire;

        private GridState _grid;
        private Pathfinder _pathfinder;
        private Vector2Int _baseCell;
        private bool _initialized;
        private bool _finished; // 도달/사망 후 이중 처리 방지

        /// <summary>
        /// 스포너가 스폰 직후 호출. 그리드/경로탐색/데이터를 주입하고 체력을 초기화한다.
        /// </summary>
        public void Init(GridState grid, Pathfinder pathfinder, EnemyData data, Vector2Int spawnCell)
        {
            _grid = grid;
            _pathfinder = pathfinder;
            Data = data;
            _baseCell = grid.BaseCell;
            CurrentHp = data != null ? data.maxHp : 1f;
            ShieldCharges = 0;
            _slowMultiplier = 1f;
            _slowUntil = 0f;
            _dotPerSecond = 0f;
            _dotUntil = 0f;
            IsDead = false;
            _finished = false;

            // 스폰 셀 중심(XZ)에 정렬. 높이는 프리팹이 배치한 값을 유지.
            Vector3 w = grid.GridToWorld(spawnCell);
            Vector3 p = transform.position;
            transform.position = new Vector3(w.x, p.y, w.z);

            // 부착된 능력(EnemyAbility) 초기화. 종류별 특수 행동의 확장점.
            var abilities = GetComponents<EnemyAbility>();
            for (int i = 0; i < abilities.Length; i++)
                abilities[i].Initialize(this);

            _initialized = true;
        }

        /// <summary>보호막을 부여한다(공병의 아우라 등). 이미 있으면 충전 수를 더 큰 값으로 갱신.</summary>
        public void AddShield(int charges)
        {
            if (charges <= 0 || IsDead || _finished) return;
            ShieldCharges = Mathf.Max(ShieldCharges, charges);
        }

        /// <summary>
        /// 이동 속도 감속을 부여한다(프리즈 터렛). 더 강한 감속(작은 배수)을 채택하고 지속 시간을 갱신한다.
        /// 상태는 적이 소유해 스스로 만료 처리하므로(<see cref="Update"/>), 터렛은 한 번 스탬프만 찍는다.
        /// </summary>
        public void ApplySlow(float multiplier, float duration)
        {
            if (IsDead || _finished || multiplier >= 1f || duration <= 0f) return;
            _slowMultiplier = Mathf.Min(_slowMultiplier, Mathf.Max(0f, multiplier));
            _slowUntil = Mathf.Max(_slowUntil, Time.time + duration);
        }

        /// <summary>
        /// 초당 지속 피해(도트)를 부여한다(파이어 터렛). 더 강한 도트를 채택하고 지속 시간을 갱신한다.
        /// 도트 틱은 <b>방어력을 무시</b>하되 속성 내성(공병의 화염 내성 등)은 적용된다(<see cref="Update"/>).
        /// </summary>
        public void ApplyDoT(float dps, float duration, DamageType type = DamageType.Fire)
        {
            if (IsDead || _finished || dps <= 0f || duration <= 0f) return;
            _dotPerSecond = Mathf.Max(_dotPerSecond, dps);
            _dotUntil = Mathf.Max(_dotUntil, Time.time + duration);
            _dotType = type;
        }

        private void Update()
        {
            if (!_initialized || _finished) return;
            if (_grid == null || _pathfinder == null) return;

            TickDoT();
            if (_finished) return; // 도트로 사망했으면 이동 처리 중단.

            Vector3 pos = transform.position;
            Vector2Int cell = _grid.WorldToGrid(pos);

            // 기지 도달.
            if (cell == _baseCell)
            {
                ReachBase();
                return;
            }

            // 다음 셀로 이동. 경로가 없으면(완전 봉쇄) 대기 — 장애물 파괴는 이후 페이즈(Phase 5).
            if (!_pathfinder.TryGetNextStep(cell, out Vector2Int next))
                return;

            Vector3 target = _grid.GridToWorld(next);
            target.y = pos.y; // 수평 이동만.

            float slow = Time.time < _slowUntil ? _slowMultiplier : 1f;
            float worldSpeed = Data.moveSpeed * _grid.CellSize * slow;
            transform.position = Vector3.MoveTowards(pos, target, worldSpeed * Time.deltaTime);

            FaceTowards(target - pos);
        }

        // 도트 상태가 살아 있으면 1초 주기로 피해를 준다. 방어력은 무시하고 속성 내성만 적용.
        private void TickDoT()
        {
            if (_dotPerSecond <= 0f || Time.time >= _dotUntil) return;

            _dotTickTimer -= Time.deltaTime;
            if (_dotTickTimer > 0f) return;
            _dotTickTimer = 1f;

            ApplyDamage(_dotPerSecond, _dotType, ignoreArmor: true, ignoreShield: true);
        }

        private void FaceTowards(Vector3 dir)
        {
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;
            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, faceTurnSpeed * Time.deltaTime);
        }

        // ---------------------------------------------------------------- 피격/사망

        public void TakeDamage(float amount, DamageType type = DamageType.Physical)
            => ApplyDamage(amount, type, ignoreArmor: false, ignoreShield: false);

        // 피격 처리의 단일 경로. 도트 틱은 방어력/보호막을 무시(ignore*=true)하되 속성 내성은 공유한다.
        private void ApplyDamage(float amount, DamageType type, bool ignoreArmor, bool ignoreShield)
        {
            if (IsDead || _finished) return;

            // 1) 보호막: 남아 있으면 이번 피격 1회를 완전히 무시하고 충전 1 소모.
            if (!ignoreShield && ShieldCharges > 0)
            {
                ShieldCharges--;
                return;
            }

            // 2) 방어력 정액 감소(최소 1 보장해 무한 탱킹 방지). 도트는 방어력을 무시한다.
            float armor = (ignoreArmor || Data == null) ? 0f : Data.armor;
            float dealt = ignoreArmor ? amount : Mathf.Max(1f, amount - armor);

            // 3) 속성 내성: 해당 속성이면 배수를 곱한다(0=완전 면역까지 허용).
            if (Data != null && Data.hasResistance && type == Data.resistantType)
                dealt *= Data.resistanceMultiplier;

            if (dealt <= 0f) return;

            CurrentHp -= dealt;
            if (CurrentHp <= 0f)
                Die();
        }

        private void Die()
        {
            if (_finished) return;
            _finished = true;
            IsDead = true;
            OnDied?.Invoke(this); // EnemyManager가 보상 드랍/집계 후 파괴
        }

        private void ReachBase()
        {
            if (_finished) return;
            _finished = true;
            OnReachedBase?.Invoke(this); // BaseCore가 목숨 감소, EnemyManager가 파괴
        }
    }
}
