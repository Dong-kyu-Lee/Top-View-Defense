# 진행/메타 아키텍처 (Progression Architecture)

> 스테이지 해금·별 기록·골드 경제의 **영구 저장 메타 계층**과 이를 표시하는 `StageSelectScene` 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 2장(스테이지 선택/승패)·7장(경제 ① 골드) 참고.
> 스테이지 세션 런타임(승패·HUD·정지)은 [`Game-Flow-Architecture.md`](./Game-Flow-Architecture.md)가 다루며,
> 이 문서는 그 계층이 남겨둔 훅([`GameManager.OnGameEnded`](../Assets/Scripts/Core/GameManager.cs))에
> **저장·해금·보상을 얹고**, 그 결과를 `StageSelectScene`에 표시한다.

---

## 1. 개요

`Game-Flow-Architecture.md` §6은 "별·골드 **영구 저장**, 다음 스테이지 **해금**, 클리어 보상은 StageSelect 메타 계층의 몫"이라
범위 밖으로 남겨 두었다. 이 문서가 그 계층이다. 세 덩어리로 구성한다.

- **모델(영속):** `PlayerProgress`(PlayerPrefs 저장소) + `StageCatalog`(스테이지 목록/보상 표).
- **글루(씬 연결):** `StageSession`(선택 스테이지 핸드오프) + `StageProgressRecorder`(종료 훅 → 저장/보상/해금).
- **UI:** `StageSelectController`(그리드·골드·뒤로가기) + `StageButtonUI`(자물쇠/별/3상태).

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md) | 대응 설계 |
|---|---|
| 최초 1번만 해금, 클리어 시 순차 해금 (2장) | `PlayerProgress.IsUnlocked` — 파생 규칙(직전 스테이지 별 ≥ 1) |
| 스테이지별 별 개수 영구 확인 (2장) | `PlayerProgress.GetStars` — **최고기록만** 저장 |
| 클리어 별 개수 차등 골드 보상, 실패 0 (7장 ①) | `StageProgressRecorder` — 보상 표, **갱신분만** 지급 |
| 보유 골드 표시 (7장 ①) | `StageSelectController` ← `PlayerProgress.Gold` |
| 선택 스테이지로 게임 시작 (2장) | `StageSession.SelectedStage` → `MapBuilder`가 로드 |

---

## 2. 의존 규약

- **`PlayerProgress`가 유일한 진실의 원천.** 별·골드·해금 상태는 오직 이 정적 저장소를 통해서만 읽고 쓴다.
  `StageSelectScene`(읽기)·`StageProgressRecorder`(쓰기)·타이틀 [상점](골드 읽기/쓰기)이 **같은 저장소를 공유**한다.
- **도메인은 건드리지 않는다.** `GameManager`/`BaseCore`는 수정하지 않는다. `StageProgressRecorder`가
  기존 `GameManager.OnGameEnded(result, stars)`를 **구독만** 해 저장한다(Game-Flow §2의 read-only 철학 유지).
- **해금은 파생, 저장하지 않는다.** 해금 플래그를 따로 두지 않고 별 기록에서 계산한다
  (`N==1 || GetStars(N-1) >= 1`). 저장 항목이 줄고 별 기록과 어긋날 여지가 없다.
- **재보상은 갱신분만.** 이미 깬 스테이지를 다시 깨도 `보상(새 별) − 보상(기존 최고 별)`(음수는 0)만 지급해
  반복 파밍(스노우볼)을 막는다.
- **씬 등록:** `TitleScene`·`StageSelectScene`·`PlayScene`이 Build Settings에 모두 등록돼야 한다
  (이름 로드로 전환하므로). `GameManager.GoToStageSelect()`도 이 이름에 의존한다.

---

## 3. 파일 구조

```
Assets/Scripts/
├── Core/
│   └── PlayerProgress.cs          # PlayerPrefs 저장소: 별/골드/해금 (정적 클래스, 비 MonoBehaviour)
├── Map/
│   └── StageSession.cs            # 선택 스테이지 씬 간 핸드오프 (정적 홀더, Map에 둬 MapBuilder→Core 의존 회피)
├── Progression/
│   ├── StageCatalog.cs            # 스테이지 목록(StageData 정렬) + 골드 보상 표 (ScriptableObject)
│   ├── StageProgressRecorder.cs   # OnGameEnded 구독 → 별 저장·골드보상·해금 (MonoBehaviour, PlayScene)
│   ├── StageSelectController.cs   # 그리드 생성 + 골드 텍스트 + BackButton (MonoBehaviour)
│   └── StageButtonUI.cs           # 버튼 1칸: 자물쇠/별/클릭 3상태 (MonoBehaviour, 프리팹)
└── Map/
    └── MapBuilder.cs              # (수정) StageSession.SelectedStage 우선 로드
```

