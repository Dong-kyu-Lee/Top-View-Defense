# 상점 아키텍처 (Shop Architecture)

> 타이틀 [상점]에서 **골드를 소모해 터렛 능력치를 영구 강화**하는 메타 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 2장(타이틀 화면)·7장 ①(골드 · 상점 시스템) 참고.
> 골드 저장소·진행 메타 계층은 [`Progression-Architecture.md`](./Progression-Architecture.md)가 세워 두었고
> (그 문서 §6이 "타이틀 상점은 `PlayerProgress.Gold`/`TrySpendGold`를 공유하는 별도 메타 화면"으로 예고),
> 이 문서는 그 저장소 위에 **강화 레벨 저장·상점 UI·전투 반영**을 얹는다.
> 강화가 실제로 적용되는 터렛 도메인은 [`Turret-Architecture.md`](./Turret-Architecture.md) 참고.

---

## 1. 개요

상점은 **인게임 전술 재화(에너지)가 아니라 영구 성장 재화(골드)** 를 소비하는 메타 화면이다(CLAUDE.md 7장 이중 경제 구조 ①).
아군 터렛 5종 각각의 **공격력·공격 속도**를 골드로 최대 3단계까지 영구 강화하며, 강화 효과는 이후 모든 스테이지의
전투에 반영된다. 세 덩어리로 구성한다.

- **모델(영속):** [`PlayerProgress`](../Assets/Scripts/Core/PlayerProgress.cs) 확장(강화 레벨 저장) + [`TurretUpgradeCatalog`](../Assets/Scripts/Turrets/TurretUpgradeCatalog.cs)(밸런스 표).
- **전투 반영(글루):** [`TurretUpgrades`](../Assets/Scripts/Turrets/TurretUpgrades.cs)(유효 수치 계산) → [`Turret`](../Assets/Scripts/Turrets/Turret.cs)이 배치 시 스냅샷.
- **UI:** [`ShopController`](../Assets/Scripts/Shop/ShopController.cs)(그리드·골드·뒤로가기) + [`ShopItemUI`](../Assets/Scripts/Shop/ShopItemUI.cs)(터렛 1칸: 강화 2행).

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md) | 대응 설계 |
|---|---|
| 골드로 공격력·공격 속도 영구 강화 (7장 ①) | `PlayerProgress.TryBuyUpgrade` — 골드 차감 후 레벨 저장 |
| 각 스탯 3회까지 강화, 횟수 표시 (요구) | 레벨 0~3 저장, `ShopItemUI` "L/3" 표시 + 만렙 시 버튼 비활성 |
| 에너지 터렛: 공격력↑=생산량↑, 공속↑=주기↓ (7장 ①) | `TurretUpgrades.EffectiveEnergyPerCycle`/`EffectiveInterval`로 의미 치환 |
| 보유 골드 표시 (7장 ①) | `ShopController` ← `PlayerProgress.Gold` |
| 타이틀 [상점] 진입 (2장) | `TitleSceneController.MoveShopScene()` → `ShopScene` |

---

## 2. 의존 규약

- **`PlayerProgress`가 유일한 진실의 원천.** 골드·강화 레벨은 오직 이 정적 저장소를 통해서만 읽고 쓴다.
  `StageProgressRecorder`(골드 적립)·상점(골드 소비·강화 저장)·전투(강화 레벨 읽기)가 **같은 저장소를 공유**한다.
- **의존 방향은 `Shop → Turrets → Core` 한 방향.** 상점 UI는 터렛 데이터를 참조하고, 터렛은 `PlayerProgress`(Core)를
  참조한다. `PlayerProgress`는 터렛 도메인을 모른다 — 강화 스탯은 **int 슬롯**(`StatDamage=0`/`StatSpeed=1`)으로 다뤄
  Core↔Turrets 결합을 피한다. `UpgradeStat` enum은 그 위의 의미 이름이고 값이 슬롯과 일치한다.
- **에셋은 불변.** [`TurretData`](../Assets/Scripts/Turrets/TurretData.cs)(ScriptableObject)는 런타임에 수정하지 않는다
  (에디터 오염 방지). 강화는 배치 시점에 [`Turret`](../Assets/Scripts/Turrets/Turret.cs)이 **유효 수치를 스냅샷해 캐시**하는 방식으로만 얹는다.
- **강화는 파밍 방지 규약을 따른다.** 만렙(레벨 3) 또는 골드 부족이면 강화 버튼은 비활성. `TryBuyUpgrade`는
  골드를 **먼저 차감한 뒤에만** 레벨을 올린다(차감 실패 시 레벨 불변 — 레이스 방지).
