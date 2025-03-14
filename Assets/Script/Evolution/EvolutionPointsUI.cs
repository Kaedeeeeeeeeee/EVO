using UnityEngine;
using TMPro;

public class EvolutionPointsUI : MonoBehaviour
{
    public static EvolutionPointsUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI evoPointsText; // 🎯 绑定 UI 组件

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

    public void UpdateEvoPoints(int points)
    {
        if (evoPointsText != null)
        {
            evoPointsText.text = $"EVO-P: {points}";
        }
    }
}
