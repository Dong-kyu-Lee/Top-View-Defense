using TopViewDefense.Combat;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 적 1종의 능력치·보상을 담는 데이터 에셋. (claude.md 4장)
    ///
    /// 적 4종은 "같은 <see cref="Enemy"/> 컴포넌트 + 서로 다른 이 에셋"으로 표현한다.
    /// 코드 분기 없이 에셋만 추가하면 종류가 늘어난다(데이터 주도, StageData와 동일 철학).
    /// 특수 행동이 필요한 종류(실드병 등)만 별도 EnemyAbility 컴포넌트로 확장한다.
    ///
    /// Create → TopViewDefense → Enemy Data 로 종류별 1개씩 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "TopViewDefense/Enemy Data", order = 1)]
    public class EnemyData : ScriptableObject
    {
        [Header("메타")]
        public EnemyType type = EnemyType.Charger;

        [Tooltip("표시 이름(디버그/UI용).")]
        public string displayName = "돌격병";

        [Tooltip("스폰할 프리팹. 비우면 스포너의 폴백 프리팹을 사용한다.")]
        public GameObject prefab;

        [Header("능력치")]
        [Tooltip("최대 체력.")]
        [Min(1f)] public float maxHp = 100f;

        [Tooltip("이동 속도(초당 이동하는 '셀' 수). 실제 월드 속도 = moveSpeed * cellSize.")]
        [Min(0f)] public float moveSpeed = 2f;

        [Tooltip("방어력. 피격 1회당 데미지를 이만큼 정액 감소(최소 1은 보장).")]
        [Min(0f)] public float armor = 0f;

        [Header("속성 내성 (선택)")]
        [Tooltip("특정 속성에 내성을 가지는지(실드병/공병 등).")]
        public bool hasResistance = false;

        [Tooltip("내성을 가지는 공격 속성.")]
        public DamageType resistantType = DamageType.Fire;

        [Tooltip("내성 배수. 0=완전 면역, 0.5=절반 피해, 1=내성 없음. armor 적용 뒤에 곱해진다.")]
        [Range(0f, 1f)] public float resistanceMultiplier = 0.5f;

        [Header("기지 피해")]
        [Tooltip("기지 도달 시 감소시키는 목숨 수(보통 1).")]
        [Min(0)] public int damageToBase = 1;

        [Header("보상 (이후 경제 시스템에서 사용)")]
        [Tooltip("처치 시 드랍하는 에너지(인게임 전술 재화).")]
        [Min(0)] public int energyDrop = 5;

        [Tooltip("처치 시 드랍하는 골드(영구 성장 재화).")]
        [Min(0)] public int goldDrop = 1;
    }
}
