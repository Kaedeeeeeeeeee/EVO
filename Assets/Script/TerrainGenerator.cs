using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// 简化版TerrainGenerator，不再支持运行时生成地形和烘焙NavMesh
/// </summary>
public class TerrainGenerator : MonoBehaviour
{
    // 单例实例
    public static TerrainGenerator Instance { get; private set; }

    // 事件系统 - 保留以兼容现有代码
    public event System.Action OnTerrainGenerationComplete;
    public event System.Action OnNavMeshBakeComplete;
    public event System.Action OnGrassGenerationComplete;
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

        // 添加Temporary标签以便在场景切换时被正确清理
        gameObject.tag = "Temporary";
    }

    private void Start()
    {
        Debug.Log("TerrainGenerator启动，使用预先设计的地形...");
        
        // 直接触发所有事件以保持与现有系统的兼容性
        TriggerEvents();
    }
    
    /// <summary>
    /// 按顺序触发所有事件，以保持与现有系统的兼容性
    /// </summary>
    public void TriggerEvents()
    {
        StartCoroutine(TriggerEventsSequentially());
    }
    
    private IEnumerator TriggerEventsSequentially()
    {
        // 等待几帧确保所有组件都已初始化
        yield return null;
        yield return null;
        
        Debug.Log("触发地形生成完成事件...");
        OnTerrainGenerationComplete?.Invoke();
        
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("触发NavMesh烘焙完成事件...");
        OnNavMeshBakeComplete?.Invoke();
        
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("触发草地生成完成事件...");
        OnGrassGenerationComplete?.Invoke();
        
        yield return new WaitForSeconds(0.2f);
        
        Debug.Log("触发地图生成完成事件...");
        OnMapGenerationComplete?.Invoke();
        
        Debug.Log("地图已准备就绪（使用预先设计地形）");
    }
} 