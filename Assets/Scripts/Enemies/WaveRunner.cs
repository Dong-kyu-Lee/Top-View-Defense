using System;
using System.Collections;
using System.Collections.Generic;
using TopViewDefense.Map;
using UnityEngine;

namespace TopViewDefense.Enemies
{
    /// <summary>
    /// WaveData(SO)에 미리 설계된 웨이브를 순서대로 재생하는 컴포넌트.
    /// (CLAUDE.md 4·6장 - 4개 모서리 등장, 웨이브 인터벌 사이 정비 시간)
    ///
    /// 진행 방식(클리어 기반): 각 웨이브의 모든 그룹을 스폰한 뒤, 그 적들이 모두 처치/도달로
    /// 사라질 때까지 대기하고, 다음 웨이브의 restBefore(정비 시간)만큼 쉰 후 다음 웨이브를 시작한다.
    ///
    /// 회전 연동: 웨이브 시작마다 <see cref="OnWaveStarted"/>(웨이브 인덱스)를 발행한다.
    /// RotationScheduler가 이를 구독해 RotationEvent.triggerWave/WarningWave에 맞춰 회전·경고를 발동한다.
    ///
    /// 스폰 셀은 스테이지의 네 모서리(<see cref="StageData.CornerSpawns"/>)를 기준으로 하되,
    /// 실제 Spawn 타일이 있으면 각 모서리를 가장 가까운 Spawn 타일로 스냅한다.
    /// MapBuilder의 Grid/Pathfinder가 준비될 때까지 대기 후 시작한다(RotationScheduler와 동일 패턴).
    /// </summary>
    [DisallowMultipleComponent]
    public class WaveRunner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("맵/그리드/경로탐색 소유자. 비우면 씬에서 탐색.")]
        [SerializeField] private MapBuilder mapBuilder;

        [Tooltip("적 레지스트리. 비우면 Instance 또는 씬에서 탐색.")]
        [SerializeField] private EnemyManager enemyManager;

        [Header("Wave Data")]
        [Tooltip("재생할 웨이브 데이터. 비우면 StageData.waves를 사용.")]
        [SerializeField] private WaveData waves;

        [Tooltip("SpawnGroup.enemy에 prefab이 없을 때 쓸 폴백 프리팹. 비우면 Resources의 Temp_Enemy 로드.")]
        [SerializeField] private GameObject fallbackPrefab;

        [Header("Spawn")]
        [Tooltip("적을 이 높이만큼 바닥 위로 띄워 배치(캡슐이 땅에 파묻히지 않게).")]
        [SerializeField] private float spawnYOffset = 1f;

        [Tooltip("체크 시 Start에서 자동 시작.")]
        [SerializeField] private bool autoBegin = true;

        /// <summary>웨이브가 시작됨(정비 시간 종료 후, 스폰 개시 시점). 인자는 웨이브 인덱스(0=첫 웨이브).</summary>
        public event Action<int> OnWaveStarted;

        /// <summary>웨이브의 정비 시간(restBefore)에 진입함. 프리뷰 UI가 구독. 인자는 곧 시작할 웨이브 인덱스.</summary>
        public event Action<int> OnWavePreview;

        /// <summary>모든 웨이브를 소진하고 마지막 웨이브 적까지 전멸함. 승리 판정이 구독.</summary>
        public event Action OnAllWavesCleared;

        /// <summary>현재 진행 중인 웨이브 인덱스(시작 전 -1).</summary>
        public int CurrentWave { get; private set; } = -1;

        /// <summary>총 웨이브 수(WaveData 기준). 데이터 확정 전에는 0.</summary>
        public int TotalWaves { get; private set; }

        /// <summary>정비 카운트다운이 향하는 웨이브 인덱스(정비 중에만 유효, 그 외 -1).</summary>
        public int PendingWave { get; private set; } = -1;

        /// <summary>정비(다음 웨이브 시작 전 카운트다운) 진행 중 여부. HUD가 카운트다운/즉시시작 노출 게이트로 사용.</summary>
        public bool IsResting { get; private set; }

        /// <summary>정비 카운트다운 남은 시간(초).</summary>
        public float RestRemaining { get; private set; }

        /// <summary>정비 카운트다운 전체 시간(초). 게이지 비율(RestRemaining/RestDuration) 계산용.</summary>
        public float RestDuration { get; private set; }

        private GridState Grid => mapBuilder != null ? mapBuilder.Grid : null;

        private Coroutine _loop;
        private int _activeGroups;             // 현재 웨이브에서 스폰 진행 중인 그룹 수
        private bool _skipRequested;           // 즉시시작(SkipRest) 요청 — 정비 카운트다운을 조기 종료
        private Vector2Int[] _cornerCells;     // [BL, BR, TL, TR]
        private EnemyData _runtimeDefault;     // SpawnGroup.enemy 미지정 시 사용

        private void Start()
        {
            if (autoBegin) Begin();
        }

        /// <summary>웨이브 재생을 시작한다(중복 무시).</summary>
        public void Begin()
        {
            if (_loop != null) return;

            if (mapBuilder == null) mapBuilder = FindObjectOfType<MapBuilder>();
            if (mapBuilder == null)
            {
                Debug.LogError("[WaveRunner] MapBuilder를 찾을 수 없습니다.", this);
                return;
            }

            if (enemyManager == null)
                enemyManager = EnemyManager.Instance ?? FindObjectOfType<EnemyManager>();
            if (enemyManager == null)
            {
                Debug.LogError("[WaveRunner] EnemyManager를 찾을 수 없습니다.", this);
                return;
            }

            _loop = StartCoroutine(RunWaves());
        }

        /// <summary>웨이브 재생과 진행 중인 모든 그룹 스폰을 중단한다.</summary>
        public void StopWaves()
        {
            StopAllCoroutines();
            _loop = null;
            _activeGroups = 0;
            IsResting = false;
            RestRemaining = 0f;
            PendingWave = -1;
            _skipRequested = false;
        }

        /// <summary>정비 카운트다운을 즉시 종료하고 다음 웨이브를 바로 시작한다(즉시시작 버튼).
        /// 정비 중이 아니면 무시된다.</summary>
        public void SkipRest()
        {
            if (IsResting) _skipRequested = true;
        }

        private IEnumerator RunWaves()
        {
            // Grid/Pathfinder 준비 대기(MapBuilder.Start의 Build 완료까지).
            while (Grid == null || mapBuilder.Pathfinder == null)
                yield return null;

            // 한 프레임 양보: 같은 프레임에 Grid 준비를 기다리던 구독자(RotationScheduler)가
            // OnWaveStarted를 구독할 틈을 주어, restBefore==0인 첫 웨이브의 회전 놓침을 방지.
            yield return null;

            WaveData data = waves != null ? waves : (mapBuilder.Stage != null ? mapBuilder.Stage.waves : null);
            if (data == null || data.Count == 0)
            {
                Debug.LogWarning("[WaveRunner] 재생할 WaveData가 없습니다(StageData.waves 또는 waves 필드 확인).", this);
                _loop = null;
                yield break;
            }

            TotalWaves = data.Count;
            _cornerCells = ResolveCornerCells();

            for (int i = 0; i < data.waves.Count; i++)
            {
                Wave wave = data.waves[i];
                if (wave == null) continue;

                // 1) 정비 시간(다음 웨이브 시작 전 카운트다운). 남은 시간을 노출하고 SkipRest로 조기 종료 가능.
                OnWavePreview?.Invoke(i);
                if (wave.restBefore > 0f)
                {
                    _skipRequested = false;
                    PendingWave = i;
                    RestDuration = wave.restBefore;
                    RestRemaining = wave.restBefore;
                    IsResting = true;

                    // Time.deltaTime 감산 → timeScale=0(일시정지) 시 카운트다운도 함께 멈춘다(EnergyDripper와 동일 원리).
                    while (RestRemaining > 0f && !_skipRequested)
                    {
                        yield return null;
                        RestRemaining -= Time.deltaTime;
                    }

                    IsResting = false;
                    RestRemaining = 0f;
                    PendingWave = -1;
                    _skipRequested = false;
                }

                // 2) 웨이브 시작 통지(회전 스케줄러가 이 시점에 회전/경고를 발동).
                CurrentWave = i;
                OnWaveStarted?.Invoke(i);

                // 3) 모든 그룹을 병렬로 스폰.
                _activeGroups = 0;
                if (wave.groups != null)
                {
                    foreach (SpawnGroup group in wave.groups)
                    {
                        if (group == null || group.count <= 0) continue;
                        _activeGroups++;
                        StartCoroutine(RunGroup(group));
                    }
                }

                // 4) 모든 그룹 스폰 완료 대기.
                while (_activeGroups > 0)
                    yield return null;

                // 5) 클리어 기반 진행: 이 웨이브 적이 모두 사라질 때까지 대기.
                while (enemyManager.Count > 0)
                    yield return null;
            }

            CurrentWave = -1;
            _loop = null;
            OnAllWavesCleared?.Invoke();
        }

        // 한 그룹: startDelay 후 count마리를 interval 간격으로 스폰.
        private IEnumerator RunGroup(SpawnGroup group)
        {
            if (group.startDelay > 0f)
                yield return new WaitForSeconds(group.startDelay);

            var wait = new WaitForSeconds(group.interval);
            for (int n = 0; n < group.count; n++)
            {
                SpawnAtCorner(group.corner, group.enemy);
                if (n < group.count - 1)
                    yield return wait;
            }

            _activeGroups--;
        }

        // corner를 해석해 해당 셀(들)에 적 1마리씩 스폰. All이면 네 모서리 동시.
        private void SpawnAtCorner(SpawnCorner corner, EnemyData data)
        {
            if (corner == SpawnCorner.All)
            {
                for (int c = 0; c < 4; c++)
                    SpawnOne(_cornerCells[c], data);
                return;
            }

            int idx = (int)corner;
            if (idx < 0 || idx >= 4) idx = 0;
            SpawnOne(_cornerCells[idx], data);
        }

        private void SpawnOne(Vector2Int cell, EnemyData data)
        {
            if (data == null) data = GetRuntimeDefault();

            GameObject prefab = ResolvePrefab(data);
            if (prefab == null)
            {
                Debug.LogError("[WaveRunner] 스폰할 프리팹이 없습니다(EnemyData.prefab/fallbackPrefab 모두 비어있음).", this);
                return;
            }

            Vector3 floor = Grid.GridToWorld(cell);
            Vector3 spawnPos = floor + Vector3.up * spawnYOffset;

            GameObject go = Instantiate(prefab, spawnPos, Quaternion.identity);
            go.name = $"{data.displayName}_{cell.x}_{cell.y}";

            Enemy enemy = go.GetComponent<Enemy>();
            if (enemy == null) enemy = go.AddComponent<Enemy>();

            enemy.Init(Grid, mapBuilder.Pathfinder, data, cell);
            enemyManager.Register(enemy);
        }

        // 네 모서리 셀을 해석. 실제 Spawn 타일이 있으면 각 모서리를 가장 가까운 Spawn 타일로 스냅.
        private Vector2Int[] ResolveCornerCells()
        {
            Vector2Int[] corners = mapBuilder.Stage != null
                ? mapBuilder.Stage.CornerSpawns()
                : new[]
                {
                    new Vector2Int(0, 0),
                    new Vector2Int(Grid.Width - 1, 0),
                    new Vector2Int(0, Grid.Height - 1),
                    new Vector2Int(Grid.Width - 1, Grid.Height - 1),
                };

            List<Vector2Int> spawnTiles = Grid.FindCells(TileType.Spawn);
            if (spawnTiles == null || spawnTiles.Count == 0)
                return corners;

            // 각 모서리를 가장 가까운 Spawn 타일로 매핑(레벨 디자이너가 Spawn 타일을 쓴 경우 대응).
            var resolved = new Vector2Int[4];
            for (int i = 0; i < 4; i++)
            {
                Vector2Int best = spawnTiles[0];
                int bestSqr = int.MaxValue;
                foreach (Vector2Int s in spawnTiles)
                {
                    int dx = s.x - corners[i].x, dy = s.y - corners[i].y;
                    int sqr = dx * dx + dy * dy;
                    if (sqr < bestSqr) { bestSqr = sqr; best = s; }
                }
                resolved[i] = best;
            }
            return resolved;
        }

        // SpawnGroup.enemy 미지정 시: 코드 상수로 기본 적 1종을 만들어 프로토타이핑 가능하게.
        private EnemyData GetRuntimeDefault()
        {
            if (_runtimeDefault == null)
            {
                _runtimeDefault = ScriptableObject.CreateInstance<EnemyData>();
                _runtimeDefault.type = EnemyType.Charger;
                _runtimeDefault.displayName = "돌격병(기본)";
                _runtimeDefault.maxHp = 100f;
                _runtimeDefault.moveSpeed = 2f;
                _runtimeDefault.armor = 0f;
                _runtimeDefault.damageToBase = 1;
            }
            return _runtimeDefault;
        }

        private GameObject ResolvePrefab(EnemyData data)
        {
            if (data != null && data.prefab != null) return data.prefab;
            if (fallbackPrefab != null) return fallbackPrefab;

            // 마지막 폴백: Resources의 임시 적 프리팹.
            fallbackPrefab = Resources.Load<GameObject>("Prefabs/Enemies/Temp_Enemy");
            return fallbackPrefab;
        }
    }
}
