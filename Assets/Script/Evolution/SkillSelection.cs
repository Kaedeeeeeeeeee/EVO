using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSelection : MonoBehaviour
{
    private SkillLoader skillLoader;
    private Dictionary<Button, SkillData> cardData = new Dictionary<Button, SkillData>();

    public List<Button> cardButtons; // ✅ 在 Inspector 手动绑定卡片按钮

    void Start()
    {
        StartCoroutine(InitializeSelection());
    }

    IEnumerator<WaitForSeconds> InitializeSelection()
    {
        yield return new WaitForSeconds(0.2f); // ✅ 等待 SkillLoader 加载完成
        skillLoader = SkillLoader.Instance;

        if (skillLoader == null || !skillLoader.IsLoaded || skillLoader.skillList.Count == 0)
        {
            Debug.LogError("❌ `SkillLoader` 数据为空，无法生成卡牌！");
            yield break;
        }

        GenerateRandomSkills();
    }

    public void GenerateRandomSkills()
    {
        if (skillLoader == null || !skillLoader.IsLoaded || skillLoader.skillList.Count == 0)
        {
            Debug.LogError("❌ `SkillLoader` 数据为空，无法生成卡牌！");
            return;
        }

        List<SkillData> selectedSkills = new List<SkillData>();
        cardData.Clear();

        while (selectedSkills.Count < cardButtons.Count)
        {
            SkillData skill = skillLoader.skillList[Random.Range(0, skillLoader.skillList.Count)];
            if (!selectedSkills.Contains(skill))
            {
                selectedSkills.Add(skill);
            }
        }

        for (int i = 0; i < cardButtons.Count; i++)
        {
            SkillData skill = selectedSkills[i];
            cardData[cardButtons[i]] = skill;

            TextMeshProUGUI buttonText = cardButtons[i].GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{skill.skillName}\n({skill.cost} EVO-P)";
                Debug.Log($"✅ 卡牌 {i + 1} 生成成功: {skill.skillName} - {skill.cost} EVO-P");
            }
            else
            {
                Debug.LogError($"❌ 卡牌 {i + 1} 缺少 `TextMeshPro` 组件！");
            }

            cardButtons[i].interactable = true;
        }
    }
}
