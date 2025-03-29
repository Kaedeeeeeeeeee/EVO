using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

/// <summary>
/// 敌人数据管理器，负责从CSV文件加载敌人配置
/// </summary>
public class EnemyDataManager : MonoBehaviour
{
    // 单例实例
    public static EnemyDataManager Instance { get; private set; }

    [Header("配置文件设置")]
    [Tooltip("敌人数据CSV文件路径（相对于Resources文件夹）")]
    public string enemyDataFilePath = "EnemyData/EnemyStats";

    [Tooltip("敌人类型CSV文件路径（相对于Resources文件夹）")]
    public string enemyTypesFilePath = "EnemyData/EnemyTypes";

    [Header("敌人预制体设置")]
    [Tooltip("敌人类型预制体映射")]
    public List<EnemyTypePrefabMapping> enemyPrefabMappings = new List<EnemyTypePrefabMapping>();

    // 敌人数据缓存
    private Dictionary<string, List<EnemyData>> enemyDataByType = new Dictionary<string, List<EnemyData>>();
    private Dictionary<string, EnemyTypeData> enemyTypeData = new Dictionary<string, EnemyTypeData>();
    private bool isInitialized = false;

    [Serializable]
    public class EnemyTypePrefabMapping
    {
        public string enemyTypeID;        // 敌人类型ID
        public string enemyTypeName;      // 敌人类型名称（显示用）
        public GameObject prefab;         // 对应的预制体
    }

    [Serializable]
    public class EnemyTypeData
    {
        public string typeID;             // 类型ID
        public string typeName;           // 类型名称
        public string description;        // 描述
        public int spawnWeight;           // 生成权重
        public GameObject prefab;         // 预制体引用
    }

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadAllEnemyData();
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 加载所有敌人数据
    /// </summary>
    public void LoadAllEnemyData()
    {
        try
        {
            // 加载敌人类型数据
            LoadEnemyTypes();
            
            // 加载敌人详细数据
            LoadEnemyDetailData();
            
            isInitialized = true;
            Debug.Log("✅ 敌人数据管理器初始化成功");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 加载敌人数据失败: {ex.Message}");
        }
    }

    /// <summary>
    /// 加载敌人类型数据
    /// </summary>
    private void LoadEnemyTypes()
    {
        var typesData = CSVReader.ReadCSVFromResources(enemyTypesFilePath);
        
        enemyTypeData.Clear();
        
        foreach (var typeRow in typesData)
        {
            if (!typeRow.ContainsKey("TypeID") || string.IsNullOrEmpty(typeRow["TypeID"]))
                continue;
                
            string typeID = typeRow["TypeID"];
            
            EnemyTypeData type = new EnemyTypeData
            {
                typeID = typeID,
                typeName = typeRow.ContainsKey("TypeName") ? typeRow["TypeName"] : typeID,
                description = typeRow.ContainsKey("Description") ? typeRow["Description"] : "",
                spawnWeight = typeRow.ContainsKey("SpawnWeight") ? ParseInt(typeRow["SpawnWeight"], 50) : 50
            };
            
            // 查找匹配的预制体
            var mapping = enemyPrefabMappings.Find(m => m.enemyTypeID == typeID);
            if (mapping != null && mapping.prefab != null)
            {
                type.prefab = mapping.prefab;
            }
            else
            {
                Debug.LogWarning($"⚠️ 敌人类型 '{typeID}' 没有找到对应的预制体映射");
            }
            
            enemyTypeData[typeID] = type;
        }
        
        Debug.Log($"✅ 成功加载 {enemyTypeData.Count} 种敌人类型");
    }

