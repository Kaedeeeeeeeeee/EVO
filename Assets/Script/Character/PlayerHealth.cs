using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;
    public Image healthBar;
    public TextMeshProUGUI healthText;

    public float healthDecayRate = 0.02f; // 每秒减少2%
    private bool isAlive = true;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthUI();
        StartCoroutine(HealthDecay()); // 开始持续扣血
    }

    IEnumerator HealthDecay()
    {
        while (isAlive)
        {
            yield return new WaitForSeconds(1f);
            TakeDamage(Mathf.CeilToInt(maxHealth * healthDecayRate));
        }
    }

    public void TakeDamage(int damage)
    {
        if (!isAlive) return;

        currentHealth -= damage;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        UpdateHealthUI();
        Debug.Log("玩家恢复了 " + amount + " 生命值，当前血量：" + currentHealth);
    }

    void UpdateHealthUI()
    {
        if (healthBar != null)
        {
            healthBar.fillAmount = (float)currentHealth / maxHealth; // **血条长度跟随 HP 变化**
        }

        if (healthText != null)
        {
            healthText.text = currentHealth + " / " + maxHealth;
        }
    }


    void Die()
    {
        isAlive = false;
        Debug.Log("玩家死亡！");
        // 这里可以添加游戏结束逻辑
    }
}
