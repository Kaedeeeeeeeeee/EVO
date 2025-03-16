using UnityEngine;

public class EnemyHealth : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    [HideInInspector]
    public EnemyHealthBar healthBar; // 改为公开以便EnemySpawner设置
    private EnemyAIExtended aiExtended;

    // 事件系统 - 可以在外部订阅这些事件
    public delegate void EnemyDeathHandler(EnemyHealth enemy);
    public static event EnemyDeathHandler OnEnemyDeath;

    // 额外的属性
    [HideInInspector] public int levelDropped = 1; // 死亡时掉落的进化点数

    void Start()
    {
        currentHealth = maxHealth;

        // 尝试查找血条组件(如果还没有设置)
        if (healthBar == null)
        {
            healthBar = GetComponentInChildren<EnemyHealthBar>();

            if (healthBar == null)
            {
                Debug.LogWarning($"⚠️ 敌人 {gameObject.name} 没有找到EnemyHealthBar组件");
            }
        }

        aiExtended = GetComponent<EnemyAIExtended>();

        // 设置掉落点数基于等级
        if (aiExtended != null)
        {
            levelDropped = aiExtended.level * 10; // 等级越高，掉落的点数越多
        }

        // 初始化血条显示
        UpdateHealthBar();
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(0, currentHealth); // 确保生命值不会低于0

        Debug.Log($"💀 敌人 {gameObject.name} 受到 {damage} 伤害，剩余血量 {currentHealth}/{maxHealth}");

        // 更新血条显示 - 确保在受伤时显示
        UpdateHealthBar();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;

        // 更新血条显示
        UpdateHealthBar();
    }

    private void UpdateHealthBar()
    {
        // 如果有血条组件则更新显示
        if (healthBar != null)
        {
            healthBar.SetHealth(currentHealth, maxHealth);
            Debug.Log($"敌人 {gameObject.name} 的血条已更新: {currentHealth}/{maxHealth}");
        }
        else
        {
            // 如果没有找到，尝试再次查找
            healthBar = GetComponentInChildren<EnemyHealthBar>();
            if (healthBar != null)
            {
                healthBar.SetHealth(currentHealth, maxHealth);
                Debug.Log($"找到并更新了敌人 {gameObject.name} 的血条");
            }
            else
            {
                Debug.LogWarning($"⚠️ 敌人 {gameObject.name} 没有血条UI，无法显示生命值");

                // 最后尝试 - 查找子对象中任何包含"health"或"bar"的物体
                Transform potentialHealthBar = null;
                foreach (Transform child in transform)
                {
                    if (child.name.ToLower().Contains("health") || child.name.ToLower().Contains("bar"))
                    {
                        potentialHealthBar = child;
                        break;
                    }
                }

                if (potentialHealthBar != null)
                {
                    healthBar = potentialHealthBar.GetComponent<EnemyHealthBar>();
                    if (healthBar != null)
                    {
                        healthBar.SetHealth(currentHealth, maxHealth);
                        Debug.Log($"找到并更新了敌人 {gameObject.name} 的备选血条");
                    }
                }
            }
        }
    }

    public bool IsDead()
    {
        return currentHealth <= 0;
    }

    public float GetHealthPercentage()
    {
        return (float)currentHealth / maxHealth;
    }

    private void Die()
    {
        Debug.Log($"☠️ 敌人 {gameObject.name} 死亡！");

        // 给玩家增加进化点数
        if (aiExtended != null)
        {
            int pointsToAdd = levelDropped;
            PlayerEvolution playerEvolution = PlayerEvolution.Instance;
            if (playerEvolution != null)
            {
                playerEvolution.AddEvolutionPoints(pointsToAdd);
                Debug.Log($"⚡ 玩家获得 {pointsToAdd} 进化点数！");
            }
        }

        // 触发死亡事件
        if (OnEnemyDeath != null)
        {
            OnEnemyDeath(this);
        }

        // 可以添加死亡动画/粒子效果
        // Instantiate(deathEffect, transform.position, Quaternion.identity);

        Destroy(gameObject);
    }
}