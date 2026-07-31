using System.Collections;
using UnityEngine;

public class MovementHelper : MonoBehaviour
{
    public static MovementHelper Instance { get; private set; }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Move coroutine!
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