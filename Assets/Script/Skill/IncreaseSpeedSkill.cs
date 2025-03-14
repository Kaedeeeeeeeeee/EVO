using UnityEngine;

public class IncreaseSpeedSkill : MonoBehaviour
{
    public void ApplyEffect(GameObject player)
    {
        Debug.Log("🏃 增加移动速度！玩家移动速度提升 5 秒");

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
        {
            movement.StartCoroutine(ApplySpeedBoost(movement));
        }
    }

    private System.Collections.IEnumerator ApplySpeedBoost(PlayerMovement movement)
    {
        float originalSpeed = movement.moveSpeed;
        movement.moveSpeed *= 1.5f; // 速度增加 50%
        yield return new WaitForSeconds(5);
        movement.moveSpeed = originalSpeed;
        Debug.Log("🏃 速度提升效果结束");
    }
}
