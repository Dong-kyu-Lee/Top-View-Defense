# 맵 아키텍처 문서 (Map Architecture)

> 회전하는 바둑판 맵 시스템의 데이터/유틸 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 3장(맵 및 회전 메커니즘) 참고.

---

## 1. 개요

바둑판(그리드) 형태의 탑뷰 디펜스 맵을 **데이터 주도(Data-driven)** 로 설계한다.
맵 구조와 회전 이벤트는 **랜덤이 아니라 스테이지별로 미리 설계**되며, ScriptableObject 에셋으로 관리한다.

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md 3장) | 대응 설계 |
|---|---|
| 바둑판 그리드, 정중앙 기지, 4모서리 스폰 | `StageData` 그리드 + `BaseCell` / `CornerSpawns()` |
| 지형 종류(설치 가능/장애물/바닥) | `TileType` enum + 통행/설치/파괴 헬퍼 |
| 일부 칸(3x3/4x4)이 90° 배수로 총 2회 회전 | `RotationEvent` 목록 (미리 설계) |
| 회전은 미리 설계, 랜덤 아님 | `StageData` 에셋에 시간·구역·회전량 확정 |
| 터렛 동반 회전(위치+방향) | `GridRotation` 좌표·방향 회전 유틸 |
| 폐쇄회로 방지(막히면 장애물 파괴 통과) | `IsWalkable` / `IsDestructible` 분리 |

---

## 2. 좌표계 규약

전 시스템 공통 규약. **반드시 통일해서 사용한다.**

- `x` = 열(column), 오른쪽으로 증가 (+x)
- `y` = 행(row), 위쪽으로 증가 (+y)
- 원점 `(0, 0)` = **좌하단**
- 탑뷰(위에서 내려다본) 기준
- 타일 1차원 직렬화: `index = y * width + x`

> Unity 월드로 매핑 시: 그리드 `(x, y)` → 월드 `(x, 0, y)` (XZ 평면)로 두는 것을 권장.

---

## 3. 파일 구조

```
Assets/Scripts/Map/
├── TileType.cs        # TileType / Direction enum + 확장 헬퍼
├── GridRotation.cs    # 90° 배수 좌표·방향 회전 (순수 유틸)
├── RotationEvent.cs   # 회전 이벤트 1건의 직렬화 데이터
├── StageData.cs       # 스테이지 맵 + 회전 이벤트 목록 (ScriptableObject)
├── GridState.cs       # 런타임 논리 그리드 (단일 진실 공급원)
├── Pathfinder.cs      # 흐름장 경로탐색 (기지 역방향 Dijkstra + 폐쇄회로 룰)
├── RotationScheduler.cs # 시간 기반 회전 발동 + 경고/데이터/경로 갱신 (MonoBehaviour)
├── MapBuilder.cs      # StageData → 씬 타일 생성 + GridState 구축 (MonoBehaviour)
└── Editor/
    └── StageDataEditor.cs  # 인스펙터 그리드 페인터 (에디터 전용)
```

---

## 4. 타입 상세

### 4.1 `TileType` (enum)

| 값 | 의미 | 통행(Walkable) | 설치(Buildable) | 파괴(Destructible) |
|---|---|:---:|:---:|:---:|
| `Ground` | 적이 지나는 바닥 길 | ✅ | ❌ | ❌ |
| `Buildable` | 터렛 설치 가능한 솟은 땅 | ❌ | ✅ | ❌ |
| `Obstacle` | 설치 불가 장애물 (파괴 가능) | ❌ | ❌ | ✅ |
| `Base` | 정중앙 기지 (방어 목표) | ✅ | ❌ | ❌ |
| `Spawn` | 모서리 스폰 칸 | ✅ | ❌ | ❌ |

헬퍼: `IsWalkable()`, `IsBuildable()`, `IsDestructible()`
→ **경로탐색(A\*)에서 통행/파괴 통과 룰을 분리**하기 위한 핵심 구분.

### 4.2 `Direction` (enum)

`North=0, East=1, South=2, West=3` — enum 순서 자체가 **시계방향**이라 회전 계산이 단순하다.
터렛이 바라보는 방향에 사용하며, 회전 구역과 함께 회전한다.

### 4.3 `GridRotation` (static util)

시각(Transform) 회전과 **분리된 순수 논리 회전**. 데이터 동기화 담당.

| 메서드 | 설명 |
|---|---|
| `RotateInBlock(local, size, turns)` | N×N 구역 내 로컬좌표를 시계 `turns*90°` 회전 |
| `RotateWorld(world, origin, size, turns)` | 전역좌표를 구역 기준으로 회전한 전역좌표 |
| `Contains(world, origin, size)` | 좌표가 구역 안에 있는지 |
| `Rotate(dir, turns)` | 방향을 시계 `turns*90°` 회전 |
| `ToYawDegrees(turns)` | Transform Y축 회전 각도(도) 변환 |

