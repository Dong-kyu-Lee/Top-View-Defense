using TopViewDefense.Combat;
using TopViewDefense.Core;
using TopViewDefense.Core.Audio;
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
    /// 발사는 히트스캔(명중 즉시 <see cref="IDamageable.TakeDamage"/>). 종류별 페이로드는 데이터로 가른다:
    /// 단일 타격(기본/더블=shotsPerFire), 광역 피해·감속·도트(프리즈/파이어=areaRadius·slowMultiplier·
    /// dotPerSecond), 공격 없이 에너지 생산(에너지=energyPerCycle). 발사 연출(총구/탄체/임팩트)은
    /// 데미지와 분리된 순수 표시 계층으로, <see cref="TurretData"/>의 VFX 프리팹을 <see cref="VfxPool"/>로
    /// 풀링해 재생한다(탄체 <see cref="TurretProjectile"/>는 이미 확정된 피격을 그리기만 한다).
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
        private float _cellSize;
        private float _cooldown;
        private Enemy _target;
        private bool _initialized;

        // 영구 강화가 반영된 유효 수치(배치 시점 스냅샷). base 에셋(Data)은 불변으로 두고 여기서만 강화를 얹는다.
        private float _damage;
        private float _fireInterval;
        private int _energyPerCycle;

        // 생산형(에너지): 타겟 없이 주기마다 에너지를 생산한다.
        private bool IsProducer => Data != null && Data.energyPerCycle > 0;

        // 공격/효과형: 피해·감속·도트 중 하나라도 실으면 조준·발사 대상.
        private bool HasPayload => Data != null &&
            (Data.damage > 0f || Data.slowMultiplier < 1f || Data.dotPerSecond > 0f);

        /// <summary>배치 직후 호출. 데이터/셀/셀 크기를 주입한다.</summary>
        public void Init(TurretData data, Vector2Int cell, float cellSize)
        {
            Data = data;
            Cell = cell;
            _cellSize = cellSize;
            _worldRange = data != null ? data.range * cellSize : 0f;

            // 상점 영구 강화를 반영한 유효 수치를 배치 시점에 스냅샷한다(CLAUDE.md 7장 ①).
            _damage = TurretUpgrades.EffectiveDamage(data);
            _fireInterval = TurretUpgrades.EffectiveInterval(data);
            _energyPerCycle = TurretUpgrades.EffectiveEnergyPerCycle(data);

            _cooldown = 0f;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || Data == null) return;

            // 생산형(에너지)은 타겟 없이 주기마다 생산만 한다.
            if (IsProducer)
            {
                TickProduction();
                return;
            }

            // 공격/효과가 없는 터렛은 조준/발사하지 않는다.
            if (!HasPayload) return;

            AcquireOrValidateTarget();

            if (_target != null)
                FaceTarget();

            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f && _target != null)
            {
                Fire();
                _cooldown = _fireInterval;
            }
        }

        // 주기(fireInterval)마다 에너지를 생산해 지갑에 넣는다(공격 대신). 개수 제한은 TurretPlacer가 관리.
        private void TickProduction()
        {
            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;
            _cooldown = _fireInterval;

            if (PlayerEconomy.Instance != null)
                PlayerEconomy.Instance.Add(_energyPerCycle);
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

        // 히트스캔 발사. 광역 터렛(프리즈/파이어)은 최근접 적 '위치'에 AoE를 터뜨리고(CLAUDE.md 5장),
        // 단일 터렛(기본/더블)은 타겟에게 shotsPerFire만큼 즉시 타격한다. 연출은 데미지 뒤에 별도로 얹는다.
        private void Fire()
        {
            // VFX 파라미터는 데미지 적용 전에 스냅샷한다(피격으로 타겟이 파괴돼도 목적지/방향이 남도록).
            Enemy tgt = _target;
            Transform targetTf = tgt != null ? tgt.transform : null;
            Vector3 destination = tgt != null ? tgt.Position : Position + transform.forward * _worldRange;

            if (Data.areaRadius > 0f)
            {
                FireArea(destination);
                SpawnFireVfx(destination, targetTf, 1);                             // 광역: 한 발이 중심으로 날아가 폭발.
            }
            else
            {
                FireSingle();
                SpawnFireVfx(destination, targetTf, Mathf.Max(1, Data.shotsPerFire)); // 단일: 타격 수만큼 탄체.
            }
        }

        // 단일 타겟: shotsPerFire만큼 즉시 타격(더블 터렛 등은 데이터만으로 표현).
        private void FireSingle()
        {
            int shots = Mathf.Max(1, Data.shotsPerFire);
            for (int i = 0; i < shots; i++)
            {
                if (_target == null || _target.IsDead) break;
                _target.TakeDamage(_damage, Data.damageType);
            }
        }

        // 광역: center 반경 안 모든 적에게 피해/감속/도트를 데이터에 실린 만큼 적용한다.
        private void FireArea(Vector3 center)
        {
            EnemyManager mgr = EnemyManager.Instance;
            if (mgr == null) return;

            float r = Data.areaRadius * _cellSize;
            float r2 = r * r;
            var enemies = mgr.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e == null || e.IsDead) continue;
                if ((e.Position - center).sqrMagnitude > r2) continue;

                if (Data.damage > 0f) e.TakeDamage(_damage, Data.damageType);
                if (Data.slowMultiplier < 1f) e.ApplySlow(Data.slowMultiplier, Data.effectDuration);
                if (Data.dotPerSecond > 0f) e.ApplyDoT(Data.dotPerSecond, Data.effectDuration, Data.damageType);
            }
        }

        // ---------------------------------------------------------------- 연출(VFX)
        // 데미지와 분리된 순수 표시 계층. 여기서는 어떤 피격도 주지 않는다(이미 Fire에서 확정).

        // 총구 월드 지점(터렛 포즈 + 데이터의 로컬 오프셋). 조준으로 이미 타겟을 향하므로 forward가 발사 방향.
        private Vector3 MuzzlePoint() => transform.position + transform.rotation * Data.muzzleLocalOffset;

        // 발사 연출: 총구 플래시 + 탄체(있으면), 탄체가 없으면 목적지에 즉시 임팩트(순수 히트스캔 표현).
        private void SpawnFireVfx(Vector3 destination, Transform targetTf, int count)
        {
            Vector3 muzzle = MuzzlePoint();

            AudioManager.PlaySfx(Data.fireSfx); // 발사음(2D 원샷). 다연장이라도 발사당 1회.

            if (Data.muzzlePrefab != null)
                VfxPool.PlayOneShot(Data.muzzlePrefab, muzzle, transform.rotation);

            if (Data.projectilePrefab == null)
            {
                if (Data.impactPrefab != null)
                    VfxPool.PlayOneShot(Data.impactPrefab, destination, transform.rotation);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                Vector3 start = muzzle + BarrelOffset(i, count);
                GameObject go = VfxPool.Get(Data.projectilePrefab, start, transform.rotation);
                TurretProjectile shot = go.GetComponent<TurretProjectile>();
                if (shot == null) shot = go.AddComponent<TurretProjectile>();
                shot.Launch(start, targetTf, destination, Data.projectileSpeed, Data.impactPrefab);
            }
        }

        // 다연장(더블 등) 시 탄체를 좌우로 살짝 벌려 여러 발이 보이게 한다(순수 연출).
        private Vector3 BarrelOffset(int index, int count)
        {
            if (count <= 1) return Vector3.zero;
            const float spacing = 0.15f;
            float t = index - (count - 1) * 0.5f;
            return transform.right * (t * spacing);
        }
    }
}
