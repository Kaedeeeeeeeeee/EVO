using UnityEngine;

/// <summary>
/// 工具类：用于清理对象上可能导致MissingReferenceException的引用
/// 可以添加到任何可能持有Transform、GameObject等引用的脚本上
/// </summary>
public class PropertyCleaner : MonoBehaviour
{
    /// <summary>
    /// 清理该组件上所有潜在的危险引用
    /// </summary>
    public void CleanReferences()
    {
        // 获取当前对象上的所有组件
        Component[] components = GetComponents<Component>();
        
        foreach (Component component in components)
        {
            if (component != null)
            {
                // 根据组件类型清理不同的引用
                if (component is EnemySpawner)
                {
                    EnemySpawner spawner = component as EnemySpawner;
                    if (spawner != null)
                    {
                        // 手动调用OnDestroy清理资源
                        spawner.enabled = false;
                    }
                }
                // 可以添加其他组件类型的处理
            }
        }
    }

    /// <summary>
    /// 当对象被禁用时调用，清理引用
    /// </summary>
    private void OnDisable()
    {
        CleanReferences();
    }
} 