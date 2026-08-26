using UnityEngine;
using UnityEngine.UI;

/// <summary>Title Scene의 직렬화된 Button과 Panel을 연결합니다.</summary>
public class TitleUIController : MonoBehaviour
{
    [SerializeField] private GameObject titlePanel;
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button reviewButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button exitButton;
    private void Awake() { SceneUIRouter.Register(this); gameStartButton.onClick.AddListener(SceneFlowManager.Instance.BeginGame); reviewButton.onClick.AddListener(SceneUIRouter.ShowReview); settingsButton.onClick.AddListener(() => SceneUIRouter.ShowSettings(true)); exitButton.onClick.AddListener(Application.Quit); }
    private void Start() => ShowTitle();
    private void OnDestroy() => SceneUIRouter.Unregister(this);
    public void ShowTitle() { if (titlePanel != null) titlePanel.SetActive(true); if (reviewButton != null) reviewButton.gameObject.SetActive(SaveManager.Instance != null && SaveManager.Instance.Data.isGameCompleted); }
}