    /// <summary>
    /// 加载敌人详细数据
    /// </summary>
    private void LoadEnemyDetailData()
    {
        var enemyStats = CSVReader.ReadCSVFromResources(enemyDataFilePath);
        
        enemyDataByType.Clear();
        
        foreach (var statRow in enemyStats)
        {
            if (!statRow.ContainsKey("TypeID") || !statRow.ContainsKey("Level"))
                continue;
                
            string typeID = statRow["TypeID"];
            
            // 创建敌人数据对象
            EnemyData enemyData = new EnemyData
            {
                // 基础属性
                enemyName = statRow.ContainsKey("Name") ? statRow["Name"] : $"{typeID}_{statRow["Level"]}",
                level = ParseInt(statRow["Level"], 1),
                
                // 战斗属性
                maxHealth = ParseInt(statRow["MaxHealth"], 50),
                attackDamage = ParseInt(statRow["AttackDamage"], 5),
                walkSpeed = ParseFloat(statRow["WalkSpeed"], 2f),
                runSpeed = ParseFloat(statRow["RunSpeed"], 4f),
                visionRange = ParseFloat(statRow["VisionRange"], 8f),
                attackRange = ParseFloat(statRow["AttackRange"], 1.5f),
                huntThreshold = ParseFloat(statRow["HuntThreshold"], 30f),
                
                // 生成权重
                spawnWeight = ParseInt(statRow["SpawnWeight"], 50),
                
                // 体力系统
                maxStamina = ParseFloat(statRow["MaxStamina"], 100f),
                staminaDecreaseRate = ParseFloat(statRow["StaminaDecreaseRate"], 1f),
                staminaRecoveryRate = ParseFloat(statRow["StaminaRecoveryRate"], 2f),
                staminaRecoveryDelay = ParseFloat(statRow["StaminaRecoveryDelay"], 1f),
                
                // 尸体属性
                corpseHealAmount = ParseInt(statRow["CorpseHealAmount"], 20),
                corpseEvoPoints = ParseInt(statRow["CorpseEvoPoints"], 10)
            };
            
            // 处理材质（如果有）
            if (statRow.ContainsKey("MaterialPath") && !string.IsNullOrEmpty(statRow["MaterialPath"]))
            {
                enemyData.enemyMaterial = Resources.Load<Material>(statRow["MaterialPath"]);
            }
            
            // 将敌人数据添加到对应类型的列表中
            if (!enemyDataByType.ContainsKey(typeID))
            {
                enemyDataByType[typeID] = new List<EnemyData>();
            }
            
            enemyDataByType[typeID].Add(enemyData);
        }
        
        // 排序每个类型的敌人（按等级）
        foreach (var type in enemyDataByType.Keys.ToList())
        {
            enemyDataByType[type].Sort((a, b) => a.level.CompareTo(b.level));
        }
        
        Debug.Log($"✅ 成功加载敌人数据，共 {enemyDataByType.Count} 种类型");
    }

    /// <summary>
    /// 获取所有敌人类型ID
    /// </summary>
    public List<string> GetAllEnemyTypeIDs()
    {
        if (!isInitialized)
            LoadAllEnemyData();
            
        return enemyTypeData.Keys.ToList();
    }

    /// <summary>
    /// 获取指定类型的敌人数据列表
    /// </summary>
    public List<EnemyData> GetEnemyDataByType(string typeID)
    {
        if (!isInitialized)
            LoadAllEnemyData();
            
        if (enemyDataByType.ContainsKey(typeID))
            return enemyDataByType[typeID];
            
        return new List<EnemyData>();
    }

    /// <summary>
    /// 获取指定类型的敌人预制体
    /// </summary>
    public GameObject GetEnemyPrefab(string typeID)
    {
        if (!isInitialized)
            LoadAllEnemyData();
            
        if (enemyTypeData.ContainsKey(typeID) && enemyTypeData[typeID].prefab != null)
            return enemyTypeData[typeID].prefab;
            
        // 如果没有找到，返回第一个可用的预制体
        var firstValidMapping = enemyPrefabMappings.FirstOrDefault(m => m.prefab != null);
        if (firstValidMapping != null)
            return firstValidMapping.prefab;
            
        Debug.LogError($"❌ 找不到敌人类型 '{typeID}' 的预制体，也没有默认预制体可用");
        return null;
    }

    /// <summary>
    /// 获取敌人类型数据
    /// </summary>
    public EnemyTypeData GetEnemyTypeData(string typeID)
    {
        if (!isInitialized)
            LoadAllEnemyData();
            
        if (enemyTypeData.ContainsKey(typeID))
            return enemyTypeData[typeID];
            
        return null;
    }

    /// <summary>
    /// 获取指定敌人类型的生成权重
    /// </summary>
    public int GetEnemyTypeSpawnWeight(string typeID)
    {
        if (!isInitialized)
            LoadAllEnemyData();
            
        if (enemyTypeData.ContainsKey(typeID))
            return enemyTypeData[typeID].spawnWeight;
            
        return 0;
    }

    /// <summary>
    /// 根据预制体获取敌人类型ID
    /// </summary>
    public string GetEnemyTypeIDByPrefab(GameObject prefab)
    {
        if (prefab == null)
            return null;
            
        var mapping = enemyPrefabMappings.Find(m => m.prefab == prefab);
        return mapping?.enemyTypeID;
    }

    // 辅助方法：安全解析整数
    private int ParseInt(string value, int defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
            
        if (int.TryParse(value, out int result))
            return result;
            
        return defaultValue;
    }

    // 辅助方法：安全解析浮点数
    private float ParseFloat(string value, float defaultValue)
    {
        if (string.IsNullOrWhiteSpace(value))
            return defaultValue;
            
        if (float.TryParse(value, out float result))
            return result;
            
        return defaultValue;
    }
} 