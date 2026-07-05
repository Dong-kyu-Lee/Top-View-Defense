using System.Collections;
using TMPro;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 웨이브 시작/클리어 순간을 화면 중앙에 잠깐 띄웠다 사라지게 하는 배너.
    /// (CLAUDE.md 6장 - 웨이브 진행의 가시적 피드백)
    ///
    /// <see cref="WaveRunner"/>의 <see cref="WaveRunner.OnWaveStarted"/>/<see cref="WaveRunner.OnWaveCleared"/>를
    /// 구독해 <see cref="CanvasGroup"/> 알파를 페이드인→유지→페이드아웃한다.
    ///
    /// - 컨트롤러는 항상 켜진 오브젝트에 두고 자식 CanvasGroup만 페이드로 토글한다
    ///   (GameResultUI/PauseMenuUI와 동일 규약 — 자기 자신을 끄면 구독이 끊긴다).
    /// - Start에서 구독(Awake 순서 안전) / OnDestroy에서 해제(LivesHudUI와 동일).
    /// - 페이드는 Time.unscaledDeltaTime으로 돌려 일시정지(timeScale=0)나 마지막 웨이브 클리어 후
    ///   GameManager의 timeScale=0에도 배너가 얼지 않고 정상적으로 사라진다.
    /// </summary>
    public class WaveBannerUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("웨이브 진행 소유자. 비우면 씬에서 탐색.")]
        [SerializeField] private WaveRunner waveRunner;

        [Tooltip("배너 배경+텍스트를 묶는 CanvasGroup. 초기 alpha=0 권장, Interactable/BlocksRaycasts는 꺼둘 것.")]
        [SerializeField] private CanvasGroup group;

        [Tooltip("배너 문구 텍스트.")]
        [SerializeField] private TMP_Text label;

        [Header("문구")]
        [Tooltip("{0}에 웨이브 번호(1-based)가 들어간다.")]
        [SerializeField] private string startFormat = "Wave {0} Start";
        [SerializeField] private string clearFormat = "Wave {0} Clear";

        [Header("타이밍(초)")]
        [SerializeField] private float fadeIn = 0.3f;
        [SerializeField] private float hold = 1.0f;
        [SerializeField] private float fadeOut = 0.5f;

        [Tooltip("마지막 웨이브 클리어는 결과 화면과 겹치므로 배너를 띄우지 않는다.")]
        [SerializeField] private bool suppressFinalClear = true;

        private Coroutine _playing;

        private void Start()
        {
            if (waveRunner == null) waveRunner = FindObjectOfType<WaveRunner>();
            if (group != null) group.alpha = 0f;

            if (waveRunner != null)
            {
                waveRunner.OnWaveStarted += HandleStarted;
                waveRunner.OnWaveCleared += HandleCleared;
            }
        }

        private void OnDestroy()
        {
            if (waveRunner != null)
            {
                waveRunner.OnWaveStarted -= HandleStarted;
                waveRunner.OnWaveCleared -= HandleCleared;
            }
        }

        private void HandleStarted(int index) => Show(string.Format(startFormat, index + 1));

        private void HandleCleared(int index)
        {
            // 마지막 웨이브 클리어는 결과 화면과 겹치므로 건너뛴다(옵션).
            if (suppressFinalClear && index >= waveRunner.TotalWaves - 1) return;
            Show(string.Format(clearFormat, index + 1));
        }

        // 진행 중인 배너가 있으면 끊고 새 문구로 다시 재생.
        private void Show(string text)
        {
            if (group == null) return;
            if (label != null) label.text = text;
            if (_playing != null) StopCoroutine(_playing);
            _playing = StartCoroutine(Play());
        }

        private IEnumerator Play()
        {
            yield return Fade(0f, 1f, fadeIn);

            float t = 0f;
            while (t < hold) { t += Time.unscaledDeltaTime; yield return null; }

            yield return Fade(1f, 0f, fadeOut);
            _playing = null;
        }

        private IEnumerator Fade(float from, float to, float dur)
        {
            if (dur <= 0f) { group.alpha = to; yield break; }

            float t = 0f;
            while (t < dur)
            {
                t += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            group.alpha = to;
        }
    }
}
