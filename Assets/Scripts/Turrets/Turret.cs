using TopViewDefense.Combat;
using TopViewDefense.Enemies;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 아군 터렛 런타임 컴포넌트. 배치된 셀에 <b>Position은 고정</b>되고, 사거리 안 가장 가까운 적을 향해
    /// <b>Rotation만</b> 돌리며 주기적으로 발사한다. 종류별 차이는 <see cref="TurretData"/>가 주입한다
    /// (<see cref="Enemies.EnemyData"/>/<see cref="Enemies.Enemy"/>와 동일 철학).
    ///
    /// 결합 최소화: 타게팅은 <see cref="EnemyManager.FindNearest"/>만 참조한다(적 시스템과 느슨히 결합).
    ///
    /// 회전 대응(중요): 터렛을 자기 셀 '타일 오브젝트의 자식'으로 배치하면, RotationScheduler가 회전 시
    /// 타일 오브젝트를 피벗 아래로 모아 돌릴 때 자식 터렛의 <b>위치·바라보는 방향이 함께 실려</b> 자동
    /// 동반 회전한다(CLAUDE.md 3장). → 터렛 쪽에 회전 대응 코드가 필요 없다(적과 같은 이점).
    ///
    /// 1차: 히트스캔(명중 즉시 <see cref="IDamageable.TakeDamage"/>). 투사체 연출과 광역/감속/도트/에너지
    /// 생산은 이후 페이즈에서 확장한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class Turret : MonoBehaviour
    {
        [Tooltip("타겟을 향해 도는 회전 보간 계수(클수록 즉각적).")]
        [SerializeField] private float faceTurnSpeed = 12f;

        public TurretData Data { get; private set; }

        /// <summary>배치 당시의 셀(디버그/표시용). 회전 후에는 실제 위치와 달라질 수 있다.</summary>
        public Vector2Int Cell { get; private set; }

        public Vector3 Position => transform.position;

        private float _worldRange;
        private float _cooldown;
        private Enemy _target;
        private bool _initialized;

        /// <summary>배치 직후 호출. 데이터/셀/셀 크기를 주입한다.</summary>
        public void Init(TurretData data, Vector2Int cell, float cellSize)
        {
            Data = data;
            Cell = cell;
            _worldRange = data != null ? data.range * cellSize : 0f;
            _cooldown = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || Data == null) return;

            // 공격 기능이 없는 터렛(에너지 등)은 조준/발사하지 않는다.
            if (Data.damage <= 0f) return;

            AcquireOrValidateTarget();

            if (_target != null)
                FaceTarget();

            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f && _target != null)
            {
                Fire();
                _cooldown = Data.fireInterval;
            }
        }

        // 기존 타겟이 죽거나 사거리를 벗어나면 버리고, 없으면 최근접 적을 새로 얻는다.
        private void AcquireOrValidateTarget()
        {
            if (_target != null)
            {
                float sqr = (_target.Position - Position).sqrMagnitude;
                if (_target.IsDead || sqr > _worldRange * _worldRange)
                    _target = null;
            }

            if (_target == null && EnemyManager.Instance != null)
                _target = EnemyManager.Instance.FindNearest(Position, _worldRange);
        }

        // Y축 회전만: Position은 그대로 두고 타겟쪽을 바라보게 한다.
        private void FaceTarget()
        {
            Vector3 dir = _target.Position - Position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f) return;

            Quaternion look = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, look, faceTurnSpeed * Time.deltaTime);
        }

        // 1차: 히트스캔. shotsPerFire만큼 즉시 타격(더블 터렛 등은 데이터만으로 표현).
        private void Fire()
        {
            int shots = Mathf.Max(1, Data.shotsPerFire);
            for (int i = 0; i < shots; i++)
            {
                if (_target == null || _target.IsDead) break;
                _target.TakeDamage(Data.damage, Data.damageType);
            }
        }
    }
}
