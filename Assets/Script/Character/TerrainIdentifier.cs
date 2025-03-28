using UnityEngine;

/// <summary>
/// 用于标识地形类型的组件
/// </summary>
public class TerrainIdentifier : MonoBehaviour
{
    // 地形类型：Volcano, Lake, FlatTerrain
    public string terrainType = "Unknown";
    
    void OnCollisionEnter(Collision collision)
    {
        // 检查是否为敌人
        EnemyAIExtended enemyAI = collision.gameObject.GetComponent<EnemyAIExtended>();
        if (enemyAI != null)
        {
            // 记录碰撞事件
            Debug.Log($"地形({terrainType})与敌人发生碰撞: {collision.gameObject.name}");
        }
    }
} 