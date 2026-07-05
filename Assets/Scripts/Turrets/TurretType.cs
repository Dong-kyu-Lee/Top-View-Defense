namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 아군 터렛 5종 (CLAUDE.md 5장: 기본/더블/프리즈/에너지/파이어).
    ///
    /// 적 <see cref="Enemies.EnemyType"/>와 동일 철학: <b>값 자체로 로직을 분기하지 않는다.</b>
    /// 능력치는 <see cref="TurretData"/>(데이터), 특수 행동은 컴포넌트/데이터 필드로 표현한다.
    /// enum 이름이 프리팹 파일명과 일치한다(Resources/Prefabs/Turrets/{TurretType}).
    /// </summary>
    public enum TurretType
    {
        Basic = 0,   // 기본 터렛: 가장 가까운 적에게 단일 대포
        Double = 1,  // 더블 터렛: 2방 동시(높은 단일 DPS)
        Freeze = 2,  // 프리즈 터렛: 광역 경직탄(이동 감속)
        Energy = 3,  // 에너지 터렛: 공격 없음, 에너지 생산(맵당 최대 3)
        Fire = 4,    // 파이어 터렛: 광역 화염탄(지속 도트)
    }
}
