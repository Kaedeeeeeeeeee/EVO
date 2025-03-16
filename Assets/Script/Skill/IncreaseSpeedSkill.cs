using System.Collections;
using UnityEngine;

// 修复IncreaseSpeedSkill，使其继承自SkillEffect
public class IncreaseSpeedSkill : SkillEffect
{
    public float speedMultiplier = 1.5f; // 速度提升50%
    public float duration = 5f; // 持续5秒

    public override void ApplyEffect(GameObject player)
    {
        Debug.Log("🏃 增加移动速度！玩家移动速度提升 5 秒");

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            StartCoroutine(ApplySpeedBoost(movement));
        }
    }

    private IEnumerator ApplySpeedBoost(PlayerMovement movement)
    {
        float originalSpeed = movement.moveSpeed;
        movement.moveSpeed *= speedMultiplier; // 速度增加50%
        yield return new WaitForSeconds(duration);
        movement.moveSpeed = originalSpeed;
        Debug.Log("🏃 速度提升效果结束");

        // 效果结束后销毁组件
        Destroy(this);
    }
}