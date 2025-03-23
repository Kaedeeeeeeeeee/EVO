using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainGenerator : MonoBehaviour
{
    [Header("地形参照物")]
    [SerializeField] private Transform mainGround; // 主地形对象，用于确定边界

    [Header("地形预制体")]
    [SerializeField] private GameObject volcanoPrefab; // 火山预制体
    [SerializeField] private GameObject[] lakePrefabs; // 湖泊预制体数组
    [SerializeField] private GameObject[] flatTerrainPrefabs; // 平坦地形预制体数组

    [Header("生成配置")]
    [SerializeField] private int minLakes = 5; // 最小湖泊数量
    [SerializeField] private int maxLakes = 6; // 最大湖泊数量
    [SerializeField] private int flatTerrainCount = 50; // 平坦地形数量
    [SerializeField] private float minObjectDistance = 10f; // 对象之间的最小距离
    [SerializeField] private float borderOffset = 5f; // 与地图边界的偏移量
    
    [Header("缩放设置")]
    [SerializeField] private float lakeMinScale = 0.8f; // 湖泊最小缩放值
    [SerializeField] private float lakeMaxScale = 1.2f; // 湖泊最大缩放值
    [SerializeField] private float flatTerrainMinScale = 0.7f; // 平坦地形最小缩放值
    [SerializeField] private float flatTerrainMaxScale = 1.3f; // 平坦地形最大缩放值
    
    // 用于存储所有已放置对象的位置
    private List<Vector3> placedObjectPositions = new List<Vector3>();
    
    // 地形边界
    private float terrainMinX;
    private float terrainMaxX;
    private float terrainMinZ;
    private float terrainMaxZ;

    // 单例实例
    public static TerrainGenerator Instance { get; private set; }

    // 添加一个事件，当地图生成完成时触发
    public event System.Action OnMapGenerationComplete;

    private void Awake()
    {
        // 单例模式设置
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void Start()
    {
        // 在游戏开始时生成地图
        GenerateMap();
    }

    public void GenerateMap()
    {
        // 清除之前生成的对象
        ClearExistingTerrain();
        
        // 计算主地形的边界
        CalculateTerrainBoundaries();
        
        // 生成火山
        GenerateVolcano();
        
        // 生成湖泊
        int lakeCount = Random.Range(minLakes, maxLakes + 1);
        for (int i = 0; i < lakeCount; i++)
        {
            GenerateLake();
        }
        
        // 生成平坦地形填充其余区域
        for (int i = 0; i < flatTerrainCount; i++)
        {
            GenerateFlatTerrain();
        }
        
        Debug.Log($"地图生成完成: 1个火山, {lakeCount}个湖泊, {flatTerrainCount}个平坦地形区域");
        
        // 触发地图生成完成事件
        OnMapGenerationComplete?.Invoke();
    }

    private void ClearExistingTerrain()
    {
        // 清除所有已生成的地形对象（如果需要重新生成地图）
        placedObjectPositions.Clear();
        
        // 查找并删除子对象
        foreach (Transform child in transform)
        {
            if (child.gameObject != mainGround.gameObject)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void CalculateTerrainBoundaries()
    {
        // 获取主地形的大小
        Renderer renderer = mainGround.GetComponent<Renderer>();
        if (renderer != null)
        {
            Bounds bounds = renderer.bounds;
            Vector3 size = bounds.size;
            Vector3 center = bounds.center;
            
            terrainMinX = center.x - size.x / 2 + borderOffset;
            terrainMaxX = center.x + size.x / 2 - borderOffset;
            terrainMinZ = center.z - size.z / 2 + borderOffset;
            terrainMaxZ = center.z + size.z / 2 - borderOffset;
        }
        else
        {
            // 如果无法获取renderer，使用默认值
            terrainMinX = -50 + borderOffset;
            terrainMaxX = 50 - borderOffset;
            terrainMinZ = -50 + borderOffset;
            terrainMaxZ = 50 - borderOffset;
            
            Debug.LogWarning("无法获取主地形的边界，使用默认值");
        }
    }

    private Vector3 GetRandomPositionOnTerrain()
    {
        // 获取地形上的随机位置
        float x = Random.Range(terrainMinX, terrainMaxX);
        float z = Random.Range(terrainMinZ, terrainMaxZ);
        
        // 使用射线检测获取正确的y坐标（高度）
        Ray ray = new Ray(new Vector3(x, 1000f, z), Vector3.down);
        RaycastHit hit;
        
        if (Physics.Raycast(ray, out hit, 2000f, LayerMask.GetMask("Default")))
        {
            return new Vector3(x, hit.point.y, z);
        }
        
        // 如果射线没有击中任何物体，使用主地形的y坐标
        return new Vector3(x, mainGround.position.y, z);
    }

    private bool IsPositionValid(Vector3 position)
    {
        // 检查位置是否与已放置的对象过近
        foreach (Vector3 placedPosition in placedObjectPositions)
        {
            if (Vector3.Distance(position, placedPosition) < minObjectDistance)
            {
                return false;
            }
        }
        
        // 检查位置是否在地形边界内
        if (position.x < terrainMinX || position.x > terrainMaxX ||
            position.z < terrainMinZ || position.z > terrainMaxZ)
        {
            return false;
        }
        
        return true;
    }

    private void GenerateVolcano()
    {
        // 检查预制体是否为空
        if (volcanoPrefab == null)
        {
            Debug.LogError("错误：火山预制体未设置！请在Inspector中设置火山预制体。");
            return;
        }
        
        // 生成火山（在地图中心区域）
        Vector3 position = Vector3.zero;
        int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            // 尝试在地图中心区域获取位置
            float centerRadius = Mathf.Min(terrainMaxX - terrainMinX, terrainMaxZ - terrainMinZ) * 0.3f;
            float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
            float distance = Random.Range(0f, centerRadius);
            
            float centerX = (terrainMinX + terrainMaxX) / 2;
            float centerZ = (terrainMinZ + terrainMaxZ) / 2;
            
            // 恢复X坐标的随机生成
            float x = centerX + Mathf.Cos(angle) * distance;
            float z = centerZ + Mathf.Sin(angle) * distance;
            
            // 将Y坐标固定为-0.2
            float y = -0.2f;
            
            position = new Vector3(x, y, z);
            if (IsPositionValid(position))
            {
                break;
            }
            
            // 如果所有尝试都失败，使用最后一个位置
            if (attempt == maxAttempts - 1)
            {
                Debug.LogWarning("无法为火山找到有效位置，使用最后尝试的位置");
            }
        }
        
        // 放置火山
        GameObject volcano = Instantiate(volcanoPrefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
        placedObjectPositions.Add(position);
        
        Debug.Log($"火山已生成在位置: {position}");
    }

    private void GenerateLake()
    {
        // 检查预制体数组是否为空
        if (lakePrefabs == null || lakePrefabs.Length == 0)
        {
            Debug.LogError("错误：湖泊预制体数组为空！请在Inspector中设置湖泊预制体。");
            return;
        }
        
        // 生成湖泊
        Vector3 position = Vector3.zero;
        int maxAttempts = 100;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            position = GetRandomPositionOnTerrain();
            if (IsPositionValid(position))
            {
                break;
            }
            
            // 如果所有尝试都失败，使用最后一个位置
            if (attempt == maxAttempts - 1)
            {
                Debug.LogWarning("无法为湖泊找到有效位置，使用最后尝试的位置");
            }
        }
        
        // 随机选择一个湖泊预制体
        GameObject lakePrefab = lakePrefabs[Random.Range(0, lakePrefabs.Length)];
        
        // 放置湖泊
        GameObject lake = Instantiate(lakePrefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
        
        // 随机缩放湖泊
        float scaleVariation = Random.Range(lakeMinScale, lakeMaxScale);
        lake.transform.localScale *= scaleVariation;
        
        placedObjectPositions.Add(position);
    }

    private void GenerateFlatTerrain()
    {
        // 检查预制体数组是否为空
        if (flatTerrainPrefabs == null || flatTerrainPrefabs.Length == 0)
        {
            Debug.LogError("错误：平坦地形预制体数组为空！请在Inspector中设置平坦地形预制体。");
            return;
        }
        
        // 生成平坦地形
        Vector3 position = Vector3.zero;
        int maxAttempts = 50;
        
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            position = GetRandomPositionOnTerrain();
            // 修改Y坐标为固定值-0.2
            position.y = -0.2f;
            
            if (IsPositionValid(position))
            {
                break;
            }
            
            // 如果达到最大尝试次数仍找不到位置，则跳过此地形
            if (attempt == maxAttempts - 1)
            {
                return;
            }
        }
        
        // 随机选择一个平坦地形预制体
        GameObject flatTerrainPrefab = flatTerrainPrefabs[Random.Range(0, flatTerrainPrefabs.Length)];
        
        // 放置平坦地形
        GameObject flatTerrain = Instantiate(flatTerrainPrefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0), transform);
        
        // 随机缩放平坦地形
        float scaleVariation = Random.Range(flatTerrainMinScale, flatTerrainMaxScale);
        flatTerrain.transform.localScale *= scaleVariation;
        
        placedObjectPositions.Add(position);
    }

    // 可以从外部重新生成地图的公共方法
    public void RegenerateMap()
    {
        GenerateMap();
    }
} 