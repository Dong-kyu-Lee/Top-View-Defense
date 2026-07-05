# 게임 흐름 아키텍처 (Game Flow Architecture)

> 스테이지 세션의 진행 제어·승패 판정·HUD·정지 계층 설계 문서.
> 기획 원문은 프로젝트 루트 [`CLAUDE.md`](../CLAUDE.md) 2장(승패)·6장(제어)·7장(경제) 참고.
> 맵/적/터렛 도메인 계층은 [`Map-Architecture.md`](./Map-Architecture.md), [`Enemy-Architecture.md`](./Enemy-Architecture.md),
> [`Turret-Architecture.md`](./Turret-Architecture.md)에 정리되어 있으며, 이 문서는 그 **위에 얹혀 세 계층을 엮는다**.

---

## 1. 개요

이 문서는 특정 도메인(맵/적/터렛)에 속하지 않고 **스테이지 한 판의 런타임을 오케스트레이션하는 계층**을 다룬다.
웨이브 진행 표시·자동 에너지·승패 판정·결과 화면·목숨 HUD가 여기에 속하며, 공통적으로 다음 두 성격을 갖는다.

- **읽기/구독 전용:** HUD·흐름 컴포넌트는 상태를 **소유하지 않고** 도메인 컴포넌트(`WaveRunner`·`BaseCore`·`PlayerEconomy`)를 읽거나 이벤트를 구독만 한다. 도메인 계층은 수정하지 않는다.
- **정지 친화:** 전체 정지를 `Time.timeScale=0` 하나로 처리하도록 설계한다(§2 정지 규약).

설계 목표(기획 요구 → 대응):

| 기획 요구 (CLAUDE.md) | 대응 설계 |
|---|---|
| 일정 시간마다 자동 에너지 지급 (7장 ②) | `EnergyDripper` → `PlayerEconomy.Add` |
| 현재/남은 웨이브 수 표시 (6장) | `WaveHudUI` ← `WaveRunner` 상태 폴링 |
| 웨이브 시작까지 카운트다운 + 즉시시작 (6장) | `WaveRunner` 정비 카운트다운 API + `WaveHudUI` 즉시시작 버튼 |
| 기지 남은 목숨 UI (2장) | `LivesHudUI` ← `BaseCore.OnLivesChanged` |
| 목숨 0 → 게임오버 / 전 웨이브 클리어 → 승리·별점 (2장) | `GameManager`(종료 판정 허브) + `GameResultUI` |

---

## 2. 의존 규약

- **HUD는 도메인을 읽기만 한다.** `WaveHudUI`/`LivesHudUI`는 `WaveRunner`/`BaseCore`의 공개 상태·이벤트만 사용하고,
  `GameResultUI`는 `GameManager`의 종료 이벤트만 구독한다. 도메인 컴포넌트에 UI 코드가 새지 않는다.
- **정지 규약(핵심):** 게임 정지는 `Time.timeScale=0` **하나로** 처리한다. `Time.deltaTime`·`WaitForSeconds` 기반
  시스템(`EnergyDripper` 드립, `WaveRunner` 정비 카운트다운, 적 이동, 터렛 발사)은 timeScale=0에서 **자동으로 함께
  멈춘다** → 시스템별 일시정지 코드가 필요 없다. 이후 그룹 C(설정 일시정지)가 이 메커니즘을 그대로 재사용한다.
- **승패는 한곳에서 단발로.** 종료 판정은 `GameManager`로 모으고, 게임오버·클리어가 겹쳐 들어와도
  **단발 가드**로 최초 1회만 처리한다(`BaseCore.IsGameOver`와 같은 철학).
- **좌표/도메인 규약**은 각 도메인 문서를 따른다(이 계층은 좌표를 직접 다루지 않는다).

---

## 3. 파일 구조

