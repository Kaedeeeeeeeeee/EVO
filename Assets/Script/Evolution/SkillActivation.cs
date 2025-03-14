using UnityEngine;

public class SkillActivation : MonoBehaviour
{
    public void ActivateSkillEffect(SkillEffectType effectType)
    {
        GameObject player = GameObject.FindWithTag("Player");

        if (player == null)
        {
            Debug.LogError("❌ 找不到 Player，无法激活技能！");
            return;
        }

        switch (effectType)
        {
            case SkillEffectType.IncreaseDamage:
                player.AddComponent<IncreaseDamageSkill>().ApplyEffect(player);
                break;
            case SkillEffectType.IncreaseSpeed:
                player.AddComponent<IncreaseSpeedSkill>().ApplyEffect(player);
                break;
            case SkillEffectType.Heal:
                player.AddComponent<HealSkill>().ApplyEffect(player);
                break;
            case SkillEffectType.Shield:
                player.AddComponent<ShieldSkill>().ApplyEffect(player);
                break;
            default:
                Debug.LogWarning($"⚠️ 未知技能类型：{effectType}");
                break;
        }
    }
}
