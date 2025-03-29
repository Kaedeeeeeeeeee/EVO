using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Linq;

public class EnemySpawner : MonoBehaviour
{
    // 移除原有敌人类型配置，改为使用EnemyDataManager
    public GameObject healthBarPrefab;         // 敌人血条预制体
    
    [Header("敌人生成设置")]
    [Tooltip("是否使用CSV配置数据")]
    public bool useCSVData = true;             // 是否使用CSV数据
    
    [Tooltip("当useCSVData=false时使用这些配置")]
    [System.Serializable]
    public class EnemyTypeConfig
    {
        public string enemyTypeName;           // 敌人类型名称
        public GameObject enemyPrefab;         // 敌人预制体
        public EnemyData[] enemyDataArray;     // 该类型敌人的数据配置
        [Range(0, 100)]
        public int spawnWeight = 50;           // 该类型敌人的生成权重
    }
    
    [Tooltip("当useCSVData=false时使用的敌人类型配置")]
    public EnemyTypeConfig[] enemyTypes;       // 敌人类型配置数组
    
    [Header("生成配置")]
    [Tooltip("要生成的敌人类型ID列表（留空则生成全部类型）")]
    public List<string> activeEnemyTypeIDs = new List<string>();  // 要生成的敌人类型ID列表
    
    // 兼容原有代码的敌人数据数组，从所有enemyTypes中收集
    private EnemyData[] enemyDataArray 
    {
        get
        {
            if (useCSVData)
            {
                List<EnemyData> allEnemyData = new List<EnemyData>();
                
                // 检查EnemyDataManager是否可用
                if (EnemyDataManager.Instance == null)
                {
                    Debug.LogError("❌ EnemyDataManager未初始化，请确保场景中有EnemyDataManager组件");
                    return allEnemyData.ToArray();
                }
                
                // 获取所有活跃的敌人类型ID
                List<string> typeIDs = activeEnemyTypeIDs.Count > 0 
                    ? activeEnemyTypeIDs 
                    : EnemyDataManager.Instance.GetAllEnemyTypeIDs();
                
                // 收集所有敌人数据
                foreach (string typeID in typeIDs)
                {
                    allEnemyData.AddRange(EnemyDataManager.Instance.GetEnemyDataByType(typeID));
                }
                
                return allEnemyData.ToArray();
            }
            else
            {
                List<EnemyData> allEnemyData = new List<EnemyData>();
                if (enemyTypes != null)
                {
                    foreach (var type in enemyTypes)
                    {
                        if (type.enemyDataArray != null)
                        {
                            allEnemyData.AddRange(type.enemyDataArray);
                        }
                    }
                }
                return allEnemyData.ToArray();
            }
        }
    }

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
        // 给敌人生成器添加Temporary标签以便场景切换时正确清理
        gameObject.tag = "Temporary";
        
        // 确保实例在开始前被正确初始化
        spawnedEnemies = new List<GameObject>();
        
