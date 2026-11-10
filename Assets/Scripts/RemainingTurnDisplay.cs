using TMPro;
using UnityEngine;

/// <summary> remainingturn display </summary>
public class RemainingTurnDisplay : MonoBehaviour
{
    private BlockMerged mergedBlock;
    private TextMeshPro remainingTurnLabel;
    private void Awake()
    {
        mergedBlock = GetComponent<BlockMerged>();

        GameObject labelObject = new GameObject("RemainingTurnLabel", typeof(TextMeshPro));
        labelObject.transform.SetParent(transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 0.4f, 0f);

        remainingTurnLabel = labelObject.GetComponent<TextMeshPro>();
        remainingTurnLabel.font = TMP_Settings.defaultFontAsset;
        remainingTurnLabel.fontSize = 18f;
        remainingTurnLabel.alignment = TextAlignmentOptions.Center;
        remainingTurnLabel.color = Color.white;
        remainingTurnLabel.sortingOrder = 10;
    }
    private void LateUpdate()
    {
        if (mergedBlock == null) return;

        remainingTurnLabel.text = mergedBlock.isInfinite
            ? "Remaining: ∞"
            : $"Remaining: {mergedBlock.remainingTurns}";
    }
}
