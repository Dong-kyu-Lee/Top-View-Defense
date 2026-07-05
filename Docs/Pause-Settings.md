# 일시정지 & 설정 (Pause & Settings)

> 스테이지 진행 중 일시정지 계층과 볼륨 설정의 **설계·씬 배선·함정** 정리 문서(그룹 C).
> 컴포넌트 API 상세는 상위 흐름 문서 [`Game-Flow-Architecture.md`](./Game-Flow-Architecture.md) §4.6~4.8에 있으며,
> 이 문서는 그 위에서 **에디터 배선 방법과 트러블슈팅**을 다룬다.
> 기획 원문은 [`CLAUDE.md`](../CLAUDE.md) 6장(제어) 참고.

---

## 1. 개요

일시정지·설정은 특정 도메인(맵/적/터렛)이 아니라 **스테이지 세션 오케스트레이션 계층**에 속한다(그룹 C).
핵심 원칙은 상위 흐름 문서와 동일하다.

- **정지 = `Time.timeScale=0` 하나로 통일**(흐름 문서 §2). `EnergyDripper` 드립·`WaveRunner` 카운트다운·
  적 이동·터렛 발사가 전부 자동으로 함께 멈춘다 → 시스템별 일시정지 코드가 없다.
- **오디오는 timeScale의 영향을 받지 않는다.** 정지 중에도 BGM/효과음/uGUI 버튼·슬라이더는 정상 동작한다.
- **HUD/메뉴는 읽기·구독 전용.** 컨트롤러는 항상 켜진 오브젝트에 두고 자식 `panel`만 토글한다(`GameResultUI`와 동일).

---

## 2. 구성 요소

| 구성 | 역할 | API 상세 |
|---|---|---|
| `PauseController` | 정지/재개(`timeScale=0`) + 키 토글, 종료·배치 가드 | 흐름 문서 §4.6 |
| `PauseMenuUI` | 일시정지 메뉴(계속/재시도/나가기/설정) | 흐름 문서 §4.7 |
| `SettingsPanelUI` | BGM/SFX 볼륨 설정 + 부팅 시 저장값 적용 | 흐름 문서 §4.8 |
| `AudioMixer`(에디터 에셋) | BGM/SFX 그룹 + 볼륨 노출 파라미터 | 본 문서 §3 |

씬 전환(재시도/나가기)과 종료 판정은 `GameManager`를 재사용한다(흐름 문서 §4.3).

### 정지 소유권 충돌 가드 (핵심)

`GameManager`(게임 종료)와 `PauseController`(일시정지)가 **둘 다 `timeScale`을 쓴다.** 충돌을 막기 위해:

- 게임이 이미 종료(`GameManager.IsGameEnded`)면 `Pause()`는 무시한다.
- `Resume()`도 종료 상태면 `timeScale`을 1로 되돌리지 않는다(되돌리면 결과 화면이 다시 흐른다).
- 결과 화면이 떠 있는 동안 ESC를 눌러도 아무 일이 없어야 정상이다.

---

## 3. 씬 구성 & 배선 가이드

### 3.1 AudioMixer 생성 (선행)

1. `Assets/Audio` 폴더 → 우클릭 **Create ▸ Audio Mixer** (예: `GameAudioMixer`).
2. Audio Mixer 창에서 `Master` 아래에 자식 그룹 2개 추가: **BGM**, **SFX**.
3. 각 그룹의 **Volume**을 노출: 그룹 선택 → Inspector의 Volume 라벨 우클릭 ▸ **Expose … to script**
   → Audio Mixer 창 우상단 **Exposed Parameters**에서 이름을 각각 **`BGMVolume`**, **`SFXVolume`**로 변경.
4. ⚠️ 이 이름은 `SettingsPanelUI`의 `bgmParam`/`sfxParam` 값과 **정확히 일치**해야 한다(대소문자 포함).

### 3.2 오브젝트 배치

```
Canvas
├── PauseController      (빈 오브젝트, PauseController 컴포넌트) — 항상 켜둠
├── PauseUI              (PauseMenuUI + SettingsPanelUI 컴포넌트) — 항상 켜둠
│   ├── PausePanel       (일시정지 메뉴 루트) — 토글 대상, 시작 시 꺼짐
│   │   ├── ResumeButton / RetryButton / StageSelectButton / SettingsButton
│   └── SettingsPanel    (설정 루트) — 토글 대상, 시작 시 꺼짐
│       ├── BgmSlider (Min 0 / Max 1)
│       └── SfxSlider (Min 0 / Max 1)
└── PauseButton          (상단 HUD 버튼)
```

> **함정:** 컨트롤러 컴포넌트(PauseMenuUI/SettingsPanelUI)는 **항상 켜진 오브젝트**에 두고, `panel` 필드에는
> 그 **자식**인 실제 패널을 연결한다. 컨트롤러 자신을 끄면 `Start`/이벤트 구독이 끊겨 메뉴가 영영 안 뜬다.

### 3.3 컴포넌트 필드 연결

- **PauseController**: `gameManager`(비우면 자동 탐색), `turretPlacer`(비우면 자동 탐색),
  `toggleWithKey`=on, `toggleKey`=**Escape**(§4.1 함정 참고).
