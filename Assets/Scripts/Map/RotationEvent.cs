using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 스테이지 진행 중 미리 설계된 "회전 이벤트" 한 건.
    /// (claude.md 3장 - 스테이지당 총 2회, 랜덤 아님)
    /// StageData가 이 목록을 들고 있고, 각 항목은 발동 시간·구역·회전량을 정의한다.
    /// </summary>
    [System.Serializable]
    public class RotationEvent
    {
        [Tooltip("회전 구역의 좌하단 시작 좌표(그리드 전역 기준).")]
        public Vector2Int origin;

        [Tooltip("구역 한 변의 칸 수. 3x3 또는 4x4 단위.")]
        public int size = 3;

        [Tooltip("스테이지 시작 후 이 이벤트가 발동하기까지의 시간(초).")]
        public float triggerTime = 10f;

        [Tooltip("시계방향 90° 스텝 수. 1=90°, 2=180°, 3=270°, 음수=반시계.")]
        public int quarterTurnsCW = 1;

        [Tooltip("회전 시작 몇 초 전에 경고 화살표 UI를 띄울지.")]
        public float warningLeadTime = 5f;

        /// <summary>경고 UI 표기용: 시계방향 여부(양수 = 시계, 음수 = 반시계).</summary>
        public bool IsClockwise => quarterTurnsCW > 0;

        /// <summary>이 이벤트가 실제로 지형을 바꾸는(0°가 아닌) 회전인지.</summary>
        public bool IsEffective => (quarterTurnsCW % 4) != 0;
    }
}
