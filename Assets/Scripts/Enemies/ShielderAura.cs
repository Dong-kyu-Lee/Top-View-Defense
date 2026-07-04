using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 공병(실드병)의 능동 능력. (claude.md 4장 - "주변 적들에게 1회성 공격 무시 보호막을 부여")
    ///
    /// 일정 주기마다 반경 내 살아있는 적들에게 <see cref="Enemy.AddShield"/>로 보호막을 부여한다.
    /// 보호막은 다음 피격 1회를 완전히 무시하고 소모된다(Enemy.TakeDamage에서 처리).
    /// </summary>
    public class ShielderAura : EnemyAbility
    {
        [Tooltip("보호막을 부여하는 반경(월드 단위).")]
        [Min(0f)] [SerializeField] private float radius = 3f;

        [Tooltip("보호막 부여 주기(초).")]
        [Min(0.1f)] [SerializeField] private float interval = 2f;

        [Tooltip("한 번에 부여하는 보호막 충전 수(보통 1 = 1회 무시).")]
        [Min(1)] [SerializeField] private int shieldCharges = 1;

        [Tooltip("자기 자신에게도 보호막을 부여할지.")]
        [SerializeField] private bool shieldSelf = true;

        private float _timer;

        private void Update()
        {
            if (Owner == null || Owner.IsDead) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = interval;

            Pulse();
        }

        private void Pulse()
        {
            EnemyManager mgr = EnemyManager.Instance;
            if (mgr == null) return;

            float r2 = radius * radius;
            Vector3 center = Owner.Position;

            var enemies = mgr.Enemies;
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e == null || e.IsDead) continue;
                if (!shieldSelf && e == Owner) continue;

                if ((e.Position - center).sqrMagnitude <= r2)
                    e.AddShield(shieldCharges);
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.4f);
            Gizmos.DrawWireSphere(transform.position, radius);
        }
#endif
    }
}
