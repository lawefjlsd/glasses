using System.Reflection;
using NUnit.Framework;
using UnityEngine;

public class GameManagerColorTests
{
    private GameObject gameManagerObject;
    private GameManager gameManager;

    [SetUp]
    public void SetUp()
    {
        gameManagerObject = new GameObject("GameManagerTest");
        gameManager = gameManagerObject.AddComponent<GameManager>();

        typeof(GameManager)
            .GetMethod("Start", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(gameManager, null);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(gameManagerObject);
    }

    [Test]
    public void UpdateCurrentColor_CombinesPrimaryColors()
    {
        gameManager.UpdateCurrentColor("Red");
        gameManager.UpdateCurrentColor("Blue");

        Assert.That(GameManager.currentColor, Is.EqualTo("Magenta"));
    }

    [Test]
    public void IsAvailableBlock_RespectsNormalAndExclusiveTiles()
    {
        Assert.That(gameManager.IsAvailableBlock("Red", false), Is.False);
        Assert.That(gameManager.IsAvailableBlock("Red", true), Is.True);

        gameManager.UpdateCurrentColor("Red");

        Assert.That(gameManager.IsAvailableBlock("Red", false), Is.True);
        Assert.That(gameManager.IsAvailableBlock("Red", true), Is.False);
    }
}
