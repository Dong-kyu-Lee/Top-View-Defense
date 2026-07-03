using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// StageData를 읽어 실제 씬에 타일 오브젝트를 생성하고 GridState를 구축한다.
    /// 플레이 시작 시 Start()에서 자동 실행.
    ///
    /// 배치 규약(문서 참고):
    /// - 그리드 (x, y) → 월드 (x, 0, y) * cellSize, XZ 평면. centerOnTransform이면 맵 중심을 이 트랜스폼에 맞춤.
    /// - 각 타일은 프리팹의 baked 스케일을 유지하고, 인스턴스 높이(localScale.y)를 읽어
    ///   바닥이 지면(y = 트랜스폼 높이)에 닿도록 배치한다. → 솟은 땅(Buildable/Obstacle)이 자연스럽게 위로 돌출.
    /// - 회전은 이 시점에 다루지 않는다(평평한 계층). RotationScheduler가 나중에 임시 피벗으로 처리.
    /// </summary>
    public class MapBuilder : MonoBehaviour
    {
        [Header("Stage")]
        [Tooltip("직접 할당. 비워두면 아래 Resources 경로에서 로드.")]
        [SerializeField] private StageData stageData;

        [Tooltip("stageData 미할당 시 Resources에서 로드할 경로.")]
        [SerializeField] private string stageResourcePath = "StageData/Stage01";

        [Header("Layout")]
        [Tooltip("셀 간 간격(타일 XZ 크기와 동일하게).")]
        [SerializeField] private float cellSize = 1f;

        [Tooltip("체크 시 맵 중심을 이 트랜스폼 위치에 맞춤.")]
        [SerializeField] private bool centerOnTransform = true;

        [Tooltip("Resources 기준 타일 프리팹 폴더. 파일명은 TileType 이름과 일치해야 함.")]
        [SerializeField] private string tilePrefabFolder = "Prefabs/Tiles";

        /// <summary>런타임 논리 그리드. 다른 시스템은 이것을 참조한다.</summary>
        public GridState Grid { get; private set; }

        /// <summary>흐름장 경로탐색. 회전/장애물 파괴 후 Recompute()로 갱신한다.</summary>
        public Pathfinder Pathfinder { get; private set; }
        public StageData Stage => stageData;

        /// <summary>생성된 타일 오브젝트들의 부모. 회전 시 reparent 기준으로 사용.</summary>
        public Transform TileRoot => _tileRoot;

        private Transform _tileRoot;
        private readonly Dictionary<TileType, GameObject> _prefabCache = new Dictionary<TileType, GameObject>();

        private void Start()
        {
            Build();
        }

        /// <summary>StageData로부터 씬 타일과 GridState를 (재)생성한다.</summary>
        public void Build()
        {
            if (stageData == null)
                stageData = Resources.Load<StageData>(stageResourcePath);
            if (stageData == null)
            {
                Debug.LogError($"[MapBuilder] StageData를 찾을 수 없습니다. (Resources 경로: {stageResourcePath})", this);
                return;
            }

            stageData.EnsureSize();
            ClearTiles();

            int w = stageData.width, h = stageData.height;

            Vector3 center = centerOnTransform
                ? new Vector3((w - 1) * cellSize * 0.5f, 0f, (h - 1) * cellSize * 0.5f)
                : Vector3.zero;
            Vector3 originWorld = transform.position - center;

            Grid = new GridState(w, h, cellSize, originWorld);

            _tileRoot = new GameObject("Tiles").transform;
            _tileRoot.SetParent(transform, false);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    TileType type = stageData.GetTile(x, y);
                    Grid.SetTile(x, y, type);
                    SpawnTile(type, x, y);
                }
            }

            // 타일이 모두 채워진 뒤 흐름장 초기 계산.
            Pathfinder = new Pathfinder(Grid);

            Debug.Log($"[MapBuilder] Stage {stageData.stageNumber} 빌드 완료 ({w}x{h}, cell {cellSize}).", this);
        }

        private void SpawnTile(TileType type, int x, int y)
        {
            Vector3 floor = Grid.GridToWorld(x, y);
            GameObject prefab = GetPrefab(type);
            GameObject go;

            if (prefab != null)
            {
                go = Instantiate(prefab, _tileRoot);
                // 프리팹의 baked 높이를 존중: 바닥이 지면에 닿도록 절반 높이만큼 올림.
                float halfHeight = go.transform.localScale.y * 0.5f;
                go.transform.position = floor + Vector3.up * halfHeight;
            }
            else
            {
                // 폴백: 프리팹이 없으면 Cube로 대체(아트 없이도 프로토타입 가능).
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.SetParent(_tileRoot, false);
                float sy = (type == TileType.Buildable || type == TileType.Obstacle) ? 1.5f : 1f;
                go.transform.localScale = new Vector3(1f, sy, 1f);
                go.transform.position = floor + Vector3.up * (sy * 0.5f);
                Debug.LogWarning($"[MapBuilder] '{tilePrefabFolder}/{type}' 프리팹을 찾지 못해 Cube로 대체했습니다.", this);
            }

            go.name = $"{type}_{x}_{y}";
            Grid.SetObject(x, y, go);
        }

        private GameObject GetPrefab(TileType type)
        {
            if (_prefabCache.TryGetValue(type, out var cached))
                return cached;

            GameObject prefab = Resources.Load<GameObject>($"{tilePrefabFolder}/{type}");
            _prefabCache[type] = prefab; // null도 캐시해 중복 로드 방지
            return prefab;
        }

        /// <summary>생성된 타일 전부 제거(재빌드용).</summary>
        public void ClearTiles()
        {
            if (_tileRoot == null) return;
            if (Application.isPlaying) Destroy(_tileRoot.gameObject);
            else DestroyImmediate(_tileRoot.gameObject);
            _tileRoot = null;
        }
    }
}
