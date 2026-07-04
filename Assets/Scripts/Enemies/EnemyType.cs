namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 적 유닛 종류 (claude.md 4장 - 총 4종, 추후 확장 가능).
    /// 값 자체로 로직을 분기하지 않는다. 능력치 차이는 EnemyData 에셋으로,
    /// 특수 행동(예: 실드병의 보호막)은 EnemyAbility 컴포넌트로 표현한다.
    /// → 5번째 적은 enum 값 + 에셋(+필요 시 컴포넌트) 추가만으로 확장된다.
    /// </summary>
    public enum EnemyType
    {
        /// <summary>돌격병: 표준 체력·속도. 기지로 최단 경로 진입.</summary>
        Charger = 0,

        /// <summary>장갑병: 매우 느리지만 압도적 체력·방어력.</summary>
        Tank = 1,

        /// <summary>정찰병: 체력 낮고 속도 매우 빠름.</summary>
        Scout = 2,

        /// <summary>공병(실드): 주변 적에 보호막/속성 내성 부여.</summary>
        Shielder = 3,
    }
}
