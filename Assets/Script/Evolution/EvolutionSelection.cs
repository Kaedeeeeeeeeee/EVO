using UnityEngine;

public class EvolutionSelection : MonoBehaviour
{
    private int evoPoints;

    public void SetEvolutionPoints(int points)
    {
        evoPoints = points;
        Debug.Log($"🌟 EvolutionSelection 初始化完成，EVO-P: {evoPoints}");
    }

    public int GetEvolutionPoints()
    {
        return evoPoints;
    }

    public void SpendEvolutionPoints(int cost)
    {
        evoPoints -= cost;
        Debug.Log($"💰 EVO-P 扣除 {cost}，剩余：{evoPoints}");
    }
}
