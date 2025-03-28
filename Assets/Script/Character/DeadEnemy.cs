using UnityEngine;
using System.Collections;
using UnityEngine.AI;

// 表示敌人死亡后留下的尸体，可以被玩家或其他敌人收集
public class DeadEnemy : MonoBehaviour
{
    [Header("尸体属性")]
    public int level = 1;            // 敌人等级
    public int healAmount = 20;      // 提供的治疗量
    public int evoPoints = 10;       // 提供的进化点数
    
    [Header("尸体状态")]
    public bool isCollectable = false;  // 是否可被收集
    public bool isFading = false;       // 是否正在消失
    public float fadeTime = 2.0f;       // 消失时间
    public float disappearTime = 60.0f; // 尸体自动消失时间
    
    // 视觉效果参数
    private float initialOpacity = 1.0f;
    private System.Collections.Generic.HashSet<Renderer> cachedRenderers;
    
    void Awake()
    {
        // Awake中不做任何操作，所有初始化移到Start中
    }
    
    void Start()
    {
        Debug.Log($"DeadEnemy.Start: 尸体初始化 Lv{level}, 治疗={healAmount}, EvoP={evoPoints}");
        
        // 确保有血池效果
        EnsureBloodPool();
        
        // 缓存所有渲染器组件，避免重复获取
        CacheRenderers();
        
        // 设置尸体自动消失的计时器
        if (disappearTime > 0)
        {
            StartCoroutine(AutoDisappear());
        }
    }
    
    // 确保尸体有血池效果
    private void EnsureBloodPool()
    {
        // 检查是否已经有血池
        Transform existingBloodPool = transform.Find("BloodPool");
        if (existingBloodPool == null)
        {
            // 没有找到血池，创建一个新的
            CreateBloodPool();
        }
    }
    
    // 创建血池效果 - 简化版
    private void CreateBloodPool()
    {
        GameObject bloodPool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bloodPool.name = "BloodPool";
        bloodPool.transform.SetParent(transform);
        bloodPool.transform.localPosition = new Vector3(0, -0.05f, 0); // 略微下沉到地面
        
        // 确保血池平躺在地面上
        bloodPool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        
        bloodPool.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f); // 扁平的圆柱体
        
        // 移除碰撞体，只保留视觉效果
        Destroy(bloodPool.GetComponent<Collider>());
        
        // 设置血池材质
        Renderer bloodRenderer = bloodPool.GetComponent<Renderer>();
        if (bloodRenderer != null)
        {
            // 简化：直接修改材质颜色，不创建新材质
            bloodRenderer.material.color = new Color(0.5f, 0.0f, 0.0f, 0.8f); // 深红色，半透明
            bloodRenderer.receiveShadows = false;
            bloodRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        
        Debug.Log("为尸体创建了血池效果");
    }
    
    // 缓存所有渲染器
    private void CacheRenderers()
    {
        cachedRenderers = new System.Collections.Generic.HashSet<Renderer>();
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        
        foreach (Renderer renderer in renderers)
        {
            if (renderer != null && !renderer.gameObject.name.Contains("Blood"))
            {
                cachedRenderers.Add(renderer);
            }
        }
        
        Debug.Log($"已缓存 {cachedRenderers.Count} 个渲染器组件");
    }
    
