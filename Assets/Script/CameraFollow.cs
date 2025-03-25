using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // 跟踪目标（Player）
    public float smoothSpeed = 0.125f;
    public Vector3 offset = new Vector3(0, 7, -10); // 增加高度值和后退距离
    
    private void Start()
    {
        // 如果没有预设目标，则查找Player
        if (target == null)
            FindPlayerTarget();
    }
    
    private void FindPlayerTarget()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            Debug.Log("相机已找到玩家目标");
        }
        else
        {
            Debug.LogError("无法找到玩家对象！请确保Player正确设置了Tag");
            // 5秒后重试
            Invoke("FindPlayerTarget", 5f);
        }
    }
    
    private void LateUpdate()
    {
        if (target == null)
        {
            FindPlayerTarget();
            return;
        }
        
        // 计算期望位置
        Vector3 desiredPosition = target.position + offset;
        
        // 平滑移动
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;
        
        // 摄像机看向玩家位置的稍上方，避免盯着角色脚部
        transform.LookAt(target.position + Vector3.up * 1.5f);
        
        // 调试信息
        Debug.DrawLine(transform.position, target.position, Color.red);
    }
}
