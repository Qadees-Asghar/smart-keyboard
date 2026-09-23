namespace SmartKeyboard.Core.DataStructures;

/// <summary>
/// A binary max heap written by hand. The item with the biggest score is
/// always at the top, so pulling the best few items out is cheap.
/// We use it to pick the top 5 suggestions out of thousands of matches.
/// Built in PriorityQueue is deliberately not used.
///
/// The heap is stored in a plain list. For the item at position i:
///   parent      is at (i - 1) / 2
///   left child  is at 2 * i + 1
///   right child is at 2 * i + 2
/// </summary>
public class MaxHeap<T>
{
    private readonly List<HeapItem> _items = new();

    private readonly struct HeapItem
    {
        public HeapItem(T value, double score)
        {
            Value = value;
            Score = score;
        }

        public T Value { get; }

        public double Score { get; }
    }

    /// <summary>How many items are in the heap.</summary>
    public int Count => _items.Count;

    /// <summary>True when the heap has no items.</summary>
    public bool IsEmpty => _items.Count == 0;

    // Adds an item, then moves it up until its parent has a bigger score.
    // Time O(log n).
    public void Push(T value, double score)
    {
        _items.Add(new HeapItem(value, score));
        SiftUp(_items.Count - 1);
    }

    // Looks at the best item without removing it.
    // Time O(1).
    public T Peek()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        return _items[0].Value;
    }

    // Removes and returns the item with the biggest score.
    // The last item is moved to the top, then pushed down into place.
    // Time O(log n).
    public T Pop()
    {
        if (IsEmpty)
        {
            throw new InvalidOperationException("The heap is empty.");
        }

        T best = _items[0].Value;
        int last = _items.Count - 1;

        _items[0] = _items[last];
        _items.RemoveAt(last);

        if (_items.Count > 0)
        {
            SiftDown(0);
        }

        return best;
    }

    // Removes and returns up to count items, biggest score first.
    // Time O(k log n) where k is the number of items taken.
    public List<T> PopTop(int count)
    {
        var results = new List<T>();
        while (results.Count < count && !IsEmpty)
        {
            results.Add(Pop());
        }

        return results;
    }

    /// <summary>Throws every item away.</summary>
    public void Clear() => _items.Clear();

    // Moves an item up while it scores higher than its parent.
    // Time O(log n).
    private void SiftUp(int index)
    {
        while (index > 0)
        {
            int parent = (index - 1) / 2;
            if (_items[index].Score <= _items[parent].Score)
            {
                break;
            }

            Swap(index, parent);
            index = parent;
        }
    }

    // Moves an item down while a child scores higher than it.
    // Time O(log n).
    private void SiftDown(int index)
    {
        int size = _items.Count;
        while (true)
        {
            int left = (2 * index) + 1;
            int right = (2 * index) + 2;
            int biggest = index;

            if (left < size && _items[left].Score > _items[biggest].Score)
            {
                biggest = left;
            }

            if (right < size && _items[right].Score > _items[biggest].Score)
            {
                biggest = right;
            }

            if (biggest == index)
            {
                break;
            }

            Swap(index, biggest);
            index = biggest;
        }
    }

    // Swaps two positions in the list. Time O(1).
    private void Swap(int a, int b)
    {
        (_items[a], _items[b]) = (_items[b], _items[a]);
    }
}
