using UnityEngine;
using UnityEngine.UI;

namespace TopViewDefense.Core.Audio
{
    /// <summary>
    /// 이 오브젝트 하위(비활성 포함)의 모든 <see cref="Button"/> 클릭에 공용 클릭음을 자동으로 붙인다.
    /// 개별 버튼 컨트롤러(TitleSceneController, PauseMenuUI 등)를 수정하지 않아도 되도록 하는 무침습 장치.
    /// 보통 각 씬의 Canvas 루트에 하나 붙인다.
    ///
    /// 주의: 런타임에 동적으로 생성되는 버튼(오브젝트 풀 등)에는 소급 적용되지 않는다. 그런 버튼은
    /// 생성 지점에서 <see cref="AudioManager.PlayButtonClick"/>를 직접 붙이거나 이 컴포넌트를 재실행한다.
    /// </summary>
    public sealed class UiClickSfx : MonoBehaviour
    {
        private void Start()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
                buttons[i].onClick.AddListener(AudioManager.PlayButtonClick);
        }
    }
}
