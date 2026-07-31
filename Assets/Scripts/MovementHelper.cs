using System.Collections;
using UnityEngine;

/// <summary>
/// 게임 오브젝트의 부드러운 이동을 처리합니다.
/// </summary>
public class MovementHelper : MonoBehaviour
{
    public static MovementHelper Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }


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