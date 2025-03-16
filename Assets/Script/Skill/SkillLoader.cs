using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class SkillLoader : MonoBehaviour
{
    public static SkillLoader Instance { get; private set; }

    public List<SkillData> skillList = new List<SkillData>();
    public bool IsLoaded { get; private set; } = false;

    // 添加事件系统，通知技能加载完成
    public event Action OnSkillsLoaded;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    void Start()
    {
        StartCoroutine(LoadSkills());
    }

    IEnumerator LoadSkills()
    {
        yield return new WaitForSeconds(0.1f); // 防止异步问题

        TextAsset skillData = Resources.Load<TextAsset>("skills");
        if (skillData == null)
        {
            Debug.LogError("❌ 未找到技能数据文件！请确保Resources文件夹中存在'skills.csv'文件");
            yield break;
        }

        string[] lines = skillData.text.Split('\n');
        Debug.Log($"📄 读取到 {lines.Length} 行数据");

        // 清空列表，防止重复加载
        skillList.Clear();

        for (int i = 1; i < lines.Length; i++) // 从第二行开始，跳过表头
        {
            string line = lines[i].Trim();
            if (string.IsNullOrEmpty(line)) continue;

            string[] fields = line.Split(',');

            if (fields.Length < 5)
            {
                Debug.LogWarning($"⚠️ 第 {i + 1} 行数据不完整，跳过: {line}");
                continue;
            }

            try
            {
                // 清理引号和空格
                string effectTypeString = fields[4].Trim().Replace("\"", "").Replace("'", "");

                if (!Enum.TryParse(effectTypeString, out SkillEffectType parsedEffectType))
                {
                    Debug.LogError($"❌ CSV 文件第 {i + 1} 行的 `effectType` 无效: '{effectTypeString}'");
                    continue;
                }

                // 尝试解析ID和cost字段
                if (!int.TryParse(fields[0], out int id))
                {
                    Debug.LogWarning($"⚠️ 第 {i + 1} 行ID字段解析失败: {fields[0]}，使用默认值");
                    id = i;
                }

                if (!int.TryParse(fields[3], out int cost))
                {
                    Debug.LogWarning($"⚠️ 第 {i + 1} 行cost字段解析失败: {fields[3]}，使用默认值");
                    cost = 1;
                }

                SkillData skill = new SkillData
                {
                    id = id,
                    skillName = fields[1],
                    description = fields[2],
                    cost = cost,
                    effectType = parsedEffectType
                };

                skillList.Add(skill);
                Debug.Log($"✅ 加载技能: {skill.skillName}, ID: {skill.id}, 类型: {skill.effectType}");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ 解析第 {i + 1} 行时出错: {e.Message}");
            }
        }

        IsLoaded = true;
        Debug.Log($"✅ `SkillLoader` 加载完成，技能数量: {skillList.Count}");

        // 触发事件通知
        OnSkillsLoaded?.Invoke();
    }
}