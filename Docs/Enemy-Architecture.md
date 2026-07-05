# 적 아키텍처 문서 (Enemy Architecture)

> 적 유닛의 스폰·이동·전투·기지 판정 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 4장(적 유닛)·2장(승패)·6·7장(제어/경제) 참고.
> 맵/경로 계층은 [`Map-Architecture.md`](./Map-Architecture.md), 터렛 계층은 [`Turret-Architecture.md`](./Turret-Architecture.md)에
> 별도 정리되어 있으며, 이 문서는 그 위에 얹힌다.

---

## 1. 개요

적 시스템을 맵 계층과 동일하게 **데이터 주도(Data-driven)** 로 설계한다.
적 4종은 **같은 `Enemy` 컴포넌트 + 서로 다른 `EnemyData` 에셋**으로 표현하고, 능동 행동(실드병 등)만
별도 컴포넌트로 분리한다. → **enum 값 + 에셋 추가만으로 종류가 확장**된다.

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md) | 대응 설계 |
|---|---|
| 적 4종, 추후 확장 (4장) | `EnemyType` enum + `EnemyData`(SO) 데이터 주도 |
| 4모서리에서 등장, 기지로 최단 진입 (3·4장) | `EnemySpawner`(스폰 셀) + `Pathfinder` 흐름장 추종 |
| 맵 회전 시 실시간 경로 변경 (3장) | 적이 매 프레임 `WorldToGrid→TryGetNextStep`만 참조(자동 대응) |
| 기지 목숨 3, 도달 시 감소, 별점 (2장) | `BaseCore`(목숨/게임오버/별점) |
| 종류별 특수 능력(실드/내성) (4장) | `IDamageable`+`DamageType`, 이후 `EnemyAbility`(확장점) |
| 처치 시 재화 드랍 (7장) | `EnemyData` 보상 필드 + `EnemyManager.OnEnemyKilled` 훅 |
| 터렛의 "가장 가까운 적" 타게팅 (5장) | `EnemyManager.FindNearest` |

---

## 2. 의존 규약

- **좌표계**는 맵 문서 2장 규약을 그대로 따른다(x=열, y=행, 좌하단 원점, 월드 XZ 평면).
- 적은 자기 위치·체력만 알고, **기지/경제와 직접 결합하지 않는다.** `Enemy`는 이벤트만 발행하고
  `EnemyManager`가 허브로서 라우팅한다(도달→`BaseCore`, 사망→경제 시스템).
- **경로는 적이 소유하지 않는다.** 맵의 `Pathfinder`(흐름장)를 참조만 하며, 회전/장애물 파괴로
  지형이 바뀌면 `Pathfinder.Recompute()`가 흐름장을 갱신하고 적은 다음 프레임에 새 방향을 읽는다.

---

## 3. 파일 구조

```
Assets/Scripts/
├── Combat/
│   └── IDamageable.cs     # 피격 계약 + DamageType enum (터렛↔적 분리)
├── Enemies/
│   ├── EnemyType.cs       # 적 4종 enum
│   ├── EnemyData.cs       # 종류별 능력치·보상·속성내성 (ScriptableObject)
│   ├── Enemy.cs           # 적 런타임 컴포넌트 (흐름장 이동 + 체력/보호막) (MonoBehaviour)
│   ├── EnemyAbility.cs    # 능동 능력 추상 베이스 (확장점) (MonoBehaviour)
│   ├── ShielderAura.cs    # 공병: 반경 내 아군에 보호막 부여 (MonoBehaviour)
│   ├── EnemyManager.cs    # 활성 적 레지스트리 + 최근접 질의 + 이벤트 허브 (MonoBehaviour)
│   ├── WaveData.cs        # 웨이브 편성 SO (Wave / SpawnGroup / SpawnCorner)
│   └── WaveRunner.cs      # WaveData 재생: 모서리별 웨이브 스폰 + 웨이브 이벤트 발행 (MonoBehaviour)
└── Core/
    ├── BaseCore.cs           # 기지 목숨·승패·별점 (MonoBehaviour)
    └── EnemyRewardDropper.cs # 처치 보상: 에너지 즉시 지급 + 골드 세션 누적→클리어 뱅킹 (MonoBehaviour)
```

---

## 4. 타입 상세

