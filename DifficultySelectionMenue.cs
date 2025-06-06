using UnityEngine;

public class DifficultySelectionMenu : MonoBehaviour
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    public static Difficulty SelectedDifficulty { get; private set; } = Difficulty.Normal; // Default difficulty

    public void SetEasyDifficulty()
    {
        SelectedDifficulty = Difficulty.Easy;
        Debug.Log("Difficulty set to Easy");
        int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }

    public void SetNormalDifficulty()
    {
        SelectedDifficulty = Difficulty.Normal;
        Debug.Log("Difficulty set to Normal");
        int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }

    public void SetHardDifficulty()
    {
        SelectedDifficulty = Difficulty.Hard;
        Debug.Log("Difficulty set to Hard");
        int nextSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex + 1;
        UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
    }
}