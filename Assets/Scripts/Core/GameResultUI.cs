using TMPro;
using TopViewDefense.Core.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 게임 종료(승리/패배) 결과 화면. (CLAUDE.md 2장 - 별 3/2/1 평가, 게임 오버)
    ///
    /// <see cref="GameManager.OnGameEnded"/>를 구독해 결과 패널을 띄우고 별점을 표시한다.
    /// 버튼은 GameManager의 씬 전환 메서드로 자가 배선한다. timeScale=0 중에도 uGUI 버튼은 동작한다.
    /// </summary>
    public class GameResultUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("종료 이벤트 소스. 비우면 씬에서 탐색.")]
        [SerializeField] private GameManager gameManager;

        [Header("표시")]
        [Tooltip("결과 패널 루트(종료 전까지 비활성). 이 컨트롤러는 항상 켜진 오브젝트에 두고, "
               + "토글 대상 패널을 자식으로 지정한다. 비우면 자기 자식 중 첫 오브젝트를 찾지 않고 경고만 낸다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("타이틀 텍스트(승리/패배).")]
        [SerializeField] private TMP_Text titleText;

        [SerializeField] private string clearTitle = "STAGE CLEAR";
        [SerializeField] private string gameOverTitle = "GAME OVER";

        [Tooltip("별 아이콘들(선택). 획득 별 수만큼 앞에서부터 켜진다.")]
        [SerializeField] private GameObject[] starIcons;

        [Header("버튼")]
        [Tooltip("재시도 버튼(선택).")]
        [SerializeField] private Button retryButton;

        [Tooltip("스테이지 선택 버튼(선택).")]
        [SerializeField] private Button stageSelectButton;

        private void Awake()
        {
            if (gameManager == null) gameManager = FindObjectOfType<GameManager>();
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (stageSelectButton != null) stageSelectButton.onClick.AddListener(OnStageSelect);

            if (panel == null)
                Debug.LogWarning("[GameResultUI] panel이 지정되지 않았습니다. 결과 패널 루트를 자식으로 지정하세요.", this);
            SetPanelActive(false);
        }

        private void Start()
        {
            if (gameManager != null) gameManager.OnGameEnded += HandleGameEnded;
        }

        private void OnDestroy()
        {
            if (gameManager != null) gameManager.OnGameEnded -= HandleGameEnded;
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            if (stageSelectButton != null) stageSelectButton.onClick.RemoveListener(OnStageSelect);
        }

        private void HandleGameEnded(GameResult result, int stars)
        {
            SetPanelActive(true);

            // 결과음(오디오는 timeScale=0 영향을 받지 않아 종료 직후에도 재생된다).
            if (result == GameResult.Cleared) AudioManager.PlayGameClear();
            else                              AudioManager.PlayGameOver();

            if (titleText != null)
                titleText.text = result == GameResult.Cleared ? clearTitle : gameOverTitle;

            if (starIcons != null)
                for (int i = 0; i < starIcons.Length; i++)
                    if (starIcons[i] != null) starIcons[i].SetActive(i < stars);
        }

        private void SetPanelActive(bool on)
        {
            // 자기 자신은 토글하지 않는다(비활성화되면 Start/이벤트 구독이 끊겨 결과가 영영 안 뜸).
            if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        }

        private void OnRetry()
        {
            if (gameManager != null) gameManager.RetryStage();
        }

        private void OnStageSelect()
        {
            if (gameManager != null) gameManager.GoToStageSelect();
        }
    }
}
