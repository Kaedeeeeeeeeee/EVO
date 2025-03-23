using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAirSpawner : MonoBehaviour
{
    [Header("生成设置")]
    [SerializeField] private float spawnHeight = 20f; // 生成高度
    [SerializeField] private float landingRadius = 10f; // 着陆区域半径
    [SerializeField] private GameObject playerPrefab; // 玩家预制体，如果需要实例化
    
    [Header("效果设置")]
    [SerializeField] private bool addEntryEffect = true; // 是否添加入场效果
    [SerializeField] private GameObject spawnEffectPrefab; // 生成特效
    [SerializeField] private GameObject landingEffectPrefab; // 着陆特效
    
    private TerrainGenerator terrainGenerator;
    private GameObject player;
    private bool hasSpawned = false;
    private Vector3 landingPosition;
    
    private void Start()
    {
        // 获取TerrainGenerator引用
        terrainGenerator = FindObjectOfType<TerrainGenerator>();
        
        if (terrainGenerator != null)
        {
            // 订阅地图生成完成事件
            terrainGenerator.OnMapGenerationComplete += SpawnPlayerFromAir;
        }
        else
        {
            // 如果找不到TerrainGenerator，直接尝试生成
            Invoke("SpawnPlayerFromAir", 1.0f);
        }
    }
    
    // 这个方法可以从TerrainGenerator完成时调用
    public void SpawnPlayerFromAir()
    {
        if (hasSpawned) return;
        
        // 获取玩家引用（如果场景中已有玩家）
        if (player == null)
        {
            player = GameObject.FindGameObjectWithTag("Player");
            
            // 如果场景中没有玩家且设置了预制体，则实例化一个
            if (player == null && playerPrefab != null)
            {
                player = Instantiate(playerPrefab);
            }
        }
        
        if (player == null)
        {
            Debug.LogError("无法找到玩家对象！");
            return;
        }
        
        // 找到一个合适的着陆位置
        FindLandingPosition();
        
        // 将玩家设置在空中
        Vector3 spawnPosition = new Vector3(landingPosition.x, landingPosition.y + spawnHeight, landingPosition.z);
        player.transform.position = spawnPosition;
        
        // 如果玩家有刚体组件，确保它启用了物理效果
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.velocity = Vector3.zero;
            rb.isKinematic = false;
        }
        
        // 生成特效
        if (addEntryEffect && spawnEffectPrefab != null)
        {
            Instantiate(spawnEffectPrefab, spawnPosition, Quaternion.identity);
        }
        
        // 监听着陆事件
        StartCoroutine(WatchForLanding());
        
        hasSpawned = true;
        Debug.Log($"玩家已从空中生成，位置: {spawnPosition}");
    }
    
    private void FindLandingPosition()
    {
        // 获取地图中心
        Vector3 mapCenter = Vector3.zero;
        if (terrainGenerator != null)
        {
            // 这里假设TerrainGenerator有一个获取中心点的方法，或者使用默认值
            // 如果TerrainGenerator类没有这样的方法，可以用估计值
            mapCenter = new Vector3(0, 0, 0);
        }
        
        // 在中心区域找一个随机位置
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float distance = Random.Range(0f, landingRadius);
        float x = mapCenter.x + Mathf.Cos(angle) * distance;
        float z = mapCenter.z + Mathf.Sin(angle) * distance;
        
        // 使用射线检测获取地面高度
        RaycastHit hit;
        if (Physics.Raycast(new Vector3(x, 1000f, z), Vector3.down, out hit, 2000f))
        {
            landingPosition = hit.point;
        }
        else
        {
            // 如果射线没有命中任何物体，使用默认高度
            landingPosition = new Vector3(x, 0, z);
        }
    }
    
    private IEnumerator WatchForLanding()
    {
        bool hasLanded = false;
        float lastY = player.transform.position.y;
        float stableTime = 0f;
        
        while (!hasLanded)
        {
            yield return new WaitForSeconds(0.1f);
            
            // 检测玩家是否已经停止下落
            float currentY = player.transform.position.y;
            if (Mathf.Approximately(lastY, currentY))
            {
                stableTime += 0.1f;
                if (stableTime >= 0.5f) // 如果玩家位置稳定超过0.5秒，认为已着陆
                {
                    hasLanded = true;
                }
            }
            else
            {
                stableTime = 0f;
            }
            
            lastY = currentY;
        }
        
        // 玩家已着陆，播放着陆特效
        if (addEntryEffect && landingEffectPrefab != null)
        {
            Instantiate(landingEffectPrefab, player.transform.position, Quaternion.identity);
        }
        
        Debug.Log("玩家已成功着陆！");
    }

    private void OnDestroy()
    {
        if (terrainGenerator != null)
        {
            terrainGenerator.OnMapGenerationComplete -= SpawnPlayerFromAir;
        }
    }
} 