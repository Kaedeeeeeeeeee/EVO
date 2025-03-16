using System.Collections;
using UnityEngine;

public class PlayerCombat : MonoBehaviour
{
    public float attackRange = 1.5f; // 攻击范围
    public int attackDamage = 20; // 伤害值
    public float attackCooldown = 0.5f; // 攻击冷却时间
    public float damageMultiplier = 1f; // 伤害倍率
    public LayerMask enemyLayer; // 仅检测敌人层

    private bool canAttack = true;

    void Start()
    {
        // 确保enemyLayer设置正确
        if (enemyLayer.value == 0)
        {
            Debug.LogWarning("⚠️ PlayerCombat: enemyLayer未设置，尝试使用默认'Enemy'层");
            enemyLayer = LayerMask.GetMask("Enemy");

            // 如果仍然没有设置，使用所有层
            if (enemyLayer.value == 0)
            {
                Debug.LogWarning("⚠️ PlayerCombat: 未找到'Enemy'层，使用默认层");
                enemyLayer = -1; // 使用所有层作为后备方案
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K) && canAttack)
        {
            StartCoroutine(Attack());
        }
    }

    // 修改PlayerCombat.cs中处理敌人伤害的部分
    // 在Attack协程中找到类似这样的代码:

    private IEnumerator Attack()
    {
        canAttack = false;
        Debug.Log("⚔️ 玩家发动攻击！");

        // 计算攻击范围位置
        Vector3 attackPosition = transform.position + transform.forward * 1f;
        float radius = attackRange;

        // 可视化攻击范围（仅调试用）
        Debug.DrawRay(attackPosition, Vector3.up * 2f, Color.red, 0.5f);
        Debug.DrawRay(attackPosition, transform.forward * radius, Color.red, 0.5f);

        // 检测敌人
        Collider[] hitEnemies = Physics.OverlapSphere(attackPosition, radius, enemyLayer);

        if (hitEnemies.Length > 0)
        {
            Debug.Log($"👹 检测到 {hitEnemies.Length} 个敌人在攻击范围内");

            foreach (Collider enemyCollider in hitEnemies)
            {
                Debug.Log($"🎯 攻击目标: {enemyCollider.name}, Layer: {LayerMask.LayerToName(enemyCollider.gameObject.layer)}");

                // 尝试获取EnemyHealth组件
                EnemyHealth enemy = enemyCollider.GetComponent<EnemyHealth>();

                // 如果没有在主物体上找到，尝试在父物体或子物体中查找
                if (enemy == null)
                {
                    enemy = enemyCollider.GetComponentInParent<EnemyHealth>();
                }

                if (enemy == null)
                {
                    enemy = enemyCollider.GetComponentInChildren<EnemyHealth>();
                }

                if (enemy != null)
                {
                    int finalDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);

                    // 造成伤害（会触发血条显示）
                    enemy.TakeDamage(finalDamage);
                    Debug.Log($"🩸 击中 {enemyCollider.name}，造成 {finalDamage} 伤害！");

                    // 额外确保血条显示
                    EnemyHealthBar healthBar = enemy.GetComponentInChildren<EnemyHealthBar>();
                    if (healthBar != null)
                    {
                        healthBar.ShowHealthBar();
                    }

                    // 播放命中特效
                    PlayHitEffect(enemyCollider.transform.position);
                }
                else
                {
                    Debug.LogWarning($"⚠️ 敌人 {enemyCollider.name} 没有EnemyHealth组件！");
                }
            }
        }
        else
        {
            Debug.Log("❌ 攻击未命中任何敌人");
        }

        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    // 播放命中特效
    private void PlayHitEffect(Vector3 position)
    {
        // 这里可以实例化一个特效预制体或粒子系统
        // 如果有预制体，可以这样使用:
        // if (hitEffectPrefab != null)
        // {
        //     Instantiate(hitEffectPrefab, position, Quaternion.identity);
        // }

        // 临时特效 - 在没有预制体的情况下使用调试线条
        Debug.DrawRay(position, Vector3.up * 0.5f, Color.red, 0.5f);
        Debug.DrawRay(position, Vector3.right * 0.5f, Color.red, 0.5f);
        Debug.DrawRay(position, Vector3.forward * 0.5f, Color.red, 0.5f);
    }

    private void OnDrawGizmosSelected()
    {
        // 在Scene视图中显示攻击范围，方便调试
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1f, attackRange);
    }
}