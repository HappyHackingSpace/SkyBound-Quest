using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneManagement : MonoBehaviour
{
    #region SceneManagement

    public void LoadMainMenuScene()
    {
        SceneManager.LoadScene("Scenes/Main Menu");
    }
    public void LoadTestLevelScene()
    {
        SceneManager.LoadScene("Scenes/Test Scene");
    }
    public void QuitGame()
    {
        Application.Quit();
    }

    #endregion
}
