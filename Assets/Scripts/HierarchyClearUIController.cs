using UnityEngine;

/// <summary>Hierarchy 기반 Clear Panel의 표시 상태를 관리합니다. 기존 ClearUI와 이름 충돌을 피합니다.</summary>
public class HierarchyClearUIController : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    public void Show() => panel.SetActive(true);
    public void Hide() => panel.SetActive(false);
}
