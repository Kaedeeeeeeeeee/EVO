using UnityEngine;
using TMPro;

public class EvolutionShopManager : MonoBehaviour
{
    public static EvolutionShopManager Instance { get; private set; }

    [SerializeField] private GameObject evolutionShopUI; // 🎯 绑定商店UI
    [SerializeField] private TextMeshProUGUI shopEvoPointsText; // 新增：商店内的进化点数显示

    private bool isShopOpen = false;
    private SkillSelection skillSelection; // 用于刷新卡牌

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        if (evolutionShopUI == null)
        {
            Debug.LogError("❌ EvolutionShopManager: EvolutionShop UI 未绑定！");
        }
        else
        {
            evolutionShopUI.SetActive(false); // 初始隐藏商店
        }

        // 获取SkillSelection引用
        skillSelection = FindObjectOfType<SkillSelection>();

        // 检查商店内的进化点数显示文本
        if (shopEvoPointsText == null)
        {
            Debug.LogWarning("⚠️ 商店内未绑定进化点数显示文本，尝试自动查找...");

            // 尝试在商店UI中查找
            if (evolutionShopUI != null)
            {
                // 查找商店UI中任何可能包含"Points"或"EVO"的TextMeshProUGUI组件
                TextMeshProUGUI[] texts = evolutionShopUI.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var text in texts)
                {
                    if (text.name.Contains("Points") || text.name.Contains("EVO") ||
                        text.name.Contains("Evo") || text.gameObject.name.Contains("Points"))
                    {
                        shopEvoPointsText = text;
                        Debug.Log($"✅ 找到商店中的点数显示文本: {text.name}");
                        break;
                    }
                }

                // 如果没找到，可以创建一个
                if (shopEvoPointsText == null && evolutionShopUI.activeInHierarchy)
                {
                    GameObject textObj = new GameObject("ShopEvoPointsText");
                    textObj.transform.SetParent(evolutionShopUI.transform, false);
                    shopEvoPointsText = textObj.AddComponent<TextMeshProUGUI>();

                    // 设置一些基本属性
                    RectTransform rectTransform = textObj.GetComponent<RectTransform>();
                    rectTransform.anchorMin = new Vector2(0.5f, 0.9f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.9f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.sizeDelta = new Vector2(200, 50);

                    shopEvoPointsText.fontSize = 24;
                    shopEvoPointsText.alignment = TextAlignmentOptions.Center;
                    shopEvoPointsText.color = Color.yellow;

                    Debug.Log("✅ 创建了新的商店点数显示文本");
                }
            }
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)) // 监听 P 键
        {
            ToggleShop();
        }
    }

    public void ToggleShop()
    {
        if (evolutionShopUI == null) return;

        isShopOpen = !isShopOpen;
        evolutionShopUI.SetActive(isShopOpen);

        if (isShopOpen)
        {
            Time.timeScale = 0f; // ⏸ 暂停游戏
            Debug.Log("✅ 进化商店已打开！");

            // 更新商店内的进化点数显示
            UpdateShopPointsDisplay();

            // 刷新卡牌
            if (skillSelection != null)
            {
                skillSelection.GenerateRandomSkills();
            }
        }
        else
        {
            Time.timeScale = 1f; // ▶ 继续游戏
            Debug.Log("✅ 进化商店已关闭！");
        }
    }

    // 新增：更新商店内的进化点数显示
    private void UpdateShopPointsDisplay()
    {
        if (shopEvoPointsText != null)
        {
            // 获取当前进化点数
            int points = 0;
            PlayerEvolution playerEvo = PlayerEvolution.Instance;
            if (playerEvo != null)
            {
                points = playerEvo.evolutionPoints;
            }

            // 更新显示
            shopEvoPointsText.text = $"EVO-P: {points}";
            Debug.Log($"✅ 商店内进化点数显示已更新: {points}");
        }
    }

    // 新增：供外部调用的更新方法
    public void RefreshShopDisplay()
    {
        if (isShopOpen)
        {
            UpdateShopPointsDisplay();

            // 刷新卡牌
            if (skillSelection != null)
            {
                skillSelection.GenerateRandomSkills();
            }
        }
    }
}