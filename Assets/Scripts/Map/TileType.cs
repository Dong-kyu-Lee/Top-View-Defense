namespace TopViewDefense.Map
{
    /// <summary>
    /// 바둑판 그리드의 각 칸이 가질 수 있는 지형 종류.
    /// (claude.md 3장 - 지형의 종류 참고)
    /// </summary>
    public enum TileType
    {
        /// <summary>적이 지나다니는 바닥 길.</summary>
        Ground = 0,

        /// <summary>아군 터렛을 설치할 수 있는 솟아오른 땅.</summary>
        Buildable = 1,

        /// <summary>설치 불가 장애물. 길이 막히면 적이 공격·파괴하고 통과.</summary>
        Obstacle = 2,

        /// <summary>정중앙 기지 칸 (방어 목표).</summary>
        Base = 3,

        /// <summary>적이 등장하는 모서리 스폰 칸.</summary>
        Spawn = 4,
    }

    /// <summary>
    /// 터렛/유닛이 바라보는 4방향. 회전 구역과 함께 방향도 회전한다.
    /// (claude.md 3장 - 터렛 동반 회전 참고)
    /// </summary>
    public enum Direction
    {
        North = 0,
        East = 1,
        South = 2,
        West = 3,
    }

    public static class TileTypeExtensions
    {
        /// <summary>해당 타일에 터렛을 설치할 수 있는지.</summary>
        public static bool IsBuildable(this TileType type) => type == TileType.Buildable;

        /// <summary>적이 통과 가능한지. 장애물/솟은 땅은 막는다.</summary>
        public static bool IsWalkable(this TileType type)
            => type == TileType.Ground || type == TileType.Spawn || type == TileType.Base;

        /// <summary>막혔을 때 적이 파괴하고 통과할 수 있는 대상인지.</summary>
        public static bool IsDestructible(this TileType type) => type == TileType.Obstacle;
    }
}
