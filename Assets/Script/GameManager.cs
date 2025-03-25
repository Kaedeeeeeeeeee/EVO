using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using UnityEngine.EventSystems;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("游戏设置")]
    [SerializeField] private float maxGameTime = 1200f; // 20分钟 = 1200秒
    
    [Header("UI引用")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject victoryPanel;
    [SerializeField] private TextMeshProUGUI gameOverText;
    [SerializeField] private GameObject fireworksEffectLeft;
    [SerializeField] private GameObject fireworksEffectRight;

    private float currentGameTime;
    private bool isGameOver = false;
    private PlayerHealth playerHealth;
    private bool isProcessingRestart = false;

    private void Awake()
    {
        // 确保场景中只有一个GameManager
        GameManager[] managers = FindObjectsOfType<GameManager>();
        if (managers.Length > 1)
        {
            // 如果已经有GameManager，销毁这个新实例
            Destroy(gameObject);
            return;
        }
        
        // 单例设置
        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        // 确保UI面板初始状态为隐藏
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (fireworksEffectLeft != null) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight != null) fireworksEffectRight.SetActive(false);
        
        // 添加场景加载事件监听
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 无论加载什么场景，都确保UI面板正确隐藏
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (fireworksEffectLeft != null) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight != null) fireworksEffectRight.SetActive(false);
        
        // 场景加载完成后重新初始化
        if (scene.name == "GameScene")
        {
            // 初始化游戏场景
            InitializeGameScene();
            
            // 重新初始化UI引用和重新绑定按钮
            InitializeUIReferences();
            
            Debug.Log("场景加载完成，UI重新初始化");
        }
    }

    private void InitializeGameScene()
    {
        // 重置游戏状态
        currentGameTime = maxGameTime;
        isGameOver = false;
        
        // 重新查找引用
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        
        // 确保所有UI面板被隐藏
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (fireworksEffectLeft != null) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight != null) fireworksEffectRight.SetActive(false);
        
        // 更新计时器显示
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(currentGameTime / 60);
            int seconds = Mathf.FloorToInt(currentGameTime % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
        
        // 初始化游戏UI
        InitializeUIReferences();
        
        // 设置必要的标签
        if (gameOverPanel != null)
        {
            gameOverPanel.tag = "UIPanel";
        }
        
        if (victoryPanel != null)
        {
            victoryPanel.tag = "UIPanel";
        }
        
        // 检查并确保地形生成
        TerrainGenerator terrainGen = TerrainGenerator.Instance;
        if (terrainGen != null)
        {
            Debug.Log("初始化游戏场景: 找到TerrainGenerator");
            
            // 如果是从主菜单首次进入，强制重新生成地形
            if (SceneManager.GetActiveScene().name == "GameScene" && 
                SceneManager.GetActiveScene().buildIndex != SceneManager.GetSceneByName("MainMenu").buildIndex)
            {
                Debug.Log("首次从主菜单进入游戏，强制重新生成地形");
                StartCoroutine(FirstTimeSceneSetup(terrainGen));
            }
        }
        else
        {
            Debug.LogWarning("初始化游戏场景: 未找到TerrainGenerator");
        }
        
        Debug.Log("游戏场景已初始化，UI面板已隐藏");
    }
    
    // 首次从主菜单进入时的特殊设置
    private IEnumerator FirstTimeSceneSetup(TerrainGenerator terrainGen)
    {
        // 等待几帧确保场景完全加载
        yield return null;
        yield return null;
        
        // 强制重新生成地形
        Debug.Log("首次场景设置: 强制重新生成地形");
        terrainGen.RegenerateMap();
        
        // 等待地形生成完成
        yield return new WaitForSeconds(0.5f);
        
        // 初始化敌人生成器
        EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner != null)
        {
            Debug.Log("首次场景设置: 重新初始化敌人生成器");
            spawner.ReInitialize();
            
            // 再次延迟，确保初始化完成
            yield return null;
            
            // 手动启动敌人生成
            spawner.SendMessage("StartSpawning", null, SendMessageOptions.DontRequireReceiver);
        }
        else
        {
            Debug.LogWarning("首次场景设置: 未找到敌人生成器");
        }
    }

    private void Start()
    {
        currentGameTime = maxGameTime;
        playerHealth = FindFirstObjectByType<PlayerHealth>();
        gameOverPanel.SetActive(false);
        victoryPanel.SetActive(false);
        if (fireworksEffectLeft) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight) fireworksEffectRight.SetActive(false);
    }

    private void Update()
    {
        // 调试信息
        if (timerText == null)
        {
            Debug.LogWarning("timerText引用丢失，尝试重新查找");
            timerText = GameObject.Find("TimerText")?.GetComponent<TextMeshProUGUI>();
        }
        
        if (!isGameOver)
        {
            UpdateTimer();
            CheckGameOver();
        }
    }

    private void UpdateTimer()
    {
        currentGameTime -= Time.deltaTime;
        
        if (currentGameTime <= 0)
        {
            currentGameTime = 0;
            Victory();
        }

        int minutes = Mathf.FloorToInt(currentGameTime / 60);
        int seconds = Mathf.FloorToInt(currentGameTime % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void CheckGameOver()
    {
        if (playerHealth != null && playerHealth.GetCurrentHealth() <= 0)
        {
            GameOver();
        }
    }

    private void Victory()
    {
        // 防止重复调用
        if (isGameOver) return;
        
        isGameOver = true;
        
        // 确保胜利面板存在并初始化
        if (victoryPanel == null)
        {
            InitializeUIReferences();
        }
        
        // 激活胜利面板
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
            
            // 重新检查按钮事件绑定
            Button[] buttons = victoryPanel.GetComponentsInChildren<Button>(true);
            bool needsRebind = false;
            
            foreach (Button btn in buttons)
            {
                if (btn.onClick.GetPersistentEventCount() == 0)
                {
                    needsRebind = true;
                    break;
                }
            }
            
            // 如果发现按钮没有事件，重新绑定所有按钮
            if (needsRebind)
            {
                Debug.Log("胜利面板按钮事件丢失，重新绑定");
                foreach (Button btn in buttons)
                {
                    if (btn.name.Contains("NewGame") || btn.name.Contains("Restart"))
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(RestartGame);
                    }
                    else if (btn.name.Contains("Menu") || btn.name.Contains("Return"))
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(ReturnToMenu);
                    }
                }
            }
        }
        
        if (fireworksEffectLeft) fireworksEffectLeft.SetActive(true);
        if (fireworksEffectRight) fireworksEffectRight.SetActive(true);
    }

    public void GameOver()
    {
        isGameOver = true;
        gameOverPanel.SetActive(true);
        
        // 替代方案：使用简单的灰色图像效果
        // 创建一个全屏的灰色半透明面板
        Image grayOverlay = gameOverPanel.GetComponent<Image>();
        if (grayOverlay != null)
        {
            grayOverlay.color = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        }
    }

    public void ReturnToMenu()
    {
        // 禁用所有UI面板
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (victoryPanel != null) victoryPanel.SetActive(false);
        if (fireworksEffectLeft != null) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight != null) fireworksEffectRight.SetActive(false);
        
        // 执行场景变更前的清理
        PrepareForSceneChange();
        
        // 直接加载主菜单场景，避免使用UnloadSceneAsync
        SceneManager.LoadScene("MainMenu");
    }

    public void RestartGame()
    {
        // 防止多次点击
        if (isProcessingRestart)
        {
            Debug.Log("已经在处理重启，忽略重复点击");
            return;
        }
        
        isProcessingRestart = true;
        Debug.Log("重启游戏: 开始执行完全重置流程");
        
        // 立即隐藏相关UI面板
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(false);
            // 不要立即销毁，只是隐藏
        }
        
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(false);
            // 不要立即销毁，只是隐藏
        }
        
        // 重置游戏状态
        isGameOver = false;
        
        // 先进行完全重置
        PrepareForFullReset();
        
        // 加载游戏场景
        Debug.Log("准备加载游戏场景...");
        try
        {
            SceneManager.sceneLoaded += OnGameSceneReloaded;
            SceneManager.LoadScene("GameScene");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"加载游戏场景时出错: {e.Message}");
            isProcessingRestart = false; // 出错时重置标志
        }
    }

    // 添加一个新方法处理游戏场景重新加载
    private void OnGameSceneReloaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"游戏场景'{scene.name}'已重新加载");
        
        // 重置重启处理标志
        isProcessingRestart = false;
        
        // 重新初始化UI引用和按钮事件
        InitializeUIReferences();
        
        // 确保所有UI被正确初始化
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"刷新Canvas: {canvas.name}");
            canvas.enabled = false;
            canvas.enabled = true;
        }
        
        // 移除监听器以避免多次调用
        SceneManager.sceneLoaded -= OnGameSceneReloaded;
        
        // 延迟一帧确保所有组件都被正确初始化
        StartCoroutine(DelayedRestartCleanup());
    }
    
    private IEnumerator DelayedRestartCleanup()
    {
        yield return null;
        
        // 在场景加载后再次隐藏所有不需要的UI面板
        GameObject[] allPanels = GameObject.FindGameObjectsWithTag("UIPanel");
        foreach (GameObject panel in allPanels)
        {
            if (panel.name.Contains("GameOver") || panel.name.Contains("Victory"))
            {
                Debug.Log($"场景加载后隐藏面板: {panel.name}");
                panel.SetActive(false);
            }
        }
        
        // 强制TerrainGenerator重新生成地形
        TerrainGenerator terrainGen = TerrainGenerator.Instance;
        if (terrainGen != null)
        {
            Debug.Log("场景加载后强制重新生成地形");
            terrainGen.RegenerateMap();
            
            // 等待一小段时间，确保地形生成完成
            yield return new WaitForSeconds(0.5f);
            
            // 检查是否有敌人生成器
            EnemySpawner enemySpawner = FindFirstObjectByType<EnemySpawner>();
            if (enemySpawner == null)
            {
                Debug.LogWarning("未找到敌人生成器，尝试创建新的...");
                
                // 尝试找到EnemySpawner预制体
                GameObject enemySpawnerPrefab = Resources.Load<GameObject>("EnemySpawner");
                if (enemySpawnerPrefab != null)
                {
                    // 实例化敌人生成器
                    GameObject newSpawnerObj = Instantiate(enemySpawnerPrefab);
                    newSpawnerObj.name = "EnemySpawner";
                    
                    Debug.Log("成功创建新的敌人生成器");
                }
                else
                {
                    Debug.LogError("无法从Resources加载EnemySpawner预制体");
                    
                    // 作为备选方案，找到场景中可能被禁用的敌人生成器
                    EnemySpawner disabledSpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
                    if (disabledSpawner != null)
                    {
                        disabledSpawner.gameObject.SetActive(true);
                        Debug.Log("启用了被禁用的敌人生成器");
                    }
                }
            }
            else 
            {
                // 如果找到了敌人生成器，确保它接收到地形生成完成事件
                Debug.Log("找到敌人生成器，手动重新初始化");
                
                // 延迟一帧再调用，确保地形真正生成完毕
                yield return null;
                
                // 调用敌人生成器的重新初始化方法
                enemySpawner.ReInitialize();
                
                // 再次延迟一帧，确保所有初始化都完成
                yield return null;
                
                // 如果TerrainGenerator已经完成生成，手动调用StartSpawning
                if (!terrainGen.isGenerating)
                {
                    Debug.Log("地形生成已完成，手动启动敌人生成");
                    enemySpawner.SendMessage("StartSpawning", null, SendMessageOptions.DontRequireReceiver);
                }
                // 如果TerrainGenerator仍在生成，事件会自动触发StartSpawning
            }
        }
        else
        {
            Debug.LogError("未找到TerrainGenerator实例，无法重新生成地形和敌人");
        }
        
        Debug.Log("游戏重启清理完成");
    }

    // 更新方法：完全重置所有游戏状态
    private void PrepareForFullReset()
    {
        // 立即隐藏所有UI面板（即使在执行其他清理前）
        if (gameOverPanel != null) 
        {
            gameOverPanel.SetActive(false);
            gameOverPanel.transform.SetParent(null); // 断开父级连接
            Debug.Log("游戏结束面板已隐藏并脱离层级");
        }
        if (victoryPanel != null) 
        {
            victoryPanel.SetActive(false);
            victoryPanel.transform.SetParent(null); // 断开父级连接
            Debug.Log("胜利面板已隐藏并脱离层级");
        }
        if (fireworksEffectLeft != null) fireworksEffectLeft.SetActive(false);
        if (fireworksEffectRight != null) fireworksEffectRight.SetActive(false);
        
        // 设置游戏状态为非结束
        isGameOver = false;
        
        // 调用普通的场景变更清理
        PrepareForSceneChange();
        
        // 查找并重置地形生成器
        TerrainGenerator terrainGen = TerrainGenerator.Instance;
        if (terrainGen != null)
        {
            // 强制地形生成器重新生成地形
            Debug.Log("强制重新生成地形...");
            try
            {
                terrainGen.RegenerateMap();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"重新生成地形时出错: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("未找到TerrainGenerator实例，无法重新生成地形");
        }
        
        // 标记需要重置的UI组件
        EvolutionPointsUI evolutionUI = EvolutionPointsUI.Instance;
        if (evolutionUI != null)
        {
            evolutionUI.needsReset = true;
            evolutionUI.ResetUI();
        }
        
        // 重置玩家进化状态
        PlayerEvolution playerEvolution = PlayerEvolution.Instance;
        if (playerEvolution != null)
        {
            playerEvolution.ResetEvolutionState();
        }
        
        // 查找并重置所有玩家相关组件
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            // 立即销毁玩家，避免在新场景中出现多个玩家
            Destroy(playerHealth.gameObject);
        }
        
        // 查找场景中所有UI面板并强制隐藏
        GameObject[] allObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in allObjects)
        {
            // 检查是否是游戏结束或胜利面板相关对象
            if (obj.name.Contains("GameOver") || obj.name.Contains("Victory") || 
                obj.name.Contains("EndGame") || obj.name.Contains("Win") ||
                obj.name.Contains("Panel"))
            {
                obj.SetActive(false);
                Debug.Log($"已隐藏可能的UI面板: {obj.name}");
                
                // 从层级中断开UI面板
                if (obj.transform.parent != null && obj.transform.parent.name.Contains("Canvas"))
                {
                    obj.transform.SetParent(null);
                }
            }
        }
        
        // 查找所有Canvas并刷新状态
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        foreach (Canvas canvas in canvases)
        {
            canvas.enabled = false;
            canvas.enabled = true; // 强制刷新Canvas
        }
        
        // 销毁根对象（除了GameManager）
        GameObject[] rootObjects = FindObjectsOfType<GameObject>(true);
        foreach (GameObject obj in rootObjects)
        {
            // 跳过非根对象
            if (obj.transform.parent != null) continue;
            
            // 不销毁GameManager所在对象
            if (obj.GetComponent<GameManager>() != null) continue;
            
            // 不销毁场景必要对象
            if (obj.CompareTag("MainCamera") || obj.CompareTag("EventSystem")) continue;
            
            // 不销毁TerrainGenerator，它需要自己处理重新生成
            if (obj.GetComponent<TerrainGenerator>() != null) continue;
            
            // 销毁其他根对象
            Destroy(obj);
        }
        
        // 强制清理内存
        Resources.UnloadUnusedAssets();
        System.GC.Collect();
        
        Debug.Log("游戏已完全重置，准备加载新游戏");
    }

    public static void PrepareForSceneChange()
    {
        // 设置一个标志表示正在进行场景切换
        bool isChangingScene = true;
        
        // 已有的清理代码
        if (Instance != null)
        {
            Instance.StopAllCoroutines();
            
            // 先保存一个引用
            MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            
            // 清除所有可能导致问题的引用
            Instance.playerHealth = null;
        }
        
        // 改进的对象清理方法
        try
        {
            // 停止所有正在运行的协程
            MonoBehaviour[] allMonoBehaviours = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
            foreach (MonoBehaviour mb in allMonoBehaviours)
            {
                if (mb != null && mb.isActiveAndEnabled)
                {
                    mb.StopAllCoroutines();
                    
                    // 检查并清理可能的Transform引用
                    PropertyCleaner cleaner = mb.GetComponent<PropertyCleaner>();
                    if (cleaner != null)
                    {
                        cleaner.CleanReferences();
                    }
                }
            }
            
            // 安全地删除所有标记为临时的对象
            GameObject[] tempObjects = GameObject.FindGameObjectsWithTag("Temporary");
            foreach (GameObject obj in tempObjects)
            {
                if (obj != null)
                {
                    // 先禁用对象，再销毁，避免在销毁过程中仍然被访问
                    obj.SetActive(false);
                    Destroy(obj);
                }
            }
            
            // 强制立即清除所有已销毁的对象，避免延迟销毁导致的引用问题
            Resources.UnloadUnusedAssets();
            System.GC.Collect();
            
            // 清除所有使用DontDestroyOnLoad的持久性对象（不包括GameManager自身）
            GameManager[] managers = FindObjectsByType<GameManager>(FindObjectsSortMode.None);
            foreach (GameManager manager in managers)
            {
                if (manager != Instance && manager != null)
                {
                    Destroy(manager.gameObject);
                }
            }
            
            // 新增：重置UI面板状态
            if (Instance != null)
            {
                if (Instance.gameOverPanel != null)
                    Instance.gameOverPanel.SetActive(false);
                if (Instance.victoryPanel != null)
                    Instance.victoryPanel.SetActive(false);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"场景切换清理过程中发生错误: {e.Message}\n{e.StackTrace}");
        }
    }

    private void OnDestroy()
    {
        // 移除场景加载事件监听，避免引用丢失后仍然被调用
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // 清理所有引用和协程
        StopAllCoroutines();
        timerText = null;
        gameOverPanel = null;
        victoryPanel = null;
        gameOverText = null;
        fireworksEffectLeft = null;
        fireworksEffectRight = null;
        playerHealth = null;
    }

    public void TriggerGameOver()
    {
        GameOver(); // 调用私有的GameOver方法
    }

    // 寻找并初始化UI引用
    private void InitializeUIReferences()
    {
        // 查找并初始化所有必要的UI元素
        if (gameOverPanel == null)
        {
            GameObject gameOverObj = GameObject.Find("GameOverPanel");
            if (gameOverObj != null)
            {
                gameOverPanel = gameOverObj;
                gameOverPanel.tag = "UIPanel";
                gameOverPanel.SetActive(false);
                
                // 重新绑定GameOver面板上的按钮事件
                Button[] gameOverButtons = gameOverPanel.GetComponentsInChildren<Button>(true);
                foreach (Button btn in gameOverButtons)
                {
                    if (btn.name.Contains("NewGame") || btn.name.Contains("Restart"))
                    {
                        // 移除所有现有监听器
                        btn.onClick.RemoveAllListeners();
                        // 添加新的监听器
                        btn.onClick.AddListener(RestartGame);
                        Debug.Log($"重新绑定了GameOver面板按钮: {btn.name}");
                    }
                    else if (btn.name.Contains("Menu") || btn.name.Contains("Return"))
                    {
                        // 移除所有现有监听器
                        btn.onClick.RemoveAllListeners();
                        // 添加新的监听器
                        btn.onClick.AddListener(ReturnToMenu);
                        Debug.Log($"重新绑定了GameOver面板按钮: {btn.name}");
                    }
                }
                
                Debug.Log("找到并初始化了游戏结束面板");
            }
            else
            {
                Debug.LogWarning("未找到游戏结束面板");
            }
        }
        
        if (victoryPanel == null)
        {
            GameObject victoryObj = GameObject.Find("VictoryPanel");
            if (victoryObj != null)
            {
                victoryPanel = victoryObj;
                victoryPanel.tag = "UIPanel";
                victoryPanel.SetActive(false);
                
                // 重新绑定Victory面板上的按钮事件
                Button[] victoryButtons = victoryPanel.GetComponentsInChildren<Button>(true);
                foreach (Button btn in victoryButtons)
                {
                    if (btn.name.Contains("NewGame") || btn.name.Contains("Restart"))
                    {
                        // 移除所有现有监听器
                        btn.onClick.RemoveAllListeners();
                        // 添加新的监听器
                        btn.onClick.AddListener(RestartGame);
                        Debug.Log($"重新绑定了Victory面板按钮: {btn.name}");
                    }
                    else if (btn.name.Contains("Menu") || btn.name.Contains("Return"))
                    {
                        // 移除所有现有监听器
                        btn.onClick.RemoveAllListeners();
                        // 添加新的监听器
                        btn.onClick.AddListener(ReturnToMenu);
                        Debug.Log($"重新绑定了Victory面板按钮: {btn.name}");
                    }
                }
                
                Debug.Log("找到并初始化了胜利面板");
            }
            else
            {
                Debug.LogWarning("未找到胜利面板");
            }
        }
    }
} 