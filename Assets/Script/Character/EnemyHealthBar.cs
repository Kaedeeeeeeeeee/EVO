using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyHealthBar : MonoBehaviour
{
    public Image healthBarFill;    // 拖拽血条填充图像
    public TextMeshProUGUI levelText;  // 等级文本
    public Slider healthSlider;    // 兼容性：原代码中使用的Slider引用

    [Header("显示设置")]
    public float showDuration = 3f;        // 显示时间
    public float fadeSpeed = 3f;           // 淡出速度
    public bool showOnStart = true;        // 初始是否显示
    public bool alwaysVisible = false;     // 是否始终显示（不淡出）

    private Canvas canvas;
    private Camera mainCamera;
    private CanvasGroup canvasGroup;
    private float hideTimer = 0f;
    private bool shouldHide = false;
    private bool hasBeenDamaged = false;   // 是否受过伤

    void Awake()
    {
        // 获取组件引用
        canvas = GetComponent<Canvas>();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // 如果没有指定healthBarFill但有Slider，使用Slider的fillRect
        if (healthBarFill == null && healthSlider != null && healthSlider.fillRect != null)
        {
            healthBarFill = healthSlider.fillRect.GetComponent<Image>();
        }

        // 确保我们有主摄像机引用
        mainCamera = Camera.main;

        // 设置Canvas的Event Camera
        if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
        {
            canvas.worldCamera = mainCamera;
        }
    }

    void Start()
    {
        // 初始显示或隐藏
        if (showOnStart)
        {
            ShowHealthBar();
        }
        else
        {
            HideHealthBar();
        }
    }

    void LateUpdate()
    {
        // 确保面向摄像机
        if (mainCamera != null)
        {
            // 直接面向摄像机
            transform.LookAt(mainCamera.transform.position);
        }

        // 处理淡出
        if (shouldHide && !alwaysVisible)
        {
            hideTimer += Time.deltaTime;
            if (hideTimer >= showDuration)
            {
                canvasGroup.alpha = Mathf.Lerp(canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
                if (canvasGroup.alpha < 0.01f)
                {
                    canvasGroup.alpha = 0;
                }
            }
        }
    }

    // 设置血条值和显示
    public void SetHealth(float currentHealth, float maxHealth)
    {
        float fillAmount = Mathf.Clamp01(currentHealth / maxHealth);
        hasBeenDamaged = true;

        // 更新滑动条（如果有）
        if (healthSlider != null)
        {
            healthSlider.value = fillAmount;
        }

        // 直接更新填充图像（如果有）
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = fillAmount;

            // 根据血量变化颜色
            healthBarFill.color = Color.Lerp(Color.red, Color.green, fillAmount);
        }

        // 显示血条
        ShowHealthBar();
    }

    // 设置敌人等级显示
    public void SetLevel(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Lv{level}";

            // 根据等级设置颜色
            switch (level)
            {
                case 1:
                    levelText.color = Color.white;
                    break;
                case 2:
                    levelText.color = Color.green;
                    break;
                case 3:
                    levelText.color = Color.blue;
                    break;
                case 4:
                    levelText.color = new Color(0.8f, 0, 0.8f); // 紫色
                    break;
                case 5:
                    levelText.color = Color.red;
                    break;
                default:
                    levelText.color = Color.white;
                    break;
            }
        }
    }

    // 显示血条
    public void ShowHealthBar()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1;
            hideTimer = 0f;
            shouldHide = true;
        }
    }

    // 隐藏血条
    public void HideHealthBar()
    {
        if (canvasGroup != null && !alwaysVisible)
        {
            shouldHide = false;
            canvasGroup.alpha = 0;
        }
    }

    // 强制永久显示
    public void SetAlwaysVisible(bool value)
    {
        alwaysVisible = value;
        if (alwaysVisible)
        {
            ShowHealthBar();
        }
    }

    // 允许外部设置显示持续时间
    public void SetShowDuration(float duration)
    {
        showDuration = duration;
    }
}