using UnityEngine;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 별·골드·해금 상태의 유일한 영구 저장 창구. (CLAUDE.md 2장 해금/별, 7장 ① 골드)
    ///
    /// <see cref="MonoBehaviour"/>가 아닌 정적 클래스라 씬에 얹지 않고 어디서든 호출한다.
    /// StageSelect(읽기)·StageProgressRecorder(쓰기)·타이틀 상점(골드)이 이 저장소를 공유한다.
    ///
    /// - 별은 <b>최고기록만</b> 저장한다(하향 덮어쓰기 금지).
    /// - 해금은 저장하지 않고 별 기록에서 <b>파생</b>한다(직전 스테이지 별 ≥ 1).
    /// - 첫 실행 기본값(전부 0)만으로 "1번만 해금·골드 0"이 자연스럽게 성립한다.
    /// </summary>
    public static class PlayerProgress
    {
        private const string StarsKeyPrefix = "progress.stars."; // + stageNumber
        private const string GoldKey = "progress.gold";
        private const string UpgradeKeyPrefix = "progress.upgrade."; // + "dmg."/"spd." + turretTypeId

        /// <summary>강화 스탯 슬롯 인덱스(0=공격력, 1=공격 속도). Shop 계층의 UpgradeStat와 값이 일치한다.</summary>
        public const int StatDamage = 0;
        public const int StatSpeed = 1;

        /// <summary>스탯당 최대 강화 레벨(CLAUDE.md 7장 ① — 각 3회).</summary>
        public const int MaxUpgradeLevel = 3;

        /// <summary>그 스테이지의 저장된 최고 별(0~3, 0=미클리어).</summary>
        public static int GetStars(int stageNumber)
        {
            return Mathf.Clamp(PlayerPrefs.GetInt(StarsKeyPrefix + stageNumber, 0), 0, 3);
        }

        /// <summary>별 기록을 갱신한다. 기존보다 높을 때만 반영(하향 금지). 갱신 시 true.</summary>
        public static bool SetBestStars(int stageNumber, int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            if (stars <= GetStars(stageNumber)) return false;
            PlayerPrefs.SetInt(StarsKeyPrefix + stageNumber, stars);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>해금 여부(파생). 1번은 항상 해금, 그 외는 직전 스테이지를 별 1개 이상으로 클리어했는지.</summary>
        public static bool IsUnlocked(int stageNumber)
        {
            return stageNumber <= 1 || GetStars(stageNumber - 1) >= 1;
        }

        /// <summary>보유 골드 총액.</summary>
        public static int Gold => Mathf.Max(0, PlayerPrefs.GetInt(GoldKey, 0));

        /// <summary>골드 획득(클리어 보상 등). amount &lt;= 0이면 무시.</summary>
        public static void AddGold(int amount)
        {
            if (amount <= 0) return;
            PlayerPrefs.SetInt(GoldKey, Gold + amount);
            PlayerPrefs.Save();
        }

        /// <summary>골드가 cost 이상이면 차감하고 true. 부족하면 변화 없이 false.(상점에서 사용)</summary>
        public static bool TrySpendGold(int cost)
        {
            if (cost < 0 || Gold < cost) return false;
            PlayerPrefs.SetInt(GoldKey, Gold - cost);
            PlayerPrefs.Save();
            return true;
        }

        // 터렛 강화 키. stat은 StatDamage(0)/StatSpeed(1).
        private static string UpgradeKey(int turretTypeId, int stat)
            => UpgradeKeyPrefix + (stat == StatSpeed ? "spd." : "dmg.") + turretTypeId;

        /// <summary>해당 터렛 종류·스탯의 저장된 강화 레벨(0~MaxUpgradeLevel). 상점/전투 양쪽이 읽는다.</summary>
        public static int GetUpgradeLevel(int turretTypeId, int stat)
            => Mathf.Clamp(PlayerPrefs.GetInt(UpgradeKey(turretTypeId, stat), 0), 0, MaxUpgradeLevel);

        /// <summary>
        /// 강화 1단계 구매. 만렙이 아니고 골드가 cost 이상일 때만 <b>골드를 먼저 차감한 뒤</b> 레벨을 1 올린다.
        /// 성공 시 true. 만렙/골드 부족이면 변화 없이 false.(상점에서 호출)
        /// </summary>
        public static bool TryBuyUpgrade(int turretTypeId, int stat, int cost)
        {
            int level = GetUpgradeLevel(turretTypeId, stat);
            if (level >= MaxUpgradeLevel) return false;   // 이미 만렙
            if (!TrySpendGold(cost)) return false;        // 골드 부족(차감 실패 시 레벨 불변)
            PlayerPrefs.SetInt(UpgradeKey(turretTypeId, stat), level + 1);
            PlayerPrefs.Save();
            return true;
        }

        /// <summary>1..stageCount 스테이지의 별 합계(진행도 표시용).</summary>
        public static int TotalStars(int stageCount)
        {
            int sum = 0;
            for (int n = 1; n <= stageCount; n++) sum += GetStars(n);
            return sum;
        }

        /// <summary>개발/디버그용 초기화. 별·골드·강화 키를 지운다(볼륨 등 다른 키는 보존).</summary>
        public static void ResetAll(int stageCount = 64, int turretTypeCount = 16)
        {
            for (int n = 1; n <= stageCount; n++) PlayerPrefs.DeleteKey(StarsKeyPrefix + n);
            for (int t = 0; t < turretTypeCount; t++)
            {
                PlayerPrefs.DeleteKey(UpgradeKey(t, StatDamage));
                PlayerPrefs.DeleteKey(UpgradeKey(t, StatSpeed));
            }
            PlayerPrefs.DeleteKey(GoldKey);
            PlayerPrefs.Save();
        }
    }
}
