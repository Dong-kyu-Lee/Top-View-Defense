using TopViewDefense.Core;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛의 <b>유효 능력치</b>(base 데이터 + 영구 강화)를 계산하는 정적 헬퍼. (CLAUDE.md 7장 ①)
    ///
    /// 설계 핵심: <see cref="TurretData"/>는 <b>불변 에셋</b>이라 런타임에 수정하지 않는다(에디터 오염 방지).
    /// 대신 <see cref="Turret.Init"/>이 배치 시점에 이 헬퍼로 유효 수치를 구해 캐시하고 전투에 쓴다.
    /// 강화 레벨은 <see cref="PlayerProgress"/>(영속), 배수는 <see cref="TurretUpgradeCatalog"/>(밸런스)에서 온다.
    ///
    /// 에너지 터렛(공격 없음)은 CLAUDE.md 7장 ① 규칙에 따라 의미가 치환된다:
    /// 공격력 강화 → <see cref="EffectiveEnergyPerCycle"/>(생산량↑), 공속 강화 → <see cref="EffectiveInterval"/>(주기↓).
    /// </summary>
    public static class TurretUpgrades
    {
        private static TurretUpgradeCatalog _catalog;

        // Resources의 단일 밸런스 표를 지연 로드·캐시. 없으면 배수 1(강화 미적용)로 폴백한다.
        private static TurretUpgradeCatalog Catalog
            => _catalog != null ? _catalog : (_catalog = Resources.Load<TurretUpgradeCatalog>("TurretUpgradeCatalog"));

        /// <summary>강화가 반영된 유효 공격력(base × 공격력 배수).</summary>
        public static float EffectiveDamage(TurretData data)
        {
            if (data == null) return 0f;
            int level = PlayerProgress.GetUpgradeLevel((int)data.type, PlayerProgress.StatDamage);
            float mult = Catalog != null ? Catalog.DamageMultiplier(level) : 1f;
            return data.damage * mult;
        }

        /// <summary>강화가 반영된 유효 발사(생산) 간격(base × 공속 배수). 최소 0.05초.</summary>
        public static float EffectiveInterval(TurretData data)
        {
            if (data == null) return 1f;
            int level = PlayerProgress.GetUpgradeLevel((int)data.type, PlayerProgress.StatSpeed);
            float factor = Catalog != null ? Catalog.SpeedIntervalFactor(level) : 1f;
            return Mathf.Max(0.05f, data.fireInterval * factor);
        }

        /// <summary>에너지 터렛 전용: 강화가 반영된 1주기 생산량(base × 공격력 배수, 반올림).</summary>
        public static int EffectiveEnergyPerCycle(TurretData data)
        {
            if (data == null) return 0;
            int level = PlayerProgress.GetUpgradeLevel((int)data.type, PlayerProgress.StatDamage);
            float mult = Catalog != null ? Catalog.DamageMultiplier(level) : 1f;
            return Mathf.RoundToInt(data.energyPerCycle * mult);
        }
    }
}
