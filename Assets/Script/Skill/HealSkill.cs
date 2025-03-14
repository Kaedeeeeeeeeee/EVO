using UnityEngine;

public class HealSkill : SkillEffect
{
    public int healAmount = 50;

    public override void ApplyEffect(GameObject player)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            health.Heal(healAmount);
            Debug.Log($"💚 玩家恢复了 {healAmount} 生命值！");
        }
    }
}