```
Assets/Scripts/
├── Core/
│   ├── GameManager.cs      # 승패 판정 허브 + 씬 전환 + timeScale 소유 (MonoBehaviour)
│   ├── GameResultUI.cs     # 결과 화면(승/패·별점·재시도/스테이지 선택) (MonoBehaviour)
│   ├── LivesHudUI.cs       # 기지 남은 목숨 HUD (MonoBehaviour)
│   ├── EnergyDripper.cs    # 주기적 자동 에너지 지급 (MonoBehaviour)
│   ├── PauseController.cs  # 일시정지/재개(timeScale=0) + 키 토글 (MonoBehaviour)
│   ├── PauseMenuUI.cs      # 일시정지 메뉴(계속/재시도/나가기/설정) (MonoBehaviour)
│   └── SettingsPanelUI.cs  # BGM/SFX 볼륨 설정 + 저장값 부팅 적용 (MonoBehaviour)
└── Enemies/
    ├── WaveHudUI.cs        # 웨이브 진행/남은시간 HUD + 즉시시작 (MonoBehaviour)
    └── WaveBannerUI.cs     # 웨이브 시작/클리어 중앙 배너(페이드인·아웃) (MonoBehaviour)
```

> 그룹 C는 별도 에디터 에셋으로 `AudioMixer`(BGM/SFX 그룹, 볼륨 노출)를 사용한다.

> 소비되는 도메인 컴포넌트는 각 도메인 문서 참고: `WaveRunner`/`BaseCore` → [`Enemy-Architecture.md`](./Enemy-Architecture.md) §4.9·§4.10,
> `PlayerEconomy` → [`Turret-Architecture.md`](./Turret-Architecture.md) §4.6.
> `WaveRunner`에 추가된 **정비 카운트다운/즉시시작 API**의 시그니처는 Enemy 문서 §4.9에 정리되어 있고, 이 문서는 그것을 소비하는 HUD를 다룬다.

---

## 4. 타입 상세

### 4.1 `EnergyDripper` (MonoBehaviour)

일정 간격으로 `PlayerEconomy`에 에너지를 자동 지급한다(CLAUDE.md 7장 ② "n초 간격 자동 드랍").
적 처치 드랍·에너지 터렛 생산과 함께 에너지 획득처의 한 축이다.

| 멤버 | 설명 |
|---|---|
| `interval` | 지급 간격(초, 기본 10) |
| `amountPerTick` | 1회 지급량(기본 10) |
| `autoBegin` / `dripOnStart` | Start 자동 시작 / 첫 간격 대기 없이 즉시 1회 지급 |
| `Begin()` / `StopDrip()` | 지급 시작(중복 무시)/중단 |

- `WaitForSeconds`가 `timeScale`을 따르므로 **일시정지 시 티커도 자동으로 멈춘다**(§2 정지 규약).
- 씬 배선: `PlayerEconomy`와 같은 `TurretSystem` 오브젝트에 얹는다. `economy`를 비우면 `Instance`/씬에서 탐색.

### 4.2 `WaveHudUI` (MonoBehaviour)

웨이브 진행 상태 HUD. `WaveRunner`의 공개 상태를 **매 프레임 폴링**해 표시한다(카운트다운은 매 프레임 변하는
값이라 이벤트보다 폴링이 적합). 3상태로 갱신:

| 상태 | 표시 |
|---|---|
| 정비 중(`IsResting`) | "Wave (PendingWave+1) / Total" + 남은 시간(텍스트/게이지) + **즉시시작 버튼 노출** |
| 진행 중(`CurrentWave≥0`) | "Wave (CurrentWave+1) / Total", 카운트다운·버튼 숨김 |
| 시작 전/전체 클리어 | 카운트다운·버튼 숨김(결과 화면은 §4.5가 담당) |

- 필드(모두 선택): `waveText`/`remainingText`/`countdownText`/`countdownFill`(Filled `Image`)/`skipButton`.
- 즉시시작: `skipButton.onClick → WaveRunner.SkipRest()`. 버튼은 정비 중에만 노출되므로 오작동 여지 없음.
- 카운트다운 텍스트는 정수 초가 바뀔 때만 갱신(매 프레임 문자열 할당 방지).
- **씬 주의:** `countdownText`/`countdownFill`/`skipButton`은 상태별로 각자의 GameObject가 `SetActive` 토글되므로,
  `waveText`가 붙은 루트 패널과 **별도 오브젝트**로 둔다(루트를 끄면 텍스트까지 사라짐).

### 4.2b `WaveBannerUI` (MonoBehaviour)

웨이브 **시작/클리어 순간**을 화면 중앙에 잠깐 띄웠다 사라지게 하는 배너(CLAUDE.md 6장 — 웨이브 진행의 가시적 피드백).
카운트다운·잔여 웨이브를 상시 표시하는 `WaveHudUI`(§4.2, 폴링)와 달리, 이쪽은 **전이 순간만** 이벤트로 받아 연출한다.

| 구독 | 표시 |
|---|---|
| `WaveRunner.OnWaveStarted(i)` | "Wave (i+1) Start" 페이드인→유지→아웃 |
| `WaveRunner.OnWaveCleared(i)` | "Wave (i+1) Clear" 동일 연출 |

- **도메인 확장:** 시작 신호는 기존 `OnWaveStarted`를 재사용하지만, **중간 웨이브 클리어 신호가 없어** `WaveRunner`에
  `OnWaveCleared(int)`를 추가했다(적 전멸 대기 루프 직후 발행). `OnAllWavesCleared`는 마지막 웨이브에만 오므로 대칭을 맞춘 것.
- **필드:** `group`(`CanvasGroup`)/`label`(`TMP_Text`)/시작·클리어 문구 포맷/타이밍(`fadeIn`/`hold`/`fadeOut`)/`suppressFinalClear`.
- **Unscaled 시간:** 페이드는 `Time.unscaledDeltaTime`으로 돌린다. 배너 도중 일시정지(`timeScale=0`)나, 마지막 웨이브
  클리어 직후 `GameManager`의 `timeScale=0`에도 얼지 않고 정상 종료된다.
- **마지막 웨이브 클리어 억제(`suppressFinalClear`, 기본 켬):** 마지막 웨이브는 클리어와 동시에 `GameResultUI`가 뜨므로
  배너를 건너뛰어 결과 화면과 겹치지 않게 한다.
- **씬 주의:** `GameResultUI`/`PauseMenuUI`와 동일 — 컨트롤러는 **항상 켜진 오브젝트**에 두고 자식 `CanvasGroup`만
  페이드로 토글한다(자기 자신을 끄면 Start 구독이 끊긴다). `CanvasGroup`의 `Interactable`/`BlocksRaycasts`는 꺼서
  배너가 버튼 클릭을 막지 않게 한다. 초기 `alpha=0`.

### 4.3 `GameManager` (MonoBehaviour)

스테이지의 승패 판정과 종료 처리를 모으는 중앙 컨트롤러(CLAUDE.md 2장). 가벼운 정적 `Instance` 제공.

| 멤버 | 설명 |
|---|---|
| `Result` / `IsGameEnded` | 현재 결과(`None`/`Cleared`/`GameOver`) / 종료 여부 |
| `OnGameEnded(result, stars)` | 게임 종료(별점 포함). 결과 UI가 구독 |
| `RetryStage()` | 현재 씬 리로드(재시도) |
| `GoToStageSelect()` | `stageSelectScene`으로 이동 |

**종료 흐름(`EndGame`, 단발 가드):**
1. `BaseCore.OnGameOver` 구독 → 패배, `WaveRunner.OnAllWavesCleared` 구독 → 승리
2. `WaveRunner.StopWaves()`(패배 후에도 도는 스폰 차단)
3. `Time.timeScale=0`(전체 정지) + 클리어면 `BaseCore.StarRating`으로 별점 캡처
4. `OnGameEnded(result, stars)` 발행

- 씬 전환 메서드는 로드 직전 `Time.timeScale=1` 복구하고, `Awake`에서도 방어적으로 복구한다(이전 판이 0으로 떠났을 경우).
- **Build Settings:** `GoToStageSelect()`가 이름(`StageSelectScene`)으로 로드하므로 해당 씬이 Build Settings에 등록돼야 한다(재시도도 동일).
- 씬 전환/timeScale 제어는 **그룹 C(설정: 나가기/종료/일시정지)가 재사용**한다.

### 4.4 `LivesHudUI` (MonoBehaviour)

기지 남은 목숨 HUD(CLAUDE.md 2장). `BaseCore.OnLivesChanged(cur, max)`를 구독해 갱신한다.
`TurretHudUI`와 동일하게 Start에서 구독(Awake 순서 안전)하고 OnDestroy에서 해제한다.

- 표시(둘 다 선택): `hearts[]`(현재 목숨 수만큼 앞에서부터 `SetActive`) / `livesText`("cur / max").

### 4.5 `GameResultUI` (MonoBehaviour)

게임 종료 결과 화면(CLAUDE.md 2장 — 별 3/2/1, 게임 오버). `GameManager.OnGameEnded`를 구독해 패널을 띄운다.

- 표시: 타이틀(승/패), `starIcons[]`를 획득 별 수만큼 점등, 버튼 2개(**재시도**→`RetryStage`, **스테이지 선택**→`GoToStageSelect`).
- `timeScale=0` 중에도 uGUI 버튼은 동작하므로 추가 처리가 없다.
- **함정 주의:** 컨트롤러는 **항상 켜진 오브젝트**에 두고 자식 `panel`만 토글한다. 자기 자신을 비활성화하면
  `Start`/이벤트 구독이 끊겨 결과가 영영 뜨지 않는다 → 코드는 자기 토글을 하지 않는다.

> **그룹 C(§4.6~4.8) 씬 배선·트러블슈팅은 [`Pause-Settings.md`](./Pause-Settings.md)에 별도 정리**되어 있다.
> 아래는 컴포넌트 API 요약이고, 믹서 생성·필드 연결·입력/오디오 함정은 그 문서를 참고한다.

### 4.6 `PauseController` (MonoBehaviour)

설정/일시정지 진입점(그룹 C). 게임 종료와 동일한 `Time.timeScale=0` 규약(§2)을 재사용하므로,
`EnergyDripper`·`WaveRunner` 카운트다운·적/터렛이 함께 멈춘다. 오디오는 timeScale의 영향을 받지 않아
정지 중에도 BGM/효과음/uGUI 버튼은 정상 동작한다.

| 멤버 | 설명 |
|---|---|
| `IsPaused` / `OnPauseChanged(bool)` | 현재 정지 여부 / 상태 변경 이벤트(UI가 구독) |
| `Pause()` / `Resume()` / `Toggle()` | 정지/재개/토글 |
| `toggleWithKey` / `toggleKey` | 키 토글 사용 여부 / 키(기본 ESC) |

- **GameManager 충돌 가드(핵심):** `GameManager`도 종료 시 `timeScale`을 소유한다. 게임이 이미 종료
  (`GameManager.IsGameEnded`)된 상태면 `Pause()`는 무시하고, `Resume()`도 timeScale을 1로 되돌리지 않는다
  (되돌리면 결과 화면이 다시 흐른다). `OnDestroy`에서도 정지 상태로 씬을 떠날 때 방어적으로 복구한다.
- **ESC 중복 가드:** `TurretPlacer`도 ESC를 배치/철거 '취소'에 쓴다. 배치/철거 중(`Armed!=null || Demolishing`)
  일 때는 키 토글을 건너뛰어 취소와 일시정지가 동시에 일어나지 않게 한다(HUD 버튼의 `Pause()`는 가드 대상 아님).
- **입력:** 프로젝트가 새 Input System을 쓰므로 `Keyboard.current[toggleKey]`로 읽는다(레거시 `Input` 금지).

### 4.7 `PauseMenuUI` (MonoBehaviour)

일시정지 메뉴. `PauseController.OnPauseChanged`를 구독해 패널을 토글한다. `GameResultUI`와 동일하게
컨트롤러는 **항상 켜진 오브젝트**에 두고 자식 `panel`만 토글한다.

- 버튼(모두 선택): **계속하기**→`Resume()`, **재시도**→`GameManager.RetryStage()`,
  **스테이지 나가기**→`GameManager.GoToStageSelect()`, **설정**→`SettingsPanelUI.Open()`.
- 씬 전환 메서드가 로드 직전 timeScale=1을 복구하므로 추가 처리가 없다. 재개 시 설정 하위 패널도 함께 닫는다.

### 4.8 `SettingsPanelUI` (MonoBehaviour)

BGM/SFX 볼륨 설정 + 부팅 시 저장값 적용. 슬라이더(0~1)를 dB로 변환해 `AudioMixer` 노출 파라미터에
적용하고 `PlayerPrefs`에 영속화한다. 컨트롤러는 항상 켜진 오브젝트에 두므로 패널이 꺼져 있어도
`Start`의 저장값 적용이 매 스테이지 부팅 시 동작한다(일시정지를 열지 않아도 볼륨이 반영됨).

| 멤버 | 설명 |
|---|---|
| `mixer` / `bgmParam` / `sfxParam` | 믹서 + 노출 파라미터 이름(기본 `BGMVolume`/`SFXVolume`) |
| `bgmSlider` / `sfxSlider` | 볼륨 슬라이더(Min 0 / Max 1) |
| `Open()` / `Close()` | 설정 패널 표시/숨김 |

- 슬라이더 초기값은 리스너 등록 **전에** 세팅해 콜백 중복을 피한다. 0은 -80dB(무음)로 클램프.
- **씬 준비:** `AudioMixer`에 BGM/SFX 그룹을 만들고 각 볼륨을 노출한 뒤, 노출 이름을 `bgmParam`/`sfxParam`과
  일치시킨다. `SetFloat`는 Awake에서 신뢰할 수 없어 Start에서 적용한다.

---

## 5. 런타임 흐름

```
[스테이지 세션 lifecycle]

WaveRunner ── 정비 카운트다운(IsResting/RestRemaining) ─→ WaveHudUI(폴링 표시 + 즉시시작→SkipRest)
   │  OnWaveStarted / OnWaveCleared ──────────────────→ WaveBannerUI(중앙 배너 페이드)
   │  OnAllWavesCleared ─────────────────────────────┐
   │                                                  ▼
EnergyDripper ── interval마다 Add ─→ PlayerEconomy   GameManager (단발 가드)
                                                      ▲   │  EndGame: StopWaves + timeScale=0 + StarRating
BaseCore ── OnLivesChanged ─→ LivesHudUI              │   ▼
   └────── OnGameOver ────────────────────────────────┘  OnGameEnded(result, stars) ─→ GameResultUI
                                                             재시도/스테이지 선택 → 씬 전환(timeScale=1)
```

권장 구현 조합(설계 결론):
**HUD는 도메인 읽기 전용 + 승패는 `GameManager` 단일 허브 + 정지는 `timeScale=0` 하나로 통일.**

> 정지를 timeScale로 통일했기에 `EnergyDripper`·`WaveRunner` 카운트다운·적/터렛이 모두 공짜로 함께 멈춘다.
> 그룹 C의 설정 일시정지는 이 규약과 `GameManager`의 씬 전환을 그대로 재사용하면 된다.

---

## 6. 범위 경계 / 다음 단계

**완료:**
- [x] `EnergyDripper`: 주기적 자동 에너지(그룹 D)
- [x] `WaveRunner` 정비 카운트다운/즉시시작 API + `WaveHudUI`(그룹 A, #2·#3·#4)
- [x] `WaveBannerUI`: 웨이브 시작/클리어 중앙 배너(`WaveRunner.OnWaveStarted`/신규 `OnWaveCleared` 구독, 페이드)
- [x] `GameManager` + `LivesHudUI` + `GameResultUI`(그룹 B, #6·#7)
- [x] **그룹 C(설정/일시정지)**: `PauseController`(timeScale=0 재사용 + ESC 토글) +
      `PauseMenuUI`(계속/재시도/나가기/설정) + `SettingsPanelUI`(BGM/SFX 볼륨, PlayerPrefs 영속).
      `GameManager` 씬 전환·timeScale과 §2 정지 규약을 재사용하고, 종료 상태 충돌 가드를 둔다(§4.6).
      씬 배선·입력/오디오 함정·트러블슈팅은 [`Pause-Settings.md`](./Pause-Settings.md) 참고.

**범위 밖(상위 메타 계층):**
- 별·골드 **영구 저장**, 다음 스테이지 **해금**, 클리어 보상은 StageSelect 메타 계층의 몫이다.
  `GameManager.OnGameEnded(result, stars)`가 결과·별점을 노출해 두었으므로, 이후 그 지점에 저장 훅만 끼우면 된다.

**범위 밖(후속):**
- BGM/효과음 **재생 소스** 자체는 아직 없다. 그룹 C는 볼륨 라우팅 골격(믹서+설정)만 세우며,
  터렛 발사음·적 사운드 등은 이후 각 이벤트에서 `AudioMixerGroup`(BGM/SFX)으로 라우팅한다.
- 별/골드 영구 저장·다음 스테이지 해금은 여전히 StageSelect 메타 계층의 몫이다(위 참고).
```
