using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAirSpawner : MonoBehaviour
{
    [Header("生成设置")]
    [SerializeField] private float spawnHeight = 20f; // 生成高度
    [SerializeField] private float landingRadius = 10f; // 着陆区域半径
    [SerializeField] private GameObject playerPrefab; // 玩家预制体，如果需要实例化
    [SerializeField] private LayerMask groundLayer; // 地面层掩码
    
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
        // 如果没有设置地面层，默认使用"Default"层
        if (groundLayer.value == 0)
        {
            groundLayer = 1 << LayerMask.NameToLayer("Default");
            Debug.Log($"使用默认层作为地面层: {LayerMask.LayerToName(Mathf.RoundToInt(Mathf.Log(groundLayer.value, 2)))}");
        }
        
        // 直接尝试生成玩家
        SpawnPlayerFromAir();
    }
    
    // 这个方法可以从外部调用来重新生成玩家
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
        
        // 确保玩家垂直方向正确 - 使胶囊体站立
        player.transform.rotation = Quaternion.identity; // 重置旋转为默认值
        
        // 如果玩家有刚体组件，确保它启用了物理效果
        Rigidbody rb = player.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false; // 先关闭Kinematic状态
            rb.velocity = Vector3.zero; // 然后再设置速度
            rb.angularVelocity = Vector3.zero; // 重置角速度，防止旋转
            
            // 冻结旋转，只允许上下移动
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
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
        // 获取MainGround对象
        GameObject mainGround = GameObject.Find("MainGround");
        if (mainGround == null)
        {
            Debug.LogError("找不到MainGround对象！");
            landingPosition = Vector3.zero;
            return;
        }

        // 获取MainGround的Renderer组件来确定边界
        Renderer groundRenderer = mainGround.GetComponent<Renderer>();
        if (groundRenderer == null)
        {
            Debug.LogError("MainGround没有Renderer组件！");
            landingPosition = Vector3.zero;
            return;
        }

        // 获取MainGround的边界
        Bounds bounds = groundRenderer.bounds;
        Debug.Log($"MainGround边界: 中心点={bounds.center}, 大小={bounds.size}");
        
        // 在边界范围内随机选择一个位置，但要留出一定的边距
        float margin = 2f; // 边距，防止玩家生成在边缘
        float x = Random.Range(bounds.min.x + margin, bounds.max.x - margin);
        float z = Random.Range(bounds.min.z + margin, bounds.max.z - margin);
        
        Debug.Log($"随机选择的位置: x={x}, z={z}");
        
        // 使用射线检测获取地面高度
        Vector3 rayStart = new Vector3(x, 1000f, z);
        Ray ray = new Ray(rayStart, Vector3.down);
        RaycastHit hit;
        
        // 在Scene视图中绘制射线，便于调试
        Debug.DrawRay(rayStart, Vector3.down * 2000f, Color.red, 5f);
        
        if (Physics.Raycast(ray, out hit, 2000f, groundLayer))
        {
            landingPosition = hit.point;
            Debug.Log($"射线检测命中点: {landingPosition}，碰撞对象: {hit.collider.gameObject.name}，层: {LayerMask.LayerToName(hit.collider.gameObject.layer)}");
            
            // 在Scene视图中绘制命中点
            Debug.DrawLine(hit.point, hit.point + Vector3.up * 2f, Color.green, 5f);
        }
        else
        {
            // 如果射线没有命中任何物体，使用MainGround的Y坐标
            landingPosition = new Vector3(x, bounds.min.y, z);
            Debug.LogWarning($"射线检测失败，使用默认高度：{landingPosition}，当前地面层设置: {groundLayer.value}");
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
            
            // 添加null检查，避免场景切换时的错误
            if (player == null || !player.gameObject || this == null)
            {
                yield break; // 立即退出协程
            }
            
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
            
            // 每帧保持玩家垂直
            player.transform.rotation = Quaternion.identity;
            
            lastY = currentY;
        }
        
        // 玩家已着陆，播放着陆特效
        if (player != null && addEntryEffect && landingEffectPrefab != null)
        {
            Instantiate(landingEffectPrefab, player.transform.position, Quaternion.identity);
            
            // 着陆后，修改刚体约束
            Rigidbody rb = player.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // 着陆后，允许玩家水平移动，但仍冻结旋转
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.angularVelocity = Vector3.zero; // 再次确保角速度为0
            }
        }
        
        Debug.Log("玩家已成功着陆！");
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        
        if (terrainGenerator != null)
        {
            terrainGenerator.OnMapGenerationComplete -= SpawnPlayerFromAir;
        }
        
        // 清除引用
        player = null;
    }
} 