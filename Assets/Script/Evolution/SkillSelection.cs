using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillSelection : MonoBehaviour
{
    private SkillLoader skillLoader;
    private Dictionary<Button, SkillData> cardData = new Dictionary<Button, SkillData>();

    public List<Button> cardButtons; // ✅ 在 Inspector 手动绑定卡片按钮

    // 添加引用到PlayerEvolution脚本
    private PlayerEvolution playerEvolution;

    // 添加引用到SkillActivation脚本
    public SkillActivation skillActivation;

    void Start()
    {
        // 查找玩家进化组件
        playerEvolution = FindObjectOfType<PlayerEvolution>();
        if (playerEvolution == null)
        {
            Debug.LogError("❌ 未找到PlayerEvolution组件！");
        }

        // 如果没有绑定SkillActivation，尝试自动查找
        if (skillActivation == null)
        {
            skillActivation = FindObjectOfType<SkillActivation>();
            if (skillActivation == null)
            {
                Debug.LogError("❌ 未找到SkillActivation组件！");
            }
        }

        // 检查卡片按钮是否正确绑定
        if (cardButtons == null || cardButtons.Count == 0)
        {
            Debug.LogError("❌ 卡片按钮列表为空！请在Inspector中设置。");
            return;
        }

        // 设置卡片按钮的点击事件
        SetupCardButtons();

        // 获取SkillLoader实例
        skillLoader = SkillLoader.Instance;

        if (skillLoader == null)
        {
            Debug.LogError("❌ 未找到SkillLoader单例！");
            return;
        }

        // 如果技能已经加载完成，直接生成卡牌
        if (skillLoader.IsLoaded)
        {
            GenerateRandomSkills();
        }
        // 否则订阅加载完成事件
        else
        {
            skillLoader.OnSkillsLoaded += OnSkillsLoaded;
            Debug.Log("⏳ 等待技能数据加载完成...");
        }
    }

    private void OnSkillsLoaded()
    {
        Debug.Log("✅ 技能数据加载完成，生成卡牌");
        GenerateRandomSkills();
    }

    private void SetupCardButtons()
    {
        // 为每个卡片按钮添加点击事件
        for (int i = 0; i < cardButtons.Count; i++)
        {
            int index = i; // 捕获索引
            Button button = cardButtons[i];

            // 确保按钮有文本组件
            if (button.GetComponentInChildren<TextMeshProUGUI>() == null)
            {
                Debug.LogError($"❌ 卡片 {i + 1} 缺少 TextMeshProUGUI 组件！");
            }

            button.onClick.RemoveAllListeners(); // 清除可能的旧监听器
            button.onClick.AddListener(() => OnCardButtonClicked(index));
        }
    }

    private void OnCardButtonClicked(int buttonIndex)
    {
        Button clickedButton = cardButtons[buttonIndex];
        if (!cardData.ContainsKey(clickedButton))
        {
            Debug.LogError($"❌ 卡片 {buttonIndex + 1} 没有关联的技能数据！");
            return;
        }

        SkillData selectedSkill = cardData[clickedButton];
        Debug.Log($"📝 选择了技能: {selectedSkill.skillName}，花费: {selectedSkill.cost} EVO-P");

        // 检查是否有足够的进化点数
        if (playerEvolution != null && playerEvolution.SpendEvolutionPoints(selectedSkill.cost))
        {
            // 激活技能效果
            if (skillActivation != null)
            {
                skillActivation.ActivateSkillEffect(selectedSkill.effectType);
                Debug.Log($"✨ 激活技能效果: {selectedSkill.effectType}");

                // 刷新卡牌
                GenerateRandomSkills();
            }
            else
            {
                Debug.LogError("❌ 无法激活技能：SkillActivation组件未找到");
            }
        }
        else
        {
            Debug.LogWarning("❌ 进化点数不足，无法购买技能！");
        }
    }

    public void GenerateRandomSkills()
    {
        if (skillLoader == null)
        {
            skillLoader = SkillLoader.Instance;
            if (skillLoader == null)
            {
                Debug.LogError("❌ 未找到SkillLoader单例！");
                return;
            }
        }

        if (!skillLoader.IsLoaded || skillLoader.skillList.Count == 0)
        {
            Debug.LogError("❌ `SkillLoader` 数据为空，无法生成卡牌！");
            return;
        }

        List<SkillData> selectedSkills = new List<SkillData>();
        cardData.Clear();

        // 检查是否有足够的技能可供选择
        if (skillLoader.skillList.Count < cardButtons.Count)
        {
            Debug.LogWarning($"⚠️ 技能总数({skillLoader.skillList.Count})少于卡片数量({cardButtons.Count})，将重复显示技能");
        }

        // 随机选择技能，尽量避免重复
        for (int i = 0; i < cardButtons.Count; i++)
        {
            // 如果已经选择的技能数量等于可用技能总数，清空选择列表以允许重复
            if (selectedSkills.Count >= skillLoader.skillList.Count)
            {
                selectedSkills.Clear();
            }

            SkillData skill;
            int attempts = 0;
            const int maxAttempts = 10;

            // 尝试选择一个未被选择的技能
            do
            {
                skill = skillLoader.skillList[Random.Range(0, skillLoader.skillList.Count)];
                attempts++;

                // 避免无限循环
                if (attempts >= maxAttempts)
                {
                    break;
                }
            } while (selectedSkills.Contains(skill));

            selectedSkills.Add(skill);
        }

        // 更新卡片UI
        for (int i = 0; i < cardButtons.Count; i++)
        {
            if (i >= selectedSkills.Count)
            {
                Debug.LogError($"❌ 选择的技能不足，无法为卡片 {i + 1} 设置技能！");
                continue;
            }

            SkillData skill = selectedSkills[i];
            Button button = cardButtons[i];
            cardData[button] = skill;

            TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = $"{skill.skillName}\n({skill.cost} EVO-P)";
                Debug.Log($"✅ 卡牌 {i + 1} 生成成功: {skill.skillName} - {skill.cost} EVO-P");
            }
            else
            {
                Debug.LogError($"❌ 卡牌 {i + 1} 缺少 `TextMeshPro` 组件！");
            }

            button.interactable = playerEvolution != null && playerEvolution.evolutionPoints >= skill.cost;
        }
    }
}