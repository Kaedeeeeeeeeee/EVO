using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    public Transform target; // 角色
    public Vector3 offset = new Vector3(0, 10, -5); // 摄像机偏移
    public float smoothSpeed = 5f; // 平滑跟随速度

    private void LateUpdate()
    {
        if (target == null) return;

        // 计算目标位置
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

        // 让摄像机朝向角色
        transform.LookAt(target);
    }
}
