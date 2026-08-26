using UnityEngine;

/// <summary>
/// 이전 Scene 참조와의 호환을 유지하며, 실제 UI 표시는 Hierarchy 기반 GameHUDController에 위임합니다.
/// </summary>
public class GameUI : MonoBehaviour
{
    /// <summary>
    /// 입력: manager. 출력: 없음. 역할: 이전 호출부 호환을 위해 남겨 둔 초기화 진입점입니다.
    /// </summary>
    public void Initialize(GameManager manager)
    {
        // 실제 UI는 Scene에 직렬화된 GameCanvas Prefab이 담당합니다.
    }

    /// <summary>
    /// 입력: 없음. 출력: 없음. 역할: Hierarchy 기반 Game Over Panel을 표시합니다.
    /// </summary>
    public void ShowGameOver()
    {
        GameHUDController.Instance?.ShowGameOver();
    }
}
