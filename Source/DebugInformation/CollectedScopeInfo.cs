namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public readonly struct CollectedScopeInfo
{
    public readonly ImmutableArray<StackElementInformation> Stack;

    public static CollectedScopeInfo Empty => new(ImmutableArray<StackElementInformation>.Empty);

    public CollectedScopeInfo(ImmutableArray<StackElementInformation> stack)
    {
        Stack = stack;
    }

    public bool TryGet(int basePointer, int absoluteOffset, int address, out StackElementInformation result)
    {
        for (int i = 0; i < Stack.Length; i++)
        {
            StackElementInformation item = Stack[i];
            Range<int> range = item.GetRange(basePointer, absoluteOffset);

            if (range.Contains(address))
            {
                result = item;
                return true;
            }
        }

        result = default;
        return false;
    }
}
