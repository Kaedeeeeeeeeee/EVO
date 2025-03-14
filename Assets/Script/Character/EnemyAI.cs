using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public Transform target; // 目标（玩家）
    public float attackRange = 2f; // 攻击范围
    public int attackDamage = 10; // 伤害值
    public float attackCooldown = 1f; // 攻击后僵直时间

    private NavMeshAgent agent;
    private bool canAttack = true; // 是否可以攻击

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Update()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        if (distance <= attackRange && canAttack)
        {
            StartCoroutine(Attack());
        }
        else
        {
            agent.SetDestination(target.position);
        }
    }

    IEnumerator Attack()
    {
        canAttack = false;
        agent.isStopped = true; // 敌人停止移动
        Debug.Log("敌人攻击！");

        // 伤害玩家
        PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.TakeDamage(attackDamage);
        }

        yield return new WaitForSeconds(attackCooldown); // 僵直 1 秒

        agent.isStopped = false; // 重新允许移动
        canAttack = true;
    }
}
