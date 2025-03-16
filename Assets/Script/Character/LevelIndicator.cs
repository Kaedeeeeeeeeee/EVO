using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LevelIndicator : MonoBehaviour
{
    public TextMeshProUGUI levelText;          // 显示等级的文本
    public GameObject levelIconPrefab;         // 等级图标预制体
    public Transform iconContainer;            // 图标容器
    public Color[] levelColors = {             // 不同等级的颜色
        Color.white,                          // 等级1
        Color.green,                          // 等级2
        Color.blue,                           // 等级3
        Color.magenta,                        // 等级4
        Color.red                             // 等级5
    };

    private EnemyAIExtended enemyAI;           // 如果挂在敌人上
    private PlayerEvolution playerEvolution;   // 如果挂在玩家上

    private GameObject[] levelIcons;           // 等级图标数组

    void Start()
    {
        // 检查是敌人还是玩家
        enemyAI = GetComponent<EnemyAIExtended>();
        playerEvolution = GetComponent<PlayerEvolution>();

        // 初始化等级显示
        if (iconContainer != null && levelIconPrefab != null)
        {
            InitializeLevelIcons();
        }

        // 更新显示
        UpdateLevelDisplay();
    }

    void Update()
    {
        // 如果是玩家，定期检查更新等级显示
        if (playerEvolution != null)
        {
            UpdateLevelDisplay();
        }
    }

    void InitializeLevelIcons()
    {
        // 清除现有图标
        foreach (Transform child in iconContainer)
        {
            Destroy(child.gameObject);
        }

        // 创建等级图标（最多5个，对应5个等级）
        levelIcons = new GameObject[5];
        for (int i = 0; i < 5; i++)
        {
            GameObject icon = Instantiate(levelIconPrefab, iconContainer);
            levelIcons[i] = icon;

            // 默认隐藏所有图标
            icon.SetActive(false);
        }
    }

    public void UpdateLevelDisplay()
    {
        int currentLevel = 0;

        // 获取当前等级
        if (enemyAI != null)
        {
            currentLevel = enemyAI.level;
        }
        else if (playerEvolution != null)
        {
            currentLevel = playerEvolution.level;
        }
        else
        {
            return; // 没有找到等级来源
        }

        // 确保等级在有效范围内
        currentLevel = Mathf.Clamp(currentLevel, 1, 5);

        // 更新文本显示
        if (levelText != null)
        {
            levelText.text = $"Lv{currentLevel}";

            // 设置对应的颜色
            if (levelColors.Length >= currentLevel)
            {
                levelText.color = levelColors[currentLevel - 1];
            }
        }

        // 更新图标显示
        if (levelIcons != null)
        {
            for (int i = 0; i < levelIcons.Length; i++)
            {
                if (levelIcons[i] != null)
                {
                    // 显示等级对应数量的图标
                    levelIcons[i].SetActive(i < currentLevel);

                    // 设置颜色
                    if (i < levelColors.Length)
                    {
                        Image iconImage = levelIcons[i].GetComponent<Image>();
                        if (iconImage != null)
                        {
                            iconImage.color = levelColors[i];
                        }
                    }
                }
            }
        }
    }
}