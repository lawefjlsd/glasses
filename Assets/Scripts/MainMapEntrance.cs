using UnityEngine;

/// <summary> Main Map 입구: Player Tag가 닿으면 해당 Stage 진입을 요청 </summary>
public class MainMapEntrance : MonoBehaviour
{
    public string stageId;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player")) SceneFlowManager.Instance.TryEnterStage(stageId);
    }
}
