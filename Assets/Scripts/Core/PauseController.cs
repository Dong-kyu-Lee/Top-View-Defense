using System;
using TopViewDefense.Turrets;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TopViewDefense.Core
{
    /// <summary>
    /// 설정/일시정지 진입점. <c>Time.timeScale=0</c>으로 전체를 멈추고 재개한다.
    /// (그룹 C, Game-Flow-Architecture §6)
    ///
    /// 정지 규약(§2)을 그대로 사용하므로 <see cref="EnergyDripper"/> 드립·WaveRunner 카운트다운·
    /// 적/터렛이 함께 멈춘다. 오디오는 timeScale의 영향을 받지 않아 정지 중에도 BGM/효과음/버튼은 정상 동작한다.
    ///
    /// <see cref="GameManager"/>와의 충돌 방지: 게임이 이미 종료(결과 화면)된 상태에서는 정지/재개를 무시한다.
    /// 그렇지 않으면 <see cref="Resume"/>가 timeScale=1로 되돌려 결과 화면이 다시 흐른다.
    /// </summary>
    [DisallowMultipleComponent]
    public class PauseController : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("종료 상태 가드용. 비우면 Instance/씬에서 탐색.")]
        [SerializeField] private GameManager gameManager;

        [Tooltip("터렛 배치/철거 중 ESC 중복 처리 방지용. 비우면 씬에서 탐색.")]
        [SerializeField] private TurretPlacer turretPlacer;

        [Header("Input")]
        [Tooltip("체크 시 지정 키로 일시정지/재개를 토글.")]
        [SerializeField] private bool toggleWithKey = true;

        [Tooltip("토글에 사용할 키(새 Input System).")]
        [SerializeField] private Key toggleKey = Key.Escape;

        /// <summary>일시정지 상태가 바뀜(true=정지). UI가 구독한다.</summary>
        public event Action<bool> OnPauseChanged;

        /// <summary>현재 일시정지 상태.</summary>
        public bool IsPaused { get; private set; }

        private void Awake()
        {
            if (gameManager == null)
                gameManager = GameManager.Instance ?? FindObjectOfType<GameManager>();
            if (turretPlacer == null) turretPlacer = FindObjectOfType<TurretPlacer>();
        }

        private void Update()
        {
            if (!toggleWithKey) return;
            // 터렛 배치/철거 중이면 ESC는 TurretPlacer의 '취소'가 소비하므로 여기선 무시(중복 처리 방지).
            if (IsPlacerBusy) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard[toggleKey].wasPressedThisFrame) Toggle();
        }

        private bool IsPlacerBusy =>
            turretPlacer != null && (turretPlacer.Armed != null || turretPlacer.Demolishing);

        /// <summary>일시정지한다(이미 정지 중이거나 게임이 종료됐으면 무시).</summary>
        public void Pause()
        {
            if (IsPaused || IsGameEnded) return; // 결과 화면 중에는 정지 개념이 없음.
            IsPaused = true;
            Time.timeScale = 0f;
            OnPauseChanged?.Invoke(true);
        }

        /// <summary>재개한다(정지 중이 아니면 무시).</summary>
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            // 종료됐다면 GameManager가 소유한 timeScale=0을 유지해야 하므로 복구하지 않는다.
            if (!IsGameEnded) Time.timeScale = 1f;
            OnPauseChanged?.Invoke(false);
        }

        /// <summary>정지/재개를 토글한다.</summary>
        public void Toggle()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        private bool IsGameEnded => gameManager != null && gameManager.IsGameEnded;

        private void OnDestroy()
        {
            // 재개 없이 씬을 떠날 때 timeScale이 0으로 남지 않도록 방어.
            // (씬 전환 메서드도 1로 복구하지만, 이중 안전장치.)
            if (IsPaused && !IsGameEnded) Time.timeScale = 1f;
        }
    }
}
