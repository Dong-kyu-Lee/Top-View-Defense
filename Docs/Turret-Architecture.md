# 터렛 아키텍처 문서 (Turret Architecture)

> 아군 터렛의 데이터·타게팅·배치·회전 동반 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 5장(아군 유닛)·6장(배치/제어)·7장(경제) 참고.
> 적/전투 계층은 [`Enemy-Architecture.md`](./Enemy-Architecture.md), 맵/회전 계층은
> [`Map-Architecture.md`](./Map-Architecture.md)에 정리되어 있으며, 이 문서는 그 위에 얹힌다.

---

## 1. 개요

터렛 시스템을 적/맵 계층과 동일하게 **데이터 주도(Data-driven)** 로 설계한다.
터렛 5종은 **같은 `Turret` 컴포넌트 + 서로 다른 `TurretData` 에셋**으로 표현하고, 배치는 데이터(선택된
`TurretData`)와 컨트롤러(`TurretPlacer`)로 분리한다. → **enum 값 + 에셋 추가만으로 종류가 확장**된다.

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md) | 대응 설계 |
|---|---|
| 아군 5종, 추후 확장 (5장) | `TurretType` enum + `TurretData`(SO) 데이터 주도 |
| Position 고정, 타겟만 조준 (5장) | `Turret`이 `EnemyManager.FindNearest` → Y축 회전만 |
| 가장 가까운 적에게 발사 (5장) | `EnemyManager.FindNearest(pos, range)` 재사용 |
| UI 슬롯 선택 → 솟은 땅에 배치 (6장) | `TurretHud`(버튼) → `TurretPlacer.Arm` → 클릭 배치 |
| 터렛 동반 회전(위치+방향) (3장) | 터렛을 **타일 오브젝트의 자식**으로 부착(회전 무료) |
| 에너지로 구매/철거 환급 (6·7장) | `PlayerEconomy` 지갑 + `TurretData.cost` |
| 에너지 터렛 맵당 3개 제한 (5장) | `TurretData.maxCount` + `TurretPlacer` 개수 검증 |

---

## 2. 의존 규약

- **좌표계**는 맵 문서 2장 규약을 그대로 따른다(x=열, y=행, 좌하단 원점, 월드 XZ 평면).
- 터렛은 **적을 직접 알지 않는다.** 타게팅은 `EnemyManager.FindNearest`(최근접 질의)만 참조하고,
  피격은 `IDamageable.TakeDamage`로만 가한다 → 적/터렛은 [`Combat/IDamageable.cs`](../Assets/Scripts/Combat/IDamageable.cs)로 분리된다.
- **회전은 터렛이 소유하지 않는다.** 터렛을 자기 셀의 **타일 오브젝트 자식**으로 배치하면, 맵의
  `RotationScheduler`가 타일 오브젝트를 피벗 회전시킬 때 위치·방향이 함께 실린다. 터렛 쪽에 회전
  대응 코드가 필요 없다(적이 흐름장을 참조만 하는 것과 같은 이점).
- **점유는 별도 자료구조를 두지 않는다.** `GridState.GetObject(cell)`가 준 타일 오브젝트에 자식
  `Turret`이 있는지로 판정한다 → 회전으로 셀↔오브젝트 매핑이 순열돼도 항상 정합.

---

## 3. 파일 구조

```
Assets/Scripts/
├── Combat/
│   └── IDamageable.cs     # 피격 계약 + DamageType enum (터렛↔적 분리, 적 문서와 공유)
├── Turrets/
│   ├── TurretType.cs      # 터렛 5종 enum
│   ├── TurretData.cs      # 종류별 비용·능력치·특수필드 (ScriptableObject)
│   ├── Turret.cs          # 터렛 런타임 컴포넌트 (타게팅 + Y회전 + 히트스캔 + 발사 VFX 훅) (MonoBehaviour)
│   ├── TurretProjectile.cs# 순수 연출용 탄체(호밍 이동 + 임팩트, 데미지 무관) (MonoBehaviour)
│   ├── VfxPool.cs         # 연출(총구/탄체/임팩트) 오브젝트 풀 + 1회성 파티클 자동 반납 (MonoBehaviour)
│   ├── TurretPlacer.cs    # 선택·검증·배치·점유 판정 컨트롤러 (MonoBehaviour)
│   ├── TurretButton.cs    # 정식 uGUI 버튼 1개(자기 TurretData + onClick 자가 배선)
│   ├── TurretHudUI.cs     # 정식 uGUI HUD 컨트롤러(버튼 상태 + 에너지 텍스트 구독)
│   └── TurretHud.cs       # 프로토타입 IMGUI 버튼바 + 에너지 표시 (임시, 정식 배선 후 제거)
└── Core/
    └── PlayerEconomy.cs   # 인게임 재화 '에너지' 지갑 (MonoBehaviour)
```

