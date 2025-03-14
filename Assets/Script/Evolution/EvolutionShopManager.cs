using UnityEngine;

public class EvolutionShopManager : MonoBehaviour
{
    public static EvolutionShopManager Instance { get; private set; }

    [SerializeField] private GameObject evolutionShopUI; // 🎯 绑定 UI
    private bool isShopOpen = false;

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
        }
        else
        {
            Time.timeScale = 1f; // ▶ 继续游戏
            Debug.Log("✅ 进化商店已关闭！");
        }
    }
}
