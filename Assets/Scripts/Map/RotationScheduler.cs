using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TopViewDefense.Map
{
    /// <summary>
    /// 스테이지에 미리 설계된 회전 이벤트(<see cref="RotationEvent"/>)를 시간에 맞춰 발동한다.
    /// (CLAUDE.md 3장 - "특정 웨이브 도달 시 일부 칸이 90° 배수로 총 2회 회전", 랜덤 아님)
    ///
    /// 처리 흐름(문서 5장 런타임 계층):
    ///   triggerTime - warningLeadTime : 경고(OnRotationWarning) → 경고 UI가 화살표 표시
    ///   triggerTime                   : 회전 시작(OnRotationStarted)
    ///     ├─ Pivot Transform 회전(연출)      : 구역 타일을 임시 피벗 아래로 모아 90° 배수 회전
    ///     ├─ GridState 데이터 동기화          : GridRotation으로 _tiles/_objects 블록 내 순열 재배치
    ///     └─ 완료 후 Pathfinder.Recompute()   : 회전으로 바뀐 지형에 맞춰 흐름장 재계산(OnRotationCompleted)
    ///
    /// 좌표계/회전 규약(문서 2·4.3장): 그리드 CW 90° = 월드 +Y 축 +90° 회전(탑뷰에서 시계방향).
    /// 터렛 동반 회전: 터렛을 자기 타일 오브젝트의 자식으로 배치하면 피벗 회전 시 위치·방향이 함께 회전한다.
    /// 터렛의 "논리 방향(Direction)"이나 셀 매핑을 따로 들고 있는 시스템은 OnRotationCompleted에서
    /// GridRotation.Rotate(dir, turns) / RotateWorld(cell, ...)로 자체 갱신하면 된다.
    /// </summary>
    [RequireComponent(typeof(Transform))]
    public class RotationScheduler : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("맵/그리드/경로탐색 소유자. 비우면 같은 오브젝트 또는 씬에서 탐색.")]
        [SerializeField] private MapBuilder mapBuilder;

        [Header("연출")]
        [Tooltip("회전 1회 연출에 걸리는 시간(초). 데이터/경로 갱신은 연출 종료 후 반영.")]
        [Min(0f)] [SerializeField] private float rotationDuration = 1f;

        [Tooltip("체크 시 시작·끝을 부드럽게(SmoothStep) 보간.")]
        [SerializeField] private bool easeInOut = true;

        [Tooltip("체크 시 Start에서 자동으로 스케줄을 시작.")]
        [SerializeField] private bool autoBegin = true;

        /// <summary>회전 예정 경고(회전 warningLeadTime 초 전). 경고 UI가 구독한다.</summary>
        public event Action<RotationEvent> OnRotationWarning;

        /// <summary>회전 연출 시작 시점.</summary>
        public event Action<RotationEvent> OnRotationStarted;

        /// <summary>회전 연출 완료 + 데이터/경로 갱신 완료 시점. 터렛 논리 갱신 등이 구독한다.</summary>
        public event Action<RotationEvent> OnRotationCompleted;

        /// <summary>현재 회전 연출이 진행 중인지.</summary>
        public bool IsRotating { get; private set; }

        private GridState Grid => mapBuilder != null ? mapBuilder.Grid : null;

        private Coroutine _schedule;

        private void Reset()
        {
            mapBuilder = GetComponent<MapBuilder>();
        }

        private void Start()
        {
            if (autoBegin) Begin();
        }

        /// <summary>스케줄을 시작한다(중복 호출 무시). Grid가 준비될 때까지 대기 후 진행.</summary>
        public void Begin()
        {
            if (_schedule != null) return;

            if (mapBuilder == null)
                mapBuilder = GetComponent<MapBuilder>() ?? FindObjectOfType<MapBuilder>();

            if (mapBuilder == null)
            {
                Debug.LogError("[RotationScheduler] MapBuilder를 찾을 수 없습니다.", this);
                return;
            }

            _schedule = StartCoroutine(RunSchedule());
        }

        /// <summary>진행 중인 스케줄을 중단한다(연출 중이면 즉시 멈춤).</summary>
        public void StopSchedule()
        {
            if (_schedule != null)
            {
                StopCoroutine(_schedule);
                _schedule = null;
            }
            IsRotating = false;
        }

        // ------------------------------------------------------------ 스케줄 루프

        private IEnumerator RunSchedule()
        {
            // MapBuilder.Build()(Start에서 실행)로 Grid가 채워질 때까지 대기 — 실행 순서 의존 제거.
            while (Grid == null)
                yield return null;

            StageData stage = mapBuilder.Stage;
            if (stage == null || stage.rotationEvents == null || stage.rotationEvents.Count == 0)
                yield break;

            // triggerTime 오름차순으로 순차 처리(설계상 이벤트는 시간상 겹치지 않게 배치).
            var events = new List<RotationEvent>(stage.rotationEvents);
            events.Sort((a, b) => a.triggerTime.CompareTo(b.triggerTime));

            float startTime = Time.time; // "스테이지 시작" 기준 시각

            foreach (RotationEvent ev in events)
            {
                if (ev == null) continue;

                if (!IsBlockValid(ev))
                {
                    Debug.LogWarning($"[RotationScheduler] 회전 구역이 그리드를 벗어나 건너뜁니다. " +
                                     $"origin={ev.origin}, size={ev.size}", this);
                    continue;
                }

                // 1) 경고 시점까지 대기 후 경고 발신.
                float warnTime = Mathf.Max(0f, ev.triggerTime - Mathf.Max(0f, ev.warningLeadTime));
                yield return WaitUntilElapsed(startTime, warnTime);
                OnRotationWarning?.Invoke(ev);

                // 2) 발동 시점까지 대기.
                yield return WaitUntilElapsed(startTime, ev.triggerTime);

                // 3) 회전(0°가 아닌 실제 회전만 연출/데이터 갱신).
                if (ev.IsEffective)
                    yield return RotateRoutine(ev);
                else
                    OnRotationCompleted?.Invoke(ev); // 항등 회전이라도 완료 통지는 한다.
            }

            _schedule = null;
        }

        private static IEnumerator WaitUntilElapsed(float startTime, float target)
        {
            while (Time.time - startTime < target)
                yield return null;
        }

        // ------------------------------------------------------------ 회전 연출 + 데이터 갱신

        private struct BlockCell
        {
            public Vector2Int cell;
            public TileType type;
            public GameObject obj;
            public Transform originalParent;
        }

        private IEnumerator RotateRoutine(RotationEvent ev)
        {
            IsRotating = true;
            OnRotationStarted?.Invoke(ev);

            GridState grid = Grid;
            int size = ev.size;
            Vector2Int origin = ev.origin;

            // 구역 셀 스냅샷 + 타일 오브젝트를 임시 피벗 아래로 모은다.
            var cells = new List<BlockCell>(size * size);
            for (int dy = 0; dy < size; dy++)
            {
                for (int dx = 0; dx < size; dx++)
                {
                    var c = new Vector2Int(origin.x + dx, origin.y + dy);
                    GameObject go = grid.GetObject(c);
                    cells.Add(new BlockCell
                    {
                        cell = c,
                        type = grid.GetTile(c),
                        obj = go,
                        originalParent = go != null ? go.transform.parent : null,
                    });
                }
            }

            // 피벗을 구역 중심(월드, 바닥 높이)에 생성. 짝수 size는 셀 사이 중점.
            float half = (size - 1) * 0.5f * grid.CellSize;
            Vector3 c0 = grid.GridToWorld(origin.x, origin.y);
            Vector3 center = c0 + new Vector3(half, 0f, half);

            var pivotGo = new GameObject($"RotationPivot_{origin.x}_{origin.y}");
            Transform pivot = pivotGo.transform;
            pivot.position = center;
            pivot.rotation = Quaternion.identity;

            foreach (BlockCell bc in cells)
                if (bc.obj != null)
                    bc.obj.transform.SetParent(pivot, true); // 월드 포즈 유지

            // 각도 선형 보간(quaternion Slerp는 270°를 최단 -90°로 돌아 방향이 뒤바뀌므로 각도로 보간).
            float target = GridRotation.ToYawDegrees(ev.quarterTurnsCW);
            float dur = Mathf.Max(0f, rotationDuration);

            if (dur > 0f)
            {
                float t = 0f;
                while (t < dur)
                {
                    t += Time.deltaTime;
                    float u = Mathf.Clamp01(t / dur);
                    if (easeInOut) u = Mathf.SmoothStep(0f, 1f, u);
                    pivot.rotation = Quaternion.Euler(0f, target * u, 0f);
                    yield return null;
                }
            }
            pivot.rotation = Quaternion.Euler(0f, target, 0f);

            // --- 연출 종료: 데이터 동기화 + 오브젝트 확정 배치 ---

            // 타일을 원부모로 복귀(월드 포즈 유지) 후 피벗 파괴.
            foreach (BlockCell bc in cells)
                if (bc.obj != null)
                    bc.obj.transform.SetParent(bc.originalParent, true);
            Destroy(pivotGo);

            // GridState _tiles/_objects 를 블록 내 순열로 재배치 + 오브젝트를 정확한 셀 중심으로 스냅.
            // dest 집합은 src 집합의 순열이므로 각 셀은 정확히 한 번만 덮어써진다.
            foreach (BlockCell bc in cells)
            {
                Vector2Int dest = GridRotation.RotateWorld(bc.cell, origin, size, ev.quarterTurnsCW);
                grid.SetTile(dest, bc.type);
                grid.SetObject(dest, bc.obj);

                if (bc.obj != null)
                {
                    Vector3 w = grid.GridToWorld(dest);
                    Vector3 p = bc.obj.transform.position;
                    bc.obj.transform.position = new Vector3(w.x, p.y, w.z); // 높이는 baked 값 유지
                }
            }

            // 회전으로 바뀐 지형에 맞춰 경로 재계산.
            mapBuilder.Pathfinder?.Recompute();

            IsRotating = false;
            OnRotationCompleted?.Invoke(ev);
        }

        // ------------------------------------------------------------ 유효성/기즈모

        private bool IsBlockValid(RotationEvent ev)
        {
            GridState grid = Grid;
            if (grid == null || ev.size <= 0) return false;
            return ev.origin.x >= 0 && ev.origin.y >= 0
                && ev.origin.x + ev.size <= grid.Width
                && ev.origin.y + ev.size <= grid.Height;
        }

#if UNITY_EDITOR
        // 플레이 중 회전 구역을 씬 뷰에 표시(레벨 디자인 확인용).
        private void OnDrawGizmosSelected()
        {
            GridState grid = Grid;
            if (grid == null || mapBuilder == null || mapBuilder.Stage == null) return;

            var events = mapBuilder.Stage.rotationEvents;
            if (events == null) return;

            foreach (RotationEvent ev in events)
            {
                if (ev == null || ev.size <= 0) continue;
                float half = (ev.size - 1) * 0.5f * grid.CellSize;
                Vector3 c0 = grid.GridToWorld(ev.origin.x, ev.origin.y);
                Vector3 center = c0 + new Vector3(half, 0.05f, half);
                Vector3 dim = new Vector3(ev.size * grid.CellSize, 0.1f, ev.size * grid.CellSize);

                Gizmos.color = new Color(1f, 0.6f, 0f, 0.9f);
                Gizmos.DrawWireCube(center, dim);
            }
        }
#endif
    }
}
