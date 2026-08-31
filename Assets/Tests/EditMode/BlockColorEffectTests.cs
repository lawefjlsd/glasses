using NUnit.Framework;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Block의 Add/Remove 효과와 순수 CC replay 규칙을 검증합니다.
/// </summary>
public class BlockColorEffectTests
{
    /// <summary>입력: 없음. 출력: 없음. 역할: 기존 Block의 기본 효과가 Add인지 검증합니다.</summary>
    [Test]
    public void Block_DefaultColorEffect_IsAdd()
    {
        GameObject gameObject = new GameObject("NormalBlock");
        Block block = gameObject.AddComponent<Block>();

        Assert.That(block.ColorEffect, Is.EqualTo(MergeColorEffect.Add));
        Object.DestroyImmediate(gameObject);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Inspector Remove 설정이 Block profile에 보존되는지 검증합니다.</summary>
    [Test]
    public void Block_RemoveColorEffect_UsesRemove()
    {
        GameObject gameObject = new GameObject("ReverseBlock");
        Block block = gameObject.AddComponent<Block>();
        SerializedObject serialized = new SerializedObject(block);
        serialized.FindProperty("colorEffect").enumValueIndex = (int)MergeColorEffect.Remove;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Assert.That(block.ColorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Object.DestroyImmediate(gameObject);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Committer가 사용할 merged timer 데이터가 Add/Remove와 event 순서를 보존하는지 검증합니다.</summary>
    [Test]
    public void BlockMerged_InitializeTimer_PreservesCommittedMergeData()
    {
        GameObject block = new GameObject("Block");
        GameObject background = new GameObject("Background");
        GameObject mergedObject = new GameObject("Merged");
        BlockMerged merged = mergedObject.AddComponent<BlockMerged>();

        merged.InitializeTimer(Vector3.left, block, background, 3, MergeColorEffect.Remove, 2, 5, 7, 10, false, true);

        Assert.That(merged.mergeColorEffect, Is.EqualTo(MergeColorEffect.Remove));
        Assert.That(merged.mergeBatchId, Is.EqualTo(2));
        Assert.That(merged.mergeEventOrder, Is.EqualTo(5));
        Assert.That(merged.createdTurnId, Is.EqualTo(7));
        Assert.That(merged.separationDueTurn, Is.EqualTo(10));
        Object.DestroyImmediate(mergedObject);
        Object.DestroyImmediate(background);
        Object.DestroyImmediate(block);
    }

    [Test]
    public void BlockMerged_ApplyCommittedStateSynchronizesTurnState()
    {
        GameObject mergedObject = new GameObject("Merged");
        GameObject blockObject = new GameObject("Block");
        GameObject backgroundObject = new GameObject("Background");
        BlockMerged merged = mergedObject.AddComponent<BlockMerged>();
        StageEntityState state = new StageEntityState
        {
            colorName = "Blue",
            mergeColorEffect = MergeColorEffect.Remove,
            mergeBatchId = 3,
            mergeEventOrder = 7,
            remainingTurns = 1,
            isInfinite = false,
            isJustCreated = false,
            createdTurnId = 4,
            separationDueTurn = 6
        };

        merged.ApplyCommittedState(state, Vector3.left, blockObject, backgroundObject);

        Assert.That(merged.remainingTurns, Is.EqualTo(1));
        Assert.That(merged.isJustCreated, Is.False);
        Assert.That(merged.createdTurnId, Is.EqualTo(4));
        Assert.That(merged.separationDueTurn, Is.EqualTo(6));
        Assert.That(merged.originBlockPrefab, Is.SameAs(blockObject));
        Assert.That(merged.originBackgroundObject, Is.SameAs(backgroundObject));

        Object.DestroyImmediate(backgroundObject);
        Object.DestroyImmediate(blockObject);
        Object.DestroyImmediate(mergedObject);
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Add 뒤 Remove가 동일 RGB 성분을 제거하는지 검증합니다.</summary>
    [Test]
    public void ColorReplay_AddThenRemoveRed_ResultsInBlack()
    {
        Assert.That(StageColorRules.TryApplyMergeEffect("Black", "Red", MergeColorEffect.Add, out string red, out _), Is.True);
        Assert.That(StageColorRules.TryApplyMergeEffect(red, "Red", MergeColorEffect.Remove, out string black, out _), Is.True);
        Assert.That(black, Is.EqualTo("Black"));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: Remove 뒤 Add가 나중 이벤트로 색상 성분을 복구하는지 검증합니다.</summary>
    [Test]
    public void ColorReplay_RemoveThenAddRed_ResultsInRed()
    {
        Assert.That(StageColorRules.TryApplyMergeEffect("Red", "Red", MergeColorEffect.Remove, out string black, out _), Is.True);
        Assert.That(StageColorRules.TryApplyMergeEffect(black, "Red", MergeColorEffect.Add, out string red, out _), Is.True);
        Assert.That(red, Is.EqualTo("Red"));
    }

    /// <summary>입력: 없음. 출력: 없음. 역할: BR Yellow가 Red와 Green을 모두 제거하는지 검증합니다.</summary>
    [Test]
    public void ColorReplay_RemoveYellow_LeavesBlue()
    {
        Assert.That(StageColorRules.TryApplyMergeEffect("White", "Yellow", MergeColorEffect.Remove, out string result, out _), Is.True);
        Assert.That(result, Is.EqualTo("Blue"));
    }
}