> `PlayerProgress`는 `MonoBehaviour`가 아니다 — 씬에 얹지 않고 어디서든 정적 호출한다.
> `StageProgressRecorder`는 PlayScene에, `StageSelectController`/`StageButtonUI`는 StageSelectScene에 배치.

---

## 4. 타입 상세

### 4.1 `PlayerProgress` (정적 클래스 · PlayerPrefs)

영구 저장의 유일한 창구. 키 접두어로 네임스페이스를 나눈다.

| 키 패턴 | 의미 |
|---|---|
| `progress.stars.{stageNumber}` | 그 스테이지 **최고** 별(0~3, 0=미클리어) |
| `progress.gold` | 보유 골드 총액 |

| 멤버 | 설명 |
|---|---|
| `GetStars(int stageNumber)` | 저장된 최고 별. 없으면 0 |
| `SetBestStars(int stageNumber, int stars)` | `Max(기존, stars)`만 반영(하향 덮어쓰기 금지) → 갱신 시 `Save()` |
| `IsUnlocked(int stageNumber)` | `stageNumber<=1 \|\| GetStars(stageNumber-1) >= 1`(파생) |
| `Gold` (get) / `AddGold(int)` / `TrySpendGold(int)` | 골드 조회/획득/차감(부족 시 false) |
| `TotalStars(int stageCount)` | 진행도 표시용 합계(선택) |
| `ResetAll()` | 개발/디버그 초기화(관련 키 삭제) |

- 첫 실행 기본값: 모든 별 0, 골드 0 → 1번만 해금(파생)으로 자연스럽게 성립. 별도 초기화 코드 불필요.
- `PlayerPrefs.Save()`는 쓰기마다 호출(볼륨 저장과 동일 관행, [`SettingsPanelUI`](../Assets/Scripts/Core/SettingsPanelUI.cs)).

### 4.2 `StageCatalog` (ScriptableObject)

스테이지 **표시 순서**와 **골드 보상 표**를 한곳에 담는다. `Resources.LoadAll` 정렬 대신 명시적 목록을 써
순서·누락을 인스펙터에서 통제한다.

| 멤버 | 설명 |
|---|---|
| `stages` (`List<StageData>`) | 표시 순서대로. 인덱스가 그리드 순서, 각 항목의 `stageNumber`가 저장 키 |
| `goldReward` (`int[3]`) | 별 1/2/3개 클리어 시 **누적** 보상액. 예: `{30, 60, 100}` |
| `RewardFor(int stars)` | `stars<=0 ? 0 : goldReward[stars-1]`. 갱신분 계산의 기준 |
| `Count` | 스테이지 수 |

- `Resources/`에 단일 에셋으로 두고 `StageSelectController`/`StageProgressRecorder`가 참조(또는 `Resources.Load`).

### 4.3 `StageSession` (정적 홀더 · Map 네임스페이스)

씬 간에 "무엇을 플레이할지" 넘기는 얇은 정적 상태. 씬 오브젝트가 아니므로 로드 후에도 값이 유지된다.
`MapBuilder`가 상위 계층(Core)을 참조하지 않도록 `TopViewDefense.Map`에 둔다(Progression은 이미 Map 참조).

| 멤버 | 설명 |
|---|---|
| `SelectedStage` (`StageData`) | StageSelect에서 클릭한 스테이지. `MapBuilder`가 읽음 |

- StageSelect 버튼 클릭 → `StageSession.SelectedStage = stage` → `SceneManager.LoadScene("PlayScene")`.
- 직접 PlayScene을 열어 디버깅할 때는 `null`이므로 `MapBuilder`가 기존 하드코딩 경로로 폴백한다(§4.6).

### 4.4 `StageProgressRecorder` (MonoBehaviour · PlayScene)

종료 훅. `GameManager.OnGameEnded(result, stars)`를 **구독만** 해 저장·보상·해금을 처리한다. GameManager를
오염시키지 않으려 별도 컴포넌트로 분리한다.

| 처리(클리어 시) | 내용 |
|---|---|
| 별 저장 | `PlayerProgress.SetBestStars(stageNumber, stars)` |
| 골드 보상 | `delta = catalog.RewardFor(stars) − catalog.RewardFor(기존최고별)`; `delta>0`이면 `AddGold(delta)` |
| 해금 | 별도 작업 없음 — 별을 저장하면 다음 스테이지 해금이 파생으로 성립 |

- **순서 주의:** 보상 delta는 `SetBestStars` **이전**의 기존 최고 별로 계산한 뒤 저장한다(같은 값 이중 지급 방지).
- 패배(`GameOver`)면 아무 것도 하지 않는다(CLAUDE.md 7장: 실패 시 보상 없음).
- 현재 플레이 중인 스테이지 번호는 `StageSession.SelectedStage.stageNumber`(없으면 `MapBuilder.Stage.stageNumber` 폴백).
- 구독/해제는 `TurretHudUI`·`LivesHudUI`와 동일하게 Start 구독 / OnDestroy 해제.

