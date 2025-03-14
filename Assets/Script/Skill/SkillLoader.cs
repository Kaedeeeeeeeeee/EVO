using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SkillLoader : MonoBehaviour
{
    public static SkillLoader Instance { get; private set; }

    public List<SkillData> skillList = new List<SkillData>();
    public bool IsLoaded { get; private set; } = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        StartCoroutine(LoadSkills()); // ✅ 确保 Start() 里正确调用协程
    }

    IEnumerator LoadSkills() // ⬅️ 修正返回类型为 IEnumerator
    {
        yield return new WaitForSeconds(0.1f); // 防止异步问题

        TextAsset skillData = Resources.Load<TextAsset>("skills");
        if (skillData == null)
        {
            Debug.LogError("❌ 未找到技能数据文件！");
            yield break;
        }

        string[] lines = skillData.text.Split('\n');
        Debug.Log($"📄 读取到 {lines.Length} 行数据");

        for (int i = 1; i < lines.Length; i++)
        {
            string[] fields = lines[i].Split(',');

            if (fields.Length < 5) continue;

            string effectTypeString = fields[4].Trim().Replace("\"", "").Replace("'", ""); // 处理引号问题
            if (!System.Enum.TryParse(effectTypeString, out SkillEffectType parsedEffectType))
            {
                Debug.LogError($"❌ CSV 文件第 {i + 1} 行的 `effectType` 无效: '{effectTypeString}'");
                continue;
            }

            SkillData skill = new SkillData
            {
                skillName = fields[1],
                description = fields[2],
                cost = int.Parse(fields[3]),
                effectType = parsedEffectType
            };

            skillList.Add(skill);
        }

        IsLoaded = true; // ✅ 标记加载完成
        Debug.Log($"✅ `SkillLoader` 加载完成，技能数量: {skillList.Count}");
    }
}
