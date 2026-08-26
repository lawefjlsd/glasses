using UnityEngine;

/// <summary>Hierarchy 기반 Game Over Panel의 표시 상태를 관리합니다.</summary>
public class GameOverUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    public void Show() => panel.SetActive(true);
    public void Hide() => panel.SetActive(false);
}
