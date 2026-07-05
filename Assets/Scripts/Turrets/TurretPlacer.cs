using System.Collections.Generic;
using TopViewDefense.Core;
using TopViewDefense.Map;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 터렛 배치 컨트롤러. UI에서 터렛을 '선택(<see cref="Arm"/>)'한 뒤 맵의 설치 가능한 칸을 클릭하면
    /// 그 자리에 배치한다. (CLAUDE.md 6장 - 하단 UI 슬롯에서 유닛 선택 → 솟은 땅에 배치)
    ///
    /// 배치 규칙(<see cref="CanPlace"/>):
    /// - 칸이 그리드 경계 안 + Buildable + 미점유여야 하고, 에너지가 충분해야 한다.
    /// - <see cref="TurretData.maxCount"/>로 종류별 맵당 개수를 제한한다(에너지 터렛 3).
    ///
    /// 배치된 터렛은 자기 셀 '타일 오브젝트의 자식'으로 붙여 회전 시 동반 회전시킨다
    /// (RotationScheduler가 타일 오브젝트를 회전하므로 자식 터렛이 자동으로 따라간다). 타일이 스케일된
    /// 큐브이므로 <c>SetParent(parent, worldPositionStays:true)</c>로 월드 스케일/포즈를 보존한다.
    ///
    /// 점유 판정은 별도 자료구조 없이 <see cref="GridState.GetObject"/>가 준 타일 오브젝트의 자식 Turret
    /// 유무로 본다 → 회전으로 셀↔오브젝트 매핑이 순열돼도 항상 정합을 유지한다.
    /// </summary>
    public class TurretPlacer : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("맵/그리드 소유자. 비우면 씬에서 탐색.")]
        [SerializeField] private MapBuilder mapBuilder;

        [Tooltip("배치 레이캐스트에 쓸 카메라. 비우면 Camera.main.")]
        [SerializeField] private Camera cam;

        [Tooltip("배치 클릭이 맞힐 타일 레이어. 비워두면(Nothing) 기본 레이캐스트 레이어 전체를 사용.")]
        [SerializeField] private LayerMask tileMask = ~0;

        [Header("Input")]
        [Tooltip("이 화면 하단 높이(px)는 HUD 영역으로 보고 배치 클릭을 무시한다(프로토타입 IMGUI HUD와 충돌 방지).")]
        [Min(0f)] [SerializeField] private float bottomUiMargin = 80f;

        /// <summary>현재 선택된(배치 대기) 터렛. null이면 배치 모드가 아니다.</summary>
        public TurretData Armed { get; private set; }

        /// <summary>배치된 터렛 목록(개수 제한/이후 철거에서 사용).</summary>
        public IReadOnlyList<Turret> Turrets => _turrets;

        private readonly List<Turret> _turrets = new List<Turret>();
        private GridState Grid => mapBuilder != null ? mapBuilder.Grid : null;

        private void Awake()
        {
            if (mapBuilder == null) mapBuilder = FindObjectOfType<MapBuilder>();
            if (cam == null) cam = Camera.main;
        }

        /// <summary>UI 버튼이 호출: 이 터렛을 배치 대기 상태로. 같은 것을 다시 누르면 해제(토글).</summary>
        public void Arm(TurretData data) => Armed = (Armed == data) ? null : data;

        /// <summary>배치 대기 해제.</summary>
        public void Disarm() => Armed = null;

        private void Update()
        {
            if (Armed == null) return;

            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            // 우클릭/ESC = 배치 취소.
            bool cancel = (mouse != null && mouse.rightButton.wasPressedThisFrame)
                       || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame);
            if (cancel)
            {
                Disarm();
                return;
            }

            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Vector2 pointer = mouse.position.ReadValue();
                if (pointer.y < bottomUiMargin) return;                          // 하단 HUD 영역
                if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
                    return;                                                        // uGUI 위 클릭(이후 정식 UI 대비)
                TryPlaceAtPointer(pointer);
            }
        }

        private void TryPlaceAtPointer(Vector2 pointer)
        {
            if (Grid == null || cam == null) return;
            if (!ScreenToCell(pointer, out Vector2Int cell)) return;
            TryPlace(Armed, cell);
        }

        /// <summary>
        /// 화면 좌표에서 쏜 광선이 실제 타일 콜라이더와 만나는 셀. 경계 밖/미명중이면 false.
        /// 바닥 평면이 아니라 타일 윗면(솟은 땅)을 직접 맞히므로, 카메라가 기울어져 있어도
        /// 시차(parallax)로 셀이 밀리지 않는다(솟은 높이 × tan(기울기)만큼의 오차 제거).
        /// </summary>
        private bool ScreenToCell(Vector3 screen, out Vector2Int cell)
        {
            cell = default;
            GridState grid = Grid;
            Ray ray = cam.ScreenPointToRay(screen);
            // 필드가 Nothing(0)이면 — 예: 씬에 이미 배선된 컴포넌트에 필드가 새로 추가된 경우 —
            // 기본 레이캐스트 레이어 전체로 폴백해 "아무것도 못 맞히는" 상태를 방지한다.
            int mask = tileMask.value != 0 ? tileMask.value : Physics.DefaultRaycastLayers;
            if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, mask))
                return false;
            cell = grid.WorldToGrid(hit.point);
            return grid.InBounds(cell);
        }

        /// <summary>해당 칸에 data 터렛을 배치할 수 있는지. 불가 사유는 reason으로 반환(로그/피드백용).</summary>
        public bool CanPlace(TurretData data, Vector2Int cell, out string reason)
        {
            reason = null;
            GridState grid = Grid;
            if (data == null || grid == null) { reason = "데이터/그리드 없음"; return false; }
            if (!grid.InBounds(cell)) { reason = "그리드 밖"; return false; }
            if (!grid.IsBuildable(cell)) { reason = "설치 불가 지형"; return false; }
            if (IsOccupied(cell)) { reason = "이미 점유됨"; return false; }
            if (data.maxCount > 0 && CountOfType(data.type) >= data.maxCount)
            {
                reason = $"최대 개수({data.maxCount}) 도달";
                return false;
            }
            if (PlayerEconomy.Instance != null && !PlayerEconomy.Instance.CanAfford(data.cost))
            {
                reason = "에너지 부족";
                return false;
            }
            return true;
        }

        /// <summary>셀의 타일 오브젝트에 이미 터렛이 붙어 있는지(회전 후에도 정합).</summary>
        public bool IsOccupied(Vector2Int cell)
        {
            GameObject tileObj = Grid != null ? Grid.GetObject(cell) : null;
            return tileObj != null && tileObj.GetComponentInChildren<Turret>() != null;
        }

        private int CountOfType(TurretType type)
        {
            int n = 0;
            for (int i = 0; i < _turrets.Count; i++)
                if (_turrets[i] != null && _turrets[i].Data != null && _turrets[i].Data.type == type)
                    n++;
            return n;
        }

        /// <summary>
        /// 해당 칸에 터렛을 배치한다(검증 → 에너지 차감 → 생성 → 타일 자식 부착). 성공 시 true.
        /// 입력(포인터 클릭)과 테스트가 공유하는 단일 배치 경로.
        /// </summary>
        public bool TryPlace(TurretData data, Vector2Int cell)
        {
            if (!CanPlace(data, cell, out string reason))
            {
                Debug.Log($"[TurretPlacer] 배치 불가 {cell}: {reason}");
                return false;
            }

            // 검증 통과 후 실제 차감. 여기서 실패하면 배치하지 않는다(레이스 방지).
            if (PlayerEconomy.Instance != null && !PlayerEconomy.Instance.TrySpend(data.cost))
                return false;

            GameObject prefab = ResolvePrefab(data);
            if (prefab == null)
            {
                Debug.LogError($"[TurretPlacer] '{data.type}' 프리팹을 찾을 수 없습니다. " +
                               $"(Resources/Prefabs/Turrets/{data.type})", this);
                if (PlayerEconomy.Instance != null) PlayerEconomy.Instance.Add(data.cost); // 환급
                return false;
            }

            GridState grid = Grid;
            GameObject tileObj = grid.GetObject(cell);
            Vector3 target = TileTop(tileObj, grid, cell);

            // 임포트 모델마다 피벗이 메시와 어긋날 수 있으므로(정점이 모델 원점에서 벗어나게 구워진 경우),
            // 메시 밑면 중심을 타일 윗면 중앙에 맞춰 정규화해 스폰한다. → 배치가 정확하고 조준 회전이 제자리에서 돈다.
            GameObject go = SpawnNormalized(prefab, target);

            // 타일 오브젝트의 자식으로(월드 포즈/스케일 유지) → 회전 동반.
            if (tileObj != null)
                go.transform.SetParent(tileObj.transform, true);

            go.name = $"{data.type}_{cell.x}_{cell.y}";

            Turret turret = go.GetComponent<Turret>();
            if (turret == null) turret = go.AddComponent<Turret>();
            turret.Init(data, cell, grid.CellSize);
            _turrets.Add(turret);
            return true;
        }

        /// <summary>
        /// 프리팹을 target(타일 윗면 중앙)에 앉혀 인스턴스화한다. 임포트 모델마다 피벗(트랜스폼 원점)이
        /// 메시와 어긋날 수 있으므로, 렌더러 바운즈의 <b>밑면 중심</b>을 target에 맞추고 그 지점을 회전
        /// 피벗으로 삼는 래퍼로 감싼다 → 배치가 정확하고, <see cref="Turret.FaceTarget"/>의 조준 회전이
        /// 메시 XZ 중심(=피벗)에서 제자리로 돈다. 반환하는 루트에 <see cref="Turret"/>을 붙인다.
        /// 렌더러가 없으면 정규화를 건너뛰고 프리팹 루트를 그대로 target에 둔다.
        /// </summary>
        private static GameObject SpawnNormalized(GameObject prefab, Vector3 target)
        {
            GameObject visual = Instantiate(prefab);

            if (!TryGetWorldBounds(visual, out Bounds b))
            {
                visual.transform.position = target;
                return visual;
            }

            GameObject root = new GameObject();
            root.transform.position = target;
            visual.transform.SetParent(root.transform, true); // 월드 포즈 유지(아직 원점 근처)

            // 메시 밑면 중심(XZ 중심 + 최저 Y)을 래퍼 원점(= target)으로 끌어온다.
            Vector3 bottomCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            visual.transform.position += target - bottomCenter;
            return root;
        }

        /// <summary>자식 렌더러 전체를 감싸는 월드 AABB. 렌더러가 없으면 false.</summary>
        private static bool TryGetWorldBounds(GameObject go, out Bounds bounds)
        {
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) { bounds = default; return false; }
            bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            return true;
        }

        // 타일 윗면 중앙. 타일은 스케일된 큐브이므로 실제 인스턴스 높이에서 top을 구한다(MapBuilder 배치 규약과 정합).
        private static Vector3 TileTop(GameObject tileObj, GridState grid, Vector2Int cell)
        {
            Vector3 c = grid.GridToWorld(cell);
            float topY = tileObj != null
                ? tileObj.transform.position.y + tileObj.transform.localScale.y * 0.5f
                : c.y;
            return new Vector3(c.x, topY, c.z);
        }

        private static GameObject ResolvePrefab(TurretData data)
        {
            if (data.prefab != null) return data.prefab;
            return Resources.Load<GameObject>($"Prefabs/Turrets/{data.type}");
        }
    }
}
