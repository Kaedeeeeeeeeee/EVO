using System.Collections;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAIExtended : MonoBehaviour
{
    // 敌人属性
    public int level = 1;                    // 敌人等级
    public float walkSpeed = 2f;             // 行走速度
    public float runSpeed = 5f;              // 奔跑速度
    public float visionRange = 10f;          // 视野范围
    public float attackRange = 2f;           // 攻击范围
    public int attackDamage = 10;            // 攻击伤害
    public float attackCooldown = 1f;        // 攻击冷却时间
    public float huntThreshold = 30f;        // 猎杀阈值（饥饿百分比）

    // 行为参数
    public float wanderRadius = 10f;         // 游荡半径
    public float wanderInterval = 5f;        // 游荡间隔时间
    public float alertDuration = 3f;         // 警觉持续时间
    public float eatingDuration = 5f;        // 进食持续时间
    public float hungerDecreaseRate = 0.1f;  // 饥饿值降低速率（每秒）

    // 组件引用
    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform target;                // 当前目标（可以是玩家或其他敌人）
    private Vector3 startPosition;           // 初始位置
    private Vector3 wanderDestination;       // 游荡目的地

    // 内部状态
    private EnemyState currentState = EnemyState.Idle;
    private float currentHunger = 100f;      // 当前饥饿值（100满，0饿）
    private bool canAttack = true;
    private float lastStateChangeTime;
    private int playerLevel = 1;             // 玩家等级（需从玩家处获取）

    // 调试可视化
    public bool showVisionRange = true;
    public bool showAttackRange = true;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        startPosition = transform.position;

        // 初始化NavMeshAgent参数
        agent.speed = walkSpeed;
        agent.stoppingDistance = attackRange * 0.8f;

        // 开始行为循环
        StartCoroutine(UpdateBehavior());
        StartCoroutine(UpdateHunger());

        // 随机初始饥饿值（60-100）
        currentHunger = Random.Range(60f, 100f);

        // 初始状态为游荡
        TransitionToState(EnemyState.Wandering);
    }

    public void InitializeEnemy()
    {
        // 在EnemySpawner中调用，确保所有参数设置正确
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        agent.speed = walkSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
    }

    IEnumerator UpdateBehavior()
    {
        while (true)
        {
            // 查找玩家
            if (target == null || (target.CompareTag("Player") && !target.gameObject.activeInHierarchy))
            {
                target = GameObject.FindGameObjectWithTag("Player")?.transform;
                if (target != null)
                {
                    // 获取玩家等级
                    PlayerEvolution playerEvolution = target.GetComponent<PlayerEvolution>();
                    if (playerEvolution != null)
                    {
                        // 这里假设PlayerEvolution有一个公开的level变量
                        // 需要修改PlayerEvolution脚本添加此变量
                        playerLevel = playerEvolution.level;
                    }
                }
            }

            // 根据当前状态执行行为
            switch (currentState)
            {
                case EnemyState.Idle:
                    HandleIdleState();
                    break;

                case EnemyState.Wandering:
                    HandleWanderingState();
                    break;

                case EnemyState.Alert:
                    HandleAlertState();
                    break;

                case EnemyState.Hunting:
                    HandleHuntingState();
                    break;

                case EnemyState.Attacking:
                    HandleAttackingState();
                    break;

                case EnemyState.Fleeing:
                    HandleFleeingState();
                    break;

                case EnemyState.Eating:
                    HandleEatingState();
                    break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }

    IEnumerator UpdateHunger()
    {
        while (true)
        {
            // 随着时间减少饥饿值
            currentHunger = Mathf.Max(0f, currentHunger - hungerDecreaseRate);

            // 更新名称以显示状态（便于调试）
            UpdateNameWithStatus();

            yield return new WaitForSeconds(1f);
        }
    }

    void UpdateNameWithStatus()
    {
        HungerStatus hungerStatus = GetHungerStatus();
        string statusText = currentState.ToString();

        switch (hungerStatus)
        {
            case HungerStatus.Satiated:
                statusText += " (饱食)";
                break;
            case HungerStatus.Normal:
                statusText += " (正常)";
                break;
            case HungerStatus.Hungry:
                statusText += " (饥饿)";
                break;
            case HungerStatus.Starving:
                statusText += " (饥饿难耐)";
                break;
        }

        gameObject.name = $"敌人Lv{level} - {statusText}";
    }

    void HandleIdleState()
    {
        // 检测附近的玩家或其他敌人
        Collider[] colliders = Physics.OverlapSphere(transform.position, visionRange);
        foreach (Collider col in colliders)
        {
            // 如果检测到玩家
            if (col.CompareTag("Player"))
            {
                // 获取玩家等级
                PlayerEvolution playerEvolution = col.GetComponent<PlayerEvolution>();
                if (playerEvolution != null)
                {
                    playerLevel = playerEvolution.level;
                }

                // 根据等级差异决定行为
                DetermineActionBasedOnLevel(col.transform, playerLevel);
                return;
            }

            // 对其他敌人的处理可以在此添加
        }

        // 如果空闲太久，开始游荡
        if (Time.time - lastStateChangeTime > 3f)
        {
            TransitionToState(EnemyState.Wandering);
        }
    }

    void HandleWanderingState()
    {
        // 检查是否需要设置新的游荡目标
        if (agent.remainingDistance < 0.5f || !agent.hasPath)
        {
            SetNewWanderDestination();
        }

        // 检测附近的玩家或其他敌人
        Collider[] colliders = Physics.OverlapSphere(transform.position, visionRange);
        foreach (Collider col in colliders)
        {
            // 如果检测到玩家
            if (col.CompareTag("Player"))
            {
                // 获取玩家等级信息并决定行为
                PlayerEvolution playerEvolution = col.GetComponent<PlayerEvolution>();
                if (playerEvolution != null)
                {
                    playerLevel = playerEvolution.level;
                }

                DetermineActionBasedOnLevel(col.transform, playerLevel);
                return;
            }

            // 其他敌人的处理（以后可以扩展）
        }
    }

    void HandleAlertState()
    {
        // 警觉状态 - 停止移动，观察周围
        agent.isStopped = true;

        // 继续检测目标
        if (target != null)
        {
            // 面向目标
            Vector3 targetDirection = target.position - transform.position;
            targetDirection.y = 0;
            if (targetDirection != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    Quaternion.LookRotation(targetDirection),
                    Time.deltaTime * 5f
                );
            }

            // 重新评估行为
            if (target.CompareTag("Player"))
            {
                DetermineActionBasedOnLevel(target, playerLevel);
            }
        }

        // 警觉时间结束后返回游荡
        if (Time.time - lastStateChangeTime > alertDuration)
        {
            TransitionToState(EnemyState.Wandering);
        }
    }

    void HandleHuntingState()
    {
        if (target == null)
        {
            TransitionToState(EnemyState.Wandering);
            return;
        }

        // 更新速度为奔跑速度
        agent.isStopped = false;
        agent.speed = runSpeed;

        // 追击目标
        agent.SetDestination(target.position);

        // 如果进入攻击范围，开始攻击
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget <= attackRange && canAttack)
        {
            TransitionToState(EnemyState.Attacking);
        }

        // 如果目标已逃离视野，回到警觉状态
        if (distanceToTarget > visionRange * 1.5f)
        {
            TransitionToState(EnemyState.Alert);
        }
    }

    void HandleAttackingState()
    {
        if (target == null)
        {
            TransitionToState(EnemyState.Wandering);
            return;
        }

        // 面向目标
        Vector3 targetDirection = target.position - transform.position;
        targetDirection.y = 0;
        if (targetDirection != Vector3.zero)
        {
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                Quaternion.LookRotation(targetDirection),
                Time.deltaTime * 8f
            );
        }

        // 如果可以攻击，执行攻击
        if (canAttack)
        {
            StartCoroutine(PerformAttack());
        }

        // 如果目标离开攻击范围，回到追击状态
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget > attackRange * 1.2f)
        {
            TransitionToState(EnemyState.Hunting);
        }
    }

    void HandleFleeingState()
    {
        if (target == null)
        {
            TransitionToState(EnemyState.Wandering);
            return;
        }

        // 设置逃跑速度
        agent.isStopped = false;
        agent.speed = runSpeed;

        // 计算逃跑方向 - 远离目标
        Vector3 fleeDirection = (transform.position - target.position).normalized;
        Vector3 fleePosition = transform.position + fleeDirection * 15f;

        // 寻找有效的逃跑位置
        NavMeshHit hit;
        if (NavMesh.SamplePosition(fleePosition, out hit, 10f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
        }

        // 检查是否已经安全（目标远离视野）
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget > visionRange * 1.5f)
        {
            TransitionToState(EnemyState.Alert);
        }
    }

    void HandleEatingState()
    {
        // 停止移动，播放进食动画/特效
        agent.isStopped = true;

        // 增加饥饿值
        currentHunger = Mathf.Min(100f, currentHunger + 5f); // 每秒恢复5点饥饿值

        // 进食结束后回到游荡状态
        if (Time.time - lastStateChangeTime > eatingDuration)
        {
            TransitionToState(EnemyState.Wandering);
        }
    }

    void DetermineActionBasedOnLevel(Transform newTarget, int targetLevel)
    {
        target = newTarget;
        int levelDifference = level - targetLevel;
        HungerStatus hungerStatus = GetHungerStatus();

        // 规则1: 遭遇到的敌人等级小于自身1级以上，不会主动追击，但如果距离很近，会顺便吃掉
        if (levelDifference > 1)
        {
            float distanceToTarget = Vector3.Distance(transform.position, target.position);
            if (distanceToTarget <= attackRange * 1.5f)
            {
                TransitionToState(EnemyState.Attacking);
            }
            else
            {
                TransitionToState(EnemyState.Alert);
            }
        }
        // 规则2: 遭遇到的敌人等级小于自身1级，会主动的追击猎物
        else if (levelDifference == 1)
        {
            TransitionToState(EnemyState.Hunting);
        }
        // 规则3: 遭遇到的敌人等级与自身相同，根据饥饿值判断
        else if (levelDifference == 0)
        {
            if (hungerStatus == HungerStatus.Hungry || hungerStatus == HungerStatus.Starving)
            {
                TransitionToState(EnemyState.Hunting);
            }
            else
            {
                // 不饥饿时远离同级敌人
                TransitionToState(EnemyState.Fleeing);
            }
        }
        // 规则4: 遭遇到的敌人等级高于自身，选择逃跑
        else
        {
            TransitionToState(EnemyState.Fleeing);
        }
    }

    void SetNewWanderDestination()
    {
        // 在起始位置周围找一个随机点
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += startPosition;

        NavMeshHit hit;
        if (NavMesh.SamplePosition(randomDirection, out hit, wanderRadius, NavMesh.AllAreas))
        {
            wanderDestination = hit.position;
            agent.SetDestination(wanderDestination);
        }
    }

    void TransitionToState(EnemyState newState)
    {
        // 退出当前状态的逻辑
        switch (currentState)
        {
            case EnemyState.Attacking:
                // 重置攻击相关变量
                break;

            case EnemyState.Hunting:
                // 可能的清理工作
                break;
        }

        // 更新状态
        currentState = newState;
        lastStateChangeTime = Time.time;

        // 进入新状态的逻辑
        switch (newState)
        {
            case EnemyState.Idle:
                agent.isStopped = true;
                break;

            case EnemyState.Wandering:
                agent.isStopped = false;
                agent.speed = walkSpeed;
                SetNewWanderDestination();
                break;

            case EnemyState.Alert:
                agent.isStopped = true;
                break;

            case EnemyState.Hunting:
                agent.isStopped = false;
                agent.speed = runSpeed;
                if (target != null)
                {
                    agent.SetDestination(target.position);
                }
                break;

            case EnemyState.Attacking:
                agent.isStopped = true;
                break;

            case EnemyState.Fleeing:
                agent.isStopped = false;
                agent.speed = runSpeed;
                break;

            case EnemyState.Eating:
                agent.isStopped = true;
                break;
        }

        // 更新显示名称
        UpdateNameWithStatus();

        Debug.Log($"🔄 敌人Lv{level} 切换到 {newState} 状态");
    }

    // 在EnemyAIExtended.cs中修改PerformAttack方法
    IEnumerator PerformAttack()
    {
        canAttack = false;

        // 执行攻击动作（可以添加动画触发器）
        Debug.Log($"⚔️ 敌人Lv{level} 发起攻击！");

        // 伤害目标
        if (target != null && Vector3.Distance(transform.position, target.position) <= attackRange)
        {
            if (target.CompareTag("Player"))
            {
                // 攻击玩家
                PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(attackDamage);
                    Debug.Log($"🩸 敌人Lv{level} 对玩家造成 {attackDamage} 伤害");

                    // 检查是否已杀死玩家
                    if (playerHealth.IsDead())
                    {
                        TransitionToState(EnemyState.Eating);
                    }
                    // 如果没有IsDead方法(向后兼容)，也可以这样检查
                    else if (playerHealth.GetHealthPercentage() <= 0)
                    {
                        TransitionToState(EnemyState.Eating);
                    }
                }
            }
            else
            {
                // 攻击其他敌人
                EnemyHealth enemyHealth = target.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    // 确保造成伤害并显示血条
                    enemyHealth.TakeDamage(attackDamage);
                    Debug.Log($"🩸 敌人Lv{level} 对另一个敌人造成 {attackDamage} 伤害");

                    // 检查是否已杀死敌人
                    if (enemyHealth.IsDead())
                    {
                        TransitionToState(EnemyState.Eating);
                        Debug.Log($"🍖 敌人Lv{level} 击杀了另一个敌人，开始进食");
                    }
                }
                else
                {
                    // 尝试在父对象或子对象中查找
                    enemyHealth = target.GetComponentInParent<EnemyHealth>();
                    if (enemyHealth == null)
                    {
                        enemyHealth = target.GetComponentInChildren<EnemyHealth>();
                    }

                    if (enemyHealth != null)
                    {
                        enemyHealth.TakeDamage(attackDamage);
                        Debug.Log($"🩸 敌人Lv{level} 对另一个敌人造成 {attackDamage} 伤害 (组件在父/子对象)");

                        if (enemyHealth.IsDead())
                        {
                            TransitionToState(EnemyState.Eating);
                        }
                    }
                }
            }
        }

        // 攻击冷却
        yield return new WaitForSeconds(attackCooldown);
        canAttack = true;
    }

    HungerStatus GetHungerStatus()
    {
        if (currentHunger > 75f)
            return HungerStatus.Satiated;
        else if (currentHunger > 50f)
            return HungerStatus.Normal;
        else if (currentHunger > 25f)
            return HungerStatus.Hungry;
        else
            return HungerStatus.Starving;
    }

    void OnDrawGizmosSelected()
    {
        if (showVisionRange)
        {
            // 显示视野范围
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, visionRange);
        }

        if (showAttackRange)
        {
            // 显示攻击范围
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }

        // 显示当前目标
        if (target != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(transform.position, target.position);
        }
    }
}