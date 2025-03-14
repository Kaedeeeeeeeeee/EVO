using UnityEngine;

public class ShieldSkill : MonoBehaviour
{
    public void ApplyEffect(GameObject player)
    {
        Debug.Log("🛡️ 启动护盾技能！玩家在 10 秒内减少 50% 伤害");

        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.StartCoroutine(ApplyShield(combat));
        }
    }

    private System.Collections.IEnumerator ApplyShield(PlayerCombat combat)
    {
        float originalMultiplier = combat.damageMultiplier;
        combat.damageMultiplier *= 0.5f; // 伤害减半
        yield return new WaitForSeconds(10);
        combat.damageMultiplier = originalMultiplier;
        Debug.Log("🛡️ 护盾效果结束");
    }
}