- **씬 등록:** `TitleScene`·`ShopScene`이 Build Settings에 등록돼야 한다(이름 로드로 전환). 상점 진입/이탈은
  타이틀↔상점 한 쌍이다(진입: 타이틀 [상점] 버튼, 이탈: 상점 BackButton → 타이틀).

---

## 3. 파일 구조

```
Assets/Scripts/
├── Core/
│   └── PlayerProgress.cs           # (수정) 강화 레벨 저장 API 추가 — GetUpgradeLevel/TryBuyUpgrade
├── Turrets/
│   ├── UpgradeStat.cs              # enum { Damage=0, Speed=1 } — 슬롯 의미 이름
│   ├── TurretUpgradeCatalog.cs     # 밸런스 표: 터렛 목록 + 레벨별 배수 + 레벨별 골드 비용 (ScriptableObject)
│   ├── TurretUpgrades.cs           # 유효 수치 계산(base + 강화) 정적 헬퍼
│   ├── TurretData.cs               # (수정) 상점 표시용 icon(Sprite) 추가
│   └── Turret.cs                   # (수정) Init에서 유효 damage/interval/energy 스냅샷·캐시
└── Shop/
    ├── ShopController.cs           # 그리드 생성 + 골드 텍스트 + BackButton (MonoBehaviour, ShopScene)
    └── ShopItemUI.cs               # 아이템 1칸: 공격력·공속 강화 2행 (MonoBehaviour, 프리팹)
```

> `TurretUpgradeCatalog`/`TurretUpgrades`/`UpgradeStat`은 **Turrets 네임스페이스**에 둔다 — 전투(터렛)가 강화 배수를
> 읽어야 하므로, 상점(Shop)이 아니라 터렛 도메인에 밸런스가 사는 것이 자연스럽고 `Shop → Turrets` 단방향 의존이 유지된다.
> `Resources/`에 `TurretUpgradeCatalog` 에셋 1개를 두고 `ShopController`(인스펙터 또는 폴백)와 `TurretUpgrades`(폴백 로드)가 공유한다.

---

## 4. 타입 상세

### 4.1 `PlayerProgress` 확장 (정적 클래스 · PlayerPrefs)

기존 별/골드 저장소([`Progression-Architecture.md`](./Progression-Architecture.md) §4.1)에 **터렛 강화 레벨**을 더한다.

| 키 패턴 | 의미 |
|---|---|
| `progress.upgrade.dmg.{turretTypeId}` | 그 터렛의 공격력 강화 레벨(0~3) |
| `progress.upgrade.spd.{turretTypeId}` | 그 터렛의 공격 속도 강화 레벨(0~3) |

| 멤버 | 설명 |
|---|---|
| `StatDamage`(0) / `StatSpeed`(1) / `MaxUpgradeLevel`(3) | 스탯 슬롯 상수 · 스탯당 최대 레벨 |
| `GetUpgradeLevel(int turretTypeId, int stat)` | 저장된 강화 레벨(0~Max). 상점/전투 양쪽이 읽는다 |
| `TryBuyUpgrade(int turretTypeId, int stat, int cost)` | 만렙이 아니고 골드 ≥ cost일 때만 **골드 차감 후** 레벨 +1. 성공 시 true |

- `turretTypeId`는 `(int)TurretType`(Basic=0 … Fire=4). Core가 터렛 enum을 참조하지 않도록 int로 받는다.
- `ResetAll`은 별·골드와 함께 강화 키도 삭제한다(디버그 초기화 시 강화도 리셋).
- 첫 실행 기본값(모든 레벨 0)은 별도 초기화 없이 "강화 안 됨"으로 자연 성립한다.

### 4.2 `UpgradeStat` (enum · Turrets 네임스페이스)

상점에서 강화하는 스탯 2종. 값은 `PlayerProgress.StatDamage`(0)/`StatSpeed`(1)와 **일치**시켜, 저장소는 int 슬롯으로
다루고 이 enum은 그 위의 의미 이름으로만 쓴다.

| 값 | 공격형 터렛 | 에너지 터렛(공격 없음) |
|---|---|---|
| `Damage`(0) | 공격력 | 1회 생산 에너지량 |
| `Speed`(1) | 공격 속도(발사 간격) | 에너지 생산 주기 |

### 4.3 `TurretUpgradeCatalog` (ScriptableObject · Turrets 네임스페이스)

강화의 **밸런스 표**. `StageCatalog`와 같은 철학으로, 코드 상수 대신 인스펙터에서 통제한다.

