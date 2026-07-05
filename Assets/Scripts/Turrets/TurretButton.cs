using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Turrets
{
    /// <summary>
    /// 정식 uGUI 터렛 선택 버튼 1개(씬의 Button 오브젝트에 부착). 자기 <see cref="TurretData"/>를 들고
    /// 자신의 <see cref="Button.onClick"/>을 <see cref="TurretHudUI"/>로 자가 배선한다.
    ///
    /// 임시 IMGUI <see cref="TurretHud"/>를 대체하는 계층. 터렛 추가는 이 버튼을 복제하고 data만 바꾸면 되며,
    /// 이름/비용 텍스트는 data에서 채워 씬 값과의 드리프트를 막는다(아이콘은 씬에서 수동 지정 — 데이터 필드 없음).
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class TurretButton : MonoBehaviour
    {
        [Tooltip("이 버튼이 선택할 터렛 데이터.")]
        [SerializeField] private TurretData data;

        [Header("표시(선택)")]
        [Tooltip("이름 텍스트. 지정 시 data.displayName으로 채운다.")]
        [SerializeField] private TMP_Text nameText;

        [Tooltip("비용 텍스트. 지정 시 data.cost로 채운다.")]
        [SerializeField] private TMP_Text costText;

        [Tooltip("선택(Armed) 중 켜질 하이라이트 오브젝트(선택). 없으면 무시.")]
        [SerializeField] private GameObject armedIndicator;

        public TurretData Data => data;

        private Button _button;
        private TurretHudUI _hud;

        /// <summary>컨트롤러가 호출: onClick을 배선하고 표시 텍스트를 데이터로 채운다.</summary>
        public void Bind(TurretHudUI hud)
        {
            _hud = hud;
            _button = GetComponent<Button>();
            _button.onClick.AddListener(HandleClick);

            if (data != null)
            {
                if (nameText != null) nameText.text = data.displayName;
                if (costText != null) costText.text = data.cost.ToString();
            }
            if (armedIndicator != null) armedIndicator.SetActive(false);
        }

        /// <summary>컨트롤러가 매 상태 변화 시 호출: 여유 여부로 interactable, 선택 여부로 하이라이트.</summary>
        public void SetState(bool affordable, bool armed)
        {
            if (_button != null) _button.interactable = affordable;
            if (armedIndicator != null) armedIndicator.SetActive(armed);
        }

        private void HandleClick()
        {
            if (_hud != null) _hud.OnButtonClicked(this);
        }
    }
}
