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

    // 新增：物理碰撞参数
    public float collisionCheckRadius = 1.0f;  // 碰撞检测半径
    public float pushForce = 5.0f;            // 推力大小
    public LayerMask terrainLayerMask;        // 地形层遮罩
    public bool useRigidbody = true;          // 是否使用刚体进行物理交互

    // 组件引用
    private NavMeshAgent agent;
    private EnemyHealth health;
    private Transform target;                // 当前目标（可以是玩家或其他敌人）
    private Transform startPosition;          // 初始位置
    private Vector3 wanderDestination;       // 游荡目的地
    private Rigidbody rb;                    // 新增：刚体组件
    private CapsuleCollider capsuleCollider;  // 新增：碰撞体引用

    // 内部状态
    private EnemyState currentState = EnemyState.Idle;
    private float currentHunger = 100f;      // 当前饥饿值（100满，0饿）
    private bool canAttack = true;
    private float lastStateChangeTime;
    private int playerLevel = 1;             // 玩家等级（需从玩家处获取）

    // 调试可视化
    public bool showVisionRange = true;
    public bool showAttackRange = true;

    private Transform currentEatingTarget; // 新增：当前正在进食的目标

    // 新增：处理斜坡和障碍物的方法
    private void OnCollisionStay(Collision collision)
    {
        // 处理与地形的持续碰撞
        TerrainIdentifier terrain = collision.gameObject.GetComponent<TerrainIdentifier>();
        if (terrain != null)
        {
            // 计算碰撞点的平均法线
            Vector3 avgNormal = Vector3.zero;
            int contactCount = collision.contactCount;
            
            for (int i = 0; i < contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            
            if (contactCount > 0)
            {
                avgNormal /= contactCount;
                float angle = Vector3.Angle(avgNormal, Vector3.up);
                
                // 检查是否在陡峭表面上
                if (angle > 45f)
                {
                    // 获取水平移动方向（远离斜坡）
                    Vector3 horizontalNormal = avgNormal;
                    horizontalNormal.y = 0;
                    horizontalNormal.Normalize();
                    
                    // 停止当前路径
                    if (IsAgentValid())
                    {
                        // 尝试找到新路径
                        Vector3 avoidDirection = horizontalNormal;
                        
                        // 稍微抬高起点以避开低障碍物
                        Vector3 startPos = transform.position + Vector3.up * 0.5f;
                        Vector3 targetPos = startPos + avoidDirection * 5f;
                        
                        // 寻找可行的导航点
                        NavMeshHit navHit;
                        if (NavMesh.SamplePosition(targetPos, out navHit, 5f, NavMesh.AllAreas))
                        {
                            // 设置新目标
                            agent.SetDestination(navHit.position);
                            
                            // 调整为慢速移动，提高稳定性
                            agent.speed = walkSpeed * 0.8f;
                            
                            // 向上推一点以防止卡住
                            if (agent.isOnOffMeshLink)
                            {
                                transform.position += Vector3.up * 0.2f;
                            }
                            
                            Debug.Log($"敌人遇到地形{terrain.terrainType}的陡峭表面，寻找新路径。角度:{angle:F1}°");
                        }
                    }
                }
            }
        }
    }
    
    // 新增：处理陷入或卡住的监测
    private Vector3 lastPosition;
    private float stuckCheckInterval = 1.0f;
    private float lastStuckCheckTime;
    private int stuckCounter = 0;
    
    private void Update()
    {
        // 检测是否卡住
        if (Time.time > lastStuckCheckTime + stuckCheckInterval)
        {
            // 只在主动移动状态下检测
            if (IsAgentValid() && !agent.isStopped && agent.hasPath)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastPosition);
                
                // 如果几乎没有移动，可能是卡住了
                if (distanceMoved < 0.1f)
                {
                    stuckCounter++;
                    
                    // 连续多次检测都没移动，采取措施
                    if (stuckCounter >= 3)
                    {
                        UnstuckAgent();
                        stuckCounter = 0;
                    }
                }
                else
                {
                    stuckCounter = 0;
                }
            }
            
            lastPosition = transform.position;
            lastStuckCheckTime = Time.time;
        }
    }
    
    // 尝试解除卡住状态
    private void UnstuckAgent()
    {
        if (!IsAgentValid())
            return;
        
        // 临时提高高度尝试解脱
        transform.position += Vector3.up * 0.5f;
        
        // 重新计算路径
        NavMeshPath path = new NavMeshPath();
        Vector3 randomDirection = Random.insideUnitSphere;
        randomDirection.y = 0;
        randomDirection.Normalize();
        
        Vector3 targetPosition = transform.position + randomDirection * 5f;
        NavMeshHit hit;
        
        if (NavMesh.SamplePosition(targetPosition, out hit, 5f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            Debug.Log("敌人卡住，尝试重新寻路");
        }
        
        // 重置导航代理
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        
        // 如果当前在追击状态但卡住了，可以考虑切换到游荡状态
        if (currentState == EnemyState.Hunting)
        {
            TransitionToState(EnemyState.Wandering);
        }
        
        // 确保NavMesh代理设置正确
        EnsureAgentOnNavMesh();
    }
    
    void Awake()
    {
        // 初始化卡住检测
        lastPosition = transform.position;
        lastStuckCheckTime = Time.time;
    }

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        health = GetComponent<EnemyHealth>();
        
        // 设置初始位置
        startPosition = transform;
        wanderDestination = transform.position;
        
        // 初始化状态和时间
        lastStateChangeTime = Time.time;
        
        // 设置根据等级的属性
        AdjustStatsByLevel();
        
        // 新增：初始化物理组件
        InitializePhysicsComponents();
        
        // 启动行为协程
        StartCoroutine(UpdateBehavior());
        StartCoroutine(DecreaseHunger());
        StartCoroutine(MonitorStuckStatus());
        
        // 启动障碍物检测协程
        StartCoroutine(ObstacleDetection());
        
        // 新增：启动连续碰撞检测协程
        StartCoroutine(ContinuousCollisionDetection());

        // 随机初始饥饿值（60-100）
        currentHunger = Random.Range(60f, 100f);

        // 初始状态为游荡
        TransitionToState(EnemyState.Wandering);
    }

    // 新增：初始化物理组件
    private void InitializePhysicsComponents()
    {
        // 检查和设置刚体
        rb = GetComponent<Rigidbody>();
        if (rb == null && useRigidbody)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.mass = 10f;
            rb.drag = 3f;
            rb.angularDrag = 5f;
            rb.useGravity = true;
            rb.isKinematic = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation; // 防止敌人翻倒
        }
        else if (rb != null && useRigidbody)
        {
            // 确保已有刚体设置正确
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
        
        // 检查和设置碰撞体
        capsuleCollider = GetComponent<CapsuleCollider>();
        if (capsuleCollider == null)
        {
            capsuleCollider = gameObject.AddComponent<CapsuleCollider>();
            capsuleCollider.center = new Vector3(0, 1f, 0);
            capsuleCollider.radius = 0.5f;
            capsuleCollider.height = 2f;
            capsuleCollider.material = CreatePhysicsMaterial();
        }
        else
        {
            // 确保碰撞体设置正确
            if (capsuleCollider.material == null)
            {
                capsuleCollider.material = CreatePhysicsMaterial();
            }
        }
        
        // 确保在正确的层
        gameObject.layer = LayerMask.NameToLayer("Enemy");
        
        // 设置地形层遮罩
        terrainLayerMask = LayerMask.GetMask("Default");
    }
    
    // 创建物理材质
    private PhysicMaterial CreatePhysicsMaterial()
    {
        PhysicMaterial material = new PhysicMaterial("EnemyPhysicsMaterial");
        material.dynamicFriction = 0.6f;
        material.staticFriction = 0.6f;
        material.bounciness = 0.1f;
        material.frictionCombine = PhysicMaterialCombine.Average;
        material.bounceCombine = PhysicMaterialCombine.Average;
        return material;
    }

    // 新增：连续碰撞检测协程
    IEnumerator ContinuousCollisionDetection()
    {
        while (true)
        {
            // 使用球形投射检测周围物体
            Collider[] hitColliders = Physics.OverlapSphere(transform.position, collisionCheckRadius, terrainLayerMask);
            
            foreach (var hitCollider in hitColliders)
            {
                // 排除自身和其他敌人
                if (hitCollider.gameObject == gameObject || hitCollider.CompareTag("Enemy") || hitCollider.CompareTag("Player"))
                    continue;
                
                // 检查是否是地形
                TerrainIdentifier terrain = hitCollider.GetComponent<TerrainIdentifier>();
                if (terrain != null)
                {
                    // 计算从地形到敌人的方向
                    Vector3 direction = transform.position - hitCollider.ClosestPoint(transform.position);
                    direction.y = 0; // 保持水平方向
                    
                    // 如果方向为零，给一个随机方向
                    if (direction.magnitude < 0.1f)
                    {
                        direction = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                    }
                    
                    // 应用推力远离地形
                    ApplyPushForce(direction.normalized * pushForce);
                    
                    // 暂时停止导航代理
                    StopAgentTemporarily(0.5f);
                    
                    Debug.Log($"敌人与地形 {terrain.terrainType} 发生碰撞，应用推力");
                }
            }
            
            yield return new WaitForSeconds(0.1f); // 每0.1秒检测一次
        }
    }
    
    // 应用推力
    private void ApplyPushForce(Vector3 force)
    {
        // 使用刚体施加力
        if (rb != null && !rb.isKinematic)
        {
            rb.AddForce(force, ForceMode.Impulse);
        }
        else
        {
            // 如果没有刚体，直接移动位置
            transform.position += force * Time.deltaTime;
        }
    }
    
    // 临时停止导航代理
    private IEnumerator StopAgentTemporarily(float duration)
    {
        if (IsAgentValid())
        {
            // 保存当前目标
            Vector3 currentTarget = agent.destination;
            
            // 停止代理
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
            
            // 等待指定时间
            yield return new WaitForSeconds(duration);
            
            // 重新启用代理并恢复路径
            if (IsAgentValid())
            {
                agent.isStopped = false;
                agent.SetDestination(currentTarget);
            }
        }
    }
    
    // 增强版碰撞响应方法
    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }
    
    private void HandleCollision(Collision collision)
    {
        // 忽略与玩家和其他敌人的碰撞
        if (collision.gameObject.CompareTag("Player") || collision.gameObject.CompareTag("Enemy"))
            return;
        
        // 检查是否与地形碰撞
        TerrainIdentifier terrain = collision.gameObject.GetComponent<TerrainIdentifier>();
        if (terrain != null)
        {
            // 计算碰撞点的平均法线
            Vector3 avgNormal = Vector3.zero;
            int contactCount = collision.contactCount;
            
            for (int i = 0; i < contactCount; i++)
            {
                avgNormal += collision.GetContact(i).normal;
            }
            
            if (contactCount > 0)
            {
                avgNormal /= contactCount;
                float angle = Vector3.Angle(avgNormal, Vector3.up);
                
                // 检查是否在陡峭表面上
                const float maxAllowedSlope = 45f;
                if (angle > maxAllowedSlope)
                {
                    // 获取碰撞点
                    ContactPoint contact = collision.GetContact(0);
                    Vector3 point = contact.point;
                    
                    // 计算推力方向（沿着法线但保持水平方向）
                    Vector3 pushDirection = avgNormal;
                    pushDirection.y = 0;
                    pushDirection.Normalize();
                    
                    // 如果方向为零，给一个基于当前位置和碰撞点的方向
                    if (pushDirection.magnitude < 0.1f)
                    {
                        pushDirection = transform.position - point;
                        pushDirection.y = 0;
                        pushDirection.Normalize();
                    }
                    
                    // 应用推力
                    ApplyPushForce(pushDirection * pushForce);
                    
                    // 重新计算路径
                    StartCoroutine(RecalculatePath());
                    
                    Debug.Log($"敌人与陡峭地形碰撞，角度: {angle}，应用推力");
                }
            }
        }
        else
        {
            // 处理与其他物体的碰撞
            // 这里可以添加针对特定物体类型的处理逻辑
        }
    }
    
    // 重新计算路径
    private IEnumerator RecalculatePath()
    {
        // 等待短暂时间确保敌人已被推开
        yield return new WaitForSeconds(0.3f);
        
        if (IsAgentValid() && agent.destination != Vector3.zero)
        {
            // 保存当前目标
            Vector3 currentDestination = agent.destination;
            
            // 重置并重新计算路径
            agent.ResetPath();
            yield return new WaitForSeconds(0.1f);
            
            // 尝试找到通向目标的新路径
            NavMeshPath path = new NavMeshPath();
            if (agent.CalculatePath(currentDestination, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetDestination(currentDestination);
                }
                else
                {
                    // 如果无法完成路径，寻找附近替代点
                    NavMeshHit hit;
                    if (NavMesh.SamplePosition(transform.position + (currentDestination - transform.position).normalized * 5f,
                        out hit, 5f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(hit.position);
                    }
                    else
                    {
                        // 如果找不到合适的点，设置游荡状态
                        TransitionToState(EnemyState.Wandering);
                    }
                }
            }
        }
    }

    // 确保Agent在有效的NavMesh上
    private void EnsureAgentOnNavMesh()
    {
        if (agent == null) return;
        
        // 检查当前位置是否在NavMesh上
        NavMeshHit hit;
        if (!NavMesh.SamplePosition(transform.position, out hit, 5f, NavMesh.AllAreas))
        {
            // 如果不在NavMesh上，寻找最近的NavMesh点
            if (NavMesh.SamplePosition(transform.position, out hit, 50f, NavMesh.AllAreas))
            {
                // 将物体移动到最近的NavMesh点
                transform.position = hit.position;
                Debug.Log($"已将敌人移动到最近的NavMesh点: {hit.position}");
            }
            else
            {
                Debug.LogWarning($"警告: 敌人 {gameObject.name} 不在NavMesh上且无法找到附近的NavMesh!");
            }
        }
        
        // 确保NavMesh代理已准备就绪
        if (agent != null)
        {
            agent.Warp(transform.position);
        }
    }
    
    // 检查NavMeshAgent是否有效
    private bool IsAgentValid()
    {
        return agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh;
    }

    public void InitializeEnemy()
    {
        // 在EnemySpawner中调用，确保所有参数设置正确
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }

        // 设置基本导航参数
        agent.speed = walkSpeed;
        agent.stoppingDistance = attackRange * 0.8f;
        
        // 设置避障参数
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 0f;
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        agent.avoidancePriority = 50;
        agent.autoTraverseOffMeshLink = true;
        
        // 新增：设置进阶导航参数
        agent.acceleration = 12f; // 加速度
        agent.angularSpeed = 360f; // 转向速度
        agent.areaMask = NavMesh.AllAreas; // 可行走区域
        
        // 新增：初始化物理组件
        InitializePhysicsComponents();
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

    IEnumerator DecreaseHunger()
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
        if (!IsAgentValid())
        {
            EnsureAgentOnNavMesh();
            return;
        }
        
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

            // 检测尸体
            DeadEnemy deadEnemy = col.GetComponent<DeadEnemy>();
            if (deadEnemy != null && IsHungry())
            {
                // 当饥饿时，发现尸体会去进食
                currentEatingTarget = col.transform;
                TransitionToState(EnemyState.Hunting); // 先进入追击状态去接近尸体
                Debug.Log($"🔍 敌人Lv{level}发现了尸体，前去进食");
                return;
            }

            // 其他敌人的处理（以后可以扩展）
        }
    }

    void HandleAlertState()
    {
        // 警觉状态 - 停止移动，观察周围
        if (IsAgentValid())
        {
            agent.isStopped = true;
        }

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
        if (target == null && currentEatingTarget == null)
        {
            TransitionToState(EnemyState.Wandering);
            return;
        }

        // 确保代理有效
        if (!IsAgentValid())
        {
            EnsureAgentOnNavMesh();
            return;
        }
        
        // 更新速度为奔跑速度
        agent.isStopped = false;
        agent.speed = runSpeed;

        // 如果正在追击尸体
        if (currentEatingTarget != null)
        {
            // 追击尸体目标
            agent.SetDestination(currentEatingTarget.position);

            // 如果接近尸体，开始进食
            float distanceToTarget = Vector3.Distance(transform.position, currentEatingTarget.position);
            if (distanceToTarget <= attackRange)
            {
                TransitionToState(EnemyState.Eating);
            }

            // 如果尸体太远或不存在了，重新进入游荡状态
            if (distanceToTarget > visionRange * 1.5f || currentEatingTarget == null)
            {
                currentEatingTarget = null;
                TransitionToState(EnemyState.Wandering);
            }
            return;
        }

        // 追击活体目标的逻辑
        if (target != null)
        {
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

        if (!IsAgentValid())
        {
            EnsureAgentOnNavMesh();
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
        if (IsAgentValid())
        {
            agent.isStopped = true;
        }

        // 如果是在吃尸体
        if (currentEatingTarget != null)
        {
            DeadEnemy deadEnemy = currentEatingTarget.GetComponent<DeadEnemy>();
            if (deadEnemy != null)
            {
                // 检查是否还在尸体附近
                if (Vector3.Distance(transform.position, currentEatingTarget.position) <= attackRange * 1.2f)
                {
                    // 增加饥饿值 - 吃尸体回复更多
                    currentHunger = Mathf.Min(100f, currentHunger + 8f); // 每秒恢复8点饥饿值
                    
                    // 食用持续一段时间后消费掉尸体
                    if (Time.time - lastStateChangeTime > eatingDuration)
                    {
                        deadEnemy.EnemyConsumeDead(gameObject);
                        currentEatingTarget = null;
                        TransitionToState(EnemyState.Wandering);
                    }
                    return;
                }
                else
                {
                    // 如果已经不在尸体附近，重新追踪
                    TransitionToState(EnemyState.Hunting);
                    return;
                }
            }
            else
            {
                // 如果尸体已经消失
                currentEatingTarget = null;
            }
        }

        // 常规进食逻辑（不是吃尸体的情况）
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
        if (!IsAgentValid())
        {
            EnsureAgentOnNavMesh();
            return;
        }
        
        // 获取随机方向
        Vector3 randomDirection = Random.insideUnitSphere * wanderRadius;
        randomDirection += startPosition.position;
        
        // 使用射线检测确保目标点是有效的
        RaycastHit hit;
        if (Physics.Raycast(randomDirection + Vector3.up * 10f, Vector3.down, out hit, 20f))
        {
            // 检查斜率是否可行
            float angle = Vector3.Angle(hit.normal, Vector3.up);
            if (angle <= 45f) // 使用固定值45度代替agent.slopeLimit
            {
                randomDirection = hit.point;
            }
        }
        
        // 确保目标点在NavMesh上
        NavMeshHit navHit;
        if (NavMesh.SamplePosition(randomDirection, out navHit, wanderRadius, NavMesh.AllAreas))
        {
            wanderDestination = navHit.position;
            
            // 检查路径是否可达
            NavMeshPath path = new NavMeshPath();
            if (agent.CalculatePath(wanderDestination, path))
            {
                if (path.status == NavMeshPathStatus.PathComplete)
                {
                    agent.SetDestination(wanderDestination);
                }
                else
                {
                    // 如果路径不完整，尝试找到更近的可达点
                    Vector3 closestPoint = wanderDestination;
                    if (NavMesh.SamplePosition(transform.position + (wanderDestination - transform.position).normalized * 5f,
                        out navHit, 5f, NavMesh.AllAreas))
                    {
                        agent.SetDestination(navHit.position);
                    }
                }
            }
        }
        else
        {
            // 如果找不到有效的导航点，尝试在更小的范围内寻找
            if (NavMesh.SamplePosition(transform.position + Random.insideUnitSphere * 5f,
                out navHit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
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

        // 确保代理有效
        bool agentValid = IsAgentValid();

        // 进入新状态的逻辑
        switch (newState)
        {
            case EnemyState.Idle:
                if (agentValid) agent.isStopped = true;
                break;

            case EnemyState.Wandering:
                if (agentValid)
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                    SetNewWanderDestination();
                }
                break;

            case EnemyState.Alert:
                if (agentValid) agent.isStopped = true;
                break;

            case EnemyState.Hunting:
                if (agentValid)
                {
                    agent.isStopped = false;
                    agent.speed = runSpeed;
                    if (target != null)
                    {
                        agent.SetDestination(target.position);
                    }
                }
                break;

            case EnemyState.Attacking:
                if (agentValid) agent.isStopped = true;
                break;

            case EnemyState.Fleeing:
                if (agentValid)
                {
                    agent.isStopped = false;
                    agent.speed = runSpeed;
                }
                break;

            case EnemyState.Eating:
                if (agentValid) agent.isStopped = true;
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

    // 新增：判断是否饥饿的工具方法
    private bool IsHungry()
    {
        HungerStatus status = GetHungerStatus();
        return status == HungerStatus.Hungry || status == HungerStatus.Starving;
    }

    // 新增：碰撞检测协程
    IEnumerator ObstacleDetection()
    {
        while (true)
        {
            // 检测前方是否有障碍物
            Vector3 forward = transform.forward;
            RaycastHit hit;
            if (Physics.Raycast(transform.position + Vector3.up, forward, out hit, 2f))
            {
                // 如果检测到障碍物且它不是其他敌人或玩家
                if (!hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Player"))
                {
                    // 计算爬坡角度
                    float angle = Vector3.Angle(hit.normal, Vector3.up);
                    
                    // 如果角度大于可处理的角度，调整路径
                    const float maxAllowedSlope = 45f; // 使用固定值45度代替agent.slopeLimit
                    if (angle > maxAllowedSlope)
                    {
                        // 尝试寻找新路径
                        Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
                        
                        // 随机选择左或右避让
                        if (Random.value > 0.5f) avoidDirection = -avoidDirection;
                        
                        // 找寻避让点
                        Vector3 avoidPoint = transform.position + avoidDirection * 5f;
                        
                        NavMeshHit navHit;
                        if (NavMesh.SamplePosition(avoidPoint, out navHit, 5f, NavMesh.AllAreas))
                        {
                            // 设置新的路径点
                            agent.SetDestination(navHit.position);
                            Debug.Log($"敌人检测到不可爬行的障碍物，选择避让。角度: {angle}");
                        }
                    }
                    else
                    {
                        // 角度可接受，可以爬行
                        Debug.Log($"敌人检测到可爬行的坡度。角度: {angle}");
                    }
                }
            }
            
            yield return new WaitForSeconds(0.5f);
        }
    }

    // 新增：被尸体调用的方法，表示已经消费掉尸体
    public void ConsumeDeadEnemy()
    {
        try
        {
            Debug.Log($"ConsumeDeadEnemy: 敌人Lv{level}开始处理尸体消费");
            
            // 确保游戏对象和组件都有效
            if (this == null || !gameObject.activeInHierarchy)
            {
                Debug.LogError("ConsumeDeadEnemy: 敌人对象无效或未激活");
                return;
            }
            
            // 大量恢复饥饿度
            float oldHunger = currentHunger;
            currentHunger = Mathf.Min(100f, currentHunger + 30f);
            currentEatingTarget = null;
            
            Debug.Log($"👅 敌人Lv{level}完成进食，饥饿度从{oldHunger:F1}恢复到{currentHunger:F1}");
            
            // 转入游荡状态
            TransitionToState(EnemyState.Wandering);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"敌人食用尸体出错: {e.Message}\n{e.StackTrace}");
        }
    }

    // 根据等级调整敌人属性
    private void AdjustStatsByLevel()
    {
        // 基础属性根据等级调整
        walkSpeed = 2f + (level - 1) * 0.2f;
        runSpeed = 5f + (level - 1) * 0.5f;
        visionRange = 10f + (level - 1) * 1f;
        attackRange = 2f + (level - 1) * 0.1f;
        attackDamage = 10 + (level - 1) * 5;
        
        // 高级敌人更强壮
        if (level >= 3)
        {
            attackCooldown = Mathf.Max(0.5f, attackCooldown - (level - 2) * 0.1f); // 更快的攻击速度
        }
        
        // 调整避障参数
        if (agent != null)
        {
            agent.speed = walkSpeed;
            agent.stoppingDistance = attackRange * 0.8f;
        }
        
        // 根据等级设置物理碰撞参数
        collisionCheckRadius = 1.0f + (level - 1) * 0.2f;
        pushForce = 5.0f + (level - 1) * 1.0f;
        
        Debug.Log($"敌人Lv{level}属性已调整 - 走速:{walkSpeed:F1} 跑速:{runSpeed:F1} 视野:{visionRange:F1} 攻击力:{attackDamage}");
    }

    // 监控卡住状态的协程
    IEnumerator MonitorStuckStatus()
    {
        Vector3 lastRecordedPosition = transform.position;
        float stuckCheckTime = 2.0f;
        int stuckCounter = 0;
        
        while (true)
        {
            yield return new WaitForSeconds(stuckCheckTime);
            
            if (IsAgentValid() && !agent.isStopped && agent.hasPath)
            {
                // 计算移动距离
                float movedDistance = Vector3.Distance(transform.position, lastRecordedPosition);
                
                // 如果几乎没有移动且有有效路径，可能卡住了
                if (movedDistance < 0.1f)
                {
                    stuckCounter++;
                    
                    // 连续多次检测到卡住
                    if (stuckCounter >= 3)
                    {
                        Debug.Log($"敌人Lv{level}连续{stuckCounter}次检测到无法移动，尝试解除卡住状态");
                        
                        // 尝试解除卡住
                        UnstuckAgent();
                        stuckCounter = 0;
                    }
                }
                else
                {
                    // 如果能正常移动，重置计数器
                    stuckCounter = 0;
                }
            }
            else
            {
                // 敌人不在主动移动，重置计数器
                stuckCounter = 0;
            }
            
            // 更新上次位置
            lastRecordedPosition = transform.position;
        }
    }
}