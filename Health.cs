using UnityEngine;
using UnityEngine.UI;

public class Health : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100; // Maximum health
    private int currentHealth;

    [SerializeField] private Slider healthBar; // Reference to the UI Slider

    private void Start()
    {
        currentHealth = maxHealth; // Initialize health
        UpdateHealthBar();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth); // Ensure health doesn't go below 0
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Debug.Log("Health is zero. Object destroyed!");
            Destroy(gameObject); // Destroy the object when health reaches 0
        }
    }

    private void UpdateHealthBar()
    {
        if (healthBar != null)
        {
            healthBar.value = (float)currentHealth / maxHealth; // Update the slider value
        }
    }
}