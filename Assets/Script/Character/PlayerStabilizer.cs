using UnityEngine;

// 这个组件确保玩家角色始终保持垂直
public class PlayerStabilizer : MonoBehaviour
{
    private Rigidbody rb;
    private CharacterController charController;
    
    // 是否对玩家进行物理约束
    public bool constrainPhysics = true;
    
    // 是否保持垂直朝向
    public bool keepVertical = true;
    
    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        charController = GetComponent<CharacterController>();
        
        // 设置标签以便正确识别
        gameObject.tag = "Player";
    }
    
    void Start()
    {
        // 初始化时确保角色保持垂直
        ResetOrientation();
        
        // 如果有刚体组件，应用约束
        if (rb != null && constrainPhysics)
        {
            // 冻结所有旋转，允许位置移动
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.angularVelocity = Vector3.zero;
            
            // 设置更高的质量，增加稳定性
            rb.mass = 70f;
        }
    }
    
    void LateUpdate()
    {
        if (keepVertical)
        {
            StabilizeOrientation();
        }
    }
    
    // 强制角色垂直站立
    public void ResetOrientation()
    {
        // 保持Y轴旋转，重置XZ轴
        float yRotation = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0f, yRotation, 0f);
        
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }
    
    // 确保角色保持垂直
    private void StabilizeOrientation()
    {
        // 只修正X和Z轴的旋转，保留Y轴（允许角色转向）
        Vector3 currentEuler = transform.eulerAngles;
        
        if (Mathf.Abs(currentEuler.x) > 1f || Mathf.Abs(currentEuler.z) > 1f)
        {
            transform.rotation = Quaternion.Euler(0f, currentEuler.y, 0f);
            
            // 如果有刚体，确保停止旋转
            if (rb != null)
            {
                rb.angularVelocity = Vector3.zero;
            }
            
            Debug.Log("已修正玩家倾斜: " + currentEuler + " -> " + transform.eulerAngles);
        }
    }
    
    // 当碰撞检测到玩家可能倾斜时调用
    void OnCollisionEnter(Collision collision)
    {
        ResetOrientation();
    }
    
    // 当玩家以高速移动时
    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (charController != null && charController.velocity.magnitude > 3f)
        {
            ResetOrientation();
        }
    }
} 