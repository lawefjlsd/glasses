using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>CutscenePanel Prefab의 표시와 완료 콜백을 처리</summary>
public class CutsceneUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private Button continueButton;
    private Action completed;
    public bool IsOpen => panel != null && panel.activeSelf;
    private void Awake() { SceneUIRouter.Register(this); continueButton.onClick.AddListener(Hide); }
    private void OnDestroy() => SceneUIRouter.Unregister(this);
    public void Show(CutsceneDefinition definition, Action callback) { completed = callback; titleLabel.text = string.IsNullOrEmpty(definition.displayName) ? definition.cutsceneId : definition.displayName; panel.SetActive(true); }
    public void Hide() { panel.SetActive(false); Action callback = completed; completed = null; callback?.Invoke(); }
}
