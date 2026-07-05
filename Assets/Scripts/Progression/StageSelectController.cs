using System.Collections.Generic;
using TopViewDefense.Core;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TopViewDefense.Progression
{
    /// <summary>
    /// StageSelectScene의 허브. <see cref="StageCatalog"/>를 돌며 그리드에 <see cref="StageButtonUI"/>를
    /// 스폰하고, 보유 골드 텍스트와 BackButton을 배선한다. (CLAUDE.md 2장·7장 ①)
    ///
    /// 별/골드/해금은 <see cref="PlayerProgress"/>(영구 저장)에서만 읽는다. PlayScene을 클리어하고
    /// 돌아왔을 때 최신 상태가 보이도록 <see cref="OnEnable"/>에서 그리드를 (재)구성한다.
    /// </summary>
    public class StageSelectController : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("스폰할 스테이지 목록/보상 표. 비우면 Resources/StageCatalog에서 로드.")]
        [SerializeField] private StageCatalog catalog;

        [Header("그리드")]
        [Tooltip("GridLayoutGroup이 달린 버튼 컨테이너.")]
        [SerializeField] private Transform gridParent;

        [Tooltip("스폰할 StageButtonUI 프리팹.")]
        [SerializeField] private StageButtonUI buttonPrefab;

        [Header("표시")]
        [Tooltip("보유 골드 텍스트(선택).")]
        [SerializeField] private TMP_Text goldText;

        [Tooltip("골드 표시 포맷. {0}에 골드 값이 들어간다.")]
        [SerializeField] private string goldFormat = "{0}";

        [Header("버튼")]
        [Tooltip("타이틀로 돌아가는 뒤로가기 버튼(선택).")]
        [SerializeField] private Button backButton;

        [Header("씬")]
        [SerializeField] private string playScene = "PlayScene";
        [SerializeField] private string titleScene = "TitleScene";

        private readonly List<StageButtonUI> _spawned = new List<StageButtonUI>();
        private bool _built;

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<StageCatalog>("StageCatalog");
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        private void OnEnable()
        {
            // 씬 로드 및 재진입마다 최신 진행 상태로 갱신.
            BuildGrid();
            RefreshGold();
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBack);
        }

        /// <summary>카탈로그대로 버튼을 (한 번) 스폰하고, 이후엔 각 버튼 Refresh만.</summary>
        private void BuildGrid()
        {
            if (catalog == null || buttonPrefab == null || gridParent == null)
            {
                Debug.LogWarning("[StageSelectController] catalog/buttonPrefab/gridParent 중 비어 있는 참조가 있습니다.", this);
                return;
            }

            if (!_built)
            {
                for (int i = 0; i < catalog.stages.Count; i++)
                {
                    var stage = catalog.stages[i];
                    if (stage == null) continue;
                    var btn = Instantiate(buttonPrefab, gridParent);
                    btn.Bind(stage, playScene);
                    _spawned.Add(btn);
                }
                _built = true;
            }
            else
            {
                foreach (var btn in _spawned)
                    if (btn != null) btn.Refresh();
            }
        }

        private void RefreshGold()
        {
            if (goldText != null) goldText.text = string.Format(goldFormat, PlayerProgress.Gold);
        }

        private void OnBack() => SceneManager.LoadScene(titleScene);
    }
}
