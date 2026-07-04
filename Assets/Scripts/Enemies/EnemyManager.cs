using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 활성 적의 레지스트리이자 이벤트 허브. 씬에 하나 두고 스포너/터렛/기지가 공유한다.
    ///
    /// 역할:
    /// - 스폰된 적을 등록/해제하고 <see cref="Enemies"/> 목록을 유지.
    /// - 터렛의 "가장 가까운 적" 타게팅을 <see cref="FindNearest"/>로 제공(claude.md 5장 터렛들이 곧 사용).
    /// - 적의 도달/사망을 받아 정리하고, 상위 시스템에 <see cref="OnEnemyReachedBase"/>/<see cref="OnEnemyKilled"/>로 알림
    ///   (BaseCore=목숨 감소, 경제 시스템=보상 드랍이 이후 구독).
    ///
    /// 터렛 등 여러 곳에서 간편히 접근하도록 가벼운 <see cref="Instance"/>를 제공한다.
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyManager : MonoBehaviour
    {
        public static EnemyManager Instance { get; private set; }

        /// <summary>적이 기지에 도달함(파괴 직전). BaseCore가 구독해 목숨을 깎는다.</summary>
        public event Action<Enemy> OnEnemyReachedBase;

        /// <summary>적이 처치됨(파괴 직전). 경제 시스템이 구독해 보상을 드랍한다.</summary>
        public event Action<Enemy> OnEnemyKilled;

        private readonly List<Enemy> _enemies = new List<Enemy>();

        /// <summary>현재 살아있는 적 목록(읽기 전용). 터렛 타게팅 등에서 순회.</summary>
        public IReadOnlyList<Enemy> Enemies => _enemies;

        /// <summary>현재 활성 적 수.</summary>
        public int Count => _enemies.Count;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[EnemyManager] 인스턴스가 이미 존재합니다. 중복을 제거합니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>스포너가 스폰 직후 호출. 적 이벤트를 구독하고 목록에 등록한다.</summary>
        public void Register(Enemy enemy)
        {
            if (enemy == null || _enemies.Contains(enemy)) return;

            _enemies.Add(enemy);
            enemy.OnReachedBase += HandleReachedBase;
            enemy.OnDied += HandleDied;
        }

        private void HandleReachedBase(Enemy enemy)
        {
            OnEnemyReachedBase?.Invoke(enemy);
            Despawn(enemy);
        }

        private void HandleDied(Enemy enemy)
        {
            OnEnemyKilled?.Invoke(enemy);
            Despawn(enemy);
        }

        private void Despawn(Enemy enemy)
        {
            if (enemy == null) return;

            enemy.OnReachedBase -= HandleReachedBase;
            enemy.OnDied -= HandleDied;
            _enemies.Remove(enemy);

            if (enemy.gameObject != null)
                Destroy(enemy.gameObject);
        }

        /// <summary>
        /// pos에서 maxRange 이내 가장 가까운 살아있는 적. 없으면 null. (터렛 타게팅용)
        /// </summary>
        public Enemy FindNearest(Vector3 pos, float maxRange = Mathf.Infinity)
        {
            Enemy best = null;
            float bestSqr = maxRange == Mathf.Infinity ? Mathf.Infinity : maxRange * maxRange;

            for (int i = 0; i < _enemies.Count; i++)
            {
                Enemy e = _enemies[i];
                if (e == null || e.IsDead) continue;

                float sqr = (e.Position - pos).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = e;
                }
            }
            return best;
        }
    }
}
