using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using System;

/// <summary>
/// 简化版NavMeshManager，不再进行运行时NavMesh烘焙
/// </summary>
public class NavMeshManager : MonoBehaviour
{
    public static NavMeshManager Instance { get; private set; }
    
    // 表示当前NavMesh状态
    public enum NavMeshState
    {
        NotInitialized,
        Initializing,
        BakingInProgress,
        BakeComplete,
        Failed
    }
    
    public NavMeshState CurrentState { get; private set; } = NavMeshState.BakeComplete;
    public event Action OnNavMeshBakeComplete;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("NavMeshManager已初始化 - 使用预烘焙NavMesh");
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }
    
    private void Start()
    {
        // 直接通知NavMesh已就绪
        StartCoroutine(TriggerNavMeshReadyEvent());
    }
    
    private IEnumerator TriggerNavMeshReadyEvent()
    {
        yield return new WaitForSeconds(0.5f);
        OnNavMeshBakeComplete?.Invoke();
        Debug.Log("NavMeshManager已通知系统NavMesh已就绪");
    }
    
    // 公共方法：保留这些方法仅用于兼容性，不再执行实际的烘焙
    public void BakeNavMesh()
    {
        Debug.Log("NavMesh已在编辑器中预先烘焙，无需运行时烘焙");
        OnNavMeshBakeComplete?.Invoke();
    }
    
    // 静态方法：从任何地方调用
    public static void BakeAllNavMeshes()
    {
        if (Instance != null)
        {
            Instance.BakeNavMesh();
        }
        else
        {
            Debug.LogWarning("NavMeshManager实例不存在，使用预烘焙的NavMesh");
        }
    }
} 