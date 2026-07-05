using System;
using TopViewDefense.Core;
using TopViewDefense.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Shop
{
    /// <summary>
    /// 상점 그리드의 아이템 1칸 = 터렛 1종. 공격력·공격 속도 강화 2행을 표시한다. (CLAUDE.md 7장 ①)
    ///
    /// Progression의 StageButtonUI와 같은 패턴: <see cref="ShopController"/>가 <see cref="Bind"/>로 터렛을 주입하고,
    /// 각 행은 레벨 텍스트 "L/3"와 강화 버튼을 갖는다. 버튼은 <b>만렙</b>이거나 <b>골드 부족</b>이면 비활성.
    /// 강화 레벨/골드는 <see cref="PlayerProgress"/>(영속), 배수·비용은 <see cref="TurretUpgradeCatalog"/>에서 온다.
    /// </summary>
    public class ShopItemUI : MonoBehaviour
    {
        [Header("표시")]
        [Tooltip("터렛 이름 라벨(선택).")]
        [SerializeField] private TMP_Text nameLabel;

        [Tooltip("터렛 아이콘(선택). TurretData.icon을 표시.")]
        [SerializeField] private Image icon;

        [Header("공격력 강화")]
        [SerializeField] private Button damageButton;
        [SerializeField] private TMP_Text damageLevelText;
        [Tooltip("공격력 강화 비용 텍스트(선택). 만렙이면 maxedText.")]
        [SerializeField] private TMP_Text damageCostText;

        [Header("공격 속도 강화")]
        [SerializeField] private Button speedButton;
        [SerializeField] private TMP_Text speedLevelText;
        [Tooltip("공격 속도 강화 비용 텍스트(선택). 만렙이면 maxedText.")]
        [SerializeField] private TMP_Text speedCostText;

        [Header("포맷")]
        [Tooltip("레벨 표시 포맷. {0}=현재 레벨, {1}=최대 레벨. 예: \"1/3\".")]
        [SerializeField] private string levelFormat = "{0}/{1}";

        [Tooltip("비용 표시 포맷. {0}=골드.")]
        [SerializeField] private string costFormat = "{0} G";

        [Tooltip("만렙일 때 비용 텍스트에 표시할 문구.")]
        [SerializeField] private string maxedText = "MAX";

        private TurretData _turret;
        private TurretUpgradeCatalog _catalog;
        private Action _onPurchased;

        /// <summary>터렛/카탈로그를 주입하고 표시를 갱신한다. onPurchased는 구매 후 상점 골드 갱신 콜백.</summary>
        public void Bind(TurretData turret, TurretUpgradeCatalog catalog, Action onPurchased)
        {
            _turret = turret;
            _catalog = catalog;
            _onPurchased = onPurchased;

            if (nameLabel != null) nameLabel.text = turret != null ? turret.displayName : string.Empty;
            if (icon != null)
            {
                bool hasIcon = turret != null && turret.icon != null;
                if (hasIcon) icon.sprite = turret.icon;
                icon.enabled = hasIcon;
            }

            if (damageButton != null)
            {
                damageButton.onClick.RemoveListener(OnBuyDamage);
                damageButton.onClick.AddListener(OnBuyDamage);
            }
            if (speedButton != null)
            {
                speedButton.onClick.RemoveListener(OnBuySpeed);
                speedButton.onClick.AddListener(OnBuySpeed);
            }
            Refresh();
        }

        /// <summary>저장소를 다시 읽어 두 강화 행(레벨/비용/버튼 활성)을 갱신한다.</summary>
        public void Refresh()
        {
            RefreshRow(UpgradeStat.Damage, damageButton, damageLevelText, damageCostText);
            RefreshRow(UpgradeStat.Speed, speedButton, speedLevelText, speedCostText);
        }

        private void RefreshRow(UpgradeStat stat, Button button, TMP_Text levelText, TMP_Text costText)
        {
            if (_turret == null) return;

            int level = PlayerProgress.GetUpgradeLevel((int)_turret.type, (int)stat);
            int max = PlayerProgress.MaxUpgradeLevel;
            bool maxed = level >= max;
            int cost = _catalog != null ? _catalog.CostForNext(level) : 0;
            bool affordable = PlayerProgress.Gold >= cost;

            if (levelText != null) levelText.text = string.Format(levelFormat, level, max);
            if (costText != null) costText.text = maxed ? maxedText : string.Format(costFormat, cost);
            // 만렙이거나 골드가 부족하면 강화 불가.
            if (button != null) button.interactable = !maxed && affordable;
        }

        private void OnBuyDamage() => TryBuy(UpgradeStat.Damage);
        private void OnBuySpeed() => TryBuy(UpgradeStat.Speed);

        private void TryBuy(UpgradeStat stat)
        {
            if (_turret == null || _catalog == null) return;

            int level = PlayerProgress.GetUpgradeLevel((int)_turret.type, (int)stat);
            int cost = _catalog.CostForNext(level);
            if (PlayerProgress.TryBuyUpgrade((int)_turret.type, (int)stat, cost))
            {
                Refresh();               // 이 칸(레벨/버튼) 즉시 반영
                _onPurchased?.Invoke();  // 상점 골드 텍스트 + 다른 칸의 '골드 부족' 재판정
            }
        }

        private void OnDestroy()
        {
            if (damageButton != null) damageButton.onClick.RemoveListener(OnBuyDamage);
            if (speedButton != null) speedButton.onClick.RemoveListener(OnBuySpeed);
        }
    }
}
