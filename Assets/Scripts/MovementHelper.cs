using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 오브젝트의 부드러운 이동을 처리합니다.
/// </summary>
public class MovementHelper : MonoBehaviour
{
    public static MovementHelper Instance { get; private set; }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Unity 초기화 단계에서 싱글턴 또는 필수 참조를 준비합니다.
    /// </summary>
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }


    /// <summary>
    /// 입력: target, targetPosition, speed. 출력: 코루틴 완료. 역할: 대상을 지정 위치까지 부드럽게 이동시키는 코루틴을 실행합니다.
    /// </summary>
    public IEnumerator SmoothMoveCoroutine(Transform target, Vector3 targetPosition, float speed)
    {
        if (target == null) yield break;

        while (Vector3.Distance(target.position, targetPosition) > 0.01f)
        {
            target.position = Vector3.MoveTowards(target.position, targetPosition, speed * Time.deltaTime);
            yield return null;
        }
        target.position = targetPosition;
    }
}
