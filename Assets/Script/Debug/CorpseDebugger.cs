using UnityEngine;

// 放在场景中任意物体上，用于调试尸体系统
public class CorpseDebugger : MonoBehaviour
{
    [Header("测试尸体设置")]
    public bool enableDebug = true;        // 是否启用调试
    public KeyCode spawnCorpseKey = KeyCode.F8;  // 生成测试尸体的按键
    public KeyCode cloneEnemyKey = KeyCode.F9;   // 复制现有敌人为尸体的按键
    public Transform spawnPoint;           // 生成点
    [Range(1, 5)]
    public int corpseLevel = 1;            // 尸体等级
    public int healAmount = 20;            // 治疗量
    public int evoPoints = 10;             // 进化点数
    
    [Header("生成位置设置")]
    public bool spawnAtPlayerPosition = true;  // 是否在玩家位置生成
    public float offsetY = 0f;                // Y轴偏移
    public float offsetZ = 2f;                // Z轴偏移（前方）
    
    [Header("视觉效果")]
    public bool useDebugMaterial = true;    // 使用特殊材质
    public Color corpseColor = new Color(0.4f, 0.4f, 0.4f, 1f); // 尸体颜色（灰色）
    public bool addBloodPool = true;        // 添加血池效果
    public Color bloodColor = new Color(0.5f, 0f, 0f, 0.8f);    // 血池颜色
    public float bloodPoolSize = 1.5f;      // 血池大小
    
    // 上次选中的敌人
    private GameObject selectedEnemy = null;
    private bool isProcessing = false;      // 防止重复处理
    
    void Update()
    {
        if (!enableDebug || isProcessing) return;
        
        // 检测按键生成测试尸体
        if (Input.GetKeyDown(spawnCorpseKey))
        {
            CreateTestCorpse();
        }
        
        // 检测按键复制现有敌人为尸体
        if (Input.GetKeyDown(cloneEnemyKey))
        {
            try 
            {
                isProcessing = true;
                
                // 查找最近的敌人
                GameObject enemy = FindNearestEnemy();
                if (enemy != null)
                {
                    selectedEnemy = enemy;
                    CloneEnemyAsCorpse(enemy);
                }
                else
                {
                    Debug.LogWarning("没有找到可复制的敌人，使用默认测试尸体");
                    CreateSimpleCorpse();
                }
            }
            finally 
            {
                isProcessing = false;
            }
        }
    }
    
