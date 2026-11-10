using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary> Main Map Player 이동과 저장된 위치 복원·기록 </summary>
public class MainMapPlayerController : MonoBehaviour
{
    public float moveSpeed = 4f;

    private void Start()
    {
        transform.position = SaveManager.Instance.Data.mainMapPlayerPosition;
    }

    private void Update()
    {
        if (UIManager.Instance != null && UIManager.Instance.IsOverlayOpen) return;
        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized;
        transform.position += (Vector3)(input * moveSpeed * Time.deltaTime);
    }

    private void OnDisable()
    {
        if (SaveManager.Instance == null) return;
        SaveManager.Instance.Data.mainMapPlayerPosition = transform.position;
        SaveManager.Instance.Data.mainMapSceneName = SceneManager.GetActiveScene().name;
        SaveManager.Instance.Save();
    }
}
