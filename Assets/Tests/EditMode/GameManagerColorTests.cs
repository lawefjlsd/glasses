using NUnit.Framework;

/// <summary>
/// StageController가 사용하는 순수 색상 규칙을 검증합니다.
/// </summary>
public class GameManagerColorTests
{
    /// <summary>입력: 없음. 출력: 없음. 역할: Add 효과가 RGB 성분을 누적하는지 검증합니다.</summary>
    [Test]
    public void ApplyMergeEffect_CombinesPrimaryColors()
    {
        Assert.That(StageColorRules.TryApplyMergeEffect("Black", "Red", MergeColorEffect.Add, out string red, out _), Is.True);
        Assert.That(StageColorRules.TryApplyMergeEffect(red, "Blue", MergeColorEffect.Add, out string magenta, out _), Is.True);
        Assert.That(magenta, Is.EqualTo("Magenta"));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: 일반·exclusive Bottom의 포함 색상 판정을 검증합니다.</summary>
    [Test]
    public void TryIsAvailable_RespectsNormalAndExclusiveBottom()
    {
        Assert.That(StageColorRules.TryIsAvailable("Black", "Red", false, out bool normalUnavailable, out _), Is.True);
        Assert.That(normalUnavailable, Is.False);
        Assert.That(StageColorRules.TryIsAvailable("Black", "Red", true, out bool exclusiveAvailable, out _), Is.True);
        Assert.That(exclusiveAvailable, Is.True);

        Assert.That(StageColorRules.TryIsAvailable("Red", "Red", false, out bool normalAvailable, out _), Is.True);
        Assert.That(normalAvailable, Is.True);
        Assert.That(StageColorRules.TryIsAvailable("Red", "Red", true, out bool exclusiveUnavailable, out _), Is.True);
        Assert.That(exclusiveUnavailable, Is.False);
    }
}
