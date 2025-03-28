using UnityEngine;
using UnityEngine.AI;

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

    // 添加新的属性用于尸体生成
    public GameObject deadEnemyPrefab; // 如果不指定，会使用当前的GameObject克隆

    private bool isDead = false;

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

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        
        try
        {
            // 先禁用组件而不是立即隐藏物体
            MonoBehaviour[] scripts = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour script in scripts)
            {
                if (script != null && script != this)
                {
                    script.enabled = false;
                }
            }
            
            // 禁用导航和刚体
            var agent = GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            
            var rb = GetComponent<Rigidbody>();
            if (rb != null) 
            {
                // 先处理刚体的碰撞检测设置再修改isKinematic，避免PhysX警告
                if (rb.collisionDetectionMode == CollisionDetectionMode.Continuous || 
                    rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic ||
                    rb.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
                rb.velocity = Vector3.zero; // 停止所有移动
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            
            // 生成尸体
            SpawnDeadBody(transform.position, transform.rotation, levelDropped, levelDropped);
            
            // 最后再隐藏物体
            gameObject.SetActive(false);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"敌人死亡处理时出错: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SpawnDeadBody(Vector3 position, Quaternion rotation, int healAmount, int evoPoints)
    {
        try
        {
            // 创建尸体容器
            GameObject corpseContainer = new GameObject($"DeadEnemy_Container_Lv{levelDropped}");
            corpseContainer.transform.position = position;
            corpseContainer.transform.rotation = Quaternion.identity;
            corpseContainer.layer = LayerMask.NameToLayer("Enemy");
            
            // 复制当前物体作为尸体模型
            GameObject corpseModel = Instantiate(gameObject, position, rotation);
            corpseModel.name = $"DeadEnemy_Model_Lv{levelDropped}";
            corpseModel.transform.SetParent(corpseContainer.transform);
            
            // 调整尸体模型的位置和旋转
            corpseModel.transform.localPosition = Vector3.zero;
            corpseModel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            
            // 安全移除不需要的组件
            SafeRemoveComponents(corpseModel);
            
            // 灰化所有渲染器
            SafeMakeRenderersGrey(corpseModel);
            
            // 在容器上添加碰撞体
            SphereCollider col = corpseContainer.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.5f;
            
            // 添加血池到容器
            AddBloodPool(corpseContainer);
            
            // 在容器上添加DeadEnemy组件
            DeadEnemy deadEnemy = corpseContainer.AddComponent<DeadEnemy>();
            deadEnemy.level = levelDropped;
            deadEnemy.healAmount = healAmount;
            deadEnemy.evoPoints = evoPoints;
            
            Debug.Log($"成功生成尸体: {corpseContainer.name}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"生成尸体时出错: {e.Message}\n{e.StackTrace}");
        }
    }

    private void SafeRemoveComponents(GameObject target)
    {
        try
        {
            // 收集要移除的组件而不是直接移除
            var componentsToRemove = new System.Collections.Generic.List<Component>();
            
            // 禁用而不是移除NavMeshAgent
            NavMeshAgent agent = target.GetComponent<NavMeshAgent>();
            if (agent != null)
            {
                agent.enabled = false;
                // 不要从列表中添加，我们只是禁用它
            }
            
            // 处理刚体 - 保留但修改设置
            Rigidbody rb = target.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 先修改碰撞检测模式，再设置isKinematic
                if (rb.collisionDetectionMode == CollisionDetectionMode.Continuous || 
                    rb.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic ||
                    rb.collisionDetectionMode == CollisionDetectionMode.ContinuousSpeculative)
                {
                    rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
                }
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
                // 不从列表中添加，保留刚体用于物理交互
            }
            
            // 收集所有可以安全移除的脚本组件
            foreach (MonoBehaviour script in target.GetComponents<MonoBehaviour>())
            {
                // 保留DeadEnemy和EnemyHealth组件
                if (script != null && 
                    !(script is DeadEnemy) && 
                    !(script is EnemyHealth) &&
                    !(script is NavMeshAgent)) // 排除NavMeshAgent，避免依赖错误
                {
                    componentsToRemove.Add(script);
                }
            }
            
            // 收集完成后，执行移除
            foreach (var component in componentsToRemove)
            {
                if (component != null)
                {
                    Destroy(component);
                }
            }
            
            // 递归处理所有子对象
            foreach (Transform child in target.transform)
            {
                if (child.gameObject.activeInHierarchy)
                {
                    SafeRemoveComponents(child.gameObject);
                }
            }
            
            Debug.Log($"安全移除了 {target.name} 的不必要组件");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"移除组件时出错: {e.Message}\n{e.StackTrace}");
        }
    }
    
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
            greyMaterial.color = new Color(0.35f, 0.35f, 0.35f, 1f);
            
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
    
    // 添加血池效果
    private void AddBloodPool(GameObject target)
    {
        try
        {
            // 创建血池
            GameObject bloodPool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            bloodPool.name = "BloodPool";
            bloodPool.transform.SetParent(target.transform);
            bloodPool.transform.localPosition = new Vector3(0, -0.05f, 0);
            
            // 更新血池旋转角度以匹配调试版本
            bloodPool.transform.localRotation = Quaternion.Euler(0f, 0f, 0f);
            
            // 设置血池大小
            bloodPool.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
            
            // 移除碰撞体
            Destroy(bloodPool.GetComponent<Collider>());
            
            // 设置血池材质
            Renderer bloodRenderer = bloodPool.GetComponent<Renderer>();
            if (bloodRenderer != null)
            {
                Material bloodMaterial = new Material(Shader.Find("Standard"));
                bloodMaterial.color = new Color(0.6f, 0f, 0f, 0.8f); // 暗红色，半透明
                bloodRenderer.material = bloodMaterial;
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"添加血池时出错: {e.Message}");
        }
    }

    // 已移除的方法依然保留空壳，以防其他代码引用
    private void RemoveUnwantedComponents(GameObject target)
    {
        // 方法被移除以避免性能问题 - 使用SafeRemoveComponents替代
    }
    
    private void MakeRendererGrey(Renderer renderer)
    {
        // 方法被移除以避免性能问题 - 使用SafeMakeRenderersGrey替代
    }
    
    private void CreateFallbackVisual(GameObject target)
    {
        // 方法被移除，改用CreateSimpleCorpseVisual
    }
}