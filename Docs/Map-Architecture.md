# 맵 아키텍처 문서 (Map Architecture)

> 회전하는 바둑판 맵 시스템의 데이터/유틸 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`claude.md`](../claude.md) 3장(맵 및 회전 메커니즘) 참고.

---

## 1. 개요

바둑판(그리드) 형태의 탑뷰 디펜스 맵을 **데이터 주도(Data-driven)** 로 설계한다.
맵 구조와 회전 이벤트는 **랜덤이 아니라 스테이지별로 미리 설계**되며, ScriptableObject 에셋으로 관리한다.

설계 목표(기획 요구 → 대응):

| 기획 요구 (claude.md 3장) | 대응 설계 |
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
└── StageData.cs       # 스테이지 맵 + 회전 이벤트 목록 (ScriptableObject)
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

### 4.5 `StageData` (ScriptableObject)

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
   └─ Pathfinder (A*) : 회전 후 경로 재계산
         └─ 경로 없음 → Obstacle 통과 허용 + 파괴 대상 지정
```

권장 구현 조합 (설계 논의 결론):
**A-1 ScriptableObject 레벨 데이터 + B-1 Pivot Transform 회전 + C-1 그리드 A\***

---

## 6. TODO / 다음 단계

- [ ] `StageData` 커스텀 에디터(그리드 페인터): 인스펙터에서 타일 클릭 편집 + 회전 구역 시각화
- [ ] `MapBuilder`: StageData → 씬 타일/기지 오브젝트 생성
- [ ] `Pathfinder`: 그리드 A\* + 폐쇄회로(장애물 파괴 통과) 룰
- [ ] `RotationScheduler`: 시간 기반 회전 발동 + 경고 UI + 터렛 동반 회전
- [ ] 비행 적: 그리드 무시 직선 이동 분기
```
