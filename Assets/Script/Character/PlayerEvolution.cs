using UnityEngine;

public class PlayerEvolution : MonoBehaviour
{
    public static PlayerEvolution Instance { get; private set; }
    public int evolutionPoints = 0;

    private EvolutionPointsUI pointsUI;
    private EvolutionShopManager shopManager;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        // 尝试查找或等待EvolutionPointsUI实例
        pointsUI = EvolutionPointsUI.Instance;

        if (pointsUI == null)
        {
            Debug.LogWarning("⚠️ 未找到EvolutionPointsUI实例，将在首次使用时重新查找");
        }
        else
        {
            // 初始化UI显示
            pointsUI.UpdateEvoPoints(evolutionPoints);
            Debug.Log($"✅ PlayerEvolution初始化完成，当前EVO-P: {evolutionPoints}");
        }

        // 查找商店管理器
        shopManager = EvolutionShopManager.Instance;
    }

    // 确保在使用前检查并获取UI引用
    private void EnsureUIReference()
    {
        if (pointsUI == null)
        {
            pointsUI = EvolutionPointsUI.Instance;

            // 如果仍然找不到，尝试在场景中查找
            if (pointsUI == null)
            {
                pointsUI = FindObjectOfType<EvolutionPointsUI>();

                if (pointsUI == null)
                {
                    Debug.LogWarning("⚠️ 无法找到EvolutionPointsUI实例，无法更新UI！");
                }
            }
        }

        // 确保有商店管理器引用
        if (shopManager == null)
        {
            shopManager = EvolutionShopManager.Instance;
        }
    }

    public void AddEvolutionPoints(int amount)
    {
        evolutionPoints += amount;

        // 确保UI引用存在
        EnsureUIReference();

        // 更新主界面UI
        if (pointsUI != null)
        {
            pointsUI.UpdateEvoPoints(evolutionPoints);
        }

        // 更新商店UI（如果商店已打开）
        if (shopManager != null)
        {
            shopManager.RefreshShopDisplay();
        }

        Debug.Log($"⚡ EVO-P 增加: {amount}, 当前 EVO-P: {evolutionPoints}");
    }

    public bool SpendEvolutionPoints(int amount)
    {
        if (evolutionPoints >= amount)
        {
            evolutionPoints -= amount;

            // 确保UI引用存在
            EnsureUIReference();

            // 更新主界面UI
            if (pointsUI != null)
            {
                pointsUI.UpdateEvoPoints(evolutionPoints);
            }

            // 更新商店UI（如果商店已打开）
            if (shopManager != null)
            {
                shopManager.RefreshShopDisplay();
            }

            Debug.Log($"✅ EVO-P 消耗: {amount}, 剩余 EVO-P: {evolutionPoints}");
            return true;
        }
        else
        {
            Debug.LogWarning("❌ EVO-P 不足！");
            return false;
        }
    }
}