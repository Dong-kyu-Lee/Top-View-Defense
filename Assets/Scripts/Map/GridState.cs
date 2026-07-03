using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 런타임 논리 그리드. 맵의 "현재 상태"에 대한 단일 진실 공급원(single source of truth).
    /// MapBuilder가 생성·초기화하며, 이후 경로탐색/터렛 배치/회전 스케줄러가 모두 이것만 참조한다.
    ///
    /// - 지형 상태(TileType[,])와 셀↔오브젝트 매핑(GameObject[,])을 함께 보관.
    /// - 그리드↔월드 좌표 변환을 이곳에 집약해 전 시스템이 공유한다.
    ///
    /// 좌표 규약(문서 2장): x=열(+오른쪽), y=행(+위/월드 +Z), 좌하단 (0,0).
    /// 월드 매핑: 그리드 (x, y) → 월드 (x, 0, y) * cellSize + OriginWorld (XZ 평면, 바닥 기준).
    /// </summary>
    public class GridState
    {
        public int Width { get; }
        public int Height { get; }
        public float CellSize { get; }

        /// <summary>그리드 셀 (0,0) 중심의 월드 좌표(바닥, y = 지면).</summary>
        public Vector3 OriginWorld { get; }

        private readonly TileType[,] _tiles;
        private readonly GameObject[,] _objects;

        public GridState(int width, int height, float cellSize, Vector3 originWorld)
        {
            Width = Mathf.Max(1, width);
            Height = Mathf.Max(1, height);
            CellSize = cellSize;
            OriginWorld = originWorld;
            _tiles = new TileType[Width, Height];
            _objects = new GameObject[Width, Height];
        }

        // ---------------------------------------------------------------- 경계

        public bool InBounds(int x, int y) => x >= 0 && x < Width && y >= 0 && y < Height;
        public bool InBounds(Vector2Int c) => InBounds(c.x, c.y);

        // ---------------------------------------------------------------- 지형

        /// <summary>경계 밖은 Obstacle(벽)로 취급.</summary>
        public TileType GetTile(int x, int y) => InBounds(x, y) ? _tiles[x, y] : TileType.Obstacle;
        public TileType GetTile(Vector2Int c) => GetTile(c.x, c.y);

        public void SetTile(int x, int y, TileType type) { if (InBounds(x, y)) _tiles[x, y] = type; }
        public void SetTile(Vector2Int c, TileType type) => SetTile(c.x, c.y, type);

        // ---------------------------------------------------------------- 셀 오브젝트

        public GameObject GetObject(int x, int y) => InBounds(x, y) ? _objects[x, y] : null;
        public GameObject GetObject(Vector2Int c) => GetObject(c.x, c.y);

        public void SetObject(int x, int y, GameObject go) { if (InBounds(x, y)) _objects[x, y] = go; }
        public void SetObject(Vector2Int c, GameObject go) => SetObject(c.x, c.y, go);

        // ---------------------------------------------------------------- 질의

        public bool IsWalkable(Vector2Int c) => GetTile(c).IsWalkable();
        public bool IsBuildable(Vector2Int c) => GetTile(c).IsBuildable();
        public bool IsDestructible(Vector2Int c) => GetTile(c).IsDestructible();

        /// <summary>기지 셀. Base 타일을 탐색, 없으면 중앙.</summary>
        public Vector2Int BaseCell
        {
            get
            {
                for (int y = 0; y < Height; y++)
                    for (int x = 0; x < Width; x++)
                        if (_tiles[x, y] == TileType.Base)
                            return new Vector2Int(x, y);
                return new Vector2Int(Width / 2, Height / 2);
            }
        }

        /// <summary>특정 타입의 모든 셀 좌표.</summary>
        public List<Vector2Int> FindCells(TileType type)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    if (_tiles[x, y] == type)
                        list.Add(new Vector2Int(x, y));
            return list;
        }

        // ---------------------------------------------------------------- 좌표 변환

        /// <summary>셀 좌표 → 셀 중심의 월드 좌표(바닥 기준, y = OriginWorld.y).</summary>
        public Vector3 GridToWorld(int x, int y)
            => OriginWorld + new Vector3(x * CellSize, 0f, y * CellSize);
        public Vector3 GridToWorld(Vector2Int c) => GridToWorld(c.x, c.y);

        /// <summary>월드 좌표 → 가장 가까운 셀 좌표.</summary>
        public Vector2Int WorldToGrid(Vector3 world)
        {
            Vector3 local = world - OriginWorld;
            return new Vector2Int(
                Mathf.RoundToInt(local.x / CellSize),
                Mathf.RoundToInt(local.z / CellSize));
        }
    }
}
