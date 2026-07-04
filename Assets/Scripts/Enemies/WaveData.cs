using System;
using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// 한 스테이지의 웨이브 편성을 미리 설계해 담는 데이터 에셋.
    /// (CLAUDE.md 4·6장 - 적이 4개 모서리에서 등장, 웨이브 인터벌 사이 정비 시간)
    ///
    /// StageData가 이 에셋을 참조하고, WaveRunner가 순서대로 재생한다.
    /// 회전(RotationEvent)은 "웨이브 인덱스"에 맞춰 발동하므로, 여기서 정의한 웨이브 순서가
    /// 회전 타이밍의 기준이 된다(Docs/Enemy-Architecture.md §6, Map 회전 문서 참조).
    ///
    /// 종류 추가 = EnemyData 에셋 추가, 편성 변경 = 이 에셋만 수정(데이터 주도, StageData와 동일 철학).
    /// </summary>
    [CreateAssetMenu(fileName = "WaveData", menuName = "TopViewDefense/Wave Data", order = 2)]
    public class WaveData : ScriptableObject
    {
        [Tooltip("순서대로 재생할 웨이브 목록. 인덱스 0이 첫 웨이브.")]
        public List<Wave> waves = new List<Wave>();

        /// <summary>정의된 웨이브 수.</summary>
        public int Count => waves != null ? waves.Count : 0;
    }

    /// <summary>
    /// 적이 등장하는 모서리. StageData의 CornerSpawns() 순서(BL, BR, TL, TR)에 대응한다.
    /// 맵 크기와 무관하게 편성을 이식할 수 있도록 원시 좌표 대신 이 enum으로 표현한다.
    /// </summary>
    public enum SpawnCorner
    {
        BottomLeft = 0,
        BottomRight = 1,
        TopLeft = 2,
        TopRight = 3,
        All = 4, // 네 모서리 동시
    }

    /// <summary>
    /// 하나의 웨이브. 여러 스폰 그룹(모서리별 편성)으로 구성되며, 그룹들은 병렬로 진행된다.
    /// </summary>
    [Serializable]
    public class Wave
    {
        [Tooltip("프리뷰/디버그 표기용 이름(예: \"Wave 3 - 정찰병 러시\").")]
        public string label;

        [Tooltip("이 웨이브가 시작하기 전 정비 시간(초). 이전 웨이브가 전멸된 뒤 이 시간만큼 대기.")]
        [Min(0f)] public float restBefore = 3f;

        [Tooltip("이 웨이브에서 진행할 스폰 그룹들(모서리·종류·수·간격). 서로 병렬로 스폰된다.")]
        public List<SpawnGroup> groups = new List<SpawnGroup>();
    }

    /// <summary>
    /// "어느 모서리에서, 어떤 적을, 몇 마리, 어떤 간격으로" 스폰할지 정의하는 최소 편성 단위.
    /// </summary>
    [Serializable]
    public class SpawnGroup
    {
        [Tooltip("스폰할 적 종류(EnemyData). 비우면 WaveRunner의 런타임 기본 적을 사용.")]
        public EnemyData enemy;

        [Tooltip("등장 모서리. All이면 네 모서리에서 동시에 count만큼 스폰.")]
        public SpawnCorner corner = SpawnCorner.BottomLeft;

        [Tooltip("스폰할 마리 수.")]
        [Min(1)] public int count = 5;

        [Tooltip("그룹 내 마리 간 스폰 간격(초).")]
        [Min(0.05f)] public float interval = 0.8f;

        [Tooltip("웨이브 시작 기준 이 그룹이 시작되기까지의 지연(초). 모서리별 시차 연출용.")]
        [Min(0f)] public float startDelay = 0f;
    }
}