    // 安全地复制敌人作为尸体
    private void CloneEnemyAsCorpse(GameObject enemyObj)
    {
        if (enemyObj == null) return;
        
        try 
        {
            // 获取敌人信息
            EnemyHealth enemyHealth = enemyObj.GetComponent<EnemyHealth>();
            EnemyAIExtended enemyAI = enemyObj.GetComponent<EnemyAIExtended>();
            int level = enemyAI != null ? enemyAI.level : corpseLevel;
            
            // 获取生成位置
            Vector3 spawnPosition = GetSpawnPosition();
            
            Debug.Log($"正在复制敌人 {enemyObj.name} (Lv{level}) 为尸体");
            
            // 创建一个空的父物体作为尸体容器
            GameObject corpseContainer = new GameObject($"DebugCorpse_Container_Lv{level}");
            corpseContainer.transform.position = spawnPosition;
            corpseContainer.transform.rotation = Quaternion.identity;
            corpseContainer.layer = LayerMask.NameToLayer("Enemy");
            
            // 复制敌人对象作为子物体
            GameObject corpseObj = Instantiate(enemyObj, spawnPosition, enemyObj.transform.rotation);
            corpseObj.name = $"DebugCorpse_Model_Lv{level}";
            corpseObj.transform.SetParent(corpseContainer.transform);
            
            // 调整尸体模型的位置和旋转
            corpseObj.transform.localPosition = Vector3.zero;
            corpseObj.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            
            // 安全移除不需要的组件
            SafeRemoveComponents(corpseObj);
            
            // 灰化所有渲染器
            SafeMakeRenderersGrey(corpseObj);
            
            // 在容器上添加碰撞体
            SphereCollider col = corpseContainer.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f;
            
            // 添加血池到容器
            AddBloodPool(corpseContainer);
            
            // 在容器上添加DeadEnemy组件
            DeadEnemy deadEnemy = corpseContainer.AddComponent<DeadEnemy>();
            deadEnemy.level = level;
            deadEnemy.healAmount = healAmount;
            deadEnemy.evoPoints = evoPoints;
            
            // 添加调试指示器到容器
            if (useDebugMaterial)
            {
                AddDebugIndicator(corpseContainer);
            }
            
            Debug.Log($"成功复制敌人为尸体: {corpseContainer.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"复制敌人失败: {e.Message}\n{e.StackTrace}");
            CreateSimpleCorpse(); // 使用CreateSimpleCorpse替代CreateTestCorpse
        }
    }
    
    // 安全移除不需要的组件
    private void SafeRemoveComponents(GameObject target)
    {
        try
        {
            // 使用列表存储要移除的组件，避免在遍历时修改集合
            System.Collections.Generic.List<Component> componentsToRemove = 
                new System.Collections.Generic.List<Component>();
            
            // 禁用脚本组件而不是删除
            MonoBehaviour[] scripts = target.GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null && !(script is DeadEnemy))
                {
                    script.enabled = false;
                }
            }
            
            // 收集要移除的组件
            UnityEngine.AI.NavMeshAgent agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) componentsToRemove.Add(agent);
            
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null) componentsToRemove.Add(rb);
            
            Collider[] colliders = target.GetComponents<Collider>();
            foreach (Collider col in colliders)
            {
                if (col != null)
                {
                    componentsToRemove.Add(col);
                }
            }
            
            // 一次性移除所有组件
            foreach (Component comp in componentsToRemove)
            {
                if (comp != null)
                {
                    Destroy(comp);
                }
            }
            
            // 处理子对象
            Transform[] children = target.GetComponentsInChildren<Transform>();
            foreach (Transform child in children)
            {
                if (child == target.transform) continue;
                
                string childName = child.name.ToLower();
                if (childName.Contains("canvas") || 
                    childName.Contains("ui") || 
                    childName.Contains("bar") ||
                    childName.Contains("health"))
                {
                    Destroy(child.gameObject);
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"移除组件时出错: {e.Message}");
        }
    }
    
    // 安全地将渲染器变灰
    private void SafeMakeRenderersGrey(GameObject target)
    {
        try
        {
            // 使用HashSet避免重复处理
            System.Collections.Generic.HashSet<Renderer> processedRenderers = 
                new System.Collections.Generic.HashSet<Renderer>();
            
            // 一次性获取所有渲染器
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
            
            // 创建共享的灰色材质
            Material greyMaterial = new Material(Shader.Find("Standard"));
            greyMaterial.color = corpseColor;
            
            foreach (Renderer renderer in renderers)
            {
                if (renderer == null || 
                    processedRenderers.Contains(renderer) || 
                    renderer.gameObject.name.Contains("Blood"))
                    continue;
                
                processedRenderers.Add(renderer);
                
                // 使用共享材质
                Material[] mats = new Material[renderer.materials.Length];
                for (int i = 0; i < mats.Length; i++)
                {
                    mats[i] = greyMaterial;
                }
                renderer.materials = mats;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"灰化渲染器时出错: {e.Message}");
        }
    }
    
    // 寻找最近的敌人
    private GameObject FindNearestEnemy()
    {
        // 查找所有敌人
        EnemyHealth[] enemies = FindObjectsOfType<EnemyHealth>();
        if (enemies.Length == 0) return null;
        
        // 计算距离
        PlayerHealth player = FindObjectOfType<PlayerHealth>();
        Vector3 referencePos = player != null ? player.transform.position : Camera.main.transform.position;
        
        float nearestDist = float.MaxValue;
        GameObject nearestEnemy = null;
        foreach (EnemyHealth enemy in enemies)
        {
            if (enemy == null || enemy.gameObject == null) continue;
            
            float dist = Vector3.Distance(enemy.transform.position, referencePos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearestEnemy = enemy.gameObject;
            }
        }
        
        if (nearestEnemy != null)
        {
            Debug.Log($"找到最近的敌人: {nearestEnemy.name}，距离: {nearestDist:F2}米");
        }
        
        return nearestEnemy;
    }
    
    // 获取生成位置
    private Vector3 GetSpawnPosition()
    {
        if (spawnAtPlayerPosition)
        {
            PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
            if (playerHealth != null)
            {
                GameObject player = playerHealth.gameObject;
                return player.transform.position + 
                       player.transform.forward * offsetZ + 
                       Vector3.up * offsetY;
            }
        }
        
        // 如果没有玩家或不在玩家位置生成，使用指定点
        return spawnPoint != null ? spawnPoint.position : transform.position;
    }
    
    // 添加血池效果 - 简化版
    private void AddBloodPool(GameObject target)
    {
        if (!addBloodPool) return;
        
        // 检查是否已经存在血池
        Transform existingBloodPool = target.transform.Find("BloodPool");
        if (existingBloodPool != null) return;
        
        GameObject bloodPool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bloodPool.name = "BloodPool";
        bloodPool.transform.SetParent(target.transform);
        bloodPool.transform.localPosition = new Vector3(0, -0.05f, 0);
        
        // 确保血池平躺在地面上
        bloodPool.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
        
        bloodPool.transform.localScale = new Vector3(bloodPoolSize, 0.02f, bloodPoolSize);
        
        // 移除碰撞体
        Destroy(bloodPool.GetComponent<Collider>());
        
        // 设置材质
        Renderer bloodRenderer = bloodPool.GetComponent<Renderer>();
        if (bloodRenderer != null)
        {
            bloodRenderer.material.color = bloodColor;
            bloodRenderer.receiveShadows = false;
            bloodRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
    
    // 添加调试指示器 - 简化版
    private void AddDebugIndicator(GameObject target)
    {
        GameObject indicator = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicator.name = "DebugIndicator";
        indicator.transform.SetParent(target.transform);
        indicator.transform.localPosition = new Vector3(0, 1.5f, 0);
        indicator.transform.localScale = Vector3.one * 0.3f;
        
        // 移除碰撞体
        Destroy(indicator.GetComponent<Collider>());
        
        // 设置简单材质
        Renderer indicatorRenderer = indicator.GetComponent<Renderer>();
        if (indicatorRenderer != null)
        {
            indicatorRenderer.material.color = Color.magenta;
            VisualDebugPulse pulse = indicator.AddComponent<VisualDebugPulse>();
            pulse.pulseColor = Color.magenta;
        }
    }
    
    // 创建标准测试尸体
    private void CreateTestCorpse()
    {
        // 直接调用本地方法创建简单尸体
        CreateSimpleCorpse();
        Debug.Log($"已通过调试器生成测试尸体: Lv{corpseLevel}");
    }

    // 创建简单的尸体
    private void CreateSimpleCorpse()
    {
        Vector3 spawnPosition = GetSpawnPosition();
        
        // 创建容器
        GameObject corpseContainer = new GameObject($"SimpleCorpse_Container_Lv{corpseLevel}");
        corpseContainer.transform.position = spawnPosition;
        corpseContainer.transform.rotation = Quaternion.identity;
        corpseContainer.layer = LayerMask.NameToLayer("Enemy");
        
        // 创建简单的视觉模型
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.name = "SimpleCorpse_Visual";
        visual.transform.SetParent(corpseContainer.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(-90f, 180f, 0f);
        visual.transform.localScale = new Vector3(1f, 0.5f, 1f);
        
        // 移除碰撞体
        Destroy(visual.GetComponent<Collider>());
        
        // 设置材质
        Renderer visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer != null)
        {
            Material greyMaterial = new Material(Shader.Find("Standard"));
            greyMaterial.color = corpseColor;
            visualRenderer.material = greyMaterial;
        }
        
        // 在容器上添加碰撞体
        SphereCollider col = corpseContainer.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
        
        // 添加血池
        AddBloodPool(corpseContainer);
        
        // 添加DeadEnemy组件
        DeadEnemy deadEnemy = corpseContainer.AddComponent<DeadEnemy>();
        deadEnemy.level = corpseLevel;
        deadEnemy.healAmount = healAmount;
        deadEnemy.evoPoints = evoPoints;
        
        // 添加调试指示器
        if (useDebugMaterial)
        {
            AddDebugIndicator(corpseContainer);
        }
        
        Debug.Log($"成功创建简单尸体: {corpseContainer.name}");
    }
}

// 用于调试物体的脉冲效果 - 简化版
public class VisualDebugPulse : MonoBehaviour
{
    public Color pulseColor = Color.magenta;
    private Renderer renderer;
    private float pulseSpeed = 2f;
    
    void Start()
    {
        renderer = GetComponent<Renderer>();
    }
    
    void Update()
    {
        if (renderer != null)
        {
            // 创建脉冲效果
            float pulse = Mathf.PingPong(Time.time * pulseSpeed, 1f);
            Color currentColor = Color.Lerp(pulseColor * 0.5f, pulseColor * 2f, pulse);
            
            // 只修改颜色，不修改材质属性
            renderer.material.color = currentColor;
        }
    }
} 