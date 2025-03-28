using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理游戏中的草地对象，确保它们不会干扰导航网格
/// </summary>
public class GrassManager : MonoBehaviour
{
    public static GrassManager Instance { get; private set; }
    
    [Header("草地预制体")]
    [SerializeField] private GameObject[] grassPrefabs; // 草地预制体数组
    
    [Header("生成设置")]
    [SerializeField] private int grassesPerFlatTerrain = 10; // 每个平坦地形上的草丛数量
    [SerializeField] private float minScale = 0.8f;
    [SerializeField] private float maxScale = 1.2f;
    [SerializeField] private float spawnDelay = 0.5f; // 生成延迟，避免过早生成
    
    [Header("NavMesh配置")]
    [SerializeField] private bool ensureNavMeshCompatibility = true; // 确保与NavMesh兼容
    [SerializeField] private string grassLayerName = "Decoration"; // 草地所在的层名称
    
    private List<GameObject> allGrassObjects = new List<GameObject>(); // 所有生成的草地对象
    private int grassLayerId = -1; // 草地层ID
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 确保对象是激活的
            if (!gameObject.activeSelf)
            {
                Debug.LogWarning("GrassManager游戏对象不活跃，正在激活");
                gameObject.SetActive(true);
            }
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // 尝试获取草地层ID
        grassLayerId = LayerMask.NameToLayer(grassLayerName);
        if (grassLayerId == -1)
        {
            Debug.LogWarning($"未找到名为 {grassLayerName} 的层，将使用默认层");
        }
        