| 멤버 | 설명 |
|---|---|
| `turrets` (`List<TurretData>`) | 상점 그리드에 표시할 아군 5종. 순서가 그리드 순서 |
| `damagePerLevel` (기본 0.25) | 공격력 레벨당 증가율 → `DamageMultiplier(L) = 1 + 0.25·L` |
| `speedPerLevel` (기본 0.15) | 공속 레벨당 발사 간격 감소율 → `SpeedIntervalFactor(L) = 1 − 0.15·L`(0.1 미만 클램프) |
| `costPerLevel` (`int[]`, 기본 `{50,120,250}`) | 레벨 업 비용(누적 아님). index 0 = 0→1, 1 = 1→2, 2 = 2→3 |
| `DamageMultiplier(int)` / `SpeedIntervalFactor(int)` | 유효 수치용 배수(전투가 읽음) |
| `CostForNext(int currentLevel)` | 다음 단계 강화 비용. 만렙/범위 밖이면 0(상점이 읽음) |

- 배수·비용이 한 에셋에 모여 있어 전투(배수)와 상점(비용)이 같은 밸런스를 공유한다.
- `Resources/`에 단일 에셋 `TurretUpgradeCatalog`로 둔다.

### 4.4 `TurretUpgrades` (정적 헬퍼 · Turrets 네임스페이스)

터렛의 **유효 능력치**(base 데이터 + 영구 강화)를 계산하는 유일한 창구. `TurretData`(불변 에셋)를 수정하지 않고,
`PlayerProgress`(레벨) + `TurretUpgradeCatalog`(배수)를 합쳐 값을 만든다. 카탈로그는 Resources에서 지연 로드·캐시하며,
없으면 배수 1(강화 미적용)로 폴백한다.

| 멤버 | 설명 |
|---|---|
| `EffectiveDamage(TurretData)` | `data.damage × 공격력 배수` |
| `EffectiveInterval(TurretData)` | `data.fireInterval × 공속 배수`(최소 0.05초) |
| `EffectiveEnergyPerCycle(TurretData)` | 에너지 터렛 전용: `data.energyPerCycle × 공격력 배수`(반올림) |

- 에너지 터렛은 CLAUDE.md 7장 ① 규칙대로 공격력 강화가 **생산량**, 공속 강화가 **생산 주기**로 치환된다.

### 4.5 `Turret` 수정 (전투 반영 지점)

기존 [`Turret`](../Assets/Scripts/Turrets/Turret.cs)은 `Data.damage`/`Data.fireInterval`/`Data.energyPerCycle`을 에셋에서 직접 읽었다.
여기에 **배치 시점 스냅샷**을 끼운다.

- `Init(data, cell, cellSize)`에서 `TurretUpgrades`로 유효 수치를 구해 `_damage`/`_fireInterval`/`_energyPerCycle`에 캐시한다.
- 이후 `Update`(발사 쿨다운)·`FireSingle`/`FireArea`(피해)·`TickProduction`(에너지 생산)이 **에셋이 아니라 캐시 값**을 쓴다.
- 감속·도트 등 그 외 특수치는 강화 대상이 아니므로 그대로 `Data`에서 읽는다.
- **스냅샷 시점 규약:** 강화는 배치 순간에 고정된다 — 스테이지 도중 상점을 열 수 없으므로(상점은 타이틀 메타 화면),
  전투 중 값이 바뀔 여지가 없어 스냅샷으로 충분하다.

### 4.6 `ShopItemUI` (MonoBehaviour · 프리팹)

상점 그리드의 아이템 1칸 = 터렛 1종. [`Progression`의 `StageButtonUI`](../Assets/Scripts/Progression/StageButtonUI.cs)와 같은 패턴으로,
`ShopController`가 `Bind`로 터렛을 주입한다. 공격력·공격 속도 **강화 2행**을 표시한다.

| 표시(행마다) | 내용 |
|---|---|
| 레벨 텍스트 | `"L/3"`(예: 1/3, 3/3) — `PlayerProgress.GetUpgradeLevel` |
| 비용 텍스트(선택) | 다음 단계 비용 `"{cost} G"`, 만렙이면 `"MAX"` — `catalog.CostForNext` |
| 강화 버튼 | 클릭 시 구매. **만렙 OR 골드 부족이면 `interactable=false`** |

- 필드(대부분 선택): `nameLabel`/`icon`(= `TurretData.icon`) · 공격력 행(`damageButton`/`damageLevelText`/`damageCostText`) ·
  공속 행(`speedButton`/`speedLevelText`/`speedCostText`) · 포맷 문자열(`levelFormat`/`costFormat`/`maxedText`).
- 구매 흐름: `TryBuyUpgrade` 성공 → 이 칸 `Refresh`(레벨/버튼 즉시 반영) → `onPurchased` 콜백으로 상점에 알림
  (골드 텍스트 + 다른 칸의 '골드 부족' 재판정).

### 4.7 `ShopController` (MonoBehaviour · ShopScene)

