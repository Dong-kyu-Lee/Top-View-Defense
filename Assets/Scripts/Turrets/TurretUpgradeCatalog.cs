using System.Collections.Generic;
using TopViewDefense.Core;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛 영구 강화의 <b>밸런스 표</b>: 상점에 노출할 터렛 목록 + 레벨별 능력치 배수 + 레벨별 골드 비용.
    /// (CLAUDE.md 7장 ① — 골드로 공격력·공격 속도를 영구 강화)
    ///
    /// <see cref="StageCatalog"/>와 같은 철학: 코드 상수 대신 인스펙터에서 밸런스를 통제한다.
    /// - 상점 UI(<c>ShopController</c>)는 <see cref="turrets"/>를 순회해 아이템을 스폰하고 <see cref="CostForNext"/>로 가격을 읽는다.
    /// - 전투 런타임(<see cref="TurretUpgrades"/>)은 <see cref="DamageMultiplier"/>/<see cref="SpeedIntervalFactor"/>로 유효 수치를 계산한다.
    ///
    /// Create → TopViewDefense → Turret Upgrade Catalog 로 생성해 Resources/ 에 "TurretUpgradeCatalog"로 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "TurretUpgradeCatalog", menuName = "TopViewDefense/Turret Upgrade Catalog", order = 3)]
    public class TurretUpgradeCatalog : ScriptableObject
    {
        [Header("상점 목록")]
        [Tooltip("상점 그리드에 표시할 터렛(아군 5종). 순서가 그리드 순서.")]
        public List<TurretData> turrets = new List<TurretData>();

        [Header("강화 배수 (레벨당)")]
        [Tooltip("공격력: 레벨당 증가율. 0.25 = 레벨 L에서 base × (1 + 0.25·L).")]
        [Min(0f)] public float damagePerLevel = 0.25f;

        [Tooltip("공격 속도: 레벨당 발사 간격 감소율. 0.15 = 레벨 L에서 interval × (1 − 0.15·L).")]
        [Range(0f, 0.3f)] public float speedPerLevel = 0.15f;

        [Header("골드 비용")]
        [Tooltip("레벨 업 비용(누적 아님). index 0 = 0→1 강화 비용, index 1 = 1→2, index 2 = 2→3.")]
        public int[] costPerLevel = { 50, 120, 250 };

        /// <summary>현재 레벨에서 공격력 배수. base × 이 값 = 유효 공격력(에너지 터렛은 생산량).</summary>
        public float DamageMultiplier(int level)
            => 1f + damagePerLevel * Mathf.Clamp(level, 0, PlayerProgress.MaxUpgradeLevel);

        /// <summary>현재 레벨에서 발사 간격 배수(작을수록 빠름). 0.1 미만으로는 내려가지 않게 클램프.</summary>
        public float SpeedIntervalFactor(int level)
            => Mathf.Max(0.1f, 1f - speedPerLevel * Mathf.Clamp(level, 0, PlayerProgress.MaxUpgradeLevel));

        /// <summary>현재 레벨에서 다음 단계로 강화하는 데 드는 골드. 만렙/범위 밖이면 0.</summary>
        public int CostForNext(int currentLevel)
            => (costPerLevel != null && currentLevel >= 0 && currentLevel < costPerLevel.Length)
                ? Mathf.Max(0, costPerLevel[currentLevel])
                : 0;
    }
}
