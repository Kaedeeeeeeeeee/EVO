using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Linq;

#if UNITY_EDITOR
public class EnemyDataViewer : EditorWindow
{
    private Vector2 scrollPosition;
    private Dictionary<string, bool> typesFoldout = new Dictionary<string, bool>();
    private Dictionary<string, List<Dictionary<string, string>>> enemyTypeData = new Dictionary<string, List<Dictionary<string, string>>>();
    private bool showRawCSV = false;
    private string selectedTypeID = "";
    
    // 敌人类型数据
    private List<Dictionary<string, string>> enemyTypes = new List<Dictionary<string, string>>();
    // 敌人详细数据
    private List<Dictionary<string, string>> enemyStats = new List<Dictionary<string, string>>();
    
    [MenuItem("工具/敌人数据查看器")]
    public static void ShowWindow()
    {
        GetWindow<EnemyDataViewer>("敌人数据查看器");
    }
    
    private void OnEnable()
    {
        LoadData();
    }
    
    private void LoadData()
    {
        string typesPath = Path.Combine(Application.dataPath, "Resources/EnemyData/EnemyTypes.csv");
        string statsPath = Path.Combine(Application.dataPath, "Resources/EnemyData/EnemyStats.csv");
        
        if (File.Exists(typesPath) && File.Exists(statsPath))
        {
            // 加载敌人类型
            enemyTypes = CSVReader.ReadCSVFromFile(typesPath);
            
            // 加载敌人详细数据
            enemyStats = CSVReader.ReadCSVFromFile(statsPath);
            
            // 按类型分组
            enemyTypeData.Clear();
            
            foreach (var type in enemyTypes)
            {
                if (!type.ContainsKey("TypeID"))
                    continue;
                
                string typeID = type["TypeID"];
                
                if (!typesFoldout.ContainsKey(typeID))
                {
                    typesFoldout[typeID] = false;
                }
                
                // 筛选该类型的敌人数据
                List<Dictionary<string, string>> typeEnemies = enemyStats
                    .Where(enemy => enemy.ContainsKey("TypeID") && enemy["TypeID"] == typeID)
                    .OrderBy(enemy => int.Parse(enemy.ContainsKey("Level") ? enemy["Level"] : "1"))
                    .ToList();
                
                enemyTypeData[typeID] = typeEnemies;
            }
        }
        else
        {
            if (!File.Exists(typesPath))
                Debug.LogError($"未找到敌人类型文件: {typesPath}");
                
            if (!File.Exists(statsPath))
                Debug.LogError($"未找到敌人数据文件: {statsPath}");
        }
    }
    
