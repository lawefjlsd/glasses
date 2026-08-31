using UnityEngine;

/// <summary>
/// StageBoard가 Final Cell을 캡처하기 위한 정적 도착 지점 표식입니다.
/// </summary>
public class FinishObject : MonoBehaviour
{
    /// <summary>
    /// 입력: collision. 출력: 없음. 역할: Clear 판정은 StageTurnResolver가 Commit 전 계산하므로 Trigger에서 상태를 변경하지 않습니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        // Finale는 PlayerActionPlan의 StageClearCandidate로만 처리합니다.
    }
}
