using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class MainMenuManager : MonoBehaviour
{
    [Header("UI引用")]
    [SerializeField] private Button startGameButton;
    [SerializeField] private Button quitGameButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private GameObject settingsPanel;

    private void Start()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        // 添加按钮监听器
        startGameButton.onClick.AddListener(StartGame);
        if (quitGameButton != null)
            quitGameButton.onClick.AddListener(QuitGame);
        if (settingsButton != null)
            settingsButton.onClick.AddListener(ToggleSettings);
    }

    public void StartGame()
    {
        // 确保UI设置正确
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        
        // 在加载新场景前清理GameManager状态
        GameManager.PrepareForSceneChange();
        
        // 直接加载游戏场景，避免使用复杂的异步加载
        SceneManager.LoadScene("GameScene");
    }

    public void QuitGame()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }

    public void ToggleSettings()
    {
        if (settingsPanel != null)
            settingsPanel.SetActive(!settingsPanel.activeSelf);
    }
} 