- **PauseMenuUI**: `pauseController`, `gameManager`, `settingsPanel`, `panel`(→PausePanel),
  버튼 4개(resume/retry/stageSelect/settings).
- **SettingsPanelUI**: `mixer`(→GameAudioMixer), `bgmParam`/`sfxParam`(=노출 이름), `panel`(→SettingsPanel),
  `bgmSlider`/`sfxSlider`.

### 3.4 버튼 onClick 배선 (코드 자가 배선과 별개로 필요한 것)

`PauseMenuUI`의 메뉴 버튼(계속/재시도/나가기/설정)은 **코드가 `Awake`에서 자동 배선**하므로 인스펙터에서
onClick을 추가할 필요가 없다. 반면 **상단 HUD의 PauseButton은 코드가 모르는 외부 버튼**이므로 직접 배선한다:

- PauseButton → Button ▸ **On Click ( ) → +** → 오브젝트 슬롯에 **PauseController** 드래그 →
  함수에서 **PauseController ▸ Pause ()** 선택.

---

## 4. 입력 & 오디오 주의점

### 4.1 입력: 새 Input System

- 프로젝트는 Active Input Handling이 **Input System Package**다. 레거시 `UnityEngine.Input` 사용은
  런타임 예외를 던진다. 키 입력은 `Keyboard.current[toggleKey].wasPressedThisFrame`로 읽는다(`TurretPlacer`와 동일).
- **`toggleKey` 저장값 함정(중요):** 필드 타입이 `UnityEngine.InputSystem.Key`(열거형)다. 과거 `KeyCode`에서
  타입을 바꾸면 씬에 남은 **옛 정수 값이 그대로 재해석**된다. 예) `KeyCode.Escape`=27인데 `Key` 열거형에서 27은
  **`Key.M`**이다(`Key.Escape`=60). 타입 변경 후에는 인스펙터 드롭다운에서 **Escape를 다시 선택**해 값을 갱신해야 한다.
- **ESC 중복 가드:** `TurretPlacer`도 ESC를 배치/철거 '취소'에 쓴다. 배치/철거 중
  (`Armed!=null || Demolishing`)에는 키 토글을 건너뛴다(HUD 버튼의 `Pause()`는 가드 대상 아님).

### 4.2 오디오: 믹서 볼륨

- 슬라이더 선형값(0~1)을 dB로 변환해 적용한다: `dB = 20 * log10(v)`, 단 `v≤0`은 **-80dB(무음)**로 클램프.
- `AudioMixer.SetFloat`는 `Awake`에서 신뢰할 수 없어 **`Start`에서 적용**한다.
- 저장값 적용(`Start`)은 설정 패널이 꺼져 있어도 동작하도록 컨트롤러를 항상 켜진 오브젝트에 둔다
  → 일시정지를 열지 않아도 매 스테이지 부팅 시 볼륨이 반영된다.
- **아직 재생 소스 없음:** BGM/효과음 AudioSource 자체는 미구현이다. 지금은 볼륨 라우팅 골격(믹서+설정)만 있고,
  이후 터렛 발사음·적 사운드 등을 각 이벤트에서 `AudioMixerGroup`(BGM/SFX)으로 라우팅하면 슬라이더가 즉시 반영된다.

---

## 5. 트러블슈팅

| 증상 | 원인 | 해결 |
|---|---|---|
| ESC를 눌러도 정지 안 됨 | `toggleKey` 저장값이 `KeyCode`→`Key` 타입 변경 잔재(27=`Key.M`) | 인스펙터 `Toggle Key` 드롭다운에서 **Escape** 재선택(§4.1) |
| PauseButton 눌러도 반응 없음 | Button `On Click`이 비어 있음(자동 배선 대상 아님) | onClick에 `PauseController.Pause()` 배선(§3.4) |
| 메뉴/설정 패널이 안 뜸 | 컨트롤러가 비활성 오브젝트에 있어 `Start`/구독 미실행 | 컨트롤러는 항상 켜진 오브젝트, 자식 `panel`만 토글(§3.2) |
| 볼륨 슬라이더가 소리에 영향 없음 | 재생 중인 AudioSource가 아직 없음(정상) | 이후 오디오를 믹서 그룹으로 라우팅(§4.2) |
| 게임 종료 후 ESC로 정지가 됨 | 종료 상태 충돌 가드 누락 | `GameManager.IsGameEnded` 가드 확인(§2) |

### 무해한 에디터 콘솔 오류

플레이 진입 시 다음 예외가 뜰 수 있으나 **게임 로직·빌드와 무관한 에디터 인스펙터 노이즈**다.
인스펙터에 선택돼 있던 오브젝트가 플레이 진입 시 파괴되며 인스펙터가 null을 그리려다 나는 것으로,
Hierarchy에서 선택을 해제하고 플레이하면 사라진다.

- `MissingReferenceException … GameObjectInspector.OnEnable`
- `SerializedObjectNotCreatableException … TransformInspector.OnEnable`
- `SerializedObjectNotCreatableException … UniversalRenderPipelineLightEditor.OnEnable`
