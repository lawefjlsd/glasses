using UnityEngine;

/// <summary>
/// 플레이어가 도착 지점에 닿았을 때 스테이지 종료를 요청합니다.
/// </summary>
public class FinishObject : MonoBehaviour
{
    /// <summary>
    /// 입력: collision. 출력: 없음. 역할: 플레이어의 도착을 감지해 스테이지 완료를 처리합니다.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) collision.GetComponent<Player>().EndStage();
    }
}
