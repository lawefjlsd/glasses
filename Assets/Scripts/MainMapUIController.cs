using TMPro;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class MainMapStageButton
{
    public string stageId;
    public Button button;
    public TextMeshProUGUI label;
}

/// <summary>Main Map의 직렬화된 Stage Button 상태와 Scene 진입을 관리.</summary>
public class MainMapUIController : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI chapterName;
    [SerializeField] private TextMeshProUGUI stageInformation;
    [SerializeField] private MainMapStageButton[] stageButtons;

    private void Start()
    {
        BindStageButtons();
        Refresh();
    }

    private void BindStageButtons()
    {
        if (stageButtons == null) return;

        foreach (MainMapStageButton stageButton in stageButtons)
        {
            if (stageButton == null || stageButton.button == null) continue;
            string stageId = stageButton.stageId;
            stageButton.button.onClick.AddListener(() => EnterStage(stageId));
        }
    }

    private void EnterStage(string stageId)
    {
        if (SceneFlowManager.Instance == null || !SceneFlowManager.Instance.TryEnterStage(stageId))
        {
            if (stageInformation != null) stageInformation.text = "This stage is locked or unavailable.";
        }
    }

    public void Refresh()
    {
        if (chapterName != null) chapterName.text = "CHAPTER 1";
        if (stageButtons == null) return;

        foreach (MainMapStageButton stageButton in stageButtons)
        {
            if (stageButton == null) continue;
            StageDefinition stage = ProgressManager.Instance != null ? ProgressManager.Instance.GetStageById(stageButton.stageId) : null;
            bool unlocked = stage != null && ProgressManager.Instance.IsStageUnlocked(stage.stageId);
            if (stageButton.button != null) stageButton.button.interactable = unlocked;
            if (stageButton.label != null)
            {
                string stageName = stage != null ? $"STAGE 1-{stage.stageNumber}" : "UNKNOWN STAGE";
                stageButton.label.text = unlocked ? $"{stageName}\n[BUTTON SPRITE]" : $"{stageName} - LOCKED\n[BUTTON SPRITE]";
            }
        }

        if (stageInformation != null) stageInformation.text = "Select an unlocked stage.";
    }
}
