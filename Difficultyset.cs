using UnityEngine;

public class DifficultySet : MonoBehaviour
{
    public enum Difficulty
    {
        Easy,
        Normal,
        Hard
    }

    [Header("Difficulty Settings")]
    public Difficulty selectedDifficulty = Difficulty.Normal;

    [Header("Values Based on Difficulty")]
    public int baseValue = 10;
    public int modifiedValue;

    private void Start()
    {
        // Retrieve the difficulty from the DifficultySelectionMenu
        selectedDifficulty = (Difficulty)System.Enum.Parse(typeof(Difficulty), DifficultySelectionMenu.SelectedDifficulty.ToString());

        // Update the modified value based on the selected difficulty
        SetModifiedValue();
    }

    private void SetModifiedValue()
    {
        switch (selectedDifficulty)
        {
            case Difficulty.Easy:
                modifiedValue = baseValue;
                break;
            case Difficulty.Normal:
                modifiedValue = baseValue * 3;
                break;
            case Difficulty.Hard:
                modifiedValue = baseValue * 6;
                break;
        }

        Debug.Log("Modified value set to: " + modifiedValue);
    }
}