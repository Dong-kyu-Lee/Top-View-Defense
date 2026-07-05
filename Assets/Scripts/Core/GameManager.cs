using System;
using TopViewDefense.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TopViewDefense.Core
{
    /// <summary>스테이지 종료 결과. None=진행 중.</summary>
    public enum GameResult { None, Cleared, GameOver }

    /// <summary>
    /// 스테이지의 승패 판정과 종료 처리를 한곳에 모으는 중앙 컨트롤러. (CLAUDE.md 2장)
    ///
    /// - <see cref="BaseCore.OnGameOver"/> 구독 → 패배(GameOver).
    /// - <see cref="WaveRunner.OnAllWavesCleared"/> 구독 → 승리(Cleared, 남은 목숨으로 별점).
    /// - 종료 시 웨이브 정지 + <c>Time.timeScale=0</c>으로 전체를 멈추고 <see cref="OnGameEnded"/>를 발행한다.
    /// - 재시도/스테이지 선택 씬 전환을 소유(그룹 C 설정 메뉴가 이 메서드들을 재사용).
    ///
    /// 별/골드 영구 저장·다음 스테이지 해금은 StageSelect 메타 계층의 몫이라 여기서 다루지 않는다.
    /// 결과·별점을 <see cref="OnGameEnded"/>로 노출해 두면 이후 그 지점에 저장 훅만 끼우면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("References")]
        [Tooltip("기지 목숨/게임오버 소스. 비우면 씬에서 탐색.")]
        [SerializeField] private BaseCore baseCore;

        [Tooltip("웨이브 진행 소유자. 비우면 씬에서 탐색.")]
        [SerializeField] private WaveRunner waveRunner;

        [Header("Scene")]
        [Tooltip("스테이지 선택 씬 이름(Build Settings에 등록되어 있어야 함).")]
        [SerializeField] private string stageSelectScene = "StageSelectScene";

        /// <summary>게임이 종료됨(승리/패배, 남은 목숨 기준 별점). 결과 UI가 구독한다.</summary>
        public event Action<GameResult, int> OnGameEnded;

        /// <summary>현재 결과. 진행 중이면 None.</summary>
        public GameResult Result { get; private set; } = GameResult.None;

        /// <summary>게임이 이미 종료되었는지(중복 종료 방지).</summary>
        public bool IsGameEnded => Result != GameResult.None;

        private bool _subscribed;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[GameManager] 인스턴스가 이미 존재합니다. 중복을 제거합니다.", this);
                Destroy(this);
                return;
            }
            Instance = this;

            // 이전 판이 timeScale=0(종료/일시정지)로 씬을 떠났을 수 있으므로 방어적으로 복구.
            Time.timeScale = 1f;
        }

        private void OnEnable() => TrySubscribe();

        private void Start() => TrySubscribe(); // 참조가 아직 없었을 경우 대비.

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void TrySubscribe()
        {
            if (_subscribed) return;

            if (baseCore == null) baseCore = FindObjectOfType<BaseCore>();
            if (waveRunner == null) waveRunner = FindObjectOfType<WaveRunner>();
            if (baseCore == null || waveRunner == null) return; // Start에서 다시 시도.

            baseCore.OnGameOver += HandleGameOver;
            waveRunner.OnAllWavesCleared += HandleAllCleared;
            _subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            if (baseCore != null) baseCore.OnGameOver -= HandleGameOver;
            if (waveRunner != null) waveRunner.OnAllWavesCleared -= HandleAllCleared;
            _subscribed = false;
        }

        private void HandleGameOver() => EndGame(GameResult.GameOver);
        private void HandleAllCleared() => EndGame(GameResult.Cleared);

        private void EndGame(GameResult result)
        {
            if (IsGameEnded) return; // 단발 가드: 두 이벤트가 겹쳐도 최초 1회만.
            Result = result;

            // 패배 후에도 계속 도는 웨이브/스폰을 정지.
            if (waveRunner != null) waveRunner.StopWaves();

            int stars = result == GameResult.Cleared && baseCore != null ? baseCore.StarRating : 0;

            Time.timeScale = 0f; // 적/터렛/에너지/카운트다운 일괄 정지.
            OnGameEnded?.Invoke(result, stars);
            Debug.Log($"[GameManager] 게임 종료: {result} (별 {stars})", this);
        }

        /// <summary>현재 스테이지를 다시 시작(씬 리로드). timeScale 복구.</summary>
        public void RetryStage()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        /// <summary>스테이지 선택 씬으로 이동. timeScale 복구.</summary>
        public void GoToStageSelect()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(stageSelectScene);
        }
    }
}
