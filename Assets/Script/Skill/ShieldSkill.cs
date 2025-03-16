using System.Collections;
using UnityEngine;

// 修复ShieldSkill，使其继承自SkillEffect
public class ShieldSkill : SkillEffect
{
    public float duration = 10f;
    public float damageReduction = 0.5f; // 伤害减少50%

    public override void ApplyEffect(GameObject player)
    {
        Debug.Log("🛡️ 启动护盾技能！玩家在 10 秒内减少 50% 伤害");

        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            StartCoroutine(ApplyShield(combat));
        }
    }

    private IEnumerator ApplyShield(PlayerCombat combat)
    {
        float originalMultiplier = combat.damageMultiplier;
        combat.damageMultiplier *= damageReduction; // 伤害减半
        yield return new WaitForSeconds(duration);
        combat.damageMultiplier = originalMultiplier;
        Debug.Log("🛡️ 护盾效果结束");

        // 效果结束后销毁组件
        Destroy(this);
    }
}