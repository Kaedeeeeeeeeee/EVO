using UnityEngine;
using TMPro;

public class EvolutionPointsUI : MonoBehaviour
{
    public static EvolutionPointsUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI evoPointsText; // 🎯 绑定 UI 组件
    
    // 添加场景重置标记
    public bool needsReset = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 确保UI实例不会被销毁
            gameObject.tag = "Temporary"; // 添加临时标签，便于清理
        }
        else
        {
            // 如果已存在实例且需要重置，则更新并销毁自己
            if (Instance.needsReset)
            {
                Instance.ResetUI();
                Instance.needsReset = false;
            }
            Destroy(gameObject);
            return;
        }

        // 检查是否正确绑定了Text组件
        if (evoPointsText == null)
        {
            Debug.LogError("❌ EvolutionPointsUI: 未绑定Text组件，请在Inspector中设置！");
            evoPointsText = GetComponentInChildren<TextMeshProUGUI>();
            if (evoPointsText != null)
            {
                Debug.Log("✅ 自动查找并绑定了TextMeshProUGUI组件");
            }
        }
    }

    void Start()
    {
        // 确保在游戏开始时初始化显示为0
        UpdateEvoPoints(0);
    }
    
    // 添加重置UI的方法
    public void ResetUI()
    {
        UpdateEvoPoints(0);
        Debug.Log("EvolutionPointsUI已重置");
    }

    public void UpdateEvoPoints(int points)
    {
        if (evoPointsText != null)
        {
            evoPointsText.text = $"EVO-P: {points}";
            Debug.Log($"✅ UI已更新：EVO-P = {points}");
        }
        else
        {
            Debug.LogError("❌ evoPointsText为空，无法更新UI！");
        }
    }
    
    // 添加OnDestroy方法确保清理
    void OnDestroy()
    {
        // 如果销毁的是当前实例，则清空静态引用
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
