using System.Collections;
using UnityEngine;

/// <summary> movement corutine </summary>
public class MovementHelper : MonoBehaviour
{
    [HideInInspector] public static MovementHelper Instance { get; private set; }
    public float defaultMoveSpeed = 8f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy(){ if (Instance == this) Instance = null; }

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