        // 加载预制体（如果未指定）
        if (grassPrefabs == null || grassPrefabs.Length == 0)
        {
            LoadGrassPrefabs();
        }
    }
    
    private void Start()
    {
        // 确保对象是激活的
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("GrassManager游戏对象不活跃，正在激活");
            gameObject.SetActive(true);
        }
        
        // 查找TerrainGenerator并订阅事件
        TerrainGenerator terrain = FindObjectOfType<TerrainGenerator>();
        if (terrain != null)
        {
            terrain.OnNavMeshBakeComplete += OnNavMeshBakeComplete;
        }
    }
    
    // 当NavMesh烘焙完成时，开始生成草地
    private void OnNavMeshBakeComplete()
    {
        Debug.Log("GrassManager收到NavMesh烘焙完成通知，准备生成草地");
        
        // 确保对象处于活跃状态
        if (!gameObject.activeSelf)
        {
            Debug.LogWarning("GrassManager游戏对象不活跃，无法启动协程。正在激活对象");
            gameObject.SetActive(true);
        }
        
        // 使用定时器替代协程，更加安全
        if (gameObject.activeSelf)
        {
            StartCoroutine(SpawnGrassWithDelay(spawnDelay));
        }
        else
        {
            // 如果对象仍然不活跃，直接调用SpawnGrassOnAllTerrains
            Debug.LogWarning("GrassManager仍然不活跃，跳过延迟直接生成草地");
            Invoke("SpawnGrassOnAllTerrains", spawnDelay);
        }
    }
    
    // 延迟生成草地
    private IEnumerator SpawnGrassWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnGrassOnAllTerrains();
    }
    
    // 在所有平坦地形上生成草地
    public void SpawnGrassOnAllTerrains()
    {
        // 清除之前生成的草地
        ClearAllGrass();
        
        // 查找所有平坦地形
        List<GameObject> flatTerrains = new List<GameObject>();
        TerrainGenerator terrain = FindObjectOfType<TerrainGenerator>();
        
        if (terrain != null)
        {
            // 在TerrainGenerator的子对象中查找平坦地形
            foreach (Transform child in terrain.transform)
            {
                TerrainIdentifier identifier = child.GetComponent<TerrainIdentifier>();
                if (identifier != null && identifier.terrainType == "FlatTerrain")
                {
                    flatTerrains.Add(child.gameObject);
                }
            }
        }
        else
        {
            // 如果找不到TerrainGenerator，尝试直接通过标识符查找
            TerrainIdentifier[] identifiers = FindObjectsOfType<TerrainIdentifier>();
            foreach (TerrainIdentifier identifier in identifiers)
            {
                if (identifier.terrainType == "FlatTerrain")
                {
                    flatTerrains.Add(identifier.gameObject);
                }
            }
        }
        
        Debug.Log($"找到 {flatTerrains.Count} 个平坦地形，准备生成草地");
        
        // 开始生成草地
        StartCoroutine(SpawnGrassOnTerrainsCoroutine(flatTerrains));
    }
    
    // 在地形上生成草地的协程
    private IEnumerator SpawnGrassOnTerrainsCoroutine(List<GameObject> terrains)
    {
        int totalGrassCount = 0;
        
        foreach (GameObject terrain in terrains)
        {
            if (terrain == null) continue;
            
            // 获取地形边界
            Bounds bounds = GetTerrainBounds(terrain);
            
            // 在地形上随机生成草地
            for (int i = 0; i < grassesPerFlatTerrain; i++)
            {
                // 随机位置
                float x = Random.Range(bounds.min.x, bounds.max.x);
                float z = Random.Range(bounds.min.z, bounds.max.z);
                
                // 射线检测获取正确的y坐标
                Ray ray = new Ray(new Vector3(x, bounds.max.y + 5f, z), Vector3.down);
                RaycastHit hit;
                
                if (Physics.Raycast(ray, out hit, 20f, LayerMask.GetMask("Default")))
                {
                    // 只在命中地形时生成
                    if (hit.collider.gameObject == terrain || hit.collider.transform.IsChildOf(terrain.transform))
                    {
                        // 生成草地
                        GameObject grass = SpawnGrass(hit.point, terrain.transform);
                        if (grass != null)
                        {
                            totalGrassCount++;
                            
                            // 确保草地在正确的层上
                            if (ensureNavMeshCompatibility && grassLayerId != -1)
                            {
                                SetLayerRecursively(grass, grassLayerId);
                            }
                        }
                    }
                }
            }
            
            // 每处理几个地形就等待一帧
            if (terrains.IndexOf(terrain) % 5 == 0)
            {
                yield return null;
            }
        }
        
        Debug.Log($"草地生成完成，共生成 {totalGrassCount} 个草丛");
        
        // 通知TerrainGenerator草地生成完成
        TerrainGenerator terrainGen = FindObjectOfType<TerrainGenerator>();
        if (terrainGen != null)
        {
            // 使用反射调用事件，避免直接访问事件
            System.Reflection.FieldInfo eventField = typeof(TerrainGenerator).GetField("OnGrassGenerationComplete", 
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            
            if (eventField != null)
            {
                System.Action eventDelegate = (System.Action)eventField.GetValue(terrainGen);
                eventDelegate?.Invoke();
                Debug.Log("已通知TerrainGenerator草地生成完成");
            }
            else
            {
                Debug.LogWarning("无法通过反射找到OnGrassGenerationComplete事件");
            }
        }
    }
    
    // 生成单个草地
    private GameObject SpawnGrass(Vector3 position, Transform parent)
    {
        if (grassPrefabs == null || grassPrefabs.Length == 0)
        {
            Debug.LogWarning("没有可用的草地预制体");
            return null;
        }
        
        // 随机选择一个草地预制体
        GameObject prefab = grassPrefabs[Random.Range(0, grassPrefabs.Length)];
        if (prefab == null)
        {
            return null;
        }
        
        // 实例化草地
        GameObject grass = Instantiate(prefab, position, Quaternion.Euler(0, Random.Range(0, 360), 0), parent);
        
        // 随机缩放
        float scale = Random.Range(minScale, maxScale);
        grass.transform.localScale = new Vector3(scale, scale, scale);
        
        // 添加到列表中
        allGrassObjects.Add(grass);
        
        return grass;
    }
    
    // 加载草地预制体
    private void LoadGrassPrefabs()
    {
        // 尝试从Resources文件夹加载
        GameObject grassPrefab = Resources.Load<GameObject>("Prefabs/Environment/Grass");
        if (grassPrefab != null)
        {
            grassPrefabs = new GameObject[] { grassPrefab };
            Debug.Log("从Resources加载了草地预制体");
        }
        else
        {
            Debug.LogWarning("无法从Resources加载草地预制体");
        }
    }
    
    // 获取地形的边界
    private Bounds GetTerrainBounds(GameObject terrain)
    {
        Bounds bounds = new Bounds(terrain.transform.position, Vector3.zero);
        
        // 获取所有子对象的渲染器
        Renderer[] renderers = terrain.GetComponentsInChildren<Renderer>();
        foreach (Renderer renderer in renderers)
        {
            bounds.Encapsulate(renderer.bounds);
        }
        
        // 获取所有子对象的碰撞体
        Collider[] colliders = terrain.GetComponentsInChildren<Collider>();
        foreach (Collider collider in colliders)
        {
            bounds.Encapsulate(collider.bounds);
        }
        
        return bounds;
    }
    
    // 递归设置层
    private void SetLayerRecursively(GameObject obj, int layerId)
    {
        obj.layer = layerId;
        foreach (Transform child in obj.transform)
        {
            SetLayerRecursively(child.gameObject, layerId);
        }
    }
    
    // 清除所有生成的草地
    public void ClearAllGrass()
    {
        foreach (GameObject grass in allGrassObjects)
        {
            if (grass != null)
            {
                Destroy(grass);
            }
        }
        
        allGrassObjects.Clear();
        Debug.Log("已清除所有草地对象");
    }
    
    // 禁用所有草地（用于NavMesh烘焙）
    public void DisableAllGrass()
    {
        foreach (GameObject grass in allGrassObjects)
        {
            if (grass != null)
            {
                grass.SetActive(false);
            }
        }
        
        Debug.Log("已禁用所有草地对象");
    }
    
    // 启用所有草地
    public void EnableAllGrass()
    {
        foreach (GameObject grass in allGrassObjects)
        {
            if (grass != null)
            {
                grass.SetActive(true);
            }
        }
        
        Debug.Log("已启用所有草地对象");
    }
    
    private void OnDestroy()
    {
        // 解除事件订阅
        TerrainGenerator terrain = FindObjectOfType<TerrainGenerator>();
        if (terrain != null)
        {
            terrain.OnNavMeshBakeComplete -= OnNavMeshBakeComplete;
        }
    }
} 