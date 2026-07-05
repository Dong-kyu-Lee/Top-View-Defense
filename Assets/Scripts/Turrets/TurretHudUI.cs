using System.Collections.Generic;
using TMPro;
using TopViewDefense.Core;
using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 정식 uGUI 터렛 HUD 컨트롤러. 임시 IMGUI <see cref="TurretHud"/>를 대체한다.
    ///
    /// - 자식 <see cref="TurretButton"/>들을 모아 배선하고, 선택(Armed)·에너지 여유에 따라 상태를 갱신한다.
    /// - <see cref="PlayerEconomy.OnEnergyChanged"/>를 구독해 잔량 텍스트를 갱신하고 버튼 여유를 재평가한다.
    /// - <see cref="TurretPlacer"/>/<see cref="PlayerEconomy"/>는 수정하지 않는다(이미 Arm/Armed/이벤트 공개).
    ///   맵 클릭 배치는 <see cref="TurretPlacer"/>가 담당하며 UI 위 클릭은 EventSystem이 이미 걸러낸다.
    /// </summary>
    public class TurretHudUI : MonoBehaviour
    {
        [Header("참조")]
        [Tooltip("배치 컨트롤러. 비우면 씬에서 탐색.")]
        [SerializeField] private TurretPlacer placer;

        [Tooltip("에너지 잔량 텍스트(RemainEnergy).")]
        [SerializeField] private TMP_Text energyText;

        [Tooltip("철거(판매) 버튼(선택). 이후 페이즈에서 철거 모드로 연결.")]
        [SerializeField] private Button destroyButton;

        [Header("버튼")]
        [Tooltip("터렛 선택 버튼들. 비우면 자식에서 자동 수집.")]
        [SerializeField] private List<TurretButton> buttons = new List<TurretButton>();

        private PlayerEconomy _economy;

        private void Awake()
        {
            if (placer == null) placer = FindObjectOfType<TurretPlacer>();
            if (buttons == null || buttons.Count == 0)
                buttons = new List<TurretButton>(GetComponentsInChildren<TurretButton>(true));

            foreach (TurretButton b in buttons)
                if (b != null) b.Bind(this);

            if (destroyButton != null) destroyButton.onClick.AddListener(OnDestroyClicked);
        }

        // PlayerEconomy.Instance는 서로의 Awake 순서에 따라 아직 null일 수 있으므로 Start에서 구독한다
        // (PlayerEconomy는 Awake에서 Instance 설정, Start에서 최초 OnEnergyChanged 발행).
        private void Start()
        {
            _economy = PlayerEconomy.Instance;
            if (_economy != null) _economy.OnEnergyChanged += HandleEnergyChanged;
            HandleEnergyChanged(_economy != null ? _economy.Energy : 0);
        }

        private void OnDestroy()
        {
            if (_economy != null) _economy.OnEnergyChanged -= HandleEnergyChanged;
            if (destroyButton != null) destroyButton.onClick.RemoveListener(OnDestroyClicked);
        }

        /// <summary>버튼이 호출: 선택 토글 후 상태 갱신.</summary>
        public void OnButtonClicked(TurretButton button)
        {
            if (placer == null || button == null) return;
            placer.Arm(button.Data);   // 같은 버튼 재클릭 = 토글 해제(TurretPlacer.Arm)
            RefreshButtons();
        }

        private void HandleEnergyChanged(int energy)
        {
            if (energyText != null) energyText.text = energy.ToString();
            RefreshButtons();  // 잔량 변동 → 여유(interactable) 재평가
        }

        private void RefreshButtons()
        {
            PlayerEconomy eco = PlayerEconomy.Instance;
            for (int i = 0; i < buttons.Count; i++)
            {
                TurretButton b = buttons[i];
                if (b == null || b.Data == null) continue;
                bool affordable = eco == null || eco.CanAfford(b.Data.cost);
                bool armed = placer != null && placer.Armed == b.Data;
                b.SetState(affordable, armed);
            }
        }

        // 철거 모드 토글은 이후 페이즈(CLAUDE.md 6장: 소모 에너지 50% 환급). 지금은 훅만 둔다.
        private void OnDestroyClicked()
        {
            Debug.Log("[TurretHudUI] 철거 버튼 클릭 — 철거 모드는 이후 페이즈에서 구현.");
        }
    }
}
