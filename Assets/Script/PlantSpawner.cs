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

    [Header("地面检测设置")]
    public LayerMask groundLayer; // 地面层
    public float raycastHeight = 10f; // 射线检测高度
    public float raycastDistance = 20f; // 射线检测距离
    public float surfaceOffset = 0.05f; // 地面偏移量，防止植物部分埋入地面

    [Header("调试设置")]
    public bool debugMode = false;
    public int maxPlacementAttempts = 30; // 每个植物尝试放置的最大次数

    private List<GameObject> weightedPlants = new List<GameObject>();
    private List<GameObject> spawnedPlants = new List<GameObject>(); // 已生成的植物列表
    private Bounds groundBounds;

    void Start()
    {
        if (groundObject == null)
        {
            Debug.LogError("PlantSpawner: 请指定 Ground 物体！");
            return;
        }

        // 初始化地面层
        if (groundLayer.value == 0)
        {
            groundLayer = 1 << groundObject.layer; // 使用groundObject的层
            Debug.Log($"自动设置groundLayer为: {LayerMask.LayerToName(groundObject.layer)}");
        }

        // 获取地面边界
        InitializeGroundBounds();

        // 初始化权重表
        PopulateWeightedPlantList();

        // 生成植物
        for (int i = 0; i < plantCount; i++)
        {
            SpawnPlant();
        }

        if (debugMode)
        {
            Debug.Log($"植物生成完成，成功生成: {spawnedPlants.Count}/{plantCount}");
        }
    }

    void InitializeGroundBounds()
    {
        // 获取地面边界
        Renderer groundRenderer = groundObject.GetComponent<Renderer>();
        if (groundRenderer != null)
        {
            groundBounds = groundRenderer.bounds;
            Debug.Log($"获取地面边界: 中心({groundBounds.center})，大小({groundBounds.size})");
        }
        else
        {
            // 尝试从子对象获取Renderer
            Renderer[] childRenderers = groundObject.GetComponentsInChildren<Renderer>();
            if (childRenderers.Length > 0)
            {
                // 计算所有子对象的总边界
                groundBounds = childRenderers[0].bounds;
                for (int i = 1; i < childRenderers.Length; i++)
                {
                    groundBounds.Encapsulate(childRenderers[i].bounds);
                }
                Debug.Log($"从子对象获取地面边界: 中心({groundBounds.center})，大小({groundBounds.size})");
            }
            else
            {
                Debug.LogError("无法获取地面边界，请确保地面对象有Renderer组件！");
            }
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

        Debug.Log($"初始化植物权重表，共 {weightedPlants.Count} 个条目");
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

        // 确定是否是稀有植物
        bool isRarePlant = selectedPlantData.spawnWeight < 3 && rareSpawnAreas.Count > 0;

        // 获取有效的生成位置
        Vector3 spawnPosition = GetValidPlantPosition(isRarePlant);
        if (spawnPosition == Vector3.zero)
        {
            Debug.LogWarning("无法找到有效的植物生成位置，跳过生成");
            return;
        }

        // 生成植物
        GameObject plant = Instantiate(selectedPlantPrefab, spawnPosition, Quaternion.identity);

        // 设置植物数据
        Plant plantComponent = plant.GetComponent<Plant>();
        if (plantComponent != null)
        {
            plantComponent.plantData = selectedPlantData;
        }

        // 随机旋转，增加自然感
        plant.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);

        // 随机缩放，增加多样性（可选）
        float randomScale = Random.Range(0.8f, 1.2f);
        plant.transform.localScale *= randomScale;

        // 添加到已生成植物列表
        spawnedPlants.Add(plant);

        if (debugMode)
        {
            Debug.Log($"生成植物: {selectedPlantData.plantName} 在位置 {spawnPosition}");
        }
    }

    Vector3 GetValidPlantPosition(bool isRarePlant)
    {
        int attempts = 0;

        while (attempts < maxPlacementAttempts)
        {
            Vector3 potentialPosition;

            // 根据植物类型选择生成区域
            if (isRarePlant && rareSpawnAreas.Count > 0)
            {
                // 稀有植物：在指定区域生成
                Transform rareArea = rareSpawnAreas[Random.Range(0, rareSpawnAreas.Count)];
                Vector3 areaCenter = rareArea.position;
                float areaRadius = rareArea.localScale.x / 2;

                potentialPosition = new Vector3(
                    areaCenter.x + Random.Range(-areaRadius, areaRadius),
                    areaCenter.y + raycastHeight, // 从上方射线向下检测
                    areaCenter.z + Random.Range(-areaRadius, areaRadius)
                );
            }
            else
            {
                // 普通植物：在MainGround范围内生成
                potentialPosition = new Vector3(
                    Random.Range(groundBounds.min.x, groundBounds.max.x),
                    groundBounds.max.y + raycastHeight, // 从上方射线向下检测
                    Random.Range(groundBounds.min.z, groundBounds.max.z)
                );
            }

            // 射线检测，确保位置在地面上
            Ray ray = new Ray(potentialPosition, Vector3.down);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, raycastDistance, groundLayer))
            {
                // 找到地面点
                Vector3 groundPoint = hit.point + Vector3.up * surfaceOffset; // 添加偏移，防止部分埋入地面

                // 检查与其他植物的距离
                bool tooCloseToOthers = false;
                foreach (GameObject otherPlant in spawnedPlants)
                {
                    if (otherPlant == null) continue;

                    if (Vector3.Distance(groundPoint, otherPlant.transform.position) < minDistanceBetweenPlants)
                    {
                        tooCloseToOthers = true;
                        break;
                    }
                }

                // 如果与其他植物距离合适，返回该位置
                if (!tooCloseToOthers)
                {
                    if (debugMode)
                    {
                        // 绘制一条射线表示地面检测
                        Debug.DrawLine(potentialPosition, groundPoint, Color.green, 5f);
                        Debug.Log($"找到有效的植物生成位置: {groundPoint}，地面法线: {hit.normal}");
                    }
                    return groundPoint;
                }
            }

            attempts++;
        }

        Debug.LogWarning($"经过 {maxPlacementAttempts} 次尝试后仍无法找到有效的植物生成位置");
        return Vector3.zero;
    }

    // 绘制调试可视化
    void OnDrawGizmosSelected()
    {
        if (!debugMode) return;

        // 显示地面边界
        if (groundObject != null)
        {
            Gizmos.color = Color.green;
            Renderer groundRenderer = groundObject.GetComponent<Renderer>();
            if (groundRenderer != null)
            {
                Bounds bounds = groundRenderer.bounds;
                Gizmos.DrawWireCube(bounds.center, bounds.size);
            }
        }

        // 显示稀有植物生成区域
        Gizmos.color = Color.magenta;
        foreach (Transform area in rareSpawnAreas)
        {
            if (area != null)
            {
                Gizmos.DrawWireSphere(area.position, area.localScale.x / 2);
            }
        }

        // 显示已生成的植物
        Gizmos.color = Color.yellow;
        foreach (GameObject plant in spawnedPlants)
        {
            if (plant != null)
            {
                Gizmos.DrawWireSphere(plant.transform.position, 0.5f);
            }
        }
    }
}