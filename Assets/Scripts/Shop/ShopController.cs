using System.Collections.Generic;
using TopViewDefense.Core;
using TopViewDefense.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TopViewDefense.Shop
{
    /// <summary>
    /// ShopScene의 허브. <see cref="TurretUpgradeCatalog"/>를 돌며 그리드에 <see cref="ShopItemUI"/>를 스폰하고,
    /// 보유 골드 텍스트와 BackButton(→ 타이틀)을 배선한다. (CLAUDE.md 2장 타이틀 [상점]·7장 ①)
    ///
    /// <see cref="Progression.StageSelectController"/>와 같은 패턴: 골드/강화는 <see cref="PlayerProgress"/>에서만
    /// 읽고, <see cref="OnEnable"/>에서 (재)구성해 최신 상태를 반영한다. 구매가 일어나면 골드가 줄어 다른 칸의
    /// '골드 부족' 판정도 바뀌므로, 구매 콜백에서 골드 텍스트와 <b>모든</b> 아이템을 함께 갱신한다.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Header("데이터")]
        [Tooltip("터렛 목록/강화 배수/비용 표. 비우면 Resources/TurretUpgradeCatalog에서 로드.")]
        [SerializeField] private TurretUpgradeCatalog catalog;

        [Header("그리드")]
        [Tooltip("GridLayoutGroup이 달린 아이템 컨테이너.")]
        [SerializeField] private Transform gridParent;

        [Tooltip("스폰할 ShopItemUI 프리팹.")]
        [SerializeField] private ShopItemUI itemPrefab;

        [Header("표시")]
        [Tooltip("보유 골드 텍스트(선택).")]
        [SerializeField] private TMP_Text goldText;

        [Tooltip("골드 표시 포맷. {0}에 골드 값이 들어간다.")]
        [SerializeField] private string goldFormat = "{0}";

        [Header("버튼")]
        [Tooltip("타이틀로 돌아가는 뒤로가기 버튼(선택).")]
        [SerializeField] private Button backButton;

        [Header("씬")]
        [SerializeField] private string titleScene = "TitleScene";

        private readonly List<ShopItemUI> _spawned = new List<ShopItemUI>();
        private bool _built;

        private void Awake()
        {
            if (catalog == null) catalog = Resources.Load<TurretUpgradeCatalog>("TurretUpgradeCatalog");
            if (backButton != null) backButton.onClick.AddListener(OnBack);
        }

        private void OnEnable()
        {
            BuildGrid();
            RefreshGold();
        }

        private void OnDestroy()
        {
            if (backButton != null) backButton.onClick.RemoveListener(OnBack);
        }

        /// <summary>카탈로그대로 아이템을 (한 번) 스폰하고, 이후엔 각 아이템 Refresh만.</summary>
        private void BuildGrid()
        {
            if (catalog == null || itemPrefab == null || gridParent == null)
            {
                Debug.LogWarning("[ShopController] catalog/itemPrefab/gridParent 중 비어 있는 참조가 있습니다.", this);
                return;
            }

            if (!_built)
            {
                for (int i = 0; i < catalog.turrets.Count; i++)
                {
                    var turret = catalog.turrets[i];
                    if (turret == null) continue;
                    var item = Instantiate(itemPrefab, gridParent);
                    item.Bind(turret, catalog, OnPurchased);
                    _spawned.Add(item);
                }
                _built = true;
            }
            else
            {
                RefreshAllItems();
            }
        }

        // 구매 후: 골드 텍스트 + 모든 칸(골드 부족 재판정)을 갱신한다.
        private void OnPurchased()
        {
            RefreshGold();
            RefreshAllItems();
        }

        private void RefreshAllItems()
        {
            foreach (var item in _spawned)
                if (item != null) item.Refresh();
        }

        private void RefreshGold()
        {
            if (goldText != null) goldText.text = string.Format(goldFormat, PlayerProgress.Gold);
        }

        private void OnBack() => SceneManager.LoadScene(titleScene);
    }
}
