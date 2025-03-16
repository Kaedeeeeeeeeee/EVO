using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemySpawner : MonoBehaviour
{
    public GameObject enemyPrefab;             // 敌人预制体
    public GameObject healthBarPrefab;         // 敌人血条预制体
    public EnemyData[] enemyDataArray;         // 敌人数据数组

    [Header("生成数量设置")]
    [Range(1, 100)]
    public int maxEnemies = 20;                // 敌人最大数量

    [Header("生成位置设置")]
    public GameObject mainGround;              // 主地图对象
    public float minDistanceFromPlayer = 10f;  // 距离玩家的最小距离
    public float minDistanceBetweenEnemies = 5f; // 敌人之间的最小距离
    public bool useMapWideSpawning = true;     // 是否使用全地图生成模式

    [Header("以玩家为中心的生成设置（后备模式）")]
    public float spawnRadius = 50f;            // 生成半径

    [Header("地面检测设置")]
    public LayerMask groundLayer;              // 地面层
    public float raycastHeight = 10f;          // 射线检测的高度
    public float raycastDistance = 20f;        // 射线检测的距离

    [Header("其他设置")]
    public Transform player;                   // 玩家引用
    public bool debugMode = true;              // 调试模式
    public int spawnAttemptsPerPosition = 30;  // 每个位置的尝试次数
    public float initialSpawnDelay = 0.1f;     // 初始生成的延迟

    private List<GameObject> spawnedEnemies = new List<GameObject>();
    private int failedSpawnAttempts = 0;       // 失败的生成尝试
    private Bounds mainGroundBounds;           // 主地图边界

    void Start()
    {
        // 检查必要引用
        if (enemyPrefab == null)
        {
            Debug.LogError("❌ EnemySpawner: 未设置敌人预制体！");
            return;
        }

        // 初始化MainGround边界
        InitializeMainGroundBounds();

        // 尝试加载血条预制体
        if (healthBarPrefab == null)
        {
            healthBarPrefab = Resources.Load<GameObject>("EnemyHealthBar");
            if (healthBarPrefab == null)
            {
                Debug.LogWarning("⚠️ 未设置敌人血条预制体且无法从Resources加载，敌人将没有血条显示");
            }
        }

        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("❌ EnemySpawner: 未找到玩家！");
                return;
            }
        }

        // 验证设置
        LogSettings();

        // 生成初始敌人
        StartCoroutine(SpawnInitialEnemies());
    }

    // 初始化MainGround边界
    void InitializeMainGroundBounds()
    {
        if (mainGround == null)
        {
            // 尝试自动查找MainGround
            mainGround = GameObject.Find("MainGround");
            if (mainGround == null)
            {
                Debug.LogWarning("⚠️ 未设置MainGround且无法自动查找，将使用基于玩家的生成模式");
                useMapWideSpawning = false;
            }
        }

        if (mainGround != null)
        {
            // 获取MainGround的边界
            Renderer groundRenderer = mainGround.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                mainGroundBounds = groundRenderer.bounds;
                Debug.Log($"✅ 成功获取MainGround边界: 中心({mainGroundBounds.center})，大小({mainGroundBounds.size})");
            }
            else
            {
                // 尝试从子对象中获取Renderer
                Renderer[] childRenderers = mainGround.GetComponentsInChildren<Renderer>();
                if (childRenderers.Length > 0)
                {
                    // 计算所有子对象的总边界
                    mainGroundBounds = childRenderers[0].bounds;
                    for (int i = 1; i < childRenderers.Length; i++)
                    {
                        mainGroundBounds.Encapsulate(childRenderers[i].bounds);
                    }
                    Debug.Log($"✅ 从子对象获取MainGround边界: 中心({mainGroundBounds.center})，大小({mainGroundBounds.size})");
                }
                else
                {
                    Debug.LogWarning("⚠️ MainGround及其子对象都没有Renderer组件，将使用基于玩家的生成模式");
                    useMapWideSpawning = false;
                }
            }
        }
    }

    // 输出当前设置到控制台
    void LogSettings()
    {
        Debug.Log($"===== 敌人生成器设置 =====");
        Debug.Log($"最大敌人数量: {maxEnemies}");
        Debug.Log($"生成模式: {(useMapWideSpawning ? "全地图生成" : "基于玩家生成")}");
        if (useMapWideSpawning)
        {
            Debug.Log($"MainGround边界: 中心({mainGroundBounds.center})，大小({mainGroundBounds.size})");
        }
        else
        {
            Debug.Log($"生成半径: {spawnRadius}");
        }
        Debug.Log($"与玩家最小距离: {minDistanceFromPlayer}");
        Debug.Log($"敌人间最小距离: {minDistanceBetweenEnemies}");
        Debug.Log($"地面层遮罩: {groundLayer.value}");
        Debug.Log($"敌人数据数组大小: {(enemyDataArray != null ? enemyDataArray.Length : 0)}");
        Debug.Log($"血条预制体: {(healthBarPrefab != null ? "已设置" : "未设置")}");
        Debug.Log($"===== 设置结束 =====");
    }

    IEnumerator SpawnInitialEnemies()
    {
        Debug.Log($"🔄 开始生成初始敌人... 目标数量: {maxEnemies}");
        yield return new WaitForSeconds(1f); // 等待场景完全加载

        int successfulSpawns = 0;

        for (int i = 0; i < maxEnemies; i++)
        {
            bool success = SpawnEnemy();
            if (success)
            {
                successfulSpawns++;
                if (debugMode)
                {
                    Debug.Log($"✅ 成功生成敌人 {successfulSpawns}/{maxEnemies}");
                }
            }
            else
            {
                if (debugMode)
                {
                    Debug.LogWarning($"⚠️ 敌人 #{i + 1} 生成失败");
                }
            }

            yield return new WaitForSeconds(initialSpawnDelay); // 分批生成，避免卡顿
        }

        Debug.Log($"🏁 初始敌人生成完成: 成功 {successfulSpawns}/{maxEnemies}");
    }

    void Update()
    {
        // 移除已销毁的敌人
        int removedCount = spawnedEnemies.RemoveAll(enemy => enemy == null);
        if (removedCount > 0 && debugMode)
        {
            Debug.Log($"🧹 清理了 {removedCount} 个无效敌人引用");
        }

        // 检查是否需要补充敌人
        if (spawnedEnemies.Count < maxEnemies)
        {
            // 随机决定是否生成新敌人（每秒约5%的几率）
            if (Random.value < 0.05f * Time.deltaTime)
            {
                bool success = SpawnEnemy();
                if (success && debugMode)
                {
                    Debug.Log($"✅ 动态补充生成了一个敌人，当前总数: {spawnedEnemies.Count}/{maxEnemies}");
                }
            }
        }
    }

    private bool SpawnEnemy()
    {
        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            failedSpawnAttempts++;
            if (debugMode && failedSpawnAttempts % 5 == 0) // 避免日志过多
            {
                Debug.LogWarning($"⚠️ 无法找到有效的敌人生成位置，已累计失败 {failedSpawnAttempts} 次");
            }
            return false;
        }

        // 随机选择敌人等级
        EnemyData enemyData = GetRandomEnemyData();
        if (enemyData == null)
        {
            Debug.LogError("❌ 没有可用的敌人数据！");
            return false;
        }

        // 生成敌人
        GameObject enemy = Instantiate(enemyPrefab, spawnPosition, Quaternion.identity);
        enemy.name = $"{enemyData.enemyName}_Lv{enemyData.level}";

        // 确保敌人在正确的层级
        enemy.layer = LayerMask.NameToLayer("Enemy");
        enemy.tag = "Enemy"; // 确保标签正确

        // 检查碰撞体
        Collider enemyCollider = enemy.GetComponent<Collider>();
        if (enemyCollider == null)
        {
            Debug.LogWarning("⚠️ 敌人预制体缺少Collider组件，正在添加...");
            // 添加胶囊碰撞体
            CapsuleCollider capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0, 1, 0); // 调整中心点
            capsule.height = 2f;
            capsule.radius = 0.5f;
        }

        // 设置敌人属性
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health == null)
        {
            Debug.LogWarning("⚠️ 敌人预制体缺少EnemyHealth组件，正在添加...");
            health = enemy.AddComponent<EnemyHealth>();
        }

        // 确保生命值设置正确
        health.maxHealth = enemyData.maxHealth;

        // 使用预制体添加血条UI
        if (healthBarPrefab != null)
        {
            GameObject healthBarObj = Instantiate(healthBarPrefab, enemy.transform);
            healthBarObj.name = "EnemyHealthBar";

            // 增加高度，确保血条位于敌人头顶上方
            healthBarObj.transform.localPosition = new Vector3(0, 3f, 0);

            // 调整血条缩放 - 可以根据需要修改
            healthBarObj.transform.localScale = new Vector3(0.02f, 0.02f, 0.02f);

            // 获取Canvas组件
            Canvas canvas = healthBarObj.GetComponent<Canvas>();
            if (canvas != null)
            {
                // 设置Canvas参数
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
            }

            // 获取并设置血条组件
            EnemyHealthBar healthBar = healthBarObj.GetComponent<EnemyHealthBar>();
            if (healthBar != null)
            {
                // 设置等级
                healthBar.SetLevel(enemyData.level);

                // 确保CanvasGroup激活
                CanvasGroup canvasGroup = healthBarObj.GetComponent<CanvasGroup>();
                if (canvasGroup != null)
                {
                    canvasGroup.alpha = 1;
                }

                // 更新EnemyHealth中的血条引用
                health.healthBar = healthBar;

                // 初始化血条显示
                health.ResetHealth();
            }
        }

        // 添加扩展的AI行为组件
        EnemyAIExtended aiExtended = enemy.GetComponent<EnemyAIExtended>();
        if (aiExtended == null)
        {
            aiExtended = enemy.AddComponent<EnemyAIExtended>();
        }

        // 配置AI属性
        if (aiExtended != null)
        {
            aiExtended.level = enemyData.level;
            aiExtended.attackDamage = enemyData.attackDamage;
            aiExtended.walkSpeed = enemyData.walkSpeed;
            aiExtended.runSpeed = enemyData.runSpeed;
            aiExtended.visionRange = enemyData.visionRange;
            aiExtended.attackRange = enemyData.attackRange;
            aiExtended.huntThreshold = enemyData.huntThreshold;
            aiExtended.InitializeEnemy();

            // 设置材质（用于视觉区分等级）
            if (enemyData.enemyMaterial != null)
            {
                Renderer renderer = enemy.GetComponentInChildren<Renderer>();
                if (renderer != null)
                {
                    renderer.material = enemyData.enemyMaterial;
                }
            }
        }

        // 添加到生成的敌人列表
        spawnedEnemies.Add(enemy);

        if (debugMode)
        {
            Debug.Log($"✅ 生成了一个 {enemyData.level} 级敌人: {enemy.name} 在位置 {spawnPosition}");
        }

        return true;
    }

    private Vector3 GetValidSpawnPosition()
    {
        // 根据设置选择生成位置的方法
        if (useMapWideSpawning && mainGroundBounds.size != Vector3.zero)
        {
            return GetMapWideSpawnPosition();
        }
        else
        {
            return GetPlayerBasedSpawnPosition();
        }
    }

    // 全地图生成位置方法
    private Vector3 GetMapWideSpawnPosition()
    {
        int maxAttempts = spawnAttemptsPerPosition;
        int groundHits = 0;
        int distanceChecks = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            // 在MainGround边界内随机选择一个位置
            Vector3 randomPoint = new Vector3(
                Random.Range(mainGroundBounds.min.x, mainGroundBounds.max.x),
                mainGroundBounds.max.y + raycastHeight, // 从上方射线向下检测
                Random.Range(mainGroundBounds.min.z, mainGroundBounds.max.z)
            );

            // 射线检测，确保位置在地面上
            Ray ray = new Ray(randomPoint, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, groundLayer))
            {
                groundHits++;
                Vector3 validPosition = hitInfo.point + Vector3.up * 0.1f; // 略微抬高，避免嵌入地面

                // 检查与玩家的距离
                float distanceToPlayer = Vector3.Distance(validPosition, player.position);
                if (distanceToPlayer < minDistanceFromPlayer)
                {
                    distanceChecks++;
                    continue; // 太靠近玩家，重新尝试
                }

                // 检查与其他敌人的距离
                bool tooCloseToOthers = false;
                foreach (GameObject otherEnemy in spawnedEnemies)
                {
                    if (otherEnemy == null) continue;

                    if (Vector3.Distance(validPosition, otherEnemy.transform.position) < minDistanceBetweenEnemies)
                    {
                        tooCloseToOthers = true;
                        distanceChecks++;
                        break;
                    }
                }

                // 如果与其他敌人距离合适，返回该位置
                if (!tooCloseToOthers)
                {
                    if (debugMode && i > 0)
                    {
                        Debug.Log($"🔍 找到有效位置，用了 {i + 1} 次尝试 (地面命中: {groundHits}, 距离检查失败: {distanceChecks})");
                    }
                    return validPosition;
                }
            }
        }

        if (debugMode)
        {
            Debug.LogWarning($"⚠️ 在MainGround上找不到有效位置，尝试基于玩家的位置");
        }

        // 如果在MainGround上找不到有效位置，回退到基于玩家的生成逻辑
        return GetPlayerBasedSpawnPosition();
    }

    // 基于玩家的生成位置方法（原方法）
    private Vector3 GetPlayerBasedSpawnPosition()
    {
        int maxAttempts = spawnAttemptsPerPosition;
        int groundHits = 0;
        int distanceChecks = 0;

        for (int i = 0; i < maxAttempts; i++)
        {
            // 生成随机角度和距离
            float angle = Random.Range(0f, 360f);
            float distance = Random.Range(minDistanceFromPlayer, spawnRadius);

            // 计算玩家周围的随机位置
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 potentialPosition = player.position + direction * distance;

            // 射线检测，确保位置在地面上
            Ray ray = new Ray(potentialPosition + Vector3.up * raycastHeight, Vector3.down);
            if (Physics.Raycast(ray, out RaycastHit hitInfo, raycastDistance, groundLayer))
            {
                groundHits++;
                Vector3 validPosition = hitInfo.point + Vector3.up * 0.1f; // 略微抬高，避免嵌入地面

                // 检查与其他敌人的距离
                bool tooCloseToOthers = false;
                foreach (GameObject otherEnemy in spawnedEnemies)
                {
                    if (otherEnemy == null) continue;

                    if (Vector3.Distance(validPosition, otherEnemy.transform.position) < minDistanceBetweenEnemies)
                    {
                        tooCloseToOthers = true;
                        distanceChecks++;
                        break;
                    }
                }

                // 如果与其他敌人距离合适，返回该位置
                if (!tooCloseToOthers)
                {
                    if (debugMode && i > 0)
                    {
                        Debug.Log($"🔍 找到有效位置，用了 {i + 1} 次尝试 (地面命中: {groundHits}, 距离检查失败: {distanceChecks})");
                    }
                    return validPosition;
                }
            }
        }

        // 如果实在找不到有效位置，在极端情况下可以忽略距离限制
        if (spawnedEnemies.Count == 0)
        {
            float angle = Random.Range(0f, 360f);
            float distance = minDistanceFromPlayer * 1.5f;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            Vector3 fallbackPosition = player.position + direction * distance + Vector3.up * 0.1f;

            Debug.LogWarning($"⚠️ 使用应急位置: {fallbackPosition}");
            return fallbackPosition;
        }

        // 无法找到有效位置
        return Vector3.zero;
    }

    private EnemyData GetRandomEnemyData()
    {
        if (enemyDataArray == null || enemyDataArray.Length == 0)
        {
            return null;
        }

        // 计算总权重
        int totalWeight = 0;
        foreach (EnemyData data in enemyDataArray)
        {
            totalWeight += data.spawnWeight;
        }

        // 随机选择基于权重的敌人类型
        int randomWeight = Random.Range(0, totalWeight);
        int currentWeight = 0;

        foreach (EnemyData data in enemyDataArray)
        {
            currentWeight += data.spawnWeight;
            if (randomWeight < currentWeight)
            {
                return data;
            }
        }

        // 默认返回第一个
        return enemyDataArray[0];
    }

    // 调试辅助方法 - 强制生成指定数量的敌人
    public void ForceSpawnEnemies(int count)
    {
        Debug.Log($"🔄 强制生成 {count} 个敌人");
        StartCoroutine(ForceSpawnCoroutine(count));
    }

    private IEnumerator ForceSpawnCoroutine(int count)
    {
        int spawned = 0;
        for (int i = 0; i < count; i++)
        {
            if (SpawnEnemy())
            {
                spawned++;
            }
            yield return new WaitForSeconds(0.1f);
        }
        Debug.Log($"✅ 强制生成完成，成功: {spawned}/{count}");
    }

    void OnDrawGizmosSelected()
    {
        // 显示MainGround边界（如果使用全地图生成）
        if (useMapWideSpawning && mainGround != null)
        {
            Gizmos.color = Color.cyan;
            Renderer groundRenderer = mainGround.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                Bounds bounds = groundRenderer.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        // 显示基于玩家的生成范围（后备模式）
        if (player != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(player.position, minDistanceFromPlayer);

            if (!useMapWideSpawning)
            {
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(player.position, spawnRadius);
            }
        }
    }
}