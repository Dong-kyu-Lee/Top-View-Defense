using TopViewDefense.Enemies;
using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 적 처치 보상 드랍 훅. <see cref="EnemyManager.OnEnemyKilled"/>를 <b>구독만</b> 해
    /// 처치 시 재화를 지급한다. (CLAUDE.md 7장 - 적 처치 시 에너지·골드 드랍)
    ///
    /// 두 재화의 성격이 달라 처리 방식도 다르다:
    /// - <b>에너지</b>(스테이지 한정 전술 재화): 처치 즉시 <see cref="PlayerEconomy.Add"/>로 지갑에 넣는다.
    /// - <b>골드</b>(영구 성장 재화): 세션 카운터에 <b>누적만</b> 하고, 스테이지를 <b>클리어했을 때만</b>
    ///   <see cref="PlayerProgress.AddGold"/>로 확정한다. 패배하면 누적분은 폐기된다
    ///   (CLAUDE.md 7장 "클리어 실패 시 보상 없음"과 일관). 클리어 시 별 보상(<see cref="Progression"/>)과
    ///   자연스럽게 합류한다 — 둘 다 <see cref="GameManager.OnGameEnded"/>==Cleared에서 확정.
    ///
    /// EnemyManager/Enemy를 오염시키지 않으려 별도 컴포넌트로 분리한다(BaseCore와 동일 철학).
    /// PlayScene에 하나 얹는다(EnemyManager 오브젝트에 부착해도 됨).
    /// </summary>
    [DisallowMultipleComponent]
    public class EnemyRewardDropper : MonoBehaviour
    {
        [Header("참조 (비우면 Instance 또는 씬에서 탐색)")]
        [Tooltip("처치 이벤트 소스.")]
        [SerializeField] private EnemyManager enemyManager;

        [Tooltip("에너지 지갑.")]
        [SerializeField] private PlayerEconomy economy;

        [Tooltip("종료 이벤트 소스(골드 뱅킹 시점).")]
        [SerializeField] private GameManager gameManager;

        /// <summary>이번 스테이지에서 처치로 누적한 골드(클리어 시에만 확정, 패배 시 폐기).</summary>
        public int PendingGold { get; private set; }

        private bool _subscribed;

        private void OnEnable() => TrySubscribe();

        private void Start() => TrySubscribe(); // 소스가 아직 없었을 경우 대비.

        private void OnDisable() => Unsubscribe();

        private void TrySubscribe()
        {
            if (_subscribed) return;

            if (enemyManager == null)
                enemyManager = EnemyManager.Instance ?? FindObjectOfType<EnemyManager>();
            if (economy == null)
                economy = PlayerEconomy.Instance ?? FindObjectOfType<PlayerEconomy>();
            if (gameManager == null)
                gameManager = GameManager.Instance ?? FindObjectOfType<GameManager>();

            // 세 소스가 모두 준비돼야 구독 성립. 하나라도 없으면 Start에서 다시 시도.
            if (enemyManager == null || economy == null || gameManager == null) return;

            enemyManager.OnEnemyKilled += HandleKilled;
            gameManager.OnGameEnded += HandleGameEnded;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (enemyManager != null) enemyManager.OnEnemyKilled -= HandleKilled;
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
            _subscribed = false;
        }

        // 처치 순간: 에너지는 즉시 지급, 골드는 세션에 누적만.
        private void HandleKilled(Enemy enemy)
        {
            if (enemy == null || enemy.Data == null) return;

            economy.Add(enemy.Data.energyDrop);
            PendingGold += Mathf.Max(0, enemy.Data.goldDrop);
        }

        // 클리어 시에만 누적 골드를 영구 확정. 패배(GameOver)면 폐기.
        private void HandleGameEnded(GameResult result, int stars)
        {
            if (result != GameResult.Cleared || PendingGold <= 0) return;

            PlayerProgress.AddGold(PendingGold);
            Debug.Log($"[EnemyRewardDropper] 처치 골드 +{PendingGold} 확정(클리어).", this);
            PendingGold = 0;
        }
    }
}
