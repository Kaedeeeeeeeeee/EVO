using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using System;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAIExtended : MonoBehaviour
{
    // 敌人属性 - 这些字段现在通过EnemySpawner从CSV数据中设置
    [HideInInspector] public int level;                    // 敌人等级
    [HideInInspector] public float walkSpeed;             // 行走速度
    [HideInInspector] public float runSpeed;              // 奔跑速度
    [HideInInspector] public float visionRange;          // 视野范围
    [HideInInspector] public float attackRange;           // 攻击范围
    [HideInInspector] public int attackDamage;            // 攻击伤害
    [HideInInspector] public float attackCooldown;        // 攻击冷却时间
    [HideInInspector] public float huntThreshold;        // 猎杀阈值（饥饿百分比）

    // 行为参数
    [HideInInspector] public float wanderRadius;         // 游荡半径
    [HideInInspector] public float wanderInterval;        // 游荡间隔时间
    [HideInInspector] public float alertDuration;         // 警觉持续时间
    [HideInInspector] public float eatingDuration;        // 进食持续时间
    [HideInInspector] public float hungerDecreaseRate;  // 饥饿值降低速率（每秒）
    
    // 体力系统参数
    [Header("体力系统")]
    [HideInInspector] public float maxStamina;           // 最大体力值
    [HideInInspector] public float currentStamina;              // 当前体力值
    [HideInInspector] public float staminaDecreaseRate;    // 每0.1秒减少的体力值
    [HideInInspector] public float staminaRecoveryRate;    // 每0.1秒恢复的体力值
    [HideInInspector] public float staminaRecoveryDelay; // 恢复体力前的延迟时间（秒）
    private float lastRunTime = 0f;           // 上次奔跑时间
    [HideInInspector] public bool canRun = true;                // 是否可以奔跑
    [HideInInspector] public float restTime;              // 休息时间（秒）
    [HideInInspector] public bool isResting = false;           // 是否正在休息
    
    // 新增：物理碰撞参数
    [HideInInspector] public float collisionCheckRadius;  // 碰撞检测半径
    [HideInInspector] public float pushForce;            // 推力大小
    [HideInInspector] public LayerMask terrainLayerMask;        // 地形层遮罩
    [HideInInspector] public bool useRigidbody = true;          // 是否使用刚体进行物理交互

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
    [HideInInspector] public bool showVisionRange = true;
    [HideInInspector] public bool showAttackRange = true;

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
        
        // 记录当前位置以供调试
        Vector3 oldPosition = transform.position;
        
        // 停止当前导航
        agent.ResetPath();
        agent.velocity = Vector3.zero;
        agent.isStopped = true;
        
        // 等待一帧后重新启动导航
        StartCoroutine(DelayedNavigationRestart());
        
        // 如果当前在追击或逃跑状态但卡住了，切换到游荡状态
        if (currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing)
        {
            Debug.Log($"敌人Lv{level}在{currentState}状态下卡住，切换到游荡状态");
            TransitionToState(EnemyState.Wandering);
        }
        
        // 确保NavMesh代理设置正确
        EnsureAgentOnNavMesh();
    }
    
    // 添加延迟重启导航的协程
    private IEnumerator DelayedNavigationRestart()
    {
        // 等待一帧
        yield return null;
        
        if (!IsAgentValid()) yield break;
        
        // 重新启动导航代理
        agent.isStopped = false;
        
        // 找一个新的随机目标点
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere;
        randomDirection.y = 0;
        randomDirection.Normalize();
        
        Vector3 targetPosition = transform.position + randomDirection * 8f;
        NavMeshHit hit;
        
        // 寻找可达的导航点
        if (NavMesh.SamplePosition(targetPosition, out hit, 8f, NavMesh.AllAreas))
        {
            agent.SetDestination(hit.position);
            Debug.Log($"敌人Lv{level}卡住后找到新目标点，距离:{Vector3.Distance(transform.position, hit.position):F1}m");
        }
        else
        {
            // 如果找不到远处的点，尝试找一个近处的点
            if (NavMesh.SamplePosition(transform.position + randomDirection * 3f, out hit, 3f, NavMesh.AllAreas))
            {
                agent.SetDestination(hit.position);
                Debug.Log($"敌人Lv{level}卡住后找到附近目标点，距离:{Vector3.Distance(transform.position, hit.position):F1}m");
            }
            else
            {
                Debug.LogWarning($"敌人Lv{level}无法找到有效的导航点，可能严重卡住");
            }
        }
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
        
        // 确保level至少为1
        if (level <= 0) level = 1;
        
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
        currentHunger = UnityEngine.Random.Range(60f, 100f);

        // 初始状态为游荡
        TransitionToState(EnemyState.Wandering);
    }

    // 新增：初始化物理组件
    private void InitializePhysicsComponents()
    {
        // 检查是否存在CharacterController并移除，因为它会与Rigidbody冲突
        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            Debug.LogWarning($"移除与Rigidbody冲突的CharacterController组件");
            Destroy(characterController);
        }
        
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
        if (terrainLayerMask.value == 0)
        {
            terrainLayerMask = LayerMask.GetMask("Default", "Terrain", "Ground");
        }
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
                        direction = new Vector3(UnityEngine.Random.Range(-1f, 1f), 0, UnityEngine.Random.Range(-1f, 1f)).normalized;
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

        // 确保属性有默认值，防止未设置时出错
        if (walkSpeed <= 0) walkSpeed = 2f;
        if (runSpeed <= 0) runSpeed = 5f;
        if (visionRange <= 0) visionRange = 10f;
        if (attackRange <= 0) attackRange = 2f;
        if (attackDamage <= 0) attackDamage = 10;
        if (attackCooldown <= 0) attackCooldown = 1f;
        if (huntThreshold <= 0) huntThreshold = 30f;
        if (wanderRadius <= 0) wanderRadius = 10f;
        if (wanderInterval <= 0) wanderInterval = 5f;
        if (alertDuration <= 0) alertDuration = 3f;
        if (eatingDuration <= 0) eatingDuration = 5f;
        if (hungerDecreaseRate <= 0) hungerDecreaseRate = 0.1f;
        if (maxStamina <= 0) maxStamina = 100f;
        if (staminaDecreaseRate <= 0) staminaDecreaseRate = 1f;
        if (staminaRecoveryRate <= 0) staminaRecoveryRate = 2f;
        if (staminaRecoveryDelay <= 0) staminaRecoveryDelay = 1f;
        if (restTime <= 0) restTime = 5f;
        if (collisionCheckRadius <= 0) collisionCheckRadius = 1f;
        if (pushForce <= 0) pushForce = 5f;

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
        
        // 设置进阶导航参数
        agent.acceleration = 12f; // 加速度
        agent.angularSpeed = 360f; // 转向速度
        agent.areaMask = NavMesh.AllAreas; // 可行走区域
        
        // 初始化体力系统
        currentStamina = maxStamina;
        canRun = true;
        lastRunTime = 0f;
        
        // 记录初始化信息
        Debug.Log($"🔋 敌人Lv{level}初始化体力系统：最大体力={maxStamina}，消耗速率={staminaDecreaseRate}/0.1s，恢复速率={staminaRecoveryRate}/0.1s，恢复延迟={staminaRecoveryDelay}s");
        
        // 初始化物理组件
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

            // 首先监控体力状态 - 确保这在任何状态处理之前
            MonitorStamina();
            
            // 强制应用体力系统的速度限制 - 这很重要，确保每一帧都考虑体力状态
            SetSpeedBasedOnStamina();

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
                    
                case EnemyState.Resting:
                    HandleRestingState();
                    break;
            }
            
            // 再次确保速度设置正确 - 因为各种状态处理方法可能会修改速度
            SetSpeedBasedOnStamina();
            
            // 增加：每5秒输出一次当前状态汇总
            if (debugStamina && Time.frameCount % 300 == 0)
            {
                // 计算体力百分比
                float staminaPercentage = (maxStamina > 0) ? (currentStamina / maxStamina) : 0;
                
                Debug.Log($"📊 [{Time.frameCount}] 敌人Lv{level}状态汇总 - " +
                          $"状态:{currentState}, " +
                          $"体力:{currentStamina:F1}/{maxStamina} ({staminaPercentage*100:F0}%), " +
                          $"休息:{isResting}, " +
                          $"设定速度:{agent.speed:F1}, " +
                          $"实际速度:{agent.velocity.magnitude:F1}");
            }
            
            // 比之前更频繁地更新
            yield return new WaitForSeconds(0.1f);
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

        // 添加饥饿状态
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
        
        // 添加体力状态
        float staminaPercentage = (currentStamina / maxStamina) * 100f;
        if (!canRun) 
        {
            statusText += $" [恢复中:{staminaPercentage:F0}%]";
        }
        else if (staminaPercentage <= 20)
        {
            statusText += $" [体力低:{staminaPercentage:F0}%]";
        }
        else if (currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing)
        {
            // 在奔跑状态下显示体力
            statusText += $" [体力:{staminaPercentage:F0}%]";
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
        
        // 根据体力状态设置速度 - 移除原有的速度设置代码，改为调用统一方法
        agent.isStopped = false;
        SetSpeedBasedOnStamina();

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
        
        // 根据体力状态设置速度 - 移除原有的速度设置代码，改为调用统一方法
        agent.isStopped = false;
        SetSpeedBasedOnStamina();

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

    void HandleRestingState()
    {
        // 现在Resting状态只是作为一个过渡状态，
        // 实际的体力管理都由MonitorStamina完成
        if (IsAgentValid())
        {
            agent.isStopped = false;
            
            // 游荡行为
            if (agent.remainingDistance < 0.5f || !agent.hasPath)
            {
                SetNewWanderDestination();
            }
            
            // 如果体力已经恢复到100%，切换回游荡状态
            if (currentStamina >= maxStamina)
            {
                TransitionToState(EnemyState.Wandering);
            }
        }
        
        // 记录休息状态
        if (debugStamina && Time.frameCount % 120 == 0)
        {
            float staminaPercentage = (currentStamina / maxStamina) * 100f;
            Debug.Log($"💤 [{Time.frameCount}] 敌人Lv{level}体力恢复中：体力={staminaPercentage:F0}%，速度={agent.speed:F1}");
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
        Vector3 randomDirection = UnityEngine.Random.insideUnitSphere * wanderRadius;
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
            if (NavMesh.SamplePosition(transform.position + UnityEngine.Random.insideUnitSphere * 5f,
                out navHit, 5f, NavMesh.AllAreas))
            {
                agent.SetDestination(navHit.position);
            }
        }
    }

    void TransitionToState(EnemyState newState)
    {
        // 如果当前状态和新状态相同，不做处理
        if (currentState == newState) return;
        
        // 记录上一个状态
        EnemyState oldState = currentState;
        
        // 退出当前状态的逻辑
        switch (currentState)
        {
            case EnemyState.Attacking:
                // 重置攻击相关变量
                break;

            case EnemyState.Hunting:
            case EnemyState.Fleeing:
                // 从奔跑状态退出
                Debug.Log($"🔄 敌人Lv{level}退出奔跑状态，当前体力: {currentStamina:F1}/{maxStamina} ({currentStamina/maxStamina:F0}%)");
                break;
                
            case EnemyState.Resting:
                // 如果体力仍然不足，禁止退出休息状态进入奔跑状态
                if ((newState == EnemyState.Hunting || newState == EnemyState.Fleeing) && 
                    currentStamina < maxStamina)
                {
                    Debug.Log($"⛔ 敌人Lv{level}体力不足({currentStamina/maxStamina:F0}%)，无法从休息状态切换到{newState}");
                    return;
                }
                
                // 如果从休息状态切换出去，重置休息状态标志
                isResting = false;
                Debug.Log($"♻️ 敌人Lv{level}退出休息状态，体力: {currentStamina:F1}/{maxStamina} ({currentStamina/maxStamina:F0}%)");
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
                    // 根据体力状态决定速度
                    if (isResting || currentStamina < maxStamina * 0.2f)
                    {
                        agent.speed = walkSpeed;
                        Debug.Log($"⚠️ 敌人Lv{level}体力不足({currentStamina/maxStamina:F0}%)，以步行速度进入追击状态");
                    }
                    else
                    {
                        agent.speed = runSpeed;
                        Debug.Log($"🏃 敌人Lv{level}开始奔跑 (Hunting)，体力: {currentStamina:F1}/{maxStamina} ({currentStamina/maxStamina:F0}%)");
                    }
                    
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
                    // 根据体力状态决定速度
                    if (isResting || currentStamina < maxStamina * 0.2f)
                    {
                        agent.speed = walkSpeed;
                        Debug.Log($"⚠️ 敌人Lv{level}体力不足({currentStamina/maxStamina:F0}%)，以步行速度进入逃跑状态");
                    }
                    else
                    {
                        agent.speed = runSpeed;
                        Debug.Log($"🏃 敌人Lv{level}开始奔跑 (Fleeing)，体力: {currentStamina:F1}/{maxStamina} ({currentStamina/maxStamina:F0}%)");
                    }
                }
                break;

            case EnemyState.Eating:
                if (agentValid) agent.isStopped = true;
                break;
                
            case EnemyState.Resting:
                if (agentValid)
                {
                    agent.isStopped = false;
                    agent.speed = walkSpeed;
                }
                isResting = true;
                Debug.Log($"😴 敌人Lv{level}体力不足({currentStamina/maxStamina:F0}%)，进入休息状态");
                break;
        }

        // 更新显示名称
        UpdateNameWithStatus();

        Debug.Log($"🔄 敌人Lv{level} 从 {oldState} 切换到 {newState} 状态，体力: {currentStamina:F1}/{maxStamina} ({currentStamina/maxStamina:F0}%)");
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
        float lastObstacleAvoidTime = 0f;
        float obstacleAvoidCooldown = 2.0f; // 避让冷却时间
        
        while (true)
        {
            // 在冷却期间不执行避让
            if (Time.time - lastObstacleAvoidTime < obstacleAvoidCooldown)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }
            
            // 只在主动移动状态下检测障碍物
            if (IsAgentValid() && !agent.isStopped && agent.hasPath)
            {
                // 检测前方是否有障碍物
                Vector3 forward = transform.forward;
                RaycastHit hit;
                // 增加检测距离，提前发现障碍物
                if (Physics.Raycast(transform.position + Vector3.up, forward, out hit, 3f))
                {
                    // 如果检测到障碍物且它不是其他敌人或玩家
                    if (!hit.collider.CompareTag("Enemy") && !hit.collider.CompareTag("Player"))
                    {
                        // 计算爬坡角度
                        float angle = Vector3.Angle(hit.normal, Vector3.up);
                        
                        // 如果角度大于可处理的角度，调整路径
                        float maxAllowedSlope = 45f; // 固定最大坡度
                        if (angle > maxAllowedSlope)
                        {
                            lastObstacleAvoidTime = Time.time; // 记录避让时间
                            
                            // 尝试寻找新路径（优先考虑周围的NavMesh点）
                            NavMeshHit navHit;
                            if (NavMesh.FindClosestEdge(transform.position, out navHit, NavMesh.AllAreas))
                            {
                                // 找到最近的NavMesh边缘点
                                if (navHit.distance < 2f)
                                {
                                    agent.SetDestination(navHit.position + (navHit.position - transform.position).normalized * 5f);
                                    Debug.Log($"敌人检测到不可爬行的障碍物，寻找NavMesh边缘避让。角度: {angle:F2}");
                                    yield return new WaitForSeconds(1.0f); // 给予足够时间执行避让
                                    continue;
                                }
                            }
                            
                            // 如果无法找到NavMesh边缘，尝试随机避让
                            Vector3 avoidDirection = Vector3.Cross(hit.normal, Vector3.up).normalized;
                            
                            // 随机选择左或右避让
                            if (UnityEngine.Random.value > 0.5f) avoidDirection = -avoidDirection;
                            
                            // 找寻避让点
                            Vector3 avoidPoint = transform.position + avoidDirection * 5f;
                            
                            // 确保避让点在NavMesh上
                            if (NavMesh.SamplePosition(avoidPoint, out navHit, 5f, NavMesh.AllAreas))
                            {
                                // 记录当前目标和路径，以便在避让后恢复
                                Vector3 currentDestination = agent.destination;
                                
                                // 设置新的避让路径
                                agent.SetDestination(navHit.position);
                                Debug.Log($"敌人检测到不可爬行的障碍物，选择避让。角度: {angle:F2}");
                                
                                // 等待足够时间让敌人有机会避让
                                yield return new WaitForSeconds(1.0f);
                                
                                // 如果敌人处于追击或逃跑状态，尝试恢复原来的目标
                                if ((currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing) && 
                                    target != null)
                                {
                                    agent.SetDestination(target.position);
                                }
                                else if (Vector3.Distance(currentDestination, transform.position) > 5f)
                                {
                                    // 其他状态下，如果原目标还很远，尝试恢复
                                    agent.SetDestination(currentDestination);
                                }
                            }
                        }
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
        // 确保level至少为1
        if (level <= 0) level = 1;
        
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
        else
        {
            attackCooldown = 1.0f; // 默认值
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

    // 体力监控方法
    private void MonitorStamina()
    {
        // 记录当前帧
        int currentFrame = Time.frameCount;
        
        // 计算体力百分比
        float staminaPercentage = currentStamina / maxStamina * 100f;
        
        // 判断是否在使用奔跑速度
        bool isUsingRunSpeed = IsUsingRunSpeed();
        bool isRunningState = (currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing);
        
        // 调试输出当前状态 - 降低频率避免刷屏
        if (debugStamina && currentFrame % 300 == 0)
        {
            Debug.Log($"🔍 [{currentFrame}] 敌人Lv{level}状态: {currentState}, 体力: {currentStamina:F1}/{maxStamina}({staminaPercentage:F0}%), " +
                      $"可奔跑: {canRun}, 速度: {agent.speed:F1}, 实际速度: {(IsAgentValid() ? agent.velocity.magnitude : 0):F1}");
        }
        
        // 处理体力变化 
        // 1. 在奔跑状态且可以奔跑 - 消耗体力
        if (isRunningState && canRun && currentStamina > 0)
        {
            // 确保速度正确设置
            if (!isUsingRunSpeed && IsAgentValid())
            {
                agent.speed = runSpeed;
                Debug.Log($"🏃 [{currentFrame}] 敌人Lv{level}进入奔跑状态，设置奔跑速度: {runSpeed:F1}");
            }
            
            // 消耗体力 - 只有当实际速度超过步行速度时才消耗
            if (IsAgentValid() && agent.velocity.magnitude > walkSpeed * 1.2f)
            {
                float decreaseAmount = staminaDecreaseRate;
                currentStamina = Mathf.Max(0, currentStamina - decreaseAmount);
                lastRunTime = Time.time;
                
                // 调试信息 - 降低频率
                if (debugStamina && currentFrame % 60 == 0)
                {
                    Debug.Log($"🏃 [{currentFrame}] 敌人Lv{level}奔跑消耗体力: {currentStamina:F1}/{maxStamina}({staminaPercentage:F0}%), 减少:{decreaseAmount:F1}点");
                }
                
                // 检查体力是否耗尽
                if (currentStamina <= 0)
                {
                    canRun = false;
                    // 强制设置速度为步行
                    if (IsAgentValid()) agent.speed = walkSpeed;
                    Debug.Log($"⚠️ [{currentFrame}] 敌人Lv{level}体力耗尽，强制步行，速度设为{walkSpeed:F1}");
                }
            }
        }
        // 2. 不在奔跑或无法奔跑 - 可能恢复体力
        else
        {
            // 确保速度正确设置为步行 - 这是安全措施
            if (isUsingRunSpeed && IsAgentValid())
            {
                agent.speed = walkSpeed;
                Debug.Log($"🚶 [{currentFrame}] 敌人Lv{level}体力管理将速度从{runSpeed:F1}降至{walkSpeed:F1}");
            }
            
            // 检查是否满足恢复条件 - 非奔跑状态持续指定时间
            if (Time.time - lastRunTime >= staminaRecoveryDelay)
            {
                // 恢复体力 - 每0.1秒恢复staminaRecoveryRate点
                float recoveryAmount = staminaRecoveryRate;
                currentStamina = Mathf.Min(maxStamina, currentStamina + recoveryAmount);
                
                // 如果体力恢复到最大值且之前不能奔跑，现在允许奔跑
                if (currentStamina >= maxStamina && !canRun)
                {
                    canRun = true;
                    Debug.Log($"✅ [{currentFrame}] 敌人Lv{level}体力恢复至100%，可以再次奔跑");
                }
                // 调试信息 - 只在体力显著变化时记录
                else if (debugStamina && currentFrame % 120 == 0 && staminaPercentage % 10 < 1)
                {
                    Debug.Log($"🔄 [{currentFrame}] 敌人Lv{level}恢复体力至: {staminaPercentage:F0}%");
                }
            }
        }
        
        // 更新状态显示
        UpdateNameWithStatus();
    }
    
    // 添加自定义方法，控制敌人速度设置，避免冲突
    private void SetSpeedBasedOnStamina()
    {
        if (!IsAgentValid()) return;
        
        // 获取当前状态和体力情况
        bool isRunningState = (currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing);
        bool shouldRun = isRunningState && canRun && currentStamina > 0;
        
        // 目标速度
        float targetSpeed = shouldRun ? runSpeed : walkSpeed;
        
        // 如果当前速度接近目标速度，不做改变
        if (Mathf.Abs(agent.speed - targetSpeed) < 0.2f)
            return;
        
        // 记录当前速度（便于调试）
        float oldSpeed = agent.speed;
        
        // 设置新速度
        agent.speed = targetSpeed;
        
        // 输出调试信息（只在速度明显变化时）
        if (debugStamina && Mathf.Abs(oldSpeed - targetSpeed) > 0.5f)
        {
            string speedChangeType = targetSpeed > oldSpeed ? "提高" : "降低";
            string reason = shouldRun ? 
                "进入奔跑状态" : 
                (!canRun ? "不允许奔跑" : (currentStamina <= 0 ? "体力耗尽" : "非奔跑状态"));
            
            Debug.Log($"⚡ [{Time.frameCount}] 敌人Lv{level}速度{speedChangeType}: {oldSpeed:F1}->{targetSpeed:F1}，原因: {reason}");
        }
    }

    // 新增：判断是否在使用奔跑速度的工具方法
    private bool IsUsingRunSpeed()
    {
        return (currentState == EnemyState.Hunting || currentState == EnemyState.Fleeing) && agent.speed == runSpeed;
    }

    // 在文件开头添加一个调试变量
    [Header("调试参数")]
    [HideInInspector] public bool debugStamina = false;  // 是否调试体力系统
}