    private void OnGUI()
    {
        EditorGUILayout.BeginVertical();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("敌人数据查看器", EditorStyles.boldLabel);
        
        if (GUILayout.Button("刷新数据"))
        {
            LoadData();
        }
        
        showRawCSV = EditorGUILayout.Toggle("显示原始CSV数据", showRawCSV);
        
        EditorGUILayout.Space(10);
        
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        
        if (showRawCSV)
        {
            DrawRawCSVData();
        }
        else
        {
            DrawFormattedData();
        }
        
        EditorGUILayout.EndScrollView();
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("注意：此工具仅用于查看数据，修改需直接编辑CSV文件。", EditorStyles.miniLabel);
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawRawCSVData()
    {
        EditorGUILayout.LabelField("敌人类型 (EnemyTypes.csv)", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (enemyTypes.Count > 0 && enemyTypes[0].Count > 0)
        {
            // 表头
            EditorGUILayout.BeginHorizontal();
            foreach (string key in enemyTypes[0].Keys)
            {
                EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(100));
            }
            EditorGUILayout.EndHorizontal();
            
            // 数据行
            foreach (var type in enemyTypes)
            {
                EditorGUILayout.BeginHorizontal();
                foreach (string key in type.Keys)
                {
                    EditorGUILayout.LabelField(type[key], GUILayout.Width(100));
                }
                EditorGUILayout.EndHorizontal();
            }
        }
        else
        {
            EditorGUILayout.LabelField("未加载敌人类型数据");
        }
        
        EditorGUILayout.EndVertical();
        
        EditorGUILayout.Space(20);
        
        EditorGUILayout.LabelField("敌人详细数据 (EnemyStats.csv)", EditorStyles.boldLabel);
        
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (enemyStats.Count > 0 && enemyStats[0].Count > 0)
        {
            // 表头
            EditorGUILayout.BeginHorizontal();
            foreach (string key in enemyStats[0].Keys)
            {
                EditorGUILayout.LabelField(key, EditorStyles.boldLabel, GUILayout.Width(80));
            }
            EditorGUILayout.EndHorizontal();
            
            // 数据行 (限制显示的行数以避免性能问题)
            int displayCount = Mathf.Min(enemyStats.Count, 100);
            for (int i = 0; i < displayCount; i++)
            {
                var enemy = enemyStats[i];
                EditorGUILayout.BeginHorizontal();
                foreach (string key in enemy.Keys)
                {
                    EditorGUILayout.LabelField(enemy[key], GUILayout.Width(80));
                }
                EditorGUILayout.EndHorizontal();
            }
            
            if (displayCount < enemyStats.Count)
            {
                EditorGUILayout.LabelField($"... 仅显示前 {displayCount} 行，共 {enemyStats.Count} 行");
            }
        }
        else
        {
            EditorGUILayout.LabelField("未加载敌人详细数据");
        }
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawFormattedData()
    {
        EditorGUILayout.LabelField("敌人类型", EditorStyles.boldLabel);
        
        foreach (var type in enemyTypes)
        {
            if (!type.ContainsKey("TypeID"))
                continue;
                
            string typeID = type["TypeID"];
            string typeName = type.ContainsKey("TypeName") ? type["TypeName"] : typeID;
            string description = type.ContainsKey("Description") ? type["Description"] : "";
            
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            
            EditorGUILayout.BeginHorizontal();
            
            typesFoldout[typeID] = EditorGUILayout.Foldout(typesFoldout[typeID], $"{typeName} ({typeID})");
            
            if (type.ContainsKey("SpawnWeight"))
            {
                EditorGUILayout.LabelField($"生成权重: {type["SpawnWeight"]}", GUILayout.Width(100));
            }
            
            if (GUILayout.Button("选择", GUILayout.Width(60)))
            {
                selectedTypeID = (selectedTypeID == typeID) ? "" : typeID;
            }
            
            EditorGUILayout.EndHorizontal();
            
            if (typesFoldout[typeID])
            {
                EditorGUILayout.LabelField($"描述: {description}");
                
                if (enemyTypeData.ContainsKey(typeID))
                {
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("敌人等级配置:", EditorStyles.boldLabel);
                    
                    foreach (var enemy in enemyTypeData[typeID])
                    {
                        DrawEnemyData(enemy);
                    }
                }
            }
            
            EditorGUILayout.EndVertical();
        }
        
        // 显示选中类型的详细数据比较
        if (!string.IsNullOrEmpty(selectedTypeID) && enemyTypeData.ContainsKey(selectedTypeID))
        {
            EditorGUILayout.Space(20);
            EditorGUILayout.LabelField($"{selectedTypeID} 敌人数据比较", EditorStyles.boldLabel);
            
            DrawEnemyDataComparison(selectedTypeID);
        }
    }
    
    private void DrawEnemyData(Dictionary<string, string> enemy)
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        if (enemy.ContainsKey("Name") && enemy.ContainsKey("Level"))
        {
            EditorGUILayout.LabelField($"Lv.{enemy["Level"]} {enemy["Name"]}", EditorStyles.boldLabel);
        }
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"生命: {GetValue(enemy, "MaxHealth")}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"攻击: {GetValue(enemy, "AttackDamage")}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"行走速度: {GetValue(enemy, "WalkSpeed")}", GUILayout.Width(120));
        EditorGUILayout.LabelField($"奔跑速度: {GetValue(enemy, "RunSpeed")}", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"视野: {GetValue(enemy, "VisionRange")}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"攻击范围: {GetValue(enemy, "AttackRange")}", GUILayout.Width(120));
        EditorGUILayout.LabelField($"猎杀阈值: {GetValue(enemy, "HuntThreshold")}%", GUILayout.Width(120));
        EditorGUILayout.LabelField($"生成权重: {GetValue(enemy, "SpawnWeight")}", GUILayout.Width(120));
        EditorGUILayout.EndHorizontal();
        
        // 体力系统
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"体力: {GetValue(enemy, "MaxStamina")}", GUILayout.Width(100));
        EditorGUILayout.LabelField($"体力消耗: {GetValue(enemy, "StaminaDecreaseRate")}/秒", GUILayout.Width(140));
        EditorGUILayout.LabelField($"体力回复: {GetValue(enemy, "StaminaRecoveryRate")}/秒", GUILayout.Width(140));
        EditorGUILayout.LabelField($"回复延迟: {GetValue(enemy, "StaminaRecoveryDelay")}秒", GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();
        
        // 尸体属性
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"尸体治疗: {GetValue(enemy, "CorpseHealAmount")}", GUILayout.Width(120));
        EditorGUILayout.LabelField($"尸体进化点: {GetValue(enemy, "CorpseEvoPoints")}", GUILayout.Width(140));
        EditorGUILayout.EndHorizontal();
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawEnemyDataComparison(string typeID)
    {
        var enemies = enemyTypeData[typeID];
        if (enemies == null || enemies.Count == 0)
            return;
            
        // 创建表格
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        
        // 表头
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("属性", EditorStyles.boldLabel, GUILayout.Width(100));
        
        foreach (var enemy in enemies)
        {
            string enemyName = GetValue(enemy, "Name");
            string level = GetValue(enemy, "Level");
            EditorGUILayout.LabelField($"Lv.{level}\n{enemyName}", EditorStyles.boldLabel, GUILayout.Width(80));
        }
        
        EditorGUILayout.EndHorizontal();
        
        // 绘制各个属性的比较
        DrawAttributeComparison(enemies, "MaxHealth", "生命值");
        DrawAttributeComparison(enemies, "AttackDamage", "攻击力");
        DrawAttributeComparison(enemies, "WalkSpeed", "行走速度");
        DrawAttributeComparison(enemies, "RunSpeed", "奔跑速度");
        DrawAttributeComparison(enemies, "VisionRange", "视野范围");
        DrawAttributeComparison(enemies, "AttackRange", "攻击范围");
        DrawAttributeComparison(enemies, "HuntThreshold", "猎杀阈值");
        DrawAttributeComparison(enemies, "MaxStamina", "最大体力");
        DrawAttributeComparison(enemies, "StaminaDecreaseRate", "体力消耗");
        DrawAttributeComparison(enemies, "StaminaRecoveryRate", "体力回复");
        DrawAttributeComparison(enemies, "StaminaRecoveryDelay", "回复延迟");
        DrawAttributeComparison(enemies, "CorpseHealAmount", "尸体治疗");
        DrawAttributeComparison(enemies, "CorpseEvoPoints", "进化点数");
        
        EditorGUILayout.EndVertical();
    }
    
    private void DrawAttributeComparison(List<Dictionary<string, string>> enemies, string attributeKey, string attributeName)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(attributeName, GUILayout.Width(100));
        
        foreach (var enemy in enemies)
        {
            EditorGUILayout.LabelField(GetValue(enemy, attributeKey), GUILayout.Width(80));
        }
        
        EditorGUILayout.EndHorizontal();
    }
    
    private string GetValue(Dictionary<string, string> data, string key, string defaultValue = "")
    {
        if (data.ContainsKey(key))
            return data[key];
        return defaultValue;
    }
}
#endif 