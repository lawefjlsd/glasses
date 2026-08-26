using UnityEngine;
using UnityEngine.UI;

/// <summary>SettingsPanel Prefab의 입력 차단과 Master Volume을 처리합니다.</summary>
public class SettingsUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button mainMapButton;
    [SerializeField] private Slider masterVolumeSlider;
    private bool fromTitle;
    public bool IsOpen => panel != null && panel.activeSelf;
    private void Awake() { SceneUIRouter.Register(this); closeButton.onClick.AddListener(Hide); mainMapButton.onClick.AddListener(GoToMainMap); masterVolumeSlider.onValueChanged.AddListener(SetVolume); }
    private void OnDestroy() { if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.RemoveListener(SetVolume); SceneUIRouter.Unregister(this); }
    public void Show(bool openedFromTitle) { fromTitle = openedFromTitle; panel.SetActive(true); masterVolumeSlider.SetValueWithoutNotify(SaveManager.Instance.Data.masterVolume); }
    public void Hide() { panel.SetActive(false); if (fromTitle) SceneUIRouter.ShowTitle(); }
    private void GoToMainMap() { Hide(); SceneFlowManager.Instance.GoToMainMap(); }
    private void SetVolume(float value) => AudioManager.Instance?.SetMasterVolume(value);
}
