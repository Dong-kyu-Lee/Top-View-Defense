using System;
using TopViewDefense.Enemies;
using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 정중앙 기지의 목숨/승패를 관리한다. (claude.md 2장)
    /// - 기본 목숨 3. 적이 도달할 때마다 적의 damageToBase만큼 감소.
    /// - 0이 되면 게임 오버(<see cref="OnGameOver"/>) → 이후 스테이지 선택 씬 전환이 구독.
    /// - 클리어 시 남은 목숨으로 별점 산정(3=별3, 1피격=별2, 2피격=별1).
    ///
    /// 적 도달은 <see cref="EnemyManager.OnEnemyReachedBase"/>를 구독해서 받는다(적과 직접 결합하지 않음).
    /// </summary>
    [DisallowMultipleComponent]
    public class BaseCore : MonoBehaviour
    {
        [Tooltip("적 도달 이벤트 소스. 비우면 Instance 또는 씬에서 탐색.")]
        [SerializeField] private EnemyManager enemyManager;

        [Tooltip("기지 기본 목숨.")]
        [Min(1)] [SerializeField] private int maxLives = 3;

        /// <summary>목숨이 변할 때(현재 목숨, 최대 목숨).</summary>
        public event Action<int, int> OnLivesChanged;

        /// <summary>목숨이 0이 되어 게임 오버된 시점.</summary>
        public event Action OnGameOver;

        public int MaxLives => maxLives;
        public int CurrentLives { get; private set; }
        public bool IsGameOver { get; private set; }

        /// <summary>현재 목숨 기준 별점(3/2/1). 게임 오버면 0.</summary>
        public int StarRating => Mathf.Clamp(CurrentLives, 0, 3);

        private bool _subscribed;

        private void Awake()
        {
            CurrentLives = maxLives;
            IsGameOver = false;
        }

        private void OnEnable() => TrySubscribe();

        private void Start() => TrySubscribe(); // EnemyManager가 아직 없었을 경우 대비.

        private void OnDisable()
        {
            if (_subscribed && enemyManager != null)
                enemyManager.OnEnemyReachedBase -= HandleEnemyReachedBase;
            _subscribed = false;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            if (enemyManager == null)
                enemyManager = EnemyManager.Instance ?? FindObjectOfType<EnemyManager>();
            if (enemyManager == null) return; // Start에서 다시 시도.

            enemyManager.OnEnemyReachedBase += HandleEnemyReachedBase;
            _subscribed = true;
        }

        private void HandleEnemyReachedBase(Enemy enemy)
        {
            int dmg = enemy != null && enemy.Data != null ? enemy.Data.damageToBase : 1;
            ApplyDamage(dmg);
        }

        /// <summary>기지에 피해를 준다(목숨 감소). 목숨이 0이 되면 게임 오버.</summary>
        public void ApplyDamage(int amount)
        {
            if (IsGameOver || amount <= 0) return;

            CurrentLives = Mathf.Max(0, CurrentLives - amount);
            OnLivesChanged?.Invoke(CurrentLives, maxLives);

            if (CurrentLives == 0)
            {
                IsGameOver = true;
                OnGameOver?.Invoke();
                Debug.Log("[BaseCore] 게임 오버 — 기지 목숨 소진.", this);
            }
        }
    }
}
