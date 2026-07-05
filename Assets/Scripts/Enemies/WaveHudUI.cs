using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 웨이브 진행 상태 HUD 컨트롤러. (CLAUDE.md 6장 - 웨이브 프리뷰/인터벌)
    ///
    /// <see cref="WaveRunner"/>의 공개 상태를 매 프레임 읽어 표시한다(카운트다운은 매 프레임 변하는 값이라
    /// 이벤트보다 폴링이 적합). WaveRunner를 수정하지 않고 읽기 전용 API만 사용한다.
    ///
    /// - 현재/총/남은 웨이브 수 표시 (#2)
    /// - 다음 웨이브 시작까지 남은 시간(텍스트 + 선택적 게이지) (#3)
    /// - 즉시시작 버튼 → <see cref="WaveRunner.SkipRest"/> (#4). 버튼은 정비 중에만 노출.
    /// </summary>
    public class WaveHudUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("웨이브 진행 소유자. 비우면 씬에서 탐색.")]
        [SerializeField] private WaveRunner waveRunner;

        [Header("표시")]
        [Tooltip("현재 웨이브 텍스트. 예) \"웨이브 2 / 10\" 또는 정비 중 \"다음 웨이브 2 / 10\".")]
        [SerializeField] private TMP_Text waveText;

        [Tooltip("남은 웨이브 수 텍스트(선택). 예) \"남은 웨이브 8\".")]
        [SerializeField] private TMP_Text remainingText;

        [Tooltip("정비 카운트다운 텍스트(선택). 예) \"8초\".")]
        [SerializeField] private TMP_Text countdownText;

        [Tooltip("정비 카운트다운 게이지(선택). fillAmount = 남은/전체.")]
        [SerializeField] private Image countdownFill;

        [Header("제어")]
        [Tooltip("즉시시작 버튼(선택). 정비 중에만 노출되며 클릭 시 카운트다운을 건너뛴다.")]
        [SerializeField] private Button skipButton;

        private int _lastShownSecond = int.MinValue;

        private void Awake()
        {
            if (waveRunner == null) waveRunner = FindObjectOfType<WaveRunner>();
            if (skipButton != null) skipButton.onClick.AddListener(OnSkipClicked);
        }

        private void OnDestroy()
        {
            if (skipButton != null) skipButton.onClick.RemoveListener(OnSkipClicked);
        }

        private void Update()
        {
            if (waveRunner == null) return;

            int total = waveRunner.TotalWaves;

            if (waveRunner.IsResting)
            {
                // 정비 중: 다가올 웨이브를 안내하고 카운트다운/즉시시작을 노출.
                int idx = waveRunner.PendingWave;                 // 0-based, 곧 시작할 웨이브
                SetText(waveText, $"Wave {idx + 1} / {total}");
                SetText(remainingText, $"Left Wave {Mathf.Max(0, total - idx)}"); // 이 웨이브 포함(아직 미시작)
                SetCountdownActive(true);
                UpdateCountdown(waveRunner.RestRemaining, waveRunner.RestDuration);
                SetActive(skipButton, true);
            }
            else if (waveRunner.CurrentWave >= 0)
            {
                // 진행 중: 카운트다운/버튼 숨김.
                int idx = waveRunner.CurrentWave;                 // 0-based, 진행 중 웨이브
                SetText(waveText, $"Wave {idx + 1} / {total}");
                SetText(remainingText, $"Left Wave {Mathf.Max(0, total - idx - 1)}"); // 진행 중 제외
                SetCountdownActive(false);
                SetActive(skipButton, false);
            }
            else
            {
                // 시작 전 또는 전체 클리어: 정비/진행 상태 아님 → 카운트다운/버튼 숨김.
                // (전체 클리어 결과 화면은 이후 그룹 B가 담당.)
                SetText(waveText, string.Empty);
                SetText(remainingText, string.Empty);
                SetCountdownActive(false);
                SetActive(skipButton, false);
            }
        }

        private void UpdateCountdown(float remaining, float duration)
        {
            if (countdownFill != null)
                countdownFill.fillAmount = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

            if (countdownText != null)
            {
                // 정수 초가 바뀔 때만 텍스트 갱신(매 프레임 문자열 할당 방지).
                int sec = Mathf.Max(0, Mathf.CeilToInt(remaining));
                if (sec != _lastShownSecond)
                {
                    _lastShownSecond = sec;
                    countdownText.text = $"{sec}s";
                }
            }
        }

        private void SetCountdownActive(bool on)
        {
            if (!on) _lastShownSecond = int.MinValue; // 다음 정비 진입 시 첫 프레임에 강제 갱신되도록.
            SetActive(countdownText, on);
            SetActive(countdownFill, on);
        }

        private void OnSkipClicked()
        {
            if (waveRunner != null) waveRunner.SkipRest();
        }

        private static void SetText(TMP_Text label, string value)
        {
            if (label != null) label.text = value;
        }

        private static void SetActive(Component c, bool on)
        {
            if (c != null && c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
        }
    }
}
