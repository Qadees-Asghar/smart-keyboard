using SmartKeyboard.Core.DataStructures;

namespace SmartKeyboard.Tests.DataStructures;

public class MaxHeapTests
{
    [Fact]
    public void Pop_ReturnsItemsFromBiggestScoreToSmallest()
    {
        var heap = new MaxHeap<string>();
        heap.Push("low", 1);
        heap.Push("high", 100);
        heap.Push("middle", 50);

        Assert.Equal("high", heap.Pop());
        Assert.Equal("middle", heap.Pop());
        Assert.Equal("low", heap.Pop());
    }

    [Fact]
    public void Pop_StaysCorrectWhenItemsArriveInAWkwardOrder()
    {
        var scores = new[] { 5, 3, 17, 10, 84, 19, 6, 22, 9 };
        var heap = new MaxHeap<int>();
        foreach (int score in scores)
        {
            heap.Push(score, score);
        }

        var popped = new List<int>();
        while (!heap.IsEmpty)
        {
            popped.Add(heap.Pop());
        }

        Assert.Equal(scores.OrderByDescending(s => s).ToList(), popped);
    }

    [Fact]
    public void Peek_ShowsTheBestItemWithoutRemovingIt()
    {
        var heap = new MaxHeap<string>();
        heap.Push("a", 10);
        heap.Push("b", 20);

        Assert.Equal("b", heap.Peek());
        Assert.Equal(2, heap.Count);
    }

    [Fact]
    public void Count_GoesUpOnPushAndDownOnPop()
    {
        var heap = new MaxHeap<string>();
        Assert.Equal(0, heap.Count);
        Assert.True(heap.IsEmpty);

        heap.Push("a", 1);
        heap.Push("b", 2);
        Assert.Equal(2, heap.Count);
        Assert.False(heap.IsEmpty);

        heap.Pop();
        Assert.Equal(1, heap.Count);
    }

    [Fact]
    public void Pop_OnEmptyHeapThrows()
    {
        var heap = new MaxHeap<string>();

        Assert.Throws<InvalidOperationException>(() => heap.Pop());
    }

    [Fact]
    public void Peek_OnEmptyHeapThrows()
    {
        var heap = new MaxHeap<string>();

        Assert.Throws<InvalidOperationException>(() => heap.Peek());
    }

    [Fact]
    public void Push_KeepsBothItemsWhenTwoScoresAreEqual()
    {
        var heap = new MaxHeap<string>();
        heap.Push("first", 10);
        heap.Push("second", 10);

        Assert.Equal(2, heap.Count);
        List<string> both = heap.PopTop(2);
        Assert.Contains("first", both);
        Assert.Contains("second", both);
    }

    [Fact]
    public void PopTop_ReturnsOnlyTheBestFew()
    {
        var heap = new MaxHeap<string>();
        heap.Push("a", 1);
        heap.Push("b", 9);
        heap.Push("c", 5);
        heap.Push("d", 7);

        Assert.Equal(new[] { "b", "d" }, heap.PopTop(2));
        Assert.Equal(2, heap.Count);
    }

    [Fact]
    public void PopTop_AsksForMoreThanThereIsAndGetsWhatIsThere()
    {
        var heap = new MaxHeap<string>();
        heap.Push("a", 1);

        Assert.Single(heap.PopTop(5));
        Assert.True(heap.IsEmpty);
    }

    [Fact]
    public void Clear_EmptiesTheHeap()
    {
        var heap = new MaxHeap<string>();
        heap.Push("a", 1);
        heap.Clear();

        Assert.True(heap.IsEmpty);
    }
}
