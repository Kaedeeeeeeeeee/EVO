using UnityEngine;
using System.Collections;
using UnityEngine.AI;
using UnityEngine.UI; // 添加UI命名空间

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

    [Header("进食系统")]
    public float consumeDuration = 5.0f;  // 完全吃掉尸体需要的时间
    public float currentConsumeProgress = 0f;   // 当前进食进度 (0-1)
    public bool isBeingConsumed = false;      // 是否正在被吃
    
    // 资源控制
    private float lastResourceTime = 0f;      // 上次提供资源的时间
    private float resourceInterval = 0.5f;    // 资源提供的时间间隔（秒）
    private float totalHealProvided = 0f;     // 已经提供的总治疗量
    private float totalEvoProvided = 0f;      // 已经提供的总进化点数
    private float targetHealAmount = 0f;      // 目标治疗总量
    private float targetEvoAmount = 0f;       // 目标进化点总量
    
    [Header("进度条")]
    public GameObject progressBarPrefab;    // 进度条预制体
    private GameObject progressBarInstance; // 进度条实例
    private Image progressFillImage;        // 进度条填充图像
    private float lastConsumptionTime;      // 上次进食时间
    
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
        
        // 初始化进度条但不显示
        CreateProgressBar();
    }
    
    // 创建进度条
    private void CreateProgressBar()
    {
        // 如果已有进度条实例，先销毁
        if (progressBarInstance != null)
        {
            Destroy(progressBarInstance);
        }
        
        // 创建一个简单的平面作为进度条
        GameObject progressObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        progressObj.name = "EatingProgressBar";
        
        // 设置位置和大小 - 尺寸扩大一倍
        progressObj.transform.SetParent(transform);
        progressObj.transform.localPosition = new Vector3(0, 2.0f, 0); // 位于尸体上方2单位
        progressObj.transform.localScale = new Vector3(2.0f, 0.2f, 0.1f); // 长方形进度条，尺寸扩大一倍
        
        // 去掉碰撞器
        Destroy(progressObj.GetComponent<Collider>());
        
        // 获取渲染器并设置为红色
        Renderer renderer = progressObj.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material = new Material(Shader.Find("Standard"));
            renderer.material.color = Color.red;
            renderer.receiveShadows = false;
        }
        
        // 保存引用
        progressBarInstance = progressObj;
        progressFillImage = null; // 不使用Image组件，而是直接调整Scale
        
        // 隐藏进度条
        progressBarInstance.SetActive(false);
        
        Debug.Log("创建了简化版进度条，尺寸扩大一倍");
    }
    
    // 更新进度条显示
    private void UpdateProgressBar(float progress)
    {
        if (progressBarInstance == null) return;
        
        // 计算剩余比例 (1.0 - progress)
        float remaining = 1.0f - progress;
        
        // 更新进度条的缩放
        Vector3 scale = progressBarInstance.transform.localScale;
        scale.x = remaining * 2.0f; // 只改变X轴缩放，保持扩大一倍的尺寸
        progressBarInstance.transform.localScale = scale;
        
        // 更新颜色 (从红色渐变到黑色)
        Renderer renderer = progressBarInstance.GetComponent<Renderer>();
        if (renderer != null && renderer.material != null)
        {
            renderer.material.color = Color.Lerp(Color.black, Color.red, remaining);
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
        // 使用GameObject.CreatePrimitive创建简单形状作为血池
        CreateSimpleBloodPool(this.gameObject);
    }
    
    // 缓存渲染器组件
    private void CacheRenderers()
    {
        if (cachedRenderers == null)
        {
            cachedRenderers = new System.Collections.Generic.HashSet<Renderer>();
            
            // 获取当前对象及其所有子对象上的渲染器
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            foreach (Renderer renderer in renderers)
            {
                cachedRenderers.Add(renderer);
            }
        }
    }
    
    // 处理淡出效果
    private IEnumerator FadeOut()
    {
        if (isFading) yield break;
        
        isFading = true;
        float startTime = Time.time;
        
        // 隐藏进度条
        if (progressBarInstance != null)
        {
            progressBarInstance.SetActive(false);
        }
        
        // 如果尚未缓存渲染器，现在缓存
        if (cachedRenderers == null || cachedRenderers.Count == 0)
        {
            CacheRenderers();
        }
        
        // 记录初始材质的透明度值
        foreach (Renderer renderer in cachedRenderers)
        {
            if (renderer == null) continue;
            
            Material mat = renderer.material;
            Color color = mat.color;
            initialOpacity = color.a;
        }
        
        // 随时间渐变透明度
        while (Time.time - startTime < fadeTime)
        {
            float t = (Time.time - startTime) / fadeTime;
            
            foreach (Renderer renderer in cachedRenderers)
            {
                if (renderer == null) continue;
                
                Material mat = renderer.material;
                Color color = mat.color;
                color.a = Mathf.Lerp(initialOpacity, 0, t);
                mat.color = color;
            }
            
            yield return null;
        }
        
        // 完全透明后销毁
        Destroy(gameObject);
    }
    
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
            isBeingConsumed = false;
            
            // 不再隐藏进度条，让玩家看到进食进度保留
            // 只在玩家离开时取消眩晕状态
            GameObject player = other.gameObject;
            PlayerMovement playerMovement = player?.GetComponent<PlayerMovement>();
            if (playerMovement != null)
            {
                playerMovement.CancelStun();
            }
            
            Debug.Log("玩家离开了尸体收集范围，进度条保持显示");
        }
    }
    
    // 尸体被吃掉
    public void Consume()
    {
        if (isFading) return;
        
        Debug.Log($"尸体被消费：Lv{level}，治疗量={healAmount}，进化点数={evoPoints}");
        StartCoroutine(FadeOut());
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
        Debug.Log("DeadEnemy.OnDestroy: 尸体被销毁");
    }

    private void CreateSimpleBloodPool(GameObject target)
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
        // 处理持续按J键进食系统
        if (isCollectable && Input.GetKey(KeyCode.J))
        {
            // 开始/继续进食
            if (!isBeingConsumed)
            {
                StartConsumption();
            }
            
            // 增加进食进度
            ContinueConsumption();
        }
        else if (isBeingConsumed)
        {
            // 玩家停止按J键，暂停进食
            StopConsumption();
        }
        
        // 确保进度条始终朝向相机，无论是否激活
        if (progressBarInstance != null)
        {
            progressBarInstance.transform.LookAt(Camera.main.transform.position);
        }
    }
    
    // 开始进食
    private void StartConsumption()
    {
        isBeingConsumed = true;
        lastConsumptionTime = Time.time;
        lastResourceTime = Time.time;
        
        // 重置资源控制变量
        totalHealProvided = 0f;
        totalEvoProvided = 0f;
        targetHealAmount = healAmount;
        targetEvoAmount = evoPoints;
        
        // 显示进度条
        if (progressBarInstance != null)
        {
            progressBarInstance.SetActive(true);
        }
        
        // 使玩家静止
        GameObject player = FindObjectOfType<PlayerHealth>()?.gameObject;
        PlayerMovement playerMovement = player?.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.Stun(consumeDuration); // 使用尸体总进食时间作为眩晕时间
            Debug.Log("玩家开始进食，暂时无法移动");
        }
        
        Debug.Log($"玩家开始进食尸体 Lv{level}，总治疗量上限={targetHealAmount}，总进化点上限={targetEvoAmount}");
    }
    
    // 继续进食
    private void ContinueConsumption()
    {
        // 计算增加的进度
        float deltaTime = Time.time - lastConsumptionTime;
        float progressIncrement = deltaTime / consumeDuration;
        
        // 更新进度
        currentConsumeProgress += progressIncrement;
        currentConsumeProgress = Mathf.Clamp01(currentConsumeProgress); // 确保不超过1
        lastConsumptionTime = Time.time;
        
        // 更新进度条显示
        UpdateProgressBar(currentConsumeProgress);
        
        // 按照固定时间间隔提供资源，而不是每帧都提供
        if (Time.time - lastResourceTime >= resourceInterval)
        {
            ProvideResourcesBasedOnProgress();
            lastResourceTime = Time.time;
        }
        
        // 检查是否完成
        if (currentConsumeProgress >= 1.0f)
        {
            CompleteConsumption();
        }
    }
    
    // 停止进食
    private void StopConsumption()
    {
        isBeingConsumed = false;
        
        // 不再隐藏进度条，让它一直显示
        // 进度条会显示当前进食进度，方便玩家下次继续
        
        // 恢复玩家移动能力
        GameObject player = FindObjectOfType<PlayerHealth>()?.gameObject;
        PlayerMovement playerMovement = player?.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.CancelStun(); // 添加此方法到PlayerMovement脚本
            Debug.Log("玩家中断进食，恢复移动能力");
        }
        
        Debug.Log($"玩家暂停进食尸体，当前进度: {currentConsumeProgress * 100:F1}%，进度条保持显示");
    }
    
    // 完成进食
    private void CompleteConsumption()
    {
        Debug.Log("玩家完成进食尸体");
        
        // 恢复玩家移动能力
        GameObject player = FindObjectOfType<PlayerHealth>()?.gameObject;
        PlayerMovement playerMovement = player?.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.CancelStun();
            Debug.Log("玩家完成进食，恢复移动能力");
        }
        
        // 完全消费尸体
        StartCoroutine(FadeOut());
    }
    
    // 根据进度提供资源（生命值和进化点数）
    private void ProvideResourcesBasedOnProgress()
    {
        // 找到玩家
        GameObject player = FindObjectOfType<PlayerHealth>()?.gameObject;
        PlayerHealth playerHealth = player?.GetComponent<PlayerHealth>();
        PlayerEvolution playerEvolution = player?.GetComponent<PlayerEvolution>();
        
        // 根据当前进度计算应提供的资源总量
        float progressRatio = currentConsumeProgress;
        float targetHealSoFar = targetHealAmount * progressRatio;
        float targetEvoSoFar = targetEvoAmount * progressRatio;
        
        // 计算本次应该提供的增量资源
        int healToProvide = Mathf.CeilToInt(targetHealSoFar - totalHealProvided);
        int evoToProvide = Mathf.CeilToInt(targetEvoSoFar - totalEvoProvided);
        
        // 提供资源（只提供正增量）
        if (playerHealth != null && healToProvide > 0)
        {
            playerHealth.Heal(healToProvide);
            totalHealProvided += healToProvide;
            Debug.Log($"玩家获得 {healToProvide} 生命值，进度 {(progressRatio*100):F1}%，累计已获得 {totalHealProvided}/{targetHealAmount}");
        }
        
        if (playerEvolution != null && evoToProvide > 0)
        {
            playerEvolution.AddEvolutionPoints(evoToProvide);
            totalEvoProvided += evoToProvide;
            Debug.Log($"玩家获得 {evoToProvide} 进化点数，进度 {(progressRatio*100):F1}%，累计已获得 {totalEvoProvided}/{targetEvoAmount}");
        }
    }

    // 玩家食用尸体 - 原来的方法，保留但不再使用
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