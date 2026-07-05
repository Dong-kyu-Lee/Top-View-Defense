using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 일시정지 메뉴 UI. <see cref="PauseController.OnPauseChanged"/>를 구독해 패널을 토글한다. (그룹 C)
    ///
    /// <see cref="GameResultUI"/>와 동일하게, 이 컨트롤러는 항상 켜진 오브젝트에 두고 자식 <c>panel</c>만
    /// 토글한다(자기 자신을 끄면 구독이 끊긴다). 씬 전환(재시도/나가기)은 <see cref="GameManager"/> 메서드를
    /// 재사용하며, 해당 메서드가 로드 직전 timeScale=1을 복구하므로 추가 처리가 없다.
    /// </summary>
    public class PauseMenuUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("정지 소스. 비우면 씬에서 탐색.")]
        [SerializeField] private PauseController pauseController;

        [Tooltip("씬 전환 소스. 비우면 Instance/씬에서 탐색.")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("설정 하위 패널(선택).")]
        [SerializeField] private SettingsPanelUI settingsPanel;

        [Header("표시")]
        [Tooltip("일시정지 패널 루트(자식). 이 컴포넌트는 항상 켜진 오브젝트에 둔다.")]
        [SerializeField] private GameObject panel;

        [Header("버튼")]
        [Tooltip("계속하기(선택).")]
        [SerializeField] private Button resumeButton;

        [Tooltip("재시도(선택).")]
        [SerializeField] private Button retryButton;

        [Tooltip("스테이지 나가기(선택).")]
        [SerializeField] private Button stageSelectButton;

        [Tooltip("설정 열기(선택).")]
        [SerializeField] private Button settingsButton;

        private void Awake()
        {
            if (pauseController == null) pauseController = FindObjectOfType<PauseController>();
            if (gameManager == null) gameManager = GameManager.Instance ?? FindObjectOfType<GameManager>();

            if (resumeButton != null) resumeButton.onClick.AddListener(OnResume);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
            if (stageSelectButton != null) stageSelectButton.onClick.AddListener(OnStageSelect);
            if (settingsButton != null) settingsButton.onClick.AddListener(OnSettings);

            if (panel == null)
                Debug.LogWarning("[PauseMenuUI] panel이 지정되지 않았습니다. 일시정지 패널 루트를 자식으로 지정하세요.", this);
            SetPanelActive(false);
        }

        private void Start()
        {
            if (pauseController != null) pauseController.OnPauseChanged += HandlePauseChanged;
        }

        private void OnDestroy()
        {
            if (pauseController != null) pauseController.OnPauseChanged -= HandlePauseChanged;
            if (resumeButton != null) resumeButton.onClick.RemoveListener(OnResume);
            if (retryButton != null) retryButton.onClick.RemoveListener(OnRetry);
            if (stageSelectButton != null) stageSelectButton.onClick.RemoveListener(OnStageSelect);
            if (settingsButton != null) settingsButton.onClick.RemoveListener(OnSettings);
        }

        private void HandlePauseChanged(bool paused)
        {
            SetPanelActive(paused);
            if (!paused && settingsPanel != null) settingsPanel.Close(); // 재개 시 설정 하위 패널도 닫는다.
        }

        private void SetPanelActive(bool on)
        {
            if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        }

        private void OnResume() { if (pauseController != null) pauseController.Resume(); }
        private void OnRetry() { if (gameManager != null) gameManager.RetryStage(); }
        private void OnStageSelect() { if (gameManager != null) gameManager.GoToStageSelect(); }
        private void OnSettings() { if (settingsPanel != null) settingsPanel.Open(); }
    }
}