    // 当玩家进入触发器范围时
    void OnTriggerEnter(Collider other)
    {
        if (isFading) return;
        
        // 检测是否是玩家
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            isCollectable = true;
            Debug.Log($"玩家可以收集尸体 Lv{level}");
        }
    }
    
    // 当玩家离开触发器范围时
    void OnTriggerExit(Collider other)
    {
        if (isFading) return;
        
        // 检测是否是玩家
        PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            isCollectable = false;
            Debug.Log("玩家离开了尸体收集范围");
        }
    }
    
    // 尸体被吃掉
    public void Consume()
    {
        if (isFading) return;
        
        Debug.Log($"尸体被消费：Lv{level}，治疗量={healAmount}，进化点数={evoPoints}");
        StartCoroutine(FadeOut());
    }
    
    // 淡出效果
    private IEnumerator FadeOut()
    {
        isFading = true;
        isCollectable = false;
        float elapsedTime = 0;
        
        // 确保在淡出前已缓存渲染器
        if (cachedRenderers == null || cachedRenderers.Count == 0)
        {
            CacheRenderers();
        }
        
        // 动态淡出逻辑
        while (elapsedTime < fadeTime)
        {
            float alpha = 1.0f - (elapsedTime / fadeTime);
            
            // 使用预缓存的渲染器列表
            foreach (Renderer renderer in cachedRenderers)
            {
                if (renderer == null) continue;
                
                Material[] mats = renderer.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null) continue;
                    
                    Color color = mats[i].color;
                    mats[i].color = new Color(color.r, color.g, color.b, color.a * alpha);
                }
                renderer.materials = mats;
            }
            
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // 淡出完成后销毁物体
        Debug.Log("尸体已完全消失");
        Destroy(gameObject);
    }
    
    // 尸体自动消失
    private IEnumerator AutoDisappear()
    {
        Debug.Log($"尸体将在 {disappearTime} 秒后自动消失");
        
        // 等待指定时间
        yield return new WaitForSeconds(disappearTime * 0.9f);
        
        // 如果尸体还没被消费，开始闪烁提示即将消失
        if (!isFading)
        {
            Debug.Log("尸体即将自动消失，开始闪烁提示");
            float blinkTime = disappearTime * 0.1f;
            float elapsedTime = 0;
            bool isVisible = true;
            
            // 如果尚未缓存渲染器，现在缓存
            if (cachedRenderers == null || cachedRenderers.Count == 0)
            {
                CacheRenderers();
            }
            
            // 闪烁效果
            while (elapsedTime < blinkTime)
            {
                isVisible = !isVisible;
                
                foreach (Renderer renderer in cachedRenderers)
                {
                    if (renderer == null) continue;
                    renderer.enabled = isVisible;
                }
                
                elapsedTime += 0.2f;
                yield return new WaitForSeconds(0.2f);
            }
            
            // 开始淡出
            StartCoroutine(FadeOut());
        }
    }

    // 供敌人AI调用的进食方法
    public void EnemyConsumeDead(GameObject consumer)
    {
        if (consumer == null)
        {
            Debug.LogError("EnemyConsumeDead: 参数consumer为null");
            return;
        }
        
        Debug.Log($"敌人{consumer.name}消费尸体");
        
        EnemyAIExtended ai = consumer.GetComponent<EnemyAIExtended>();
        if (ai != null)
        {
            // 通知敌人AI已经吃掉了尸体
            ai.ConsumeDeadEnemy();
            Debug.Log($"🍖 敌人Lv{ai.level}吃掉了Lv{level}的尸体！");
        }
        else
        {
            Debug.LogWarning($"找不到EnemyAIExtended组件，敌人{consumer.name}无法进食");
        }

        // 销毁尸体
        Debug.Log("尸体被敌人消费，即将销毁");
        SafeDestroy();
    }

    // 安全销毁方法
    private void SafeDestroy()
    {
        try
        {
            Debug.Log($"安全销毁尸体: {gameObject.name}");
            Destroy(gameObject);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"销毁尸体时出错: {e.Message}");
        }
    }

    // 自动销毁计时器
    IEnumerator AutoDestroy(float time)
    {
        Debug.Log($"尸体将在{time}秒后自动销毁");
        yield return new WaitForSeconds(time);
        
        // 开始淡出效果
        Debug.Log("开始尸体淡出效果");
        StartFading();
        
        // 2秒后销毁
        yield return new WaitForSeconds(2.0f);
        Debug.Log("尸体自动销毁时间到");
        SafeDestroy();
    }
    
    // 开始淡出效果
    private void StartFading()
    {
        float fadeStartTime = Time.time;
        isFading = true;
        Debug.Log("开始淡出效果");
    }
    
    // 检查尸体是否在特定位置附近
    public bool IsNearPosition(Vector3 position, float distance)
    {
        return Vector3.Distance(transform.position, position) <= distance;
    }
    
    void OnDestroy()
    {
        Debug.Log($"DeadEnemy.OnDestroy: 尸体被销毁 ID={GetInstanceID()}, 名称={gameObject.name}");
    }

    // 用于调试的静态方法，方便直接创建测试尸体
    public static DeadEnemy CreateTestCorpse(Vector3 position, int level, int healAmount, int evoPoints)
    {
        Debug.Log($"正在创建测试尸体：Lv{level}，位置={position}");
        
        // 创建尸体对象
        GameObject corpseObj = new GameObject($"TestCorpse_Lv{level}");
        corpseObj.transform.position = position;
        corpseObj.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // 横躺
        corpseObj.layer = LayerMask.NameToLayer("Enemy");
        
        // 添加碰撞器
        SphereCollider col = corpseObj.AddComponent<SphereCollider>();
        col.isTrigger = true;
        col.radius = 1.5f;
        
        // 添加简单视觉元素 - 横躺的胶囊体
        GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        visual.transform.SetParent(corpseObj.transform);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.Euler(0, 0, 90); // 横躺
        visual.transform.localScale = new Vector3(1f, 0.5f, 1f);
        
        // 移除碰撞体
        Destroy(visual.GetComponent<Collider>());
        
        // 设置简单的灰色材质
        Renderer visualRenderer = visual.GetComponent<Renderer>();
        if (visualRenderer != null)
        {
            visualRenderer.material.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        }
        
        // 添加血池
        AddBloodPoolStatic(corpseObj);
        
        // 添加DeadEnemy组件
        DeadEnemy deadEnemy = corpseObj.AddComponent<DeadEnemy>();
        deadEnemy.level = level;
        deadEnemy.healAmount = healAmount;
        deadEnemy.evoPoints = evoPoints;
        
        Debug.Log($"测试尸体创建成功: {corpseObj.name}");
        
        return deadEnemy;
    }
    
    // 静态方法添加血池
    private static void AddBloodPoolStatic(GameObject target)
    {
        GameObject bloodPool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        bloodPool.name = "BloodPool";
        bloodPool.transform.SetParent(target.transform);
        bloodPool.transform.localPosition = new Vector3(0, -0.05f, 0);
        bloodPool.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bloodPool.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f);
        
        // 移除碰撞体
        Destroy(bloodPool.GetComponent<Collider>());
        
        // 设置材质
        Renderer bloodRenderer = bloodPool.GetComponent<Renderer>();
        if (bloodRenderer != null)
        {
            bloodRenderer.material.color = new Color(0.5f, 0f, 0f, 0.8f);
            bloodRenderer.receiveShadows = false;
            bloodRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }

    void Update()
    {
        // 玩家按J键可以食用尸体
        if (isCollectable && Input.GetKeyDown(KeyCode.J))
        {
            Debug.Log("玩家按下J键，开始进食尸体");
            PlayerConsumeDead();
        }
    }

    // 玩家食用尸体
    void PlayerConsumeDead()
    {
        Debug.Log("PlayerConsumeDead: 开始处理玩家进食逻辑");
        
        // 找到玩家但不使用tag
        GameObject player = FindObjectOfType<PlayerHealth>()?.gameObject;
        
        PlayerHealth playerHealth = player?.GetComponent<PlayerHealth>();
        PlayerMovement playerMovement = player?.GetComponent<PlayerMovement>();
        PlayerEvolution playerEvolution = player?.GetComponent<PlayerEvolution>();

        if (playerHealth != null)
        {
            // 使用专门的方法根据尸体等级恢复生命值
            playerHealth.HealFromEnemy(healAmount);
            Debug.Log($"玩家恢复生命值成功，恢复量: {healAmount}");
        }
        else
        {
            Debug.LogWarning("找不到PlayerHealth组件，无法恢复生命值");
        }

        if (playerEvolution != null)
        {
            // 增加进化点数
            playerEvolution.AddEvolutionPoints(evoPoints);
            Debug.Log($"⚡ 玩家食用Lv{level}的尸体，获得了{evoPoints}进化点数！");
        }
        else
        {
            Debug.LogWarning("找不到PlayerEvolution组件，无法增加进化点数");
        }

        if (playerMovement != null)
        {
            // 食用时让玩家静止一会
            playerMovement.Stun(1f);
            Debug.Log("玩家正在进食，暂时不能移动...");
        }
        else
        {
            Debug.LogWarning("找不到PlayerMovement组件，无法使玩家静止");
        }
        
        // 销毁尸体
        StartCoroutine(FadeOut());
    }
} 