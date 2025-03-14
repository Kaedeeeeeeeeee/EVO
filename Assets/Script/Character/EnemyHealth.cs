using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    private EnemyHealthBar healthBar;

    void Start()
    {
        currentHealth = maxHealth;
        healthBar = GetComponentInChildren<EnemyHealthBar>();

        if (healthBar == null)
        {
            Debug.LogError("❌ 未找到 EnemyHealthBar 组件，请检查是否正确挂载！");
        }
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        Debug.Log($"💀 敌人受到 {damage} 伤害，剩余血量 {currentHealth}");

        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth, maxHealth);
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        Debug.Log("☠️ 敌人死亡！");
        Destroy(gameObject);
    }
}
