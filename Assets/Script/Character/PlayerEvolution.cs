using UnityEngine;
using TMPro;
using System;

public class PlayerEvolution : MonoBehaviour
{
    public static PlayerEvolution Instance { get; private set; }
    public int evolutionPoints = 0;

    [Range(1, 5)]
    public int level = 1;               // 玩家等级 (1-5)
    public int[] levelUpThresholds = { 0, 100, 250, 500, 1000 };  // 每个等级所需的进化点数

    // 使用弱引用以防止引用被销毁的对象
    private EvolutionPointsUI pointsUI;
    private EvolutionShopManager shopManager;

    [Header("UI 引用")]
    [SerializeField] private TextMeshProUGUI levelText;  // 显示等级的文本

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 确保玩家对象有正确的标签
            gameObject.tag = "Player";
            
            // 添加临时标签会覆盖Player标签，这可能是问题所在
            // 所以不使用Temporary标签，而是通过其他方式清理
            // gameObject.tag = "Temporary"; // 这行可能导致Player标签丢失
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        try
        {
            // 尝试查找或等待EvolutionPointsUI实例
            pointsUI = SafeFindObjectOfType<EvolutionPointsUI>();

            if (pointsUI == null)
            {
                Debug.LogWarning("⚠️ 未找到EvolutionPointsUI实例，将在首次使用时重新查找");
            }
            else
            {
                // 初始化UI显示
                pointsUI.UpdateEvoPoints(evolutionPoints);
                Debug.Log($"✅ PlayerEvolution初始化完成，当前EVO-P: {evolutionPoints}，等级: {level}");
            }

            // 查找商店管理器
            shopManager = SafeFindObjectOfType<EvolutionShopManager>();

            // 更新等级显示
            UpdateLevelDisplay();
        }
        catch (Exception e)
        {
            Debug.LogError($"PlayerEvolution启动时发生错误: {e.Message}");
        }
    }

    // 安全地查找对象，避免错误
    private T SafeFindObjectOfType<T>() where T : MonoBehaviour
    {
        try
        {
            return FindFirstObjectByType<T>();
        }
        catch
        {
            return null;
        }
    }

    // 确保在使用前检查并获取UI引用
    private void EnsureUIReference()
    {
        try
        {
            if (pointsUI == null)
            {
                pointsUI = SafeFindObjectOfType<EvolutionPointsUI>();

                // 如果仍然找不到，尝试在场景中查找
                if (pointsUI == null)
                {
                    Debug.LogWarning("⚠️ 无法找到EvolutionPointsUI实例，无法更新UI！");
                }
            }

            // 确保有商店管理器引用
            if (shopManager == null)
            {
                shopManager = SafeFindObjectOfType<EvolutionShopManager>();
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"获取UI引用时发生错误: {e.Message}");
        }
    }

    public void AddEvolutionPoints(int amount)
    {
        try
        {
            evolutionPoints += amount;

            // 检查是否可以升级
            CheckForLevelUp();

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

            Debug.Log($"⚡ EVO-P 增加: {amount}, 当前 EVO-P: {evolutionPoints}, 等级: {level}");
        }
        catch (Exception e)
        {
            Debug.LogError($"添加进化点数时发生错误: {e.Message}");
        }
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

    private void CheckForLevelUp()
    {
        // 计算当前应该处于的等级
        int newLevel = 1;
        for (int i = 1; i < levelUpThresholds.Length; i++)
        {
            if (evolutionPoints >= levelUpThresholds[i])
            {
                newLevel = i + 1;
            }
            else
            {
                break;
            }
        }

        // 如果等级提升，执行升级效果
        if (newLevel > level)
        {
            int oldLevel = level;
            level = newLevel;
            OnLevelUp(oldLevel, newLevel);
        }
    }

    private void OnLevelUp(int oldLevel, int newLevel)
    {
        Debug.Log($"🌟 玩家等级提升: {oldLevel} -> {newLevel}！");

        // 播放升级效果
        PlayLevelUpEffect();

        // 更新等级显示
        UpdateLevelDisplay();

        // 可以在这里添加升级奖励
        // 例如: 恢复全部生命值，获得特殊技能等
        PlayerHealth playerHealth = GetComponent<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.Heal(playerHealth.maxHealth); // 升级时恢复全部生命
            Debug.Log("💚 升级后生命值已完全恢复！");
        }

        // 提升玩家属性
        PlayerCombat playerCombat = GetComponent<PlayerCombat>();
        if (playerCombat != null)
        {
            playerCombat.attackDamage += 5; // 每升一级增加5点伤害
            Debug.Log($"⚔️ 攻击力提升到 {playerCombat.attackDamage}！");
        }
    }

    private void PlayLevelUpEffect()
    {
        // 在这里添加升级特效
        // 例如: 粒子效果，声音，屏幕闪烁等
        Debug.Log("✨ 播放升级特效");

        // 可以在这里实例化一个粒子系统
        // Instantiate(levelUpParticles, transform.position, Quaternion.identity);
    }

    private void UpdateLevelDisplay()
    {
        if (levelText != null)
        {
            levelText.text = $"Level: {level}";

            // 可以根据等级设置不同的颜色
            switch (level)
            {
                case 1:
                    levelText.color = Color.white;
                    break;
                case 2:
                    levelText.color = Color.green;
                    break;
                case 3:
                    levelText.color = Color.blue;
                    break;
                case 4:
                    levelText.color = Color.magenta;
                    break;
                case 5:
                    levelText.color = Color.red;
                    break;
            }
        }
        else
        {
            Debug.LogWarning("⚠️ 未找到等级显示文本组件！");
        }
    }

    // 获取距离下一级所需的进化点数
    public int GetPointsToNextLevel()
    {
        if (level >= levelUpThresholds.Length)
        {
            return 0; // 已达最高等级
        }

        return levelUpThresholds[level] - evolutionPoints;
    }

    // 获取当前等级进度百分比
    public float GetLevelProgress()
    {
        if (level >= levelUpThresholds.Length)
        {
            return 1f; // 已达最高等级
        }

        int currentLevelPoints = levelUpThresholds[level - 1];
        int nextLevelPoints = levelUpThresholds[level];
        int levelRange = nextLevelPoints - currentLevelPoints;

        return (float)(evolutionPoints - currentLevelPoints) / levelRange;
    }

    // 新增方法：重置所有进化状态
    public void ResetEvolutionState()
    {
        try
        {
            evolutionPoints = 0;  // 重置进化点数
            level = 1;            // 重置等级为1
            
            // 更新UI
            EnsureUIReference();
            if (pointsUI != null)
            {
                pointsUI.UpdateEvoPoints(evolutionPoints);
            }
            
            // 更新等级显示
            UpdateLevelDisplay();
            
            Debug.Log("已重置玩家进化状态");
        }
        catch (Exception e)
        {
            Debug.LogError($"重置进化状态时发生错误: {e.Message}");
        }
    }
    
    // 添加OnDestroy方法安全清理引用
    void OnDestroy()
    {
        // 如果销毁的是当前实例，则清空静态引用
        if (Instance == this)
        {
            Instance = null;
        }
        
        // 清除所有引用
        pointsUI = null;
        shopManager = null;
    }
}