씬 배선: `TurretSystem` 오브젝트 하나에 `PlayerEconomy` + `TurretPlacer` + `TurretHud`를 얹는다.
`EnemyManager`/`MapBuilder`는 런타임에 탐색(`FindObjectOfType`)해 연결한다.

---

## 4. 타입 상세

### 4.1 `TurretType` (enum)

`Basic=0, Double=1, Freeze=2, Energy=3, Fire=4` (CLAUDE.md 5장: 기본/더블/프리즈/에너지/파이어).
적 `EnemyType`과 동일 철학으로 **값 자체로 로직을 분기하지 않는다.** 능력치는 `TurretData`, 특수
행동은 데이터 필드/컴포넌트로 표현한다. **enum 이름이 프리팹 파일명과 일치**한다
(`Resources/Prefabs/Turrets/{TurretType}`).

### 4.2 `TurretData` (ScriptableObject)

`Create → TopViewDefense → Turret Data` 로 종류당 1개 생성.

| 필드 | 의미 |
|---|---|
| `type` | 터렛 종류(`TurretType`) |
| `displayName` | 표시 이름(UI/디버그) |
| `prefab` | 배치 프리팹(**비우면 `Resources/Prefabs/Turrets/{type}` 이름으로 폴백 로드**) |
| `cost` | 배치에 드는 에너지 |
| `maxCount` | 맵당 최대 설치 개수(0 = 무제한). 에너지 터렛 = 3 |
| `range` | 사거리(**셀 단위**). 월드 사거리 = `range * cellSize` |
| `fireInterval` | 발사 주기(초). 1 = 1초에 1회 |
| `damage` | 1회 발사 데미지(0이면 비공격 터렛 = 에너지) |
| `damageType` | 공격 속성(`DamageType`, 적 내성/도트 판정) |
| `shotsPerFire` | 1회 발사 시 타격 횟수(**더블 터렛 = 2**, 데이터만으로 표현) |
| `areaRadius` / `slowMultiplier` / `effectDuration` / `dotPerSecond` / `energyPerCycle` | 특수(종류별) 필드 — 프리즈 감속·파이어 도트·에너지 생산. `Turret`이 이 필드로 행동을 가른다(`areaRadius>0`=광역, `energyPerCycle>0`=생산) |
| `muzzlePrefab` / `projectilePrefab` / `impactPrefab` | 연출(VFX) 프리팹(**모두 선택**). 총구 플래시 / 날아가는 탄체 / 명중·폭발 파티클. 순수 표시용 — 데미지와 무관. `projectilePrefab`을 비우면 탄체 없이 목적지에 `impactPrefab`만 즉시 재생(순수 히트스캔) |
| `projectileSpeed` / `muzzleLocalOffset` | 탄체 속도(월드 유닛/초, 빠를수록 히트스캔에 근접) / 총구 스폰 지점의 터렛 로컬 오프셋(모델 포신에 맞춤) |

**작성된 에셋** (`Resources/TurretData/`): 기본 터렛(cost 50 / range 5 / interval 1 / dmg 20). 나머지
4종은 에셋 추가로 확장(`TurretHud`가 폴더 전체를 로드하므로 버튼이 자동으로 늘어난다).

### 4.3 `Turret` (MonoBehaviour)

배치된 셀에 **Position은 고정**되고, 사거리 안 가장 가까운 적을 향해 **Rotation(Y축)만** 돌리며 주기적으로
발사한다. 종류별 차이는 `TurretData`가 주입한다(`EnemyData`/`Enemy`와 동일 철학).