### 4.5 `StageSelectController` + `StageButtonUI` (MonoBehaviour · StageSelectScene)

`StageSelectController`가 `StageCatalog`를 돌며 `GridLayoutGroup` 아래 `StageButtonUI` 프리팹을 스폰하고,
골드 텍스트와 BackButton을 배선한다.

**`StageButtonUI` — 버튼 1칸의 3상태:**

| 상태(판정) | 자물쇠 | 클릭 | 별 표시 |
|---|---|---|---|
| 미해금 (`!IsUnlocked`) | ON | 불가(`interactable=false`) | 숨김 |
| 해금·미클리어 (`IsUnlocked && stars==0`) | OFF | 가능 | 숨김 |
| 클리어 (`stars>0`) | OFF | 가능 | 획득 개수만큼(최대 3) 점등 |

- 필드: `stage`(주입)·`lockOverlay`·`starIcons[3]`·`button`·`label`(스테이지 번호/이름, 선택).
- 클릭 → `StageSession.SelectedStage = stage` → PlayScene 로드.

**`StageSelectController`:**

| 멤버 | 설명 |
|---|---|
| `catalog` | 스폰할 스테이지 목록 |
| `gridParent` / `buttonPrefab` | `GridLayoutGroup` 컨테이너 / `StageButtonUI` 프리팹 |
| `goldText` | `PlayerProgress.Gold` 표시 |
| `backButton` | → `TitleScene` 로드 |
| `playScene` / `titleScene` | 씬 이름(기본 `PlayScene`/`TitleScene`) |

- `OnEnable`/`Start`에서 그리드를 (재)구성해, PlayScene을 클리어하고 돌아왔을 때 최신 별/해금이 반영된다.
- (선택) 디버그용 `ResetAll` 버튼을 두면 반복 검증이 쉽다.

### 4.6 `MapBuilder` 수정 (Map 계층 소폭 변경)

기존 [`MapBuilder`](../Assets/Scripts/Map/MapBuilder.cs)는 `stageData` 미할당 시 `"StageData/Stage01"`을
Resources에서 로드한다. 여기에 **`StageSession.SelectedStage` 우선** 한 줄을 끼운다.

```
로드 우선순위: 인스펙터 stageData → StageSession.SelectedStage → Resources 폴백 경로
```

- StageSelect 경유 시 선택 스테이지가, PlayScene 직접 실행(디버깅) 시 폴백이 뜬다. 기존 동작은 폴백으로 보존.

---

## 5. 런타임 흐름

```
[StageSelectScene]
 StageCatalog ─→ StageSelectController ─(스폰)→ StageButtonUI × N
   PlayerProgress.GetStars/IsUnlocked ─→ 자물쇠/별 3상태 표시
   PlayerProgress.Gold ─→ goldText
   [버튼 클릭] StageSession.SelectedStage = stage ─→ LoadScene(PlayScene)
   [BackButton] ─→ LoadScene(TitleScene)

[PlayScene]
 MapBuilder ← StageSession.SelectedStage (없으면 폴백)
 …게임 진행(Game-Flow-Architecture.md)…
 GameManager.OnGameEnded(result, stars)
   └→ StageProgressRecorder
        Cleared: delta=RewardFor(stars)−RewardFor(old); SetBestStars(n,stars); AddGold(delta>0)
        GameOver: 무시
 (결과 화면 → 스테이지 선택)  → StageSelectScene 재구성 시 갱신 반영
```

---

## 6. 구현 순서 / 범위 경계

**구현 순서(모델 → 글루 → UI):**
1. `PlayerProgress` + `StageCatalog` (모델·영속)
2. `StageSession` + `MapBuilder` 수정 (선택 스테이지 핸드오프)
3. `StageProgressRecorder` (종료 훅 저장/보상/해금) — UI 전에 PlayScene만으로 검증 가능
4. `StageButtonUI` 프리팹 (3상태)
5. `StageSelectController` (그리드·골드·뒤로가기)
6. 씬 배선 + Build Settings + 첫 실행/리셋 검증

**범위 밖(후속):**
- 타이틀 **[상점]**(골드로 터렛 영구 강화, CLAUDE.md 7장 ①)은 `PlayerProgress.Gold`/`TrySpendGold`를
  공유하는 별도 메타 화면이다. 이 문서는 그 저장소만 세워 두고 상점 UI는 다루지 않는다.
  → 구현됨: [`Shop-Architecture.md`](./Shop-Architecture.md)(강화 레벨 저장·상점 UI·전투 반영).
- 스테이지 확장(현재 `Stage01`만 존재 → 총 5개)은 `StageData`/`WaveData` 에셋 추가로 해결되며,
  `StageCatalog.stages`에 등록만 하면 그리드·해금이 자동 반영된다(코드 변경 불필요).
