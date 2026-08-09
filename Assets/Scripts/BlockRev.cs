/// <summary>
/// 기존 Block 이동 규칙을 공유하면서 병합 시 색상 성분을 제거하는 BR Block을 정의합니다.
/// </summary>
public class BlockRev : Block
{
    /// <summary>
    /// 입력: 없음. 출력: MergeColorEffect. 역할: BR 병합이 현재 색상 성분을 제거하도록 Remove 효과를 반환합니다.
    /// </summary>
    public override MergeColorEffect GetMergeColorEffect() => MergeColorEffect.Remove;
}
