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

        [Header("Base")]
        [Tooltip("Base 타일 위에 세울 기지 프리팹. 비우면 아래 Resources 경로에서 로드.")]
        [SerializeField] private GameObject basePrefab;

        [Tooltip("basePrefab 미할당 시 Resources에서 로드할 경로.")]
        [SerializeField] private string baseResourcePath = "Prefabs/Base";

        /// <summary>런타임 논리 그리드. 다른 시스템은 이것을 참조한다.</summary>
        public GridState Grid { get; private set; }

        /// <summary>흐름장 경로탐색. 회전/장애물 파괴 후 Recompute()로 갱신한다.</summary>
        public Pathfinder Pathfinder { get; private set; }
        public StageData Stage => stageData;

        /// <summary>Base 타일 위에 생성된 기지 오브젝트. 없으면 null.</summary>
        public GameObject BaseInstance { get; private set; }

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
            // 로드 우선순위: 인스펙터 stageData → StageSelect에서 넘긴 SelectedStage → Resources 폴백.
            if (stageData == null)
                stageData = StageSession.SelectedStage;
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

            // 정중앙 Base 타일 위에 기지 오브젝트를 세운다(타일의 자식 → 맵 회전 시 동반 회전).
            SpawnBase();

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

            // 배치 클릭 레이캐스트(TurretPlacer)가 타일 윗면을 맞힐 수 있도록 콜라이더 보장.
            // 폴백 Cube는 이미 BoxCollider를 갖지만, 아트 프리팹은 없을 수 있으므로 보강한다.
            if (go.GetComponentInChildren<Collider>() == null)
                go.AddComponent<BoxCollider>();

            go.name = $"{type}_{x}_{y}";
            Grid.SetObject(x, y, go);
        }

        /// <summary>
        /// Base 타일 위에 기지 오브젝트를 세운다. 기지를 그 셀의 타일 오브젝트 자식으로 붙여
        /// 맵 회전(RotationScheduler가 타일 오브젝트를 회전)에 위치·방향이 함께 따라가게 한다
        /// — 터렛 동반 회전과 동일한 규약. 프리팹이 없으면 폴백 큐브로 대체한다.
        /// </summary>
        private void SpawnBase()
        {
            Vector2Int cell = Grid.BaseCell;
            GameObject tileObj = Grid.GetObject(cell);
            Vector3 target = TileTop(tileObj, cell);

            GameObject prefab = basePrefab != null ? basePrefab : Resources.Load<GameObject>(baseResourcePath);
            GameObject go;

            if (prefab != null)
            {
                // 임포트 모델 피벗이 메시와 어긋날 수 있으므로 밑면 중심을 타일 윗면에 맞춰 정규화.
                go = SpawnBaseNormalized(prefab, target);
            }
            else
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.transform.localScale = new Vector3(cellSize * 0.6f, cellSize * 0.6f, cellSize * 0.6f);
                go.transform.position = target + Vector3.up * (go.transform.localScale.y * 0.5f);
                Debug.LogWarning($"[MapBuilder] 기지 프리팹('{baseResourcePath}')을 찾지 못해 Cube로 대체했습니다.", this);
            }

            go.name = "Base";

            // 타일 오브젝트의 자식으로(월드 포즈/스케일 유지) → 맵 회전 동반.
            if (tileObj != null)
                go.transform.SetParent(tileObj.transform, true);

            BaseInstance = go;
        }

        // 프리팹을 target(타일 윗면 중앙)에 앉힌다. 메시 밑면 중심(XZ 중심 + 최저 Y)을 target에 맞추는
        // 래퍼로 감싸, 피벗이 어긋난 임포트 모델도 정확히 타일 위에 놓인다. 렌더러가 없으면 그대로 배치.
        private static GameObject SpawnBaseNormalized(GameObject prefab, Vector3 target)
        {
            GameObject visual = Instantiate(prefab);

            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                visual.transform.position = target;
                return visual;
            }

            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);

            GameObject root = new GameObject();
            root.transform.position = target;
            visual.transform.SetParent(root.transform, true);

            Vector3 bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            visual.transform.position += target - bottomCenter;
            return root;
        }

        // 타일 윗면 중앙 월드 좌표. 타일은 스케일된 큐브이므로 실제 인스턴스 높이로 top을 구한다.
        private Vector3 TileTop(GameObject tileObj, Vector2Int cell)
        {
            Vector3 c = Grid.GridToWorld(cell);
            float topY = tileObj != null
                ? tileObj.transform.position.y + tileObj.transform.localScale.y * 0.5f
                : c.y;
            return new Vector3(c.x, topY, c.z);
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
            // 기지는 Base 타일의 자식(= _tileRoot 하위)이라 함께 파괴된다. 참조만 정리.
            BaseInstance = null;
            if (_tileRoot == null) return;
            if (Application.isPlaying) Destroy(_tileRoot.gameObject);
            else DestroyImmediate(_tileRoot.gameObject);
            _tileRoot = null;
        }
    }
}
