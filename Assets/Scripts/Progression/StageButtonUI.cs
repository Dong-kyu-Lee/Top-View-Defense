using TopViewDefense.Core;
using TopViewDefense.Map;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TopViewDefense.Progression
{
    /// <summary>
    /// 스테이지 선택 그리드의 버튼 1칸. 자물쇠/별/클릭 3상태를 표시한다. (CLAUDE.md 2장)
    ///
    /// <see cref="StageSelectController"/>가 <see cref="Bind"/>로 스테이지를 주입한다.
    /// | 상태 | 자물쇠 | 클릭 | 별 |
    /// | 미해금 | ON | 불가 | 숨김 |
    /// | 해금·미클리어 | OFF | 가능 | 숨김 |
    /// | 클리어 | OFF | 가능 | 획득 수만큼 점등 |
    /// </summary>
    public class StageButtonUI : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("이 칸의 버튼. 미해금이면 interactable=false.")]
        [SerializeField] private Button button;

        [Tooltip("미해금일 때 덮는 자물쇠 오버레이.")]
        [SerializeField] private GameObject lockOverlay;

        [Tooltip("별 아이콘 3개. 획득 개수만큼 앞에서부터 켜진다.")]
        [SerializeField] private GameObject[] starIcons = new GameObject[3];

        [Tooltip("스테이지 번호/이름 라벨(선택).")]
        [SerializeField] private TMP_Text label;

        private StageData _stage;
        private string _playScene = "PlayScene";

        /// <summary>스테이지를 주입하고 저장된 진행 상태로 표시를 갱신한다.</summary>
        public void Bind(StageData stage, string playScene)
        {
            _stage = stage;
            _playScene = playScene;

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
                button.onClick.AddListener(OnClick);
            }
            Refresh();
        }

        /// <summary>저장소를 다시 읽어 3상태를 반영한다(씬 재진입 시 최신화).</summary>
        public void Refresh()
        {
            if (_stage == null) return;

            int number = _stage.stageNumber;
            bool unlocked = PlayerProgress.IsUnlocked(number);
            int stars = PlayerProgress.GetStars(number);

            if (label != null) label.text = number.ToString();
            if (button != null) button.interactable = unlocked;
            if (lockOverlay != null) lockOverlay.SetActive(!unlocked);

            // 별은 해금·클리어(stars>0)일 때만 노출. 미해금/미클리어는 전부 숨김.
            if (starIcons != null)
                for (int i = 0; i < starIcons.Length; i++)
                    if (starIcons[i] != null) starIcons[i].SetActive(unlocked && i < stars);
        }

        private void OnClick()
        {
            if (_stage == null || !PlayerProgress.IsUnlocked(_stage.stageNumber)) return;
            StageSession.SelectedStage = _stage;
            SceneManager.LoadScene(_playScene);
        }

        private void OnDestroy()
        {
            if (button != null) button.onClick.RemoveListener(OnClick);
        }
    }
}
