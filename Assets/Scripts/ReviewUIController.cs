using UnityEngine;
using UnityEngine.UI;

/// <summary>Title Scene의 Review Panel을 표시</summary>
public class ReviewUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button backButton;
    public bool IsOpen => panel != null && panel.activeSelf;
    private void Awake() { SceneUIRouter.Register(this); backButton.onClick.AddListener(Hide); }
    private void OnDestroy() => SceneUIRouter.Unregister(this);
    public void Show() => panel.SetActive(true);
    public void Hide() => panel.SetActive(false);
}
