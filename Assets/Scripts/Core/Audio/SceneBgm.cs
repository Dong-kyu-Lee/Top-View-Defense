using UnityEngine;

namespace TopViewDefense.Core.Audio
{
    /// <summary>
    /// 씬 루트에 두고 이 씬의 BGM 트랙을 지정한다. <see cref="AudioManager"/>가 이전 트랙과 크로스페이드한다.
    /// Title/StageSelect/Play/Shop 각 씬에 하나씩 놓고 <c>track</c>만 지정하면 된다(AudioManager는 자동 부트스트랩).
    /// </summary>
    public sealed class SceneBgm : MonoBehaviour
    {
        [Tooltip("이 씬에서 재생할 BGM 클립.")]
        [SerializeField] private AudioClip track;

        private void Start() => AudioManager.PlayBgm(track);
    }
}