| 멤버 | 설명 |
|---|---|
| `Init(data, cell, cellSize)` | 배치 직후 주입(사거리 `range*cellSize`로 환산) |
| `Data`, `Cell`, `Position` | 상태 조회(`Cell`은 배치 당시 셀, 회전 후 실제 위치와 달라질 수 있음) |

**동작 원리(매 프레임):** 페이로드는 enum이 아니라 **데이터 필드**로 가른다.
1. **생산형**(`energyPerCycle > 0`, 에너지)은 조준하지 않고 `TickProduction` — 주기마다 `PlayerEconomy.Add`.
2. 페이로드가 없으면(피해·감속·도트 모두 없음) 즉시 반환. 있으면(`HasPayload`) 조준·발사한다
   (순수 프리즈처럼 `damage=0`이어도 감속을 실으면 발사 대상).
3. `AcquireOrValidateTarget` — 기존 타겟이 죽거나 사거리를 벗어나면 버리고, 없으면
   `EnemyManager.Instance.FindNearest(Position, range)`로 최근접 적을 새로 얻는다.
4. 타겟이 있으면 `FaceTarget` — `LookRotation`을 향해 `faceTurnSpeed`로 보간(**Y축만**, Position 불변).
5. 쿨다운이 끝나면 `Fire`(히트스캔) — `areaRadius > 0`이면 최근접 적 **위치**에 광역(`FireArea`: 반경 내
   전원에 피해/`ApplySlow`/`ApplyDoT`), 아니면 단일(`FireSingle`: 타겟에 `shotsPerFire`만큼 `TakeDamage`).
6. 데미지 확정 **후** `SpawnFireVfx` — 순수 연출 계층. 총구 플래시(`muzzlePrefab`) + 탄체(`projectilePrefab`,
   단일은 `shotsPerFire`발을 `BarrelOffset`로 살짝 벌려 스폰) 또는 탄체 없이 즉시 임팩트. VFX 파라미터
   (목적지/타겟 Transform/방향)는 데미지 적용 **전에 스냅샷**해, 피격으로 타겟이 파괴돼도 마지막 지점으로
   날아가 사라진다. 데미지는 히트스캔에서 이미 확정 → 탄체(`TurretProjectile`)는 재차 피격을 주지 않는다.

> **연출은 게임플레이와 분리(비권위):** 탄체를 실제로 날려 명중 시 피해를 주는 방식으로 바꾸지 않는다.
> 적 이동 + 맵 회전 탓에 그러면 예측사격/탄속 종속 밸런싱이 필요해지고 `FindNearest → TakeDamage`의
> 단순·정합이 깨진다. 대신 데미지는 즉시 확정하고 탄체는 "이미 맞은 것"을 그리기만 한다(TD 관용).
> 탄체·파티클은 `VfxPool`로 프리팹별 재사용(초당 다수 발사의 GC 스파이크 방지). 탄체는 월드 좌표로
> 날아가므로 맵 회전과 무관하고, 총구는 터렛 자식이라 회전에 함께 실린다.

> 회전 대응 코드가 없다. 터렛이 타일 오브젝트의 자식이면 `RotationScheduler`의 피벗 회전에 위치·방향이
> 함께 실려 자동 동반 회전한다. 조준은 매 프레임 월드 기준으로 다시 계산되므로 회전 후에도 정확하다.

### 4.4 `TurretPlacer` (MonoBehaviour)

UI에서 터렛을 '선택(`Arm`)'한 뒤 맵의 설치 가능한 칸을 클릭하면 그 자리에 배치한다(CLAUDE.md 6장).
입력(포인터 클릭)과 테스트가 **단일 배치 경로(`TryPlace`)** 를 공유한다.