### 4.1 `DamageType` (enum)

`Physical=0, Fire=1, Freeze=2, Energy=3`.
실드병(공병)의 속성 내성, 파이어/프리즈 터렛의 광역 효과를 구분하기 위한 태그.
현재는 값만 정의하고, 내성/도트 등 속성별 로직은 이후 페이즈에서 붙인다.

### 4.2 `IDamageable` (interface)

데미지를 받을 수 있는 대상의 공통 계약. **터렛(공격 측)과 적(피격 측)을 분리**하는 핵심 인터페이스로,
이후 파괴 가능한 장애물 등도 동일하게 확장할 수 있다.

| 멤버 | 설명 |
|---|---|
| `IsDead` | 사망(파괴) 여부 — 타게팅/피격에서 제외 |
| `Position` | 월드 좌표(타게팅·투사체 조준용) |
| `TakeDamage(amount, type)` | 데미지 적용. `type`은 속성(내성/도트 판정용) |

### 4.3 `EnemyType` (enum)

`Charger=0, Tank=1, Scout=2, Shielder=3` (CLAUDE.md 4장: 돌격/장갑/정찰/공병).
**값 자체로 로직을 분기하지 않는다.** 능력치는 `EnemyData`, 특수 행동은 컴포넌트로 표현한다.

### 4.4 `EnemyData` (ScriptableObject)

`Create → TopViewDefense → Enemy Data` 로 종류당 1개 생성.

| 필드 | 의미 |
|---|---|
| `type` | 적 종류(`EnemyType`) |
| `displayName` | 표시 이름(디버그/UI) |
| `prefab` | 스폰 프리팹(비우면 스포너 폴백 사용) |
| `maxHp` | 최대 체력 |
| `moveSpeed` | 이동 속도(**초당 셀 수**). 월드 속도 = `moveSpeed * cellSize` |
| `armor` | 방어력(피격당 정액 감소, 최소 1 보장) |
| `hasResistance` / `resistantType` / `resistanceMultiplier` | 속성 내성(0=면역~1=없음). armor 적용 뒤 곱해짐 |
| `damageToBase` | 기지 도달 시 감소시키는 목숨(보통 1) |
| `energyDrop` / `goldDrop` | 처치 보상(이후 경제 시스템에서 사용) |

**작성된 4종 에셋** (`Resources/EnemyData/`): 돌격병(hp100/속2), 장갑병(hp400/속0.8/방5),
정찰병(hp40/속4), 공병(hp120/속1.6/방2 + 화염 내성 0.5 + 보호막 아우라). 종류 추가 = 에셋 추가.

### 4.5 `Enemy` (MonoBehaviour, `IDamageable`)

흐름장을 따라 기지로 이동하고 체력·피격·도달을 처리한다. 종류별 차이는 `EnemyData`가 주입한다.

| 멤버 | 설명 |
|---|---|
| `Init(grid, pathfinder, data, spawnCell)` | 스폰 직후 주입·초기화(셀 정렬 + 부착 능력 `Initialize`) |
| `Data`, `CurrentHp`, `IsDead`, `Position` | 상태 조회 |
| `ShieldCharges` / `AddShield(n)` | 보호막 충전 수 조회 / 부여(공병 아우라가 호출) |
| `TakeDamage(amount, type)` | 보호막→방어력→속성내성 순 적용 후 체력 감소, 0이면 사망 |
| `OnReachedBase` / `OnDied` | 기지 도달 / 사망 이벤트(허브가 구독) |

**피격 처리 순서(`TakeDamage`):** ① 보호막이 있으면 충전 1 소모하고 피격 완전 무시 →
② 방어력 정액 감소(최소 1 보장) → ③ 내성 속성이면 배수 곱(0이면 면역). `Init`에서
`GetComponents<EnemyAbility>()`로 부착 능력을 찾아 `Initialize(this)`로 소유자를 주입한다.

**이동 원리(흐름장 추종):** 매 프레임
1. `cell = grid.WorldToGrid(pos)` — 자기 월드 위치를 셀로 환산
2. `cell == grid.BaseCell` 이면 도달 처리
3. `pathfinder.TryGetNextStep(cell, out next)` 로 다음 셀을 얻어 그 중심으로 `MoveTowards`

