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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K) && canAttack)
        {
            StartCoroutine(Attack());
        }
    }

    private IEnumerator Attack()
    {
        canAttack = false;
        Debug.Log("⚔️ 玩家发动攻击！");

        // 计算攻击范围位置
        Vector3 attackPosition = transform.position + transform.forward * 1f;
        float radius = attackRange;

        Collider[] hitEnemies = Physics.OverlapSphere(attackPosition, radius, enemyLayer);

        if (hitEnemies.Length > 0)
        {
            foreach (Collider enemyCollider in hitEnemies)
            {
                EnemyHealth enemy = enemyCollider.GetComponent<EnemyHealth>();
                if (enemy != null)
                {
                    int finalDamage = Mathf.RoundToInt(attackDamage * damageMultiplier);
                    enemy.TakeDamage(finalDamage);
                    Debug.Log($"🩸 击中 {enemyCollider.name}，造成 {finalDamage} 伤害！");
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

    private void OnDrawGizmosSelected()
    {
        // 在 Scene 视图中显示攻击范围，方便调试
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + transform.forward * 1f, attackRange);
    }
}