**회전 수식 (시계 90°):** `(x, y) → (y, size-1-x)`
- 4번 적용 시 원위치(항등) — 검증됨.
- `turns` 음수 = 반시계. 내부에서 `mod 4` 정규화.

예시 (3×3 구역, 좌하단 `(0,0)` 을 시계 90°):
```
(0,0) → (0,2)   좌하단이 좌상단으로 이동
```

### 4.4 `RotationEvent` (직렬화 클래스)

미리 설계된 회전 1건. `StageData`가 목록으로 보유.

| 필드 | 의미 |
|---|---|
| `origin` | 회전 구역 좌하단 시작 좌표 |
| `size` | 구역 한 변 칸 수 (3 또는 4) |
| `triggerTime` | 스테이지 시작 후 발동 시간(초) |
| `quarterTurnsCW` | 시계 90° 스텝 수 (1/2/3, 음수=반시계) |
| `warningLeadTime` | 회전 몇 초 전 경고 UI 표시 |

파생: `IsClockwise`(경고 UI 방향), `IsEffective`(0°가 아닌 실제 회전인지).

### 4.5 `GridState` (런타임 클래스)

MapBuilder가 생성하는 **런타임 논리 그리드**. 회전·경로탐색·터렛 배치가 모두 이것만 참조한다(단일 진실 공급원).

| 멤버 | 설명 |
|---|---|
| `Width`, `Height`, `CellSize` | 그리드 크기와 셀 간격 |
| `OriginWorld` | 셀 (0,0) 중심의 월드 좌표(바닥) |
| `GetTile / SetTile` | 현재 지형 상태(회전으로 변함) |
| `GetObject / SetObject` | 셀 ↔ 씬 오브젝트 매핑(회전 reparent·파괴에 사용) |
| `GridToWorld / WorldToGrid` | 좌표 변환(전 시스템 공유) |
| `IsWalkable/IsBuildable/IsDestructible` | 셀 질의 |
| `BaseCell`, `FindCells(type)` | 기지/스폰 등 탐색 |

### 4.6 `MapBuilder` (MonoBehaviour)

`Start()`에서 `Build()` 실행. StageData → 타일 오브젝트 생성 + GridState 구축.

- 타일 프리팹은 `Resources/Prefabs/Tiles/{TileType}` 에서 로드(파일명=enum명). 없으면 Cube 폴백.
- 프리팹의 **baked 스케일 유지**, 인스턴스 `localScale.y`를 읽어 **바닥을 지면에 정렬**(솟은 땅이 위로 돌출).
- `centerOnTransform`이면 맵 중심을 트랜스폼 위치에 맞춤.
- 회전은 다루지 않음(평평한 계층). 재빌드용 `ClearTiles()` 제공.

### 4.7 `Pathfinder` (런타임 클래스)

`GridState`만 참조하는 **흐름장(Flow Field)** 경로탐색. 기지에서 전체 그리드로 역방향 Dijkstra를 1회 돌려 모든 셀에 "기지로 가는 다음 셀"과 최소 누적 비용을 채운다.

| 멤버 | 설명 |
|---|---|
| `Recompute()` | 지형 변경(회전/장애물 파괴) 후 흐름장 전체 재계산 |
| `TryGetNextStep(from, out next)` | 셀에서 기지로 가는 다음 셀(적이 매 프레임 참조) |
| `HasPath(from)` | 기지 도달 가능 경로 존재 여부 |
| `GetCost(from)` | 기지까지 최소 누적 비용 |
| `DestructionTargets` | 스폰→기지 경로 위에서 적이 관통·파괴할 Obstacle 목록 |

**진입 비용:** 통행 가능 셀 `1`, 파괴 가능 장애물 큰 값(`1,000,000`), 통행 불가(Buildable 등) 차단.
장애물 비용이 어떤 통행 경로 총비용보다 크므로 **걸어갈 길이 있으면 장애물을 절대 쓰지 않고**, 완전히 막힌 곳에서만 최소한의 장애물을 관통한다(2단계 탐색을 1패스로 흡수).

- 다중 소스: `Base` 타일(들)을 비용 0으로 시드, 없으면 `GridState.BaseCell` 폴백.
- 4방향 이동(대각선 없음 — 코너 컷팅 방지). `PriorityQueue` 미지원 환경 대비 이진 최소 힙 자체 구현(할당 최소화).

### 4.8 `RotationScheduler` (MonoBehaviour)

미리 설계된 `RotationEvent`를 시간에 맞춰 발동하는 런타임 계층. `MapBuilder`를 참조해 `Grid`/`Pathfinder`를 사용한다.

| 멤버 | 설명 |
|---|---|
| `Begin()` / `StopSchedule()` | 스케줄 시작(Grid 준비 대기 후)/중단 |
| `IsRotating` | 회전 연출 진행 중 여부 |
| `OnRotationWarning` | 회전 `warningLeadTime`초 전 — 경고 화살표 UI가 구독 |
| `OnRotationStarted` / `OnRotationCompleted` | 연출 시작 / (데이터·경로 갱신까지 끝난) 완료 |

**처리 흐름(1 이벤트):**
1. `triggerTime - warningLeadTime` → `OnRotationWarning`
2. `triggerTime` → **Pivot Transform 회전**: 구역 타일을 임시 피벗 아래로 reparent 후 각도 선형 보간으로 `quarterTurnsCW*90°` 회전(연출)
3. 연출 종료 → 원부모 복귀·셀 중심 스냅, `GridRotation.RotateWorld`로 `GridState` `_tiles`/`_objects` **블록 내 순열 재배치**, `Pathfinder.Recompute()` → `OnRotationCompleted`

- 이벤트는 `triggerTime` 오름차순으로 **순차 처리**(설계상 시간 비겹침 전제).
- 회전 방향 규약: 그리드 CW 90° = 월드 +Y `+90°`. 각도는 quaternion Slerp가 아닌 **선형 각도 보간**(270°가 최단 -90°로 뒤집히는 것 방지).
- **터렛 동반 회전:** 터렛을 자기 타일 오브젝트의 자식으로 배치하면 위치·방향이 자동 동반 회전. 논리 방향/셀을 따로 보관하는 시스템은 `OnRotationCompleted`에서 `GridRotation.Rotate`/`RotateWorld`로 자체 갱신.

### 4.9 `StageData` (ScriptableObject)

`Create → TopViewDefense → Stage Data` 로 스테이지당 1개 생성.

| 멤버 | 설명 |
|---|---|
| `stageNumber` | 스테이지 번호 |
| `width`, `height` | 그리드 크기 (정중앙 기지 위해 홀수 권장) |
| `tiles` (private) | `width*height` 1차원 배열, `GetTile/SetTile`로 접근 |
| `rotationEvents` | 회전 이벤트 목록 (보통 2개) |
| `BaseCell` | 기지 좌표(Base 탐색, 없으면 중앙) |
| `CornerSpawns()` | 4모서리 스폰 좌표 |
| `EnsureSize()` | 크기 변경 시 배열 리사이즈(데이터 보존) |

경계 밖 `GetTile`은 `Obstacle`을 반환해 벽처럼 취급.

---

## 5. 런타임 흐름 (예정)

이 문서는 **데이터/유틸 계층**만 다룬다. 아래는 이 위에 얹을 런타임 계층(미구현) 개요.

```
StageData (에셋)
   │  로드
   ▼
MapBuilder ──→ 씬에 타일/터렛 오브젝트 생성
   │
   ▼
GridState (런타임 논리 그리드) ◀── 경로탐색이 참조
   │
   ├─ RotationScheduler : triggerTime 도달 시 회전 발동
   │     ├─ 경고 UI (warningLeadTime 전)
   │     ├─ Transform 회전(연출) + GridRotation(데이터)
   │     └─ 터렛 위치·방향 동반 회전
   │
   └─ Pathfinder (Flow Field) : 회전 후 흐름장 재계산
         └─ 경로 없음 → Obstacle 통과 허용(큰 진입 비용) + 파괴 대상 지정
```

권장 구현 조합 (설계 논의 결론):
**A-1 ScriptableObject 레벨 데이터 + B-1 Pivot Transform 회전 + C-2 흐름장(Flow Field)**

> 경로탐색은 초기 A\* 안에서 **흐름장(Flow Field)** 으로 확정했다. 목표(기지)가 하나이고
> 적이 다수이며 회전으로 적 위치가 수시로 바뀌고 전체 재계산이 잦은 이 게임 패턴에는,
> 적마다 경로를 들고 다니는 A\*보다 셀마다 "다음 방향"을 심어두는 흐름장이 더 맞기 때문이다.
> (상세: [`Pathfinder.cs`](../Assets/Scripts/Map/Pathfinder.cs) 주석 참고.)

---

## 6. TODO / 다음 단계

- [x] `StageData` 커스텀 에디터(그리드 페인터): 인스펙터에서 타일 클릭 편집 + 회전 구역 시각화
- [x] `GridState`: 런타임 논리 그리드(좌표 변환·셀 매핑)
- [x] `MapBuilder`: StageData → 씬 타일/기지 오브젝트 생성
- [x] `Pathfinder`: 흐름장(Flow Field) + 폐쇄회로(장애물 파괴 통과) 룰
- [x] `RotationScheduler`: 시간 기반 회전 발동 + 경고 이벤트 + 데이터/경로 갱신 (경고 UI·터렛 논리 갱신은 구독 측에서)
```
