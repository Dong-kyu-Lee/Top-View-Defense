using TopViewDefense.Combat;
using UnityEngine;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛 1종의 비용·능력치를 담는 데이터 에셋. (CLAUDE.md 5장)
    ///
    /// 적 <see cref="Enemies.EnemyData"/>와 동일 철학: 터렛 5종은 "같은 <see cref="Turret"/> 컴포넌트 +
    /// 서로 다른 이 에셋"으로 표현한다. 더블 터렛(2방)까지는 <see cref="shotsPerFire"/> 같은 데이터만으로
    /// 커버되고, 광역/감속/도트/에너지 생산 같은 능동 행동은 이후 페이즈에서 컴포넌트로 확장한다.
    ///
    /// Create → TopViewDefense → Turret Data 로 종류별 1개씩 생성.
    /// </summary>
    [CreateAssetMenu(fileName = "TurretData", menuName = "TopViewDefense/Turret Data", order = 2)]
    public class TurretData : ScriptableObject
    {
        [Header("메타")]
        public TurretType type = TurretType.Basic;

        [Tooltip("표시 이름(UI/디버그용).")]
        public string displayName = "기본 터렛";

        [Tooltip("배치할 프리팹. 비우면 Resources/Prefabs/Turrets/{type} 에서 이름으로 로드.")]
        public GameObject prefab;

        [Tooltip("상점/HUD 아이콘(선택). 없으면 이름만 표시.")]
        public Sprite icon;

        [Header("비용 / 배치")]
        [Tooltip("배치에 드는 에너지.")]
        [Min(0)] public int cost = 50;

        [Tooltip("맵당 최대 설치 개수(0 = 무제한). 에너지 터렛은 3 (CLAUDE.md 5장).")]
        [Min(0)] public int maxCount = 0;

        [Header("공격")]
        [Tooltip("사거리(셀 단위). 실제 월드 사거리 = range * cellSize.")]
        [Min(0f)] public float range = 5f;

        [Tooltip("발사 주기(초). 1 = 1초에 1회.")]
        [Min(0.05f)] public float fireInterval = 1f;

        [Tooltip("1회 발사 데미지. 0이면 비공격 터렛(에너지 등).")]
        [Min(0f)] public float damage = 20f;

        [Tooltip("공격 속성(적의 내성/도트 판정).")]
        public DamageType damageType = DamageType.Physical;

        [Tooltip("1회 발사 시 타격 횟수(더블 터렛 = 2). 기본 1.")]
        [Min(1)] public int shotsPerFire = 1;

        [Header("특수 (종류별, 이후 페이즈에서 사용)")]
        [Tooltip("광역 효과 반경(셀). 0이면 단일 타겟(프리즈/파이어에서 사용).")]
        [Min(0f)] public float areaRadius = 0f;

        [Tooltip("프리즈: 적용 시 이동속도 배수(0.5 = 절반). 1이면 감속 없음.")]
        [Range(0f, 1f)] public float slowMultiplier = 1f;

        [Tooltip("프리즈/파이어: 효과 지속 시간(초).")]
        [Min(0f)] public float effectDuration = 0f;

        [Tooltip("파이어: 초당 도트(DoT) 데미지.")]
        [Min(0f)] public float dotPerSecond = 0f;

        [Tooltip("에너지 터렛: 생산 주기(fireInterval)당 생산하는 에너지량(공격 대신).")]
        [Min(0)] public int energyPerCycle = 0;
    }
}
