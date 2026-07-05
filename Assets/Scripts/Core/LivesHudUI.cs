using TMPro;
using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 기지 남은 목숨 HUD. (CLAUDE.md 2장 - 기본 목숨 3, 피격 시 감소)
    ///
    /// <see cref="BaseCore.OnLivesChanged"/>를 구독해 하트 아이콘/텍스트를 갱신한다.
    /// TurretHudUI와 동일하게 Start에서 구독(Awake 순서 안전)하고 OnDestroy에서 해제한다.
    /// </summary>
    public class LivesHudUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("목숨 소스. 비우면 씬에서 탐색.")]
        [SerializeField] private BaseCore baseCore;

        [Header("표시")]
        [Tooltip("목숨 텍스트(선택). 예) \"3 / 3\".")]
        [SerializeField] private TMP_Text livesText;

        [Tooltip("하트 아이콘들(선택). 현재 목숨 수만큼 앞에서부터 켜진다.")]
        [SerializeField] private GameObject[] hearts;

        private void Awake()
        {
            if (baseCore == null) baseCore = FindObjectOfType<BaseCore>();
        }

        private void Start()
        {
            if (baseCore == null) return;
            baseCore.OnLivesChanged += HandleLivesChanged;
            HandleLivesChanged(baseCore.CurrentLives, baseCore.MaxLives);
        }

        private void OnDestroy()
        {
            if (baseCore != null) baseCore.OnLivesChanged -= HandleLivesChanged;
        }

        private void HandleLivesChanged(int current, int max)
        {
            if (livesText != null) livesText.text = $"{current} / {max}";

            if (hearts != null)
                for (int i = 0; i < hearts.Length; i++)
                    if (hearts[i] != null) hearts[i].SetActive(i < current);
        }
    }
}
