using UnityEngine;
using System.Collections;


public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    private CharacterController controller;
    private Vector3 velocity;
    private bool isStunned = false; // **是否处于僵直状态**
    private Rigidbody rb; // 添加刚体引用

    void Start()
    {
        controller = GetComponent<CharacterController>();
        rb = GetComponent<Rigidbody>();
        
        // 如果有刚体组件，确保约束旋转
        if (rb != null)
        {
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.angularVelocity = Vector3.zero;
        }
        
        // 确保角色初始状态垂直站立
        transform.rotation = Quaternion.identity;
    }

    void Update()
    {
        if (!isStunned) // **如果没有僵直，才允许移动**
        {
            Move();
        }
        ApplyGravity();
        
        // 每帧确保角色保持垂直
        EnsureVerticalOrientation();
    }
    
    // 确保角色保持垂直站立
    void EnsureVerticalOrientation()
    {
        // 只修改旋转的X和Z轴，保留Y轴旋转（允许角色转向）
        float currentYRotation = transform.eulerAngles.y;
        transform.rotation = Quaternion.Euler(0, currentYRotation, 0);
        
        // 如果有刚体，确保没有角速度
        if (rb != null)
        {
            rb.angularVelocity = Vector3.zero;
        }
    }

    void Move()
    {
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        Vector3 moveDirection = new Vector3(moveX, 0, moveZ).normalized;
        
        // 如果有移动输入，根据移动方向设置角色的朝向
        if (moveDirection.magnitude > 0.1f)
        {
            // 使角色面向移动方向
            float targetAngle = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, targetAngle, 0);
        }
        
        controller.Move(moveDirection * moveSpeed * Time.deltaTime);
    }

    void ApplyGravity()
    {
        if (controller.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }

    public void Stun(float duration)
    {
        StartCoroutine(StunCoroutine(duration));
    }

    private IEnumerator StunCoroutine(float duration)
    {
        isStunned = true;
        yield return new WaitForSeconds(duration);
        isStunned = false;
    }
    
    // 添加取消眩晕的方法
    public void CancelStun()
    {
        // 停止所有协程，防止多个眩晕协程同时运行
        StopAllCoroutines();
        
        // 立即解除眩晕状态
        isStunned = false;
        
        Debug.Log("玩家眩晕状态已取消");
    }
}
