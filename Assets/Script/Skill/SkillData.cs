using UnityEngine;

[System.Serializable]
public class SkillData
{
    public int id; // 技能 ID
    public string skillName; // 技能名称
    public string description; // 技能效果
    public int cost; // 进化点数消耗
    public SkillEffectType effectType; // 技能效果类型
}

// **技能效果类型（枚举）**这里的 SkillEffectType 和 CSV 里的 effectType 字段相匹配。
public enum SkillEffectType
{
    IncreaseDamage,
    IncreaseSpeed,
    Heal,
    Shield
}
