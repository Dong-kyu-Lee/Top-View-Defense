using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 볼륨 설정 패널(BGM/SFX 분리) + 부팅 시 저장값 적용. (그룹 C)
    ///
    /// 슬라이더(0~1)를 dB로 변환해 <see cref="AudioMixer"/>의 노출 파라미터에 적용하고
    /// <see cref="PlayerPrefs"/>에 영속화한다. 이 컴포넌트는 항상 켜진 오브젝트에 두고 자식 <c>panel</c>만
    /// 토글하므로, 설정 패널이 꺼져 있어도 <c>Start</c>의 저장값 적용이 매 스테이지 부팅 시 동작한다.
    /// 오디오는 timeScale의 영향을 받지 않아 일시정지 중에도 볼륨 변경이 즉시 반영된다.
    ///
    /// 씬 준비: AudioMixer에 BGM/SFX 그룹을 만들고 각 그룹의 볼륨을 노출(Expose)한 뒤,
    /// 노출 이름을 아래 <c>bgmParam</c>/<c>sfxParam</c>과 일치시킨다.
    /// </summary>
    public class SettingsPanelUI : MonoBehaviour
    {
        [Header("오디오")]
        [Tooltip("BGM/SFX 그룹을 가진 믹서.")]
        [SerializeField] private AudioMixer mixer;

        [Tooltip("믹서에 노출한 BGM 볼륨 파라미터 이름.")]
        [SerializeField] private string bgmParam = "BGMVolume";

        [Tooltip("믹서에 노출한 SFX 볼륨 파라미터 이름.")]
        [SerializeField] private string sfxParam = "SFXVolume";

        [Header("표시")]
        [Tooltip("설정 패널 루트(자식). 이 컴포넌트는 항상 켜진 오브젝트에 둔다.")]
        [SerializeField] private GameObject panel;

        [Tooltip("BGM 슬라이더(선택, Min 0 / Max 1).")]
        [SerializeField] private Slider bgmSlider;

        [Tooltip("SFX 슬라이더(선택, Min 0 / Max 1).")]
        [SerializeField] private Slider sfxSlider;

        private const string BgmKey = "BGMVolume";
        private const string SfxKey = "SFXVolume";
        private const float DefaultVolume = 0.8f;

        private void Awake()
        {
            if (panel == null)
                Debug.LogWarning("[SettingsPanelUI] panel이 지정되지 않았습니다. 설정 패널 루트를 자식으로 지정하세요.", this);
            SetPanelActive(false);
        }

        private void Start()
        {
            float bgm = PlayerPrefs.GetFloat(BgmKey, DefaultVolume);
            float sfx = PlayerPrefs.GetFloat(SfxKey, DefaultVolume);

            // 저장값을 믹서에 적용(부팅 시 1회). SetFloat는 Awake에서 신뢰할 수 없어 Start에서 수행.
            ApplyToMixer(bgmParam, bgm);
            ApplyToMixer(sfxParam, sfx);

            // 슬라이더 초기값은 리스너 등록 전에 세팅해 콜백 중복(저장→재적용)을 피한다.
            if (bgmSlider != null)
            {
                bgmSlider.SetValueWithoutNotify(bgm);
                bgmSlider.onValueChanged.AddListener(OnBgmChanged);
            }
            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(sfx);
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }
        }

        private void OnDestroy()
        {
            if (bgmSlider != null) bgmSlider.onValueChanged.RemoveListener(OnBgmChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.RemoveListener(OnSfxChanged);
        }

        /// <summary>설정 패널을 표시한다.</summary>
        public void Open() => SetPanelActive(true);

        /// <summary>설정 패널을 숨긴다.</summary>
        public void Close() => SetPanelActive(false);

        private void OnBgmChanged(float v)
        {
            ApplyToMixer(bgmParam, v);
            PlayerPrefs.SetFloat(BgmKey, v);
        }

        private void OnSfxChanged(float v)
        {
            ApplyToMixer(sfxParam, v);
            PlayerPrefs.SetFloat(SfxKey, v);
        }

        // 슬라이더 선형값(0~1) → dB. 0은 -80dB(무음)로 클램프하고, 그 외는 로그 변환.
        private void ApplyToMixer(string param, float linear01)
        {
            if (mixer == null || string.IsNullOrEmpty(param)) return;
            float dB = linear01 <= 0.0001f ? -80f : Mathf.Log10(linear01) * 20f;
            mixer.SetFloat(param, dB);
        }

        private void SetPanelActive(bool on)
        {
            if (panel != null && panel.activeSelf != on) panel.SetActive(on);
        }
    }
}
