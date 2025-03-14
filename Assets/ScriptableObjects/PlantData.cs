using UnityEngine;

[CreateAssetMenu(fileName = "NewPlant", menuName = "Plants/Plant Data")]
public class PlantData : ScriptableObject
{
    public string plantName;
    public int healAmount; // 恢复生命值
    public int evolutionPoints; // 进化点数
    public int spawnWeight = 1; // **新增：植物刷新权重**
}