        // 确保在地形生成后再生成敌人
        TerrainGenerator terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null)
        {
            // 移除所有旧的监听器
            terrain.OnMapGenerationComplete -= StartSpawning;
            terrain.OnTerrainGenerationComplete -= OnTerrainGenerationComplete;
            terrain.OnNavMeshBakeComplete -= OnNavMeshBakeComplete;
            terrain.OnGrassGenerationComplete -= OnGrassGenerationComplete;
            terrain.OnMapGenerationComplete -= OnMapGenerationComplete;
            
            // 只添加一次事件监听
            terrain.OnTerrainGenerationComplete += OnTerrainGenerationComplete;
            terrain.OnNavMeshBakeComplete += OnNavMeshBakeComplete;
            terrain.OnGrassGenerationComplete += OnGrassGenerationComplete;
            terrain.OnMapGenerationComplete += OnMapGenerationComplete;
            
            Debug.Log("敌人生成器已连接到地形生成器的事件系统");
        }
        else
        {
            Debug.LogWarning("未找到TerrainGenerator，延迟启动敌人生成");
            Invoke("DelayedSpawn", 3f);
        }

        // 检查必要引用
        if (useCSVData)
        {
            // 检查EnemyDataManager是否可用
            if (EnemyDataManager.Instance == null)
            {
                Debug.LogError("❌ EnemyDataManager未初始化，请确保场景中有EnemyDataManager组件");
                
                // 创建EnemyDataManager
                GameObject managerObj = new GameObject("EnemyDataManager");
                managerObj.AddComponent<EnemyDataManager>();
                Debug.Log("✅ 已自动创建EnemyDataManager");
            }
        }
        else
        {
            if (enemyTypes == null || enemyTypes.Length == 0)
            {
                Debug.LogError("❌ EnemySpawner: 未设置敌人类型配置！");
                return;
            }

            bool allValid = true;
            foreach (var enemyType in enemyTypes)
            {
                if (enemyType.enemyPrefab == null)
                {
                    Debug.LogError($"❌ EnemySpawner: 敌人类型 '{enemyType.enemyTypeName}' 未设置敌人预制体！");
                    allValid = false;
                }
                
                if (enemyType.enemyDataArray == null || enemyType.enemyDataArray.Length == 0)
                {
                    Debug.LogError($"❌ EnemySpawner: 敌人类型 '{enemyType.enemyTypeName}' 未设置敌人数据！");
                    allValid = false;
                }
            }
            
            if (!allValid)
            {
                return;
            }
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

        // 延迟初始化player引用
        StartCoroutine(TryFindPlayer());
    }

    // 添加一个新的协程来反复尝试查找玩家
    private IEnumerator TryFindPlayer(int maxAttempts = 10)
    {
        int attempts = 0;
        
        while (player == null && attempts < maxAttempts)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            
            if (player != null)
            {
                Debug.Log("✅ 成功找到Player对象");
                
                // 验证设置
                LogSettings();
                
                // 玩家找到后，开始生成敌人
                if (!IsInvoking("DelayedSpawn"))
                {
                    StartCoroutine(SpawnInitialEnemies());
                }
                
                break;
            }
            
            attempts++;
            Debug.LogWarning($"⚠️ 未找到玩家，尝试 {attempts}/{maxAttempts}");
            
            // 等待一段时间再试
            yield return new WaitForSeconds(1f);
        }
        
        if (player == null)
        {
            Debug.LogError("❌ 多次尝试后仍未找到玩家！请检查玩家对象是否具有'Player'标签");
        }
    }

    void DelayedSpawn()
    {
        Debug.Log("延迟启动敌人生成");
        
        // 再次尝试找到玩家
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            
            if (player == null)
            {
                Debug.LogWarning("DelayedSpawn中仍未找到玩家，再次延迟尝试...");
                Invoke("DelayedSpawn", 2f); // 如果还是找不到，继续延迟
                return; // 玩家未找到，先不进行后续操作
            }
        }
        
        // 强制初始化
        InitializeMainGroundBounds();
        
        // 开始生成
        StartCoroutine(SpawnInitialEnemies());
    }

    // 修改StartSpawning方法，确保它会开始生成敌人
    void StartSpawning()
    {
        // 防止重复生成
        if (spawnedEnemies.Count > 0)
        {
            Debug.LogWarning("已经存在生成的敌人，跳过本次生成");
            return;
        }

        Debug.Log("开始生成敌人...");
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
        int attempts = 0;
        int maxAttempts = maxEnemies * 2; // 设置最大尝试次数

        while (successfulSpawns < maxEnemies && attempts < maxAttempts)
        {
            if (spawnedEnemies.Count >= maxEnemies)
            {
                Debug.Log("已达到最大敌人数量，停止生成");
                break;
            }

            bool success = SpawnEnemy();
            if (success)
            {
                successfulSpawns++;
                if (debugMode)
                {
                    Debug.Log($"✅ 成功生成敌人 {successfulSpawns}/{maxEnemies}");
                }
            }

            attempts++;
            yield return new WaitForSeconds(initialSpawnDelay);
        }

        Debug.Log($"🏁 初始敌人生成完成: 成功 {successfulSpawns}/{maxEnemies}，总尝试次数：{attempts}");
    }

    void Update()
    {
        // 只保留清理无效引用的逻辑
        int removedCount = spawnedEnemies.RemoveAll(enemy => enemy == null);
        if (removedCount > 0 && debugMode)
        {
            Debug.Log($"🧹 清理了 {removedCount} 个无效敌人引用，当前敌人数量：{spawnedEnemies.Count}");
        }
    }

    private bool SpawnEnemy()
    {
        // 添加数量检查
        if (spawnedEnemies.Count >= maxEnemies)
        {
            if (debugMode)
            {
                Debug.LogWarning($"⚠️ 已达到最大敌人数量限制 ({maxEnemies})，不再生成新敌人");
            }
            return false;
        }

        Vector3 spawnPosition = GetValidSpawnPosition();
        if (spawnPosition == Vector3.zero)
        {
            failedSpawnAttempts++;
            if (debugMode && failedSpawnAttempts % 5 == 0)
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
        GameObject enemy = SpawnEnemy(enemyData, spawnPosition);
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
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyHealth.healthBar = healthBar;
                }

                // 初始化血条显示
                EnemyHealth enemyHealthComponent = enemy.GetComponent<EnemyHealth>();
                if (enemyHealthComponent != null)
                {
                    enemyHealthComponent.ResetHealth();
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
        // 添加安全检查，如果player为null则尝试查找
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("❌ GetMapWideSpawnPosition: player引用为null，无法计算生成位置");
                return Vector3.zero;
            }
        }

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

                // 安全检查
                if (player != null)
                {
                    // 检查与玩家的距离
                    float distanceToPlayer = Vector3.Distance(validPosition, player.position);
                    if (distanceToPlayer < minDistanceFromPlayer)
                    {
                        distanceChecks++;
                        continue; // 太靠近玩家，重新尝试
                    }
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
        // 添加安全检查，如果player为null则尝试查找
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("❌ GetPlayerBasedSpawnPosition: player引用为null，无法计算生成位置");
                return Vector3.zero;
            }
        }

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
        if (player != null && spawnedEnemies.Count == 0)
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
        if (useCSVData)
        {
            // 使用CSV数据系统
            if (EnemyDataManager.Instance == null)
            {
                Debug.LogError("❌ EnemyDataManager未初始化！");
                return null;
            }
            
            // 获取要生成的敌人类型ID列表
            List<string> typeIDs = activeEnemyTypeIDs.Count > 0 
                ? activeEnemyTypeIDs 
                : EnemyDataManager.Instance.GetAllEnemyTypeIDs();
                
            if (typeIDs.Count == 0)
            {
                Debug.LogError("❌ 没有可用的敌人类型！");
                return null;
            }
            
            // 计算所有类型的总权重
            int totalTypeWeight = 0;
            Dictionary<string, int> typeWeights = new Dictionary<string, int>();
            
            foreach (string typeID in typeIDs)
            {
                int weight = EnemyDataManager.Instance.GetEnemyTypeSpawnWeight(typeID);
                if (weight > 0)
                {
                    typeWeights[typeID] = weight;
                    totalTypeWeight += weight;
                }
            }
            
            if (totalTypeWeight == 0)
            {
                Debug.LogError("❌ 所有敌人类型的权重都为0！");
                return null;
            }
            
            // 按权重随机选择一个类型
            int randomTypeWeight = Random.Range(0, totalTypeWeight);
            int currentTypeWeight = 0;
            string selectedTypeID = typeIDs[0];
            
            foreach (var entry in typeWeights)
            {
                currentTypeWeight += entry.Value;
                if (randomTypeWeight < currentTypeWeight)
                {
                    selectedTypeID = entry.Key;
                    break;
                }
            }
            
            // 获取选中类型的敌人数据列表
            List<EnemyData> enemiesOfType = EnemyDataManager.Instance.GetEnemyDataByType(selectedTypeID);
            if (enemiesOfType.Count == 0)
            {
                Debug.LogWarning($"⚠️ 类型 '{selectedTypeID}' 没有敌人数据，尝试使用其他类型");
                
                // 尝试其他类型
                foreach (string typeID in typeIDs)
                {
                    enemiesOfType = EnemyDataManager.Instance.GetEnemyDataByType(typeID);
                    if (enemiesOfType.Count > 0)
                    {
                        selectedTypeID = typeID;
                        break;
                    }
                }
                
                if (enemiesOfType.Count == 0)
                {
                    Debug.LogError("❌ 所有敌人类型都没有数据！");
                    return null;
                }
            }
            
            // 根据权重选择该类型中的一个敌人数据
            int totalEnemyWeight = enemiesOfType.Sum(e => e.spawnWeight);
            int randomEnemyWeight = Random.Range(0, totalEnemyWeight);
            int currentEnemyWeight = 0;
            
            EnemyData selectedData = null;
            
            foreach (EnemyData data in enemiesOfType)
            {
                currentEnemyWeight += data.spawnWeight;
                if (randomEnemyWeight < currentEnemyWeight)
                {
                    selectedData = data;
                    break;
                }
            }
            
            if (selectedData == null && enemiesOfType.Count > 0)
            {
                selectedData = enemiesOfType[0];
            }
            
            // 创建包装类，包含敌人数据和对应的预制体
            if (selectedData != null)
            {
                CSVEnemyDataWrapper wrapper = new CSVEnemyDataWrapper();
                wrapper.data = selectedData;
                wrapper.typeID = selectedTypeID;
                wrapper.prefab = EnemyDataManager.Instance.GetEnemyPrefab(selectedTypeID);
                return wrapper;
            }
            
            return null;
        }
        else
        {
            // 原有的数据管理方式
            if (enemyTypes == null || enemyTypes.Length == 0)
            {
                return null;
            }

            // 先按权重选择敌人类型
            int totalTypeWeight = 0;
            foreach (var type in enemyTypes)
            {
                totalTypeWeight += type.spawnWeight;
            }

            // 随机选择敌人类型
            int randomTypeWeight = Random.Range(0, totalTypeWeight);
            int currentTypeWeight = 0;
            
            EnemyTypeConfig selectedType = enemyTypes[0];
            
            foreach (var type in enemyTypes)
            {
                currentTypeWeight += type.spawnWeight;
                if (randomTypeWeight < currentTypeWeight)
                {
                    selectedType = type;
                    break;
                }
            }
            
            // 确保选中的类型有敌人数据
            if (selectedType.enemyDataArray == null || selectedType.enemyDataArray.Length == 0)
            {
                Debug.LogWarning($"⚠️ 选中的敌人类型 '{selectedType.enemyTypeName}' 没有敌人数据，使用第一个有效类型");
                // 寻找第一个有效的敌人类型
                foreach (var type in enemyTypes)
                {
                    if (type.enemyDataArray != null && type.enemyDataArray.Length > 0)
                    {
                        selectedType = type;
                        break;
                    }
                }
            }
            
            // 从选中的类型中按权重选择敌人数据
            int totalWeight = 0;
            foreach (EnemyData data in selectedType.enemyDataArray)
            {
                totalWeight += data.spawnWeight;
            }

            int randomWeight = Random.Range(0, totalWeight);
            int currentWeight = 0;

            // 包装返回的敌人数据和预制体
            EnemyDataWithPrefab result = new EnemyDataWithPrefab();
            
            foreach (EnemyData data in selectedType.enemyDataArray)
            {
                currentWeight += data.spawnWeight;
                if (randomWeight < currentWeight)
                {
                    // 记录选中的敌人数据和对应的预制体
                    result.data = data;
                    result.prefab = selectedType.enemyPrefab;
                    return result;
                }
            }

            // 默认返回第一个
            if (selectedType.enemyDataArray.Length > 0)
            {
                result.data = selectedType.enemyDataArray[0];
                result.prefab = selectedType.enemyPrefab;
                return result;
            }
            
            Debug.LogError("❌ 无法选择有效的敌人数据！");
            return null;
        }
    }

    // 新增包装类，用于包装来自CSV的敌人数据
    private class CSVEnemyDataWrapper : EnemyData
    {
        public string typeID;
        public GameObject prefab;
        public EnemyData data;
        
        // 转发所有属性到data对象
        public new string enemyName { get { return data != null ? data.enemyName : ""; } }
        public new int level { get { return data != null ? data.level : 1; } }
        public new int maxHealth { get { return data != null ? data.maxHealth : 50; } }
        public new int attackDamage { get { return data != null ? data.attackDamage : 5; } }
        public new float walkSpeed { get { return data != null ? data.walkSpeed : 2f; } }
        public new float runSpeed { get { return data != null ? data.runSpeed : 4f; } }
        public new float visionRange { get { return data != null ? data.visionRange : 8f; } }
        public new float attackRange { get { return data != null ? data.attackRange : 1.5f; } }
        public new float huntThreshold { get { return data != null ? data.huntThreshold : 30f; } }
        public new Material enemyMaterial { get { return data != null ? data.enemyMaterial : null; } }
        public new int spawnWeight { get { return data != null ? data.spawnWeight : 50; } }
        public new float maxStamina { get { return data != null ? data.maxStamina : 100f; } }
        public new float staminaDecreaseRate { get { return data != null ? data.staminaDecreaseRate : 1f; } }
        public new float staminaRecoveryRate { get { return data != null ? data.staminaRecoveryRate : 2f; } }
        public new float staminaRecoveryDelay { get { return data != null ? data.staminaRecoveryDelay : 1f; } }
        public new int corpseHealAmount { get { return data != null ? data.corpseHealAmount : 20; } }
        public new int corpseEvoPoints { get { return data != null ? data.corpseEvoPoints : 10; } }
    }

    // 原有的包装类，用于包装编辑器配置的敌人数据
    private class EnemyDataWithPrefab : EnemyData
    {
        public GameObject prefab;
        public EnemyData data;
        
        // 转发所有属性到data对象
        public new string enemyName { get { return data != null ? data.enemyName : ""; } }
        public new int level { get { return data != null ? data.level : 1; } }
        public new int maxHealth { get { return data != null ? data.maxHealth : 50; } }
        public new int attackDamage { get { return data != null ? data.attackDamage : 5; } }
        public new float walkSpeed { get { return data != null ? data.walkSpeed : 2f; } }
        public new float runSpeed { get { return data != null ? data.runSpeed : 4f; } }
        public new float visionRange { get { return data != null ? data.visionRange : 8f; } }
        public new float attackRange { get { return data != null ? data.attackRange : 1.5f; } }
        public new float huntThreshold { get { return data != null ? data.huntThreshold : 30f; } }
        public new Material enemyMaterial { get { return data != null ? data.enemyMaterial : null; } }
        public new int spawnWeight { get { return data != null ? data.spawnWeight : 50; } }
        public new float maxStamina { get { return data != null ? data.maxStamina : 100f; } }
        public new float staminaDecreaseRate { get { return data != null ? data.staminaDecreaseRate : 1f; } }
        public new float staminaRecoveryRate { get { return data != null ? data.staminaRecoveryRate : 2f; } }
        public new float staminaRecoveryDelay { get { return data != null ? data.staminaRecoveryDelay : 1f; } }
        public new int corpseHealAmount { get { return data != null ? data.corpseHealAmount : 20; } }
        public new int corpseEvoPoints { get { return data != null ? data.corpseEvoPoints : 10; } }
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

    // 添加一个新方法，用于在每一帧检查并安全清理所有失效引用
    void LateUpdate()
    {
        // 移除所有为null的敌人引用，防止MissingReferenceException
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null)
            {
                spawnedEnemies.RemoveAt(i);
            }
        }
        
        // 如果玩家引用丢失，尝试重新获取
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogError("无法找到玩家对象！请确保Player正确设置了Tag");
            }
            else
            {
                Debug.Log("成功重新查找到Player对象");
            }
        }
    }

    // 添加OnDestroy方法确保正确清理资源和取消事件订阅
    void OnDestroy()
    {
        // 移除事件监听
        TerrainGenerator terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null)
        {
            terrain.OnMapGenerationComplete -= StartSpawning;
            
            // 移除分阶段事件监听
            terrain.OnTerrainGenerationComplete -= OnTerrainGenerationComplete;
            terrain.OnNavMeshBakeComplete -= OnNavMeshBakeComplete;
            terrain.OnGrassGenerationComplete -= OnGrassGenerationComplete;
            terrain.OnMapGenerationComplete -= OnMapGenerationComplete;
        }
        
        // 停止所有协程
        StopAllCoroutines();
        
        // 销毁所有已生成的敌人，避免引用问题
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        
        // 清理敌人列表
        spawnedEnemies.Clear();
        player = null; // 显式清除对玩家的引用
    }

    // 添加一个新方法，用于在游戏重启时重新初始化敌人生成器
    public void ReInitialize()
    {
        Debug.Log("重新初始化敌人生成器...");
        
        // 清空现有敌人
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }
        spawnedEnemies.Clear();
        
        // 重新连接到TerrainGenerator
        TerrainGenerator terrain = FindFirstObjectByType<TerrainGenerator>();
        if (terrain != null)
        {
            // 移除旧的监听器
            terrain.OnMapGenerationComplete -= StartSpawning;
            
            // 使用新的分阶段事件系统
            terrain.OnTerrainGenerationComplete -= OnTerrainGenerationComplete;
            terrain.OnNavMeshBakeComplete -= OnNavMeshBakeComplete;
            terrain.OnGrassGenerationComplete -= OnGrassGenerationComplete;
            terrain.OnMapGenerationComplete -= OnMapGenerationComplete;
            
            // 添加新的监听器
            terrain.OnTerrainGenerationComplete += OnTerrainGenerationComplete;
            terrain.OnNavMeshBakeComplete += OnNavMeshBakeComplete;
            terrain.OnGrassGenerationComplete += OnGrassGenerationComplete;
            terrain.OnMapGenerationComplete += OnMapGenerationComplete;
            
            // 保留向后兼容
            terrain.OnMapGenerationComplete += StartSpawning;
            
            Debug.Log("敌人生成器重新连接到地形生成器的分阶段事件系统");
        }
        else
        {
            Debug.LogWarning("ReInitialize时未找到TerrainGenerator");
        }
        
        // 重新查找玩家
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        if (player == null)
        {
            Debug.LogWarning("ReInitialize时未找到玩家，稍后将尝试查找");
        }
        
        // 重新初始化MainGround边界
        InitializeMainGroundBounds();
    }

    // 新增：接收地形生成事件
    private void OnTerrainGenerationComplete()
    {
        Debug.Log("EnemySpawner接收到地形生成完成事件");
    }
    
    // 新增：接收NavMesh烘焙完成事件
    private void OnNavMeshBakeComplete()
    {
        Debug.Log("EnemySpawner接收到NavMesh烘焙完成事件");
    }
    
    // 新增：接收草地生成完成事件
    private void OnGrassGenerationComplete()
    {
        Debug.Log("EnemySpawner接收到草地生成完成事件");
    }
    
    // 新增：接收地图生成完成事件
    private void OnMapGenerationComplete()
    {
        Debug.Log("EnemySpawner接收到地图全部生成完成事件，准备生成敌人");
        StartSpawning(); // 直接调用StartSpawning，不再依赖外部调用
    }
    
    // 新增：供GameManager调用的公共方法
    public void SpawnEnemies()
    {
        Debug.Log("GameManager请求生成敌人");
        
        // 确保有玩家引用
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player == null)
            {
                Debug.LogWarning("SpawnEnemies时未找到玩家，延迟尝试...");
                Invoke("DelayedSpawn", 1f);
                return;
            }
        }
        
        // 确保已初始化地形边界
        InitializeMainGroundBounds();
        
        // 开始生成敌人
        StartCoroutine(SpawnInitialEnemies());
    }

    private GameObject SpawnEnemy(EnemyData enemyData, Vector3 spawnPosition)
    {
        // 获取正确的敌人预制体
        GameObject prefabToSpawn;
        
        // 根据不同的数据源获取预制体
        if (enemyData is CSVEnemyDataWrapper)
        {
            CSVEnemyDataWrapper dataWithPrefab = (CSVEnemyDataWrapper)enemyData;
            prefabToSpawn = dataWithPrefab.prefab;
            
            // 使用包装类中的实际数据
            enemyData = dataWithPrefab.data;
            
            if (prefabToSpawn == null)
            {
                Debug.LogError($"❌ 类型 '{dataWithPrefab.typeID}' 的预制体为空！");
                return null;
            }
        }
        else if (enemyData is EnemyDataWithPrefab)
        {
            EnemyDataWithPrefab dataWithPrefab = (EnemyDataWithPrefab)enemyData;
            prefabToSpawn = dataWithPrefab.prefab;
            
            // 使用包装类中的实际数据
            enemyData = dataWithPrefab.data;
        }
        else
        {
            // 尝试查找预制体
            if (useCSVData && EnemyDataManager.Instance != null)
            {
                // 尝试从所有注册的敌人类型中找到匹配的预制体
                prefabToSpawn = null;
                
                foreach (string typeID in EnemyDataManager.Instance.GetAllEnemyTypeIDs())
                {
                    List<EnemyData> enemies = EnemyDataManager.Instance.GetEnemyDataByType(typeID);
                    if (enemies.Contains(enemyData))
                    {
                        prefabToSpawn = EnemyDataManager.Instance.GetEnemyPrefab(typeID);
                        break;
                    }
                }
                
                if (prefabToSpawn == null)
                {
                    // 使用第一个可用的预制体
                    var typeIDs = EnemyDataManager.Instance.GetAllEnemyTypeIDs();
                    if (typeIDs.Count > 0)
                    {
                        prefabToSpawn = EnemyDataManager.Instance.GetEnemyPrefab(typeIDs[0]);
                        Debug.LogWarning("⚠️ 无法确定敌人数据对应的预制体，使用默认预制体");
                    }
                }
            }
            else if (enemyTypes != null && enemyTypes.Length > 0)
            {
                // 查找本地配置的预制体
                prefabToSpawn = enemyTypes[0].enemyPrefab;
                Debug.LogWarning("⚠️ 使用默认敌人预制体，因为无法确定当前敌人数据对应的预制体");
            }
            else
            {
                Debug.LogError("❌ 无法找到有效的敌人预制体！");
                return null;
            }
        }

        // 实例化敌人预制体
        GameObject enemyInstance = Instantiate(prefabToSpawn, spawnPosition, Quaternion.identity);

        // 设置敌人属性
        EnemyAIExtended enemyAI = enemyInstance.GetComponent<EnemyAIExtended>();
        if (enemyAI != null)
        {
            // 设置基本属性
            enemyAI.level = enemyData.level;
            enemyAI.walkSpeed = enemyData.walkSpeed;
            enemyAI.runSpeed = enemyData.runSpeed;
            enemyAI.visionRange = enemyData.visionRange;
            enemyAI.attackRange = enemyData.attackRange;
            enemyAI.attackDamage = enemyData.attackDamage;
            enemyAI.huntThreshold = enemyData.huntThreshold;
            
            // 设置新的体力系统参数
            enemyAI.maxStamina = enemyData.maxStamina;
            enemyAI.staminaDecreaseRate = enemyData.staminaDecreaseRate;
            enemyAI.staminaRecoveryRate = enemyData.staminaRecoveryRate;
            enemyAI.staminaRecoveryDelay = enemyData.staminaRecoveryDelay;
            enemyAI.canRun = true;

            // 初始化敌人
            enemyAI.InitializeEnemy();
        }

        // 设置敌人的生命值
        EnemyHealth enemyHealth = enemyInstance.GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.maxHealth = enemyData.maxHealth;
            enemyHealth.ResetHealth();
        }

        // 设置敌人的材质
        if (enemyData.enemyMaterial != null)
        {
            Renderer renderer = enemyInstance.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material = enemyData.enemyMaterial;
            }
        }

        // 设置死亡时获得的资源
        DeadEnemy deadEnemy = enemyInstance.GetComponent<DeadEnemy>();
        if (deadEnemy != null)
        {
            deadEnemy.healAmount = enemyData.corpseHealAmount;
            deadEnemy.evoPoints = enemyData.corpseEvoPoints;
        }

        return enemyInstance;
    }
}