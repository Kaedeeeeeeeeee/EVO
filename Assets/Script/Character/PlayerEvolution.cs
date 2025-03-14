using UnityEngine;

public class PlayerEvolution : MonoBehaviour
{
    public static PlayerEvolution Instance { get; private set; }
    public int evolutionPoints = 0;

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

    public void AddEvolutionPoints(int amount)
    {
        evolutionPoints += amount;
        EvolutionPointsUI.Instance.UpdateEvoPoints(evolutionPoints);
        Debug.Log($"⚡ EVO-P 增加: {amount}, 当前 EVO-P: {evolutionPoints}");
    }

    public bool SpendEvolutionPoints(int amount)
    {
        if (evolutionPoints >= amount)
        {
            evolutionPoints -= amount;
            EvolutionPointsUI.Instance.UpdateEvoPoints(evolutionPoints);
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
