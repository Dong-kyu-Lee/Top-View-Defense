using System.Collections;
using UnityEngine;
using UnityEngine.Audio;

namespace TopViewDefense.Core.Audio
{
    /// <summary>
    /// 사운드 중앙 허브. 씬을 넘나들며 살아남아(BGM 연속성) SFX 원샷과 BGM 크로스페이드를 담당한다.
    ///
    /// 모든 소스를 <see cref="AudioMixer"/>의 SFX/BGM 그룹으로 라우팅하므로 <see cref="SettingsPanelUI"/>의
    /// 볼륨(BGMVolume/SFXVolume)이 그대로 적용된다. 오디오는 <c>Time.timeScale</c>의 영향을 받지 않아
    /// 결과 화면/일시정지(timeScale=0) 중에도 정상 재생된다.
    ///
    /// 씬 배선 불필요: <see cref="VfxPool"/>처럼 최초 사용 시 <c>Resources/Audio/AudioManager</c> 프리팹에서
    /// 자동 부트스트랩된다. 프리팹이 없으면 빈 오브젝트로 생성되지만, 그 경우 믹서 그룹/공용 클립이 비어
    /// SFX만 무음 없이 라우팅되지 않으니 프리팹을 두는 것을 권장한다.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private static AudioManager _instance;
        private static bool _quitting;

        public static AudioManager Instance
        {
            get
            {
                if (_instance == null && !_quitting)
                {
                    var prefab = Resources.Load<AudioManager>("Audio/AudioManager");
                    _instance = prefab != null
                        ? Instantiate(prefab)
                        : new GameObject("AudioManager").AddComponent<AudioManager>();
                    _instance.name = "AudioManager";
                    DontDestroyOnLoad(_instance.gameObject);
                    _instance.EnsureSources();
                }
                return _instance;
            }
        }

        [Header("믹서 라우팅")]
        [Tooltip("SFX 소스를 라우팅할 믹서 그룹(SettingsPanelUI의 SFXVolume 대상).")]
        [SerializeField] private AudioMixerGroup sfxGroup;

        [Tooltip("BGM 소스를 라우팅할 믹서 그룹(SettingsPanelUI의 BGMVolume 대상).")]
        [SerializeField] private AudioMixerGroup bgmGroup;

        [Header("공용 SFX")]
        [Tooltip("버튼 클릭음.")]
        [SerializeField] private AudioClip buttonClick;

        [Tooltip("스테이지 클리어(승리) 결과음.")]
        [SerializeField] private AudioClip gameClear;

        [Tooltip("게임 오버(패배) 결과음.")]
        [SerializeField] private AudioClip gameOver;

        [Header("설정")]
        [Tooltip("동시 재생 가능한 SFX 보이스 수(초과 시 가장 오래된 것을 재사용).")]
        [SerializeField] private int sfxVoices = 12;

        [Tooltip("BGM 트랙 교체 시 크로스페이드 시간(초).")]
        [Min(0f)] [SerializeField] private float bgmFadeSeconds = 0.6f;

        private AudioSource[] _sfx;
        private int _next;
        private AudioSource _bgm;
        private AudioClip _currentBgm;
        private Coroutine _fade;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureSources();
        }

        private void OnApplicationQuit() => _quitting = true;

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        // SFX 보이스 풀 + BGM 소스를 한 번만 구성한다. 프로퍼티 부트스트랩과 Awake 양쪽에서 안전하게 호출된다.
        private void EnsureSources()
        {
            if (_sfx != null) return;

            _sfx = new AudioSource[Mathf.Max(1, sfxVoices)];
            for (int i = 0; i < _sfx.Length; i++)
            {
                var s = gameObject.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f; // 2D 원샷.
                s.outputAudioMixerGroup = sfxGroup;
                _sfx[i] = s;
            }

            _bgm = gameObject.AddComponent<AudioSource>();
            _bgm.playOnAwake = false;
            _bgm.loop = true;
            _bgm.spatialBlend = 0f;
            _bgm.outputAudioMixerGroup = bgmGroup;
        }

        // ---------------------------------------------------------------- SFX

        /// <summary>2D SFX 원샷 재생. clip이 null이면 무시.</summary>
        public static void PlaySfx(AudioClip clip, float volume = 1f)
        {
            if (clip == null || _quitting) return;
            Instance.PlaySfxInternal(clip, volume);
        }

        /// <summary>공용 버튼 클릭음.</summary>
        public static void PlayButtonClick()
        {
            if (_quitting) return;
            Instance.PlaySfxInternal(Instance.buttonClick, 1f);
        }

        /// <summary>클리어(승리) 결과음.</summary>
        public static void PlayGameClear()
        {
            if (_quitting) return;
            Instance.PlaySfxInternal(Instance.gameClear, 1f);
        }

        /// <summary>게임 오버(패배) 결과음.</summary>
        public static void PlayGameOver()
        {
            if (_quitting) return;
            Instance.PlaySfxInternal(Instance.gameOver, 1f);
        }

        private void PlaySfxInternal(AudioClip clip, float volume)
        {
            if (clip == null) return;
            AudioSource s = _sfx[_next];
            _next = (_next + 1) % _sfx.Length; // 라운드로빈: 초과 시 가장 오래된 보이스를 덮어쓴다.
            s.PlayOneShot(clip, volume);
        }

        // ---------------------------------------------------------------- BGM

        /// <summary>씬 BGM을 재생한다. 이미 같은 트랙이면 재시작하지 않는다(씬 재로드 대응).</summary>
        public static void PlayBgm(AudioClip clip)
        {
            if (_quitting) return;
            Instance.PlayBgmInternal(clip);
        }

        private void PlayBgmInternal(AudioClip clip)
        {
            if (clip == null || clip == _currentBgm) return;
            _currentBgm = clip;

            if (_fade != null) StopCoroutine(_fade);
            _fade = StartCoroutine(Crossfade(clip));
        }

        // timeScale=0 중에도 진행되도록 unscaled 시간으로 페이드한다.
        private IEnumerator Crossfade(AudioClip next)
        {
            float fade = Mathf.Max(0.0001f, bgmFadeSeconds);

            float t = 0f, start = _bgm.isPlaying ? _bgm.volume : 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                _bgm.volume = Mathf.Lerp(start, 0f, t / fade);
                yield return null;
            }

            _bgm.clip = next;
            _bgm.volume = 0f;
            _bgm.Play();

            t = 0f;
            while (t < fade)
            {
                t += Time.unscaledDeltaTime;
                _bgm.volume = Mathf.Lerp(0f, 1f, t / fade);
                yield return null;
            }
            _bgm.volume = 1f;
            _fade = null;
        }
    }
}