셀 좌표는 월드에 고정이고 회전은 '타일 내용'만 순열하므로, **위치→셀 변환만으로 재계산된 흐름장을
자동 추종**한다(적 쪽에 회전 대응 코드가 필요 없다). 경로가 완전히 막히면(TryGetNextStep 실패)
현재는 대기한다 — 장애물 파괴 관통은 이후(§6 Phase 5).

- 수평(XZ) 이동만 하며 높이는 스폰 시 값을 유지. 진행 방향으로 부드럽게 회전(`faceTurnSpeed`).
- `_finished` 플래그로 도달/사망의 이중 처리를 막는다.

### 4.6 `EnemyAbility` (추상 MonoBehaviour)

적의 **능동 행동을 표현하는 확장점**. 능력치는 `EnemyData`(데이터), 능동 행동은 이 컴포넌트로 분리한다.
능력을 적 프리팹에 부착해 두면 `Enemy.Init`가 `Initialize(owner)`로 소유자를 주입한다.
5번째 이후의 특수 적도 이 클래스를 상속한 컴포넌트를 프리팹에 붙이는 것만으로 확장된다.

### 4.7 `ShielderAura` (`EnemyAbility`)

공병(실드병)의 능력(CLAUDE.md 4장 - "주변 적에 1회성 공격 무시 보호막 부여").
`interval`초마다 `radius` 반경 내 살아있는 적들에게 `Enemy.AddShield`로 보호막을 부여한다.
필드: `radius`(반경), `interval`(주기), `shieldCharges`(1회 부여량), `shieldSelf`(자기 포함).
`Shielder.prefab`(= `Temp_Enemy` 복제 + `Enemy` + `ShielderAura`)에 부착되어 있고,
공병 `EnemyData`가 이 프리팹을 참조한다.

### 4.8 `EnemyManager` (MonoBehaviour)

활성 적의 레지스트리이자 이벤트 허브. 씬에 1개 두고 스포너/터렛/기지가 공유한다.
여러 곳에서 간편히 접근하도록 가벼운 정적 `Instance`를 제공한다.

| 멤버 | 설명 |
|---|---|
| `Register(enemy)` | 스폰 직후 등록 + 적 이벤트 구독 |
| `Enemies`, `Count` | 현재 활성 적 목록/수 |
| `FindNearest(pos, maxRange)` | 최근접 적 반환(**터렛 타게팅용**, 없으면 null) |
| `OnEnemyReachedBase` | 기지 도달 통지 — `BaseCore`가 목숨 감소 구독 |
| `OnEnemyKilled` | 처치 통지 — 경제 시스템이 보상 드랍 구독 |

적의 `OnReachedBase`/`OnDied`를 받아 상위 이벤트로 재발행하고 `Despawn`(구독 해제 + `Destroy`)한다.
→ 적의 생성/소멸과 상위 시스템(기지·경제)의 결합을 여기서 흡수한다.

### 4.9 `WaveData` (ScriptableObject) + `WaveRunner` (MonoBehaviour)

정식 웨이브 시스템. 편성은 데이터(`WaveData` SO), 재생은 컴포넌트(`WaveRunner`)로 분리한다.

**`WaveData`** — `Create → TopViewDefense → Wave Data`. `StageData.waves`가 이 에셋을 참조한다.
3계층 데이터 모델:

| 타입 | 필드 | 의미 |
|---|---|---|
| `WaveData`(SO) | `waves` | 순서대로 재생할 `Wave` 목록(인덱스 0 = 첫 웨이브) |
| `Wave` | `label` / `restBefore` / `groups` | 표기명 / 시작 전 정비 시간(초) / 병렬 스폰 그룹들 |
| `SpawnGroup` | `enemy` / `corner` / `count` / `interval` / `startDelay` | 종류 / 등장 모서리(`SpawnCorner`) / 마리 수 / 마리 간격 / 웨이브 시작 기준 지연 |

`SpawnCorner`(`BottomLeft/BottomRight/TopLeft/TopRight/All`)는 맵 크기와 무관하게 편성을 이식하기 위한
enum으로, 런타임에 `StageData.CornerSpawns()`(순서 BL/BR/TL/TR) 셀로 해석된다.

**`WaveRunner`** — `WaveData`를 순서대로 재생한다(`waves` 비우면 `StageData.waves` 사용).

- **진행(클리어 기반):** 웨이브의 전 그룹 스폰 완료 후 그 적들이 모두 사라지면, 다음 웨이브의
  `restBefore`만큼 쉬고 다음 웨이브를 시작한다.
- **회전 연동:** 웨이브 시작마다 `OnWaveStarted(waveIndex)`를 발행 → `RotationScheduler`가 구독해
  `RotationEvent.triggerWave`/`WarningWave`에 맞춰 회전·경고를 발동한다(회전이 **웨이브 인덱스**에 묶임).
- 이벤트: `OnWavePreview(i)`(정비 시간 진입, 프리뷰 UI용), `OnWaveStarted(i)`, `OnWaveCleared(i)`(웨이브 적 전멸, 배너 UI용), `OnAllWavesCleared`(승리 판정용).
- **정비 카운트다운/즉시시작(HUD·즉시시작용 API):** `restBefore` 대기를 불투명한 `WaitForSeconds`가 아니라
  **노출·스킵 가능한 카운트다운**으로 처리한다. 공개 상태 `TotalWaves`·`PendingWave`·`IsResting`·`RestRemaining`·`RestDuration`와
  메서드 `SkipRest()`(즉시시작)를 제공 → `WaveHudUI`가 폴링 표시·배선한다(HUD·흐름은 [`Game-Flow-Architecture.md`](./Game-Flow-Architecture.md) §4.2).
  `Time.deltaTime` 감산이라 `timeScale=0`에서 자동 정지하며, `restBefore==0`은 기존과 동일(카운트다운 없이 통과). 기존 `CurrentWave`/`OnWaveStarted` 의미·타이밍은 불변.
- `MapBuilder.Grid`/`Pathfinder` 준비 대기 후 시작하고, 구독자가 붙을 틈을 위해 한 프레임 양보한다.
- 스폰 셀은 네 모서리를 기준으로 하되, 실제 `Spawn` 타일이 있으면 각 모서리를 최근접 `Spawn` 타일로 스냅.
- 프리팹에 `Enemy`가 없으면 `AddComponent`로 부착 → 임시 프리팹으로도 즉시 동작.

### 4.10 `BaseCore` (MonoBehaviour)

정중앙 기지의 목숨/승패 관리(CLAUDE.md 2장). **위치와 무관한 순수 로직**이다 —
적 도달은 `Enemy`가 `grid.BaseCell` 논리 셀 비교로 판정하므로, 이 컴포넌트의 Transform 위치는 영향이 없다.

| 멤버 | 설명 |
|---|---|
| `maxLives` / `CurrentLives` | 최대·현재 목숨(기본 3) |
| `IsGameOver` | 목숨 소진 여부 |
| `StarRating` | 현재 목숨 기준 별점(3/2/1, 게임오버 0) |
| `ApplyDamage(amount)` | 목숨 감소, 0이면 게임 오버 |
| `OnLivesChanged(cur, max)` | 목숨 변동(`LivesHudUI`가 구독) |
| `OnGameOver` | 게임 오버(`GameManager`가 구독 → 정지·결과·씬 전환) |

`EnemyManager.OnEnemyReachedBase`를 구독해 적의 `damageToBase`만큼 목숨을 깎는다
(적과 직접 결합하지 않음). 매니저가 늦게 생성돼도 `OnEnable`/`Start`에서 재시도 구독한다.

> 목숨 HUD와 종료 처리(웨이브 정지·`timeScale=0`·결과 화면·씬 전환·별점)는 상위 게임 흐름 계층이
> 담당한다 → [`Game-Flow-Architecture.md`](./Game-Flow-Architecture.md)(`LivesHudUI`·`GameManager`·`GameResultUI`).

---

## 5. 런타임 흐름

```
StageData → MapBuilder ─→ GridState + Pathfinder(흐름장)
   │ waves                       ▲            ▲
   ▼                            │ 참조        │ TryGetNextStep
   WaveRunner ──스폰──────→ Enemy(IDamageable)
        │  Register              │  이동: WorldToGrid → TryGetNextStep → MoveTowards
        │  OnWaveStarted(i) ─→ RotationScheduler(웨이브 인덱스 기반 회전/경고)
        ▼                        │
   EnemyManager ◀──이벤트(OnReachedBase / OnDied)──┘
        │
        ├─ OnEnemyReachedBase ─→ BaseCore : 목숨 감소 → (0) OnGameOver
        ├─ OnEnemyKilled       ─→ EnemyRewardDropper : 에너지 즉시 지급 + 골드 세션 누적(클리어 시 뱅킹)
        └─ FindNearest         ─→ Turret 타게팅 (Turret-Architecture.md §4.3)
```

권장 구현 조합(설계 결론):
**데이터 주도(`EnemyData` SO) + 흐름장 추종 이동 + 이벤트 허브(`EnemyManager`)로 결합 분리.**

> 적이 경로를 소유하지 않고 흐름장을 참조만 하므로, 맵 회전·장애물 파괴에 대한 대응이
> 적 코드에 없다. 회전 대응은 맵 계층(`Pathfinder.Recompute`)에서 끝나고 적은 공짜로 따라간다.

---

## 6. TODO / 다음 단계

**완료 (Phase 0~3 — 굴러가는 기본 루프, PlayScene 검증 완료):**

- [x] `IDamageable` + `DamageType`: 터렛↔적 분리 계약
- [x] `EnemyType` + `EnemyData`(SO): 데이터 주도 적 정의
- [x] `Enemy`: 흐름장 추종 이동 + 체력/피격/도달 이벤트(회전 자동 대응)
- [x] `EnemyManager`: 레지스트리 + `FindNearest` + 도달/처치 이벤트 허브
- [x] `WaveRunner`: 웨이브 스폰(구 EnemySpawner를 대체)
- [x] `BaseCore`: 목숨 3·게임오버·별점

**완료 (Phase 4 — 적 4종 확정 + 능력/내성):**

- [x] 적 4종 `EnemyData` 에셋 작성(`Resources/EnemyData/`): 돌격·장갑·정찰·공병
- [x] `EnemyAbility` 추상 + `ShielderAura`: 공병의 보호막 아우라(확장점 컴포넌트)
- [x] `Enemy` 보호막(1회 무시) + `EnemyData` 속성 내성 적용
- [x] `Shielder.prefab` 생성(Enemy + ShielderAura) + 공병 데이터가 참조

**완료 (Phase 5 준비 — 웨이브 데이터 분리):**

- [x] **웨이브 데이터 분리**: `WaveData`(SO) + `WaveRunner`. 모서리별 스폰 목록·간격, 웨이브 수,
      정비 시간(`restBefore`). `StageData.waves`가 참조.
- [x] **회전-웨이브 연동**: `RotationEvent`가 `triggerTime`(초) → `triggerWave`(웨이브 인덱스)로 전환.
      `RotationScheduler`가 `WaveRunner.OnWaveStarted`를 구독해 발동(경고는 `warningWavesBefore` 웨이브 전).
      ⚠️ 이관: 기존 스테이지의 회전 이벤트는 `triggerWave`를 재입력해야 함(필드 변경으로 옛 값 리셋).

**완료 (속성 처리 + 경제 연동):**

- [x] **속성 처리 확장**: 도트(DoT) 지속 피해 파이프라인(파이어 터렛), 프리즈 감속.
      `Enemy.ApplySlow`/`ApplyDoT`/`TickDoT`(방어력 무시·속성 내성 적용) + 터렛 `FireArea`가 적용.
- [x] **경제 연동**: `OnEnemyKilled` → `EnemyRewardDropper`. 에너지는 처치 즉시 `PlayerEconomy.Add`,
      골드는 세션 누적 후 **클리어 시에만** `PlayerProgress.AddGold`로 확정(패배 시 폐기, CLAUDE.md 7장).

**미착수:**

- [ ] **Phase 5 — 폐쇄회로 대응**: 길이 완전히 막히면 `Pathfinder.DestructionTargets`의 Obstacle을
      적이 공격·파괴하고 통과(CLAUDE.md 3장). 현재는 막히면 대기.
- [x] **터렛 연동(1차)**: `FindNearest`로 타게팅하는 기본 터렛 배치·조준·히트스캔 구현
      ([`Turret-Architecture.md`](./Turret-Architecture.md)). 나머지 4종·투사체·경제 드랍은 그 문서 §6 참고.
```