ShopScene의 허브. [`StageSelectController`](../Assets/Scripts/Progression/StageSelectController.cs)와 같은 패턴으로
`TurretUpgradeCatalog`를 돌며 그리드에 `ShopItemUI`를 스폰하고, 골드 텍스트와 BackButton을 배선한다.

| 멤버 | 설명 |
|---|---|
| `catalog` | 스폰할 터렛 목록/배수/비용. 비우면 `Resources.Load("TurretUpgradeCatalog")` |
| `gridParent` / `itemPrefab` | `GridLayoutGroup` 컨테이너 / `ShopItemUI` 프리팹 |
| `goldText` | `PlayerProgress.Gold` 표시 |
| `backButton` | → `TitleScene` 로드 |
| `titleScene` | 씬 이름(기본 `TitleScene`) |

- `OnEnable`에서 그리드를 (재)구성하고 골드를 갱신한다(타이틀을 다녀오거나 씬 재진입 시 최신 반영).
- **구매 콜백 `OnPurchased`(핵심):** 골드가 줄면 다른 칸의 '골드 부족' 판정도 바뀌므로, 구매 후 골드 텍스트 +
  **모든** 아이템을 함께 갱신한다.

---

## 5. 런타임 흐름

```
[TitleScene]
 [상점 버튼] TitleSceneController.MoveShopScene() ─→ LoadScene(ShopScene)

[ShopScene]
 TurretUpgradeCatalog ─→ ShopController ─(스폰)→ ShopItemUI × N
   PlayerProgress.GetUpgradeLevel ─→ 각 행 "L/3" + 버튼 활성(만렙/골드 판정)
   PlayerProgress.Gold ─→ goldText
   [강화 버튼] PlayerProgress.TryBuyUpgrade(typeId, stat, cost)
        성공 → 이 칸 Refresh + OnPurchased(골드 텍스트 + 전 아이템 재판정)
   [BackButton] ─→ LoadScene(TitleScene)

[PlayScene] (이후 스테이지)
 TurretPlacer.TryPlace ─→ Turret.Init
        TurretUpgrades.Effective*(data) = TurretData(base) × Catalog 배수(PlayerProgress 레벨)
        → _damage / _fireInterval / _energyPerCycle 캐시 → 전투에 반영
```

권장 구현 조합(설계 결론):
**골드/강화는 `PlayerProgress` 단일 저장소 + 강화는 `TurretUpgrades`로 에셋 불변 유지 + UI는 저장소 읽기 전용.**

---

## 6. 구현 순서 / 범위 경계

**구현 순서(모델 → 전투 반영 → UI):**
1. `PlayerProgress` 강화 저장 API (`GetUpgradeLevel`/`TryBuyUpgrade`)
2. `UpgradeStat` + `TurretUpgradeCatalog` (밸런스: 배수·비용 표)
3. `TurretUpgrades` + `Turret` 스냅샷 — UI 전에 PlayScene에서 강화 레벨을 넣어 전투 반영을 먼저 검증 가능
4. `ShopItemUI` 프리팹 (강화 2행)
5. `ShopController` (그리드·골드·뒤로가기)
6. 씬 배선(`Resources/TurretUpgradeCatalog` 에셋 + ShopScene 구성 + Build Settings 등록 + 타이틀 [상점] 버튼) + 첫 실행/리셋 검증

**완료:**
- [x] `PlayerProgress` 강화 레벨 저장(별/골드 저장소 확장) + `ResetAll` 강화 키 삭제
- [x] `UpgradeStat` / `TurretUpgradeCatalog`(배수 `0.25`/`0.15`, 비용 `{50,120,250}`) / `TurretUpgrades`(유효 수치)
- [x] `Turret` 배치 시 유효 damage/interval/energy 스냅샷 + `TurretData.icon` 추가
- [x] `ShopItemUI`(강화 2행·만렙/골드 비활성) / `ShopController`(그리드·골드·BackButton→타이틀)
- [x] `TitleSceneController.MoveShopScene()` + ShopScene UI 배선 + Build Settings 등록

**범위 밖(후속):**
- 강화 수치는 **발사 성능(공격력·공격 속도/에너지 생산)** 에 한정한다. 사거리·광역 반경·감속·도트 강화는 도입하지 않았다
  (필요 시 `TurretUpgradeCatalog`에 배수 + `TurretUpgrades`에 계산만 추가하면 확장된다 — 저장소·UI 골격은 그대로 재사용).
- 강화 미리보기(강화 전/후 수치 툴팁)·사운드/연출은 다루지 않는다.
- 골드 획득은 여전히 스테이지 클리어 보상(`StageProgressRecorder`, [`Progression-Architecture.md`](./Progression-Architecture.md) §4.4)의 몫이다.
```
