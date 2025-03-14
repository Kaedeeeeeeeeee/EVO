using System.Collections;
using UnityEngine;

public class IncreaseDamageSkill : SkillEffect
{
    public float damageMultiplier = 1.5f;
    public float duration = 10f;

    public override void ApplyEffect(GameObject player)
    {
        PlayerCombat combat = player.GetComponent<PlayerCombat>();
        if (combat != null)
        {
            combat.StartCoroutine(BoostDamage(combat));
        }
    }

    private IEnumerator BoostDamage(PlayerCombat combat)
    {
        Debug.Log("🔥 伤害提升技能激活！");
        combat.damageMultiplier *= damageMultiplier;
        yield return new WaitForSeconds(duration);
        combat.damageMultiplier /= damageMultiplier;
        Debug.Log("⚡ 伤害提升技能结束");
    }
}
