namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 상점에서 영구 강화하는 터렛 스탯 2종 (CLAUDE.md 7장 ① — 공격력·공격 속도).
    ///
    /// 값은 <see cref="Core.PlayerProgress.StatDamage"/>(0)/<see cref="Core.PlayerProgress.StatSpeed"/>(1)와
    /// 일치시킨다 — 저장소는 int 슬롯으로 다루고(도메인 결합 회피), 이 enum은 그 위의 의미 이름이다.
    ///
    /// 에너지 터렛은 공격이 없으므로 의미가 치환된다(CLAUDE.md 7장 ①):
    /// <see cref="Damage"/> → 1회 생산 에너지량↑, <see cref="Speed"/> → 생산 주기 단축.
    /// </summary>
    public enum UpgradeStat
    {
        Damage = 0, // 공격력 (에너지 터렛: 생산량)
        Speed = 1,  // 공격 속도 (에너지 터렛: 생산 주기)
    }
}
