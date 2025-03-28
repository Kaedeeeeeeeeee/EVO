using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

public class PlayerHealth : MonoBehaviour
{
    public int maxHealth = 100;
    public float currentHealth { get; private set; }
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
        while (isAlive && this != null)
        {
            if (this == null || !gameObject)
                yield break;
            
            yield return new WaitForSeconds(1f);
            
            if (this == null || !gameObject)
                yield break;
            
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

    // 修改：根据敌人等级计算恢复量，改为直接使用参数传入的值
    public void HealFromEnemy(int healAmount)
    {
        Heal(healAmount);
        Debug.Log($"🍖 玩家从敌人尸体获得 {healAmount} 生命值！");
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
        StopAllCoroutines();
        
        // 通知GameManager处理游戏结束
        if (GameManager.Instance != null)
        {
            GameManager.Instance.GameOver();
            // 不要在这里直接加载场景
        }
    }

    // 添加IsDead方法供敌人AI使用
    public bool IsDead()
    {
        return !isAlive || currentHealth <= 0;
    }

    // 获取当前生命值百分比
    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    // 添加一个公共方法获取当前生命值
    public float GetCurrentHealth()
    {
        return currentHealth;
    }

    // 添加重置玩家生命值的方法
    public void ResetHealth()
    {
        currentHealth = maxHealth;
        isAlive = true;
        UpdateHealthUI();
        
        // 重新开始持续扣血
        StopAllCoroutines();
        StartCoroutine(HealthDecay());
        
        Debug.Log("玩家生命值已重置为最大值: " + maxHealth);
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        // 例如，如果您订阅了某个事件：
        // GameManager.OnGameOver -= HandleGameOver;
    }
}