| 멤버 | 설명 |
|---|---|
| `Arm(data)` / `Disarm()` | 배치 대기 터렛 설정(같은 것 재클릭 = 토글 해제). `Disarm`은 철거 모드도 해제 |
| `Armed` | 현재 선택된 터렛(null = 배치 모드 아님) |
| `ArmDemolish()` / `Demolishing` | 철거 모드 토글 / 현재 철거 모드 여부(배치와 **상호 배타**) |
| `OnModeChanged` | 모드(배치/철거) 변경 이벤트. HUD가 구독해 버튼 하이라이트 갱신 |
| `CanPlace(data, cell, out reason)` | 배치 가능 여부 + 불가 사유(경계/지형/점유/개수/에너지) |
| `TurretAt(cell)` / `IsOccupied(cell)` | 셀 타일 오브젝트의 자식 `Turret`(없으면 null) / 존재 여부(회전 후에도 정합) |
| `TryPlace(data, cell)` | 검증 → 에너지 차감 → 생성 → 타일 자식 부착. 성공 시 true |
| `TryDemolish(cell)` | 셀 터렛 철거 → **비용 50%(반올림) 환급** → 제거. 성공 시 철거 모드 종료(버튼당 1개) |
| `Turrets` | 배치된 터렛 목록(개수 제한/철거에서 사용) |

**배치 규칙(`CanPlace`):** 그리드 경계 안 + `Buildable` + 미점유 + (개수 제한 미도달) + 에너지 충분.

**철거 처리(`TryDemolish`):** 배치와 대칭인 단일 경로. 좌클릭 → `ScreenToCell`(배치와 동일하게 타일
콜라이더 레이캐스트) → `TurretAt(cell)`으로 해당 셀 터렛을 찾아, `PlayerEconomy.Add(round(cost*0.5))`로
환급하고 목록에서 제거·`Destroy`(타일의 자식인 정규화 래퍼 루트만 제거, 타일은 유지)한다. 성공 시
`Demolishing=false`로 **모드가 꺼져** 다음 철거는 철거 버튼 재클릭이 필요하다(CLAUDE.md 6장, 버튼당 1개).
빈 칸을 클릭하면 아무 일도 없고 모드는 유지된다(재시도 가능).

**배치 처리(`TryPlace`):**
1. `PlayerEconomy.TrySpend(cost)`로 실제 차감(실패 시 배치 안 함 — 레이스 방지).
2. 프리팹 해석: `data.prefab` 없으면 `Resources/Prefabs/Turrets/{type}` 로드.
3. `SpawnNormalized`로 타일 윗면 중앙(`TileTop`, 스케일된 큐브 높이 반영)에 **정규화 스폰** 후,
   `SetParent(tileObj, worldPositionStays:true)`로 **타일 오브젝트의 자식**으로 부착(월드 스케일/포즈 보존).
4. `Turret` 컴포넌트가 없으면 `AddComponent`(정규화 래퍼 루트에) 후 `Init` → 목록 등록.

> **정규화 스폰(`SpawnNormalized`)**: 임포트 모델마다 피벗(트랜스폼 원점)이 메시와 어긋날 수 있다(정점이
> 모델 원점에서 벗어나게 구워진 OBJ 등). 그대로 두면 배치가 어긋나 보이고, 루트를 도는 `FaceTarget` 조준이
> 메시를 궤도로 돌린다. → 렌더러 바운즈의 **밑면 중심**을 `TileTop`에 맞추고 그 지점을 피벗으로 삼는 래퍼로
> 감싼다(`Turret`은 이 래퍼에 부착). 배치 정확 + 조준이 제자리 회전. 렌더러가 없으면 정규화를 건너뛴다.

**입력(신형 Input System):** `Mouse.current`/`Keyboard.current`로 좌클릭(배치)/우클릭·ESC(취소)를 읽는다.
좌클릭 화면 좌표에서 **타일 콜라이더와 광선 교차**(`ScreenToCell` → `Physics.Raycast`)로 셀을 얻는다.
바닥 평면 방식은 솟은 땅의 **윗면이 아니라 바닥면**에서 교차해, 카메라가 기울어진 만큼(솟은 높이 ×
tan(기울기)) 셀이 밀리는 시차 오차가 있었다 → 실제 타일 윗면을 직접 맞혀 제거한다. 타일 콜라이더는
`MapBuilder`가 스폰 시 보장(없으면 `BoxCollider` 보강). 맞힐 레이어는 `tileMask`(비우면 기본 레이어 전체).
화면 하단 `bottomUiMargin`(px) 영역은 IMGUI HUD와 충돌하지 않도록 배치 클릭에서 제외한다.

### 4.5 HUD 계층 — `TurretHudUI` + `TurretButton` (정식 uGUI) / `TurretHud` (임시 IMGUI)

**정식(`TurretHudUI` + `TurretButton`):** PlayScene의 uGUI 버튼(Basic/Double/Freeze/Energy/Fire)과
`RemainEnergy`(TMP) 텍스트에 배선한다. `TurretPlacer`/`PlayerEconomy`는 수정하지 않는다(이미 `Arm`/`Armed`/
`OnEnergyChanged` 공개, UI 위 클릭은 `EventSystem`이 걸러냄) → 교체 대상은 "그리는 계층"뿐.

- `TurretButton`(버튼마다 1개): 자기 `TurretData`를 들고 `Button.onClick`을 자가 배선, 이름/비용을 데이터로 표기.
  터렛 추가 = **버튼 복제 + data 지정**(데이터 주도 철학과 정합).
- `TurretHudUI`(패널에 1개): 자식 `TurretButton` 수집·배선, `OnEnergyChanged` 구독 → 잔량 텍스트 갱신 +
  버튼 여유(`interactable`) 재평가, 선택(`Armed`) 하이라이트를 갱신한다. **철거 버튼**은 `TurretPlacer.
  ArmDemolish`를 호출해 철거 모드를 토글하고, `OnModeChanged`를 구독해 배치 버튼과 철거 버튼 하이라이트
  (`destroyArmedIndicator`, 배치 버튼의 `armedIndicator`와 동일 방식)를 한 곳(`RefreshMode`)에서 일관 갱신한다
  → 철거 성공 후 자동 해제·ESC 취소·배치↔철거 전환이 모두 반영된다. 경제 구독은 `Start`에서(Awake 순서 무관
  안전, 초기값 1회 반영).

**임시(`TurretHud`, IMGUI):** 씬 배선 없이 배치 루프를 굴려보기 위한 프로토타입 계층(MapBuilder의 Cube 폴백과
같은 성격). 정식 UI 배선 후에는 `TurretSystem`에서 이 컴포넌트를 제거한다(겹쳐 그려짐 방지).

### 4.6 `PlayerEconomy` (MonoBehaviour)

인게임 전술 재화 '에너지' 지갑(CLAUDE.md 7장 ②). 스테이지 입장 시 초기 에너지로 시작해 터렛 구매에
소모하고, 철거 시 환급받는다. `EnemyManager`처럼 가벼운 정적 `Instance`를 제공한다.

| 멤버 | 설명 |
|---|---|
| `startingEnergy` / `Energy` | 초기 지급(기본 100) / 현재 보유 |
| `TrySpend(cost)` | 충분하면 차감하고 true, 부족하면 변화 없이 false |
| `Add(amount)` | 획득(처치 드랍/자동 드랍/철거 환급) |
| `CanAfford(cost)` | 감당 가능 여부(차감 안 함) |
| `OnEnergyChanged(cur)` | 잔량 변동(HUD가 구독) |

적 처치 드랍(`EnemyManager.OnEnemyKilled`) 연동은 이후 경제 페이즈에서 `Add`로 붙인다.

> **주기적 자동 지급은 구현됨:** `EnergyDripper`가 일정 간격으로 `Add`를 호출한다(같은 `TurretSystem` 오브젝트에 부착).
> 상세는 [`Game-Flow-Architecture.md`](./Game-Flow-Architecture.md) §4.1.

---

## 5. 런타임 흐름

```
StageData → MapBuilder ─→ GridState(타일↔오브젝트) + Pathfinder
   │                              ▲  GetObject(cell) = 타일 오브젝트
   ▼                              │
TurretHud ──버튼 클릭──→ TurretPlacer.Arm(data)
   │  (Resources/TurretData)          │  좌클릭 → ScreenToCell → CanPlace → TryPlace
   │                                  ▼
PlayerEconomy ◀── TrySpend(cost) ── 배치: 프리팹 생성 → 타일 오브젝트 자식으로 SetParent
                                          │
                                          ▼
                                        Turret(고정 위치)
                                          │  매 프레임: FindNearest → Y회전 → TakeDamage
                                          ▼
   RotationScheduler ──타일 회전──→ (자식 터렛 위치·방향 동반 회전) ← 코드 없이 무료
                                          │
   EnemyManager.FindNearest ◀────────────┘   IDamageable.TakeDamage ─→ Enemy
```

권장 구현 조합(설계 결론):
**데이터 주도(`TurretData` SO) + 타일 자식 부착(회전 동반) + `EnemyManager`/`IDamageable`로 결합 분리.**

> 터렛이 회전을 소유하지 않고 타일 오브젝트의 자식으로만 존재하므로, 맵 회전 대응이 터렛 코드에 없다.
> 회전 동반은 맵 계층(`RotationScheduler`의 피벗 reparent)에서 끝나고 터렛은 공짜로 따라간다.

---

## 6. TODO / 다음 단계

**완료 (1차 — 기본 터렛 배치 수직 관통, PlayScene 검증 완료):**

- [x] `TurretType` + `TurretData`(SO): 데이터 주도 터렛 정의(+ 기본 터렛 에셋)
- [x] `Turret`: `FindNearest` 타게팅 + Y축 조준 회전 + 히트스캔 발사
- [x] `TurretPlacer`: 선택(`Arm`)·검증(`CanPlace`)·배치(`TryPlace`)·점유 판정, 신형 Input System 대응
- [x] **타일 자식 부착으로 회전 동반** 검증(위치·방향 함께 회전, 회전 후 점유 정합)
- [x] `PlayerEconomy`: 에너지 지갑 스텁(구매 차감/환급)
- [x] `TurretHud`: 프로토타입 IMGUI 버튼바 + 에너지 표시
- [x] PlayScene에 `TurretSystem` 오브젝트 배선

**완료 (2차 — 4종 특수 행동, 코드 관통. 에셋/프리팹은 미생성):**

- [x] **나머지 4종**: 더블(`shotsPerFire=2`, 데이터만), 프리즈(광역 감속), 파이어(광역 DoT),
      에너지(공격 없음·`energyPerCycle` 생산). `Turret`이 데이터 필드로 분기(생산/광역/단일).
- [x] **적 피동 상태**: `Enemy.ApplySlow`(이동속도 배수)·`Enemy.ApplyDoT`(1초 주기 틱, **방어력 무시**,
      속성 내성은 적용). 적이 상태를 소유·만료 처리(`AddShield`와 같은 계층) → 터렛은 1회 스탬프만.

**미착수:**

- [x] **투사체/발사 연출**: `TurretData` VFX 필드(총구/탄체/임팩트/속도/오프셋) + `Turret.SpawnFireVfx` 훅 +
      순수 연출 탄체 `TurretProjectile`(호밍, 데미지 무관) + 프리팹 재사용 `VfxPool`(1회성 파티클 자동 반납).
      **데미지는 히트스캔 유지**(비권위 연출). 남은 건 에디터 배선: 각 `TurretData`에 VFX 프리팹 지정(비우면 무연출).
- [x] **철거/판매**: 철거 버튼 → 철거 모드 → 맵의 터렛 클릭 → 소모 에너지 50%(반올림) 환급·제거,
      버튼당 1개(성공 시 모드 종료). `TurretPlacer.TryDemolish` + `TurretHudUI` 철거 모드 배선(CLAUDE.md 6장).
- [x] **정식 UI**: IMGUI `TurretHud` → uGUI(`TurretHudUI` + `TurretButton`) 교체. PlayScene 배선 완료
      (버튼 5종·RemainEnergy·DestroyButton·철거 `ArmedIndicator`). 배치 버튼 `armedIndicator`는 선택(미배선).
- [x] **경제 연동**: `EnemyManager.OnEnemyKilled` → `EnemyRewardDropper`(에너지 즉시 지급 + 골드 세션 누적→
      클리어 시 뱅킹) + 주기적 자동 드랍(`EnergyDripper`) + 에너지 터렛 생산(`Turret.TickProduction`).
- [x] **속성 처리**: 프리즈 감속(적 이동속도 배수)·파이어 도트 파이프라인(`Enemy.ApplySlow`/`ApplyDoT`).
```
