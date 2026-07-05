using System.Collections.Generic;
using TopViewDefense.Map;
using UnityEngine;

namespace TopViewDefense.Progression
{
    /// <summary>
    /// 스테이지 <b>표시 순서</b>와 <b>골드 보상 표</b>를 한곳에 담는 카탈로그 에셋. (CLAUDE.md 2장·7장 ①)
    ///
    /// <see cref="StageSelectController"/>가 이 순서대로 버튼을 스폰하고,
    /// <see cref="StageProgressRecorder"/>가 <see cref="RewardFor"/>로 클리어 보상을 계산한다.
    /// 스테이지 확장 시 코드 변경 없이 <see cref="stages"/>에 <see cref="StageData"/>만 등록하면 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "StageCatalog", menuName = "TopViewDefense/Stage Catalog", order = 1)]
    public class StageCatalog : ScriptableObject
    {
        [Tooltip("표시 순서대로의 스테이지 목록. 인덱스가 그리드 순서, 각 항목의 stageNumber가 저장 키.")]
        public List<StageData> stages = new List<StageData>();

        [Tooltip("별 1/2/3개 클리어 시 지급되는 누적 골드 보상. index 0=별1, 1=별2, 2=별3.")]
        public int[] goldReward = { 30, 60, 100 };

        /// <summary>등록된 스테이지 수.</summary>
        public int Count => stages != null ? stages.Count : 0;

        /// <summary>별 개수에 해당하는 누적 보상액. 0별(미클리어)은 0.</summary>
        public int RewardFor(int stars)
        {
            stars = Mathf.Clamp(stars, 0, 3);
            if (stars <= 0 || goldReward == null || goldReward.Length == 0) return 0;
            int idx = Mathf.Min(stars, goldReward.Length) - 1;
            return goldReward[idx];
        }
    }
}
