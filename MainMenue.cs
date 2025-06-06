using UnityEngine;

public class MainMenu : MonoBehaviour
{
    public void PlayGame()
    {
        Debug.Log("Play Game button clicked!");
        int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }
    public void QuitGame()
    {
        Debug.Log("Quit Game button clicked!");
        Application.Quit();
    }
}