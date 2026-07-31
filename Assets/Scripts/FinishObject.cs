using UnityEngine;

/// <summary>
/// 플레이어가 도착 지점에 닿았을 때 스테이지 종료를 요청합니다.
/// </summary>
public class FinishObject : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player")) collision.GetComponent<Player>().EndStage();
    }
}