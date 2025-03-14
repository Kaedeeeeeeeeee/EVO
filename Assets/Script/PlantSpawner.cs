using UnityEngine;
using System.Collections.Generic;

public class PlantSpawner : MonoBehaviour
{
    public GameObject[] plantPrefabs; // 普通 & 稀有植物的预制体数组
    public PlantData[] plantDataArray; // 植物数据数组
    public int plantCount = 10; // 总体植物数量
    public GameObject groundObject; // 主要地面
    public List<Transform> rareSpawnAreas; // **稀有植物刷新的区域**
    public float minDistanceBetweenPlants = 2f; // **植物之间的最小间距**

    private List<GameObject> weightedPlants = new List<GameObject>();
    private Bounds groundBounds;

    void Start()
    {
        if (groundObject == null)
        {
            Debug.LogError("PlantSpawner: 请指定 Ground 物体！");
            return;
        }

        // 获取地面边界
        Renderer groundRenderer = groundObject.GetComponent<Renderer>();
        if (groundRenderer == null)
        {
            Debug.LogError("PlantSpawner: Ground 物体缺少 Renderer 组件！");
            return;
        }
        groundBounds = groundRenderer.bounds;

        // 初始化权重表
        PopulateWeightedPlantList();

        // 生成植物
        for (int i = 0; i < plantCount; i++)
        {
            SpawnPlant();
        }
    }

    void PopulateWeightedPlantList()
    {
        weightedPlants.Clear();

        for (int i = 0; i < plantPrefabs.Length; i++)
        {
            if (plantDataArray[i] != null)
            {
                int weight = plantDataArray[i].spawnWeight;
                for (int j = 0; j < weight; j++)
                {
                    weightedPlants.Add(plantPrefabs[i]);
                }
            }
        }
    }

    void SpawnPlant()
    {
        if (weightedPlants.Count == 0)
        {
            Debug.LogError("PlantSpawner: 没有可用的植物预制体！");
            return;
        }

        GameObject selectedPlantPrefab = weightedPlants[Random.Range(0, weightedPlants.Count)];
        PlantData selectedPlantData = plantDataArray[System.Array.IndexOf(plantPrefabs, selectedPlantPrefab)];

        Vector3 spawnPosition;
        bool isRarePlant = selectedPlantData.spawnWeight < 3 && rareSpawnAreas.Count > 0; // **判断是否是稀有植物**

        int maxAttempts = 10; // **防止无限循环**
        int attempts = 0;
        do
        {
            // **普通植物：在 MainGround 范围内生成**
            if (!isRarePlant)
            {
                spawnPosition = new Vector3(
                    Random.Range(groundBounds.min.x, groundBounds.max.x),
                    groundBounds.max.y + 0.1f ,
                    Random.Range(groundBounds.min.z, groundBounds.max.z)
                );
            }
            // **稀有植物：在指定区域生成**
            else
            {
                Transform rareArea = rareSpawnAreas[Random.Range(0, rareSpawnAreas.Count)];
                Vector3 areaCenter = rareArea.position;
                float areaRadius = rareArea.localScale.x / 2; // **假设区域是个球形范围**
                spawnPosition = new Vector3(
                    areaCenter.x + Random.Range(-areaRadius, areaRadius),
                    areaCenter.y + 0.1f,
                    areaCenter.z + Random.Range(-areaRadius, areaRadius)
                );
            }

            attempts++;
            if (attempts >= maxAttempts)
            {
                Debug.LogWarning("PlantSpawner: 生成位置尝试过多，强制放置植物！");
                break;
            }

        } while (Physics.OverlapSphere(spawnPosition, minDistanceBetweenPlants).Length > 0); // **检查植物之间的距离**

        Instantiate(selectedPlantPrefab, spawnPosition, Quaternion.identity);
    }
}
