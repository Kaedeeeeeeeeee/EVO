using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemyHealthBar : MonoBehaviour
{
    public Slider healthSlider; // 绑定 UI 的 Slider
    private CanvasGroup canvasGroup;
    private Coroutine hideCoroutine;

    void Start()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            Debug.LogError("❌ `EnemyCanvas` 上缺少 CanvasGroup 组件，自动添加！");
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        Debug.Log("✅ `EnemyHealthBar` 初始化完成");
        HideHealthBar();
    }

    public void SetHealth(float currentHealth, float maxHealth)
    {
        healthSlider.value = currentHealth / maxHealth;
        ShowHealthBar();
    }

    private void ShowHealthBar()
    {
        canvasGroup.alpha = 1; // 显示血条
        if (hideCoroutine != null) StopCoroutine(hideCoroutine);
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void HideHealthBar()
    {
        canvasGroup.alpha = 0; // 隐藏血条
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(3f);
        HideHealthBar();
    }
}
