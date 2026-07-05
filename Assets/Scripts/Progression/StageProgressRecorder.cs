using TopViewDefense.Core;
using TopViewDefense.Map;
using UnityEngine;

namespace TopViewDefense.Progression
{
    /// <summary>
    /// 스테이지 종료 훅. <see cref="GameManager.OnGameEnded"/>를 <b>구독만</b> 해
    /// 별 최고기록 저장·골드 보상·다음 스테이지 해금(파생)을 처리한다. (CLAUDE.md 2장·7장 ①)
    ///
    /// GameManager/BaseCore를 오염시키지 않으려 별도 컴포넌트로 분리한다(Game-Flow §2 read-only 철학).
    /// PlayScene에 하나 얹는다. UI(StageSelect)보다 먼저 만들면 PlayScene만으로 저장·해금을 검증할 수 있다.
    /// </summary>
    public class StageProgressRecorder : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("종료 이벤트 소스. 비우면 씬에서 탐색.")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("골드 보상 표. 비우면 Resources/StageCatalog에서 로드.")]
        [SerializeField] private StageCatalog catalog;

        [Tooltip("스테이지 번호 폴백 소스(SelectedStage가 없을 때). 비우면 씬에서 탐색.")]
        [SerializeField] private MapBuilder mapBuilder;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (mapBuilder == null) mapBuilder = FindObjectOfType<MapBuilder>();
            if (catalog == null) catalog = Resources.Load<StageCatalog>("StageCatalog");
        }

        private void Start()
        {
            if (gameManager != null) gameManager.OnGameEnded += HandleGameEnded;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
        }

        private void HandleGameEnded(GameResult result, int stars)
        {
            if (result != GameResult.Cleared) return; // 패배는 보상/저장 없음(CLAUDE.md 7장).

            int stageNumber = ResolveStageNumber();
            if (stageNumber <= 0)
            {
                Debug.LogWarning("[StageProgressRecorder] 스테이지 번호를 확인할 수 없어 진행 저장을 건너뜁니다.", this);
                return;
            }

            // 보상 delta는 저장 '이전'의 기존 최고 별로 계산한다(같은 값 이중 지급 방지).
            int previousStars = PlayerProgress.GetStars(stageNumber);
            bool improved = PlayerProgress.SetBestStars(stageNumber, stars); // 별 저장 → 다음 스테이지 해금 파생.

            if (improved && catalog != null)
            {
                int delta = catalog.RewardFor(stars) - catalog.RewardFor(previousStars);
                if (delta > 0)
                {
                    PlayerProgress.AddGold(delta);
                    Debug.Log($"[StageProgressRecorder] Stage {stageNumber} 별 {previousStars}→{stars}, 골드 +{delta}", this);
                }
            }
        }

        /// <summary>현재 플레이 중인 스테이지 번호. SelectedStage 우선, 없으면 MapBuilder의 스테이지.</summary>
        private int ResolveStageNumber()
        {
            if (StageSession.SelectedStage != null) return StageSession.SelectedStage.stageNumber;
            if (mapBuilder != null && mapBuilder.Stage != null) return mapBuilder.Stage.stageNumber;
            return 0;
        }
    }
}
