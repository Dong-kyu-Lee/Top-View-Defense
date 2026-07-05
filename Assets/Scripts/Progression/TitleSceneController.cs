using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleSceneController : MonoBehaviour
{
    // Title Scene의 Play Button 이벤트 함수
    public void MoveStageSelectScene()
    {
        SceneManager.LoadScene("StageSelectScene");
    }

    // Title Scene의 Shop Button 이벤트 함수 (CLAUDE.md 2장 타이틀 [상점])
    public void MoveShopScene()
    {
        SceneManager.LoadScene("ShopScene");
    }

    public void ApplicationQuit()
    {
        Application.Quit();
    }
}
