using UnityEngine;

[System.Serializable]
public class EnemyData
{
    public string enemyName;          // 敌人名称
    public int level;                 // 敌人等级（1-5）
    public int maxHealth;             // 最大生命值
    public int attackDamage;          // 攻击伤害
    public float walkSpeed;           // 行走速度
    public float runSpeed;            // 奔跑速度
    public float visionRange;         // 视野范围
    public float attackRange;         // 攻击范围
    public float huntThreshold;       // 开始猎杀的饥饿阈值（百分比）
    public Material enemyMaterial;    // 敌人材质（用于区分不同等级）

    [Range(0, 100)]
    public int spawnWeight;           // 生成权重（用于控制不同等级敌人的生成概率）
    
    // 新增尸体相关属性
    [Header("尸体属性")]
    public int corpseHealAmount = 20; // 尸体恢复生命值
    public int corpseEvoPoints = 10;  // 尸体提供的进化点数
}

// 饥饿状态枚举
public enum HungerStatus
{
    Satiated,      // 饱食
    Normal,        // 正常
    Hungry,        // 饥饿
    Starving       // 饥饿难耐
}

// 敌人行为状态枚举
public enum EnemyState
{
    Idle,           // 闲置
    Wandering,      // 游荡
    Alert,          // 警觉
    Hunting,        // 追猎
    Attacking,      // 攻击
    Fleeing,        // 逃跑
    Eating          // 进食
}