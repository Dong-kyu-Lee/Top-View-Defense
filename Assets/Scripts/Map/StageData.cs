using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 한 스테이지의 맵 구조와 회전 이벤트를 미리 설계해 담는 데이터 에셋.
    /// (claude.md 3장 - "회전하는 구역과 타이밍은 미리 설계되어 있음, 랜덤 아님")
    ///
    /// 타일은 1차원 배열로 직렬화하고 (x, y) 좌표로 접근한다.
    /// index = y * width + x  (x = 열, y = 행, 좌하단이 (0,0)).
    ///
    /// 스테이지별로 이 에셋을 하나씩 만들어 관리한다. (총 5개 + 확장)
    /// </summary>
    [CreateAssetMenu(fileName = "StageData", menuName = "TopViewDefense/Stage Data", order = 0)]
    public class StageData : ScriptableObject
    {
        [Header("메타")]
        [Tooltip("스테이지 번호 (1부터).")]
        public int stageNumber = 1;

        [Header("그리드 구조")]
        [Tooltip("맵 가로 칸 수. 정중앙 기지를 위해 홀수 권장.")]
        [Min(1)] public int width = 11;

        [Tooltip("맵 세로 칸 수. 정중앙 기지를 위해 홀수 권장.")]
        [Min(1)] public int height = 11;

        [Tooltip("width * height 개의 타일. index = y * width + x.")]
        [SerializeField] private TileType[] tiles;

        [Header("회전 이벤트 (스테이지당 보통 2개, 미리 설계)")]
        public List<RotationEvent> rotationEvents = new List<RotationEvent>();

        /// <summary>기지의 그리드 좌표(정중앙). Base 타일을 탐색해 반환, 없으면 중앙 계산값.</summary>
        public Vector2Int BaseCell
        {
            get
            {
                for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                        if (GetTile(x, y) == TileType.Base)
                            return new Vector2Int(x, y);
                return new Vector2Int(width / 2, height / 2);
            }
        }

        public bool InBounds(int x, int y) => x >= 0 && x < width && y >= 0 && y < height;
        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        public TileType GetTile(int x, int y)
        {
            EnsureSize();
            if (!InBounds(x, y)) return TileType.Obstacle; // 경계 밖은 벽처럼 취급
            return tiles[y * width + x];
        }

        public TileType GetTile(Vector2Int c) => GetTile(c.x, c.y);

        public void SetTile(int x, int y, TileType type)
        {
            EnsureSize();
            if (!InBounds(x, y)) return;
            tiles[y * width + x] = type;
        }

        public void SetTile(Vector2Int c, TileType type) => SetTile(c.x, c.y, type);

        /// <summary>tiles 배열이 width*height 크기와 일치하도록 보정(리사이즈 시 데이터 유지).</summary>
        public void EnsureSize()
        {
            int needed = Mathf.Max(0, width * height);
            if (tiles != null && tiles.Length == needed) return;

            var resized = new TileType[needed];
            if (tiles != null)
            {
                int copy = Mathf.Min(tiles.Length, needed);
                System.Array.Copy(tiles, resized, copy);
            }
            tiles = resized;
        }

        /// <summary>적이 등장하는 4개 모서리 좌표.</summary>
        public Vector2Int[] CornerSpawns()
        {
            return new[]
            {
                new Vector2Int(0, 0),
                new Vector2Int(width - 1, 0),
                new Vector2Int(0, height - 1),
                new Vector2Int(width - 1, height - 1),
            };
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureSize();
        }
#endif
    }
}
