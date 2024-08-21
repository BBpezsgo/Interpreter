using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public readonly struct StackOffsets
{
    public required int SavedCodePointer { get; init; }
    public required int SavedBasePointer { get; init; }
}

[ExcludeFromCodeCoverage]
public static class DebugUtils
{
    static bool CanTraceCallsWith(ImmutableArray<byte> stack, int basePointer, [NotNullWhen(true)] StackOffsets? stackOffsets)
    {
        if (!stackOffsets.HasValue) { return false; }

        int savedCodePointerAddress = basePointer + stackOffsets.Value.SavedCodePointer;
        int savedBasePointerAddress = basePointer + stackOffsets.Value.SavedBasePointer;

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static bool CanTraceBPsWith(ImmutableArray<byte> stack, int basePointer, [NotNullWhen(true)] StackOffsets? stackOffsets)
    {
        if (!stackOffsets.HasValue) { return false; }

        int savedBasePointerAddress = basePointer + stackOffsets.Value.SavedBasePointer;

        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    public static void TraceCalls(ImmutableArray<byte> stack, int basePointer, StackOffsets? stackOffsets, List<int> callTrace)
    {
        if (!CanTraceCallsWith(stack, basePointer, stackOffsets)) return;

        int savedCodePointer = stack.AsSpan()[(basePointer + stackOffsets.Value.SavedCodePointer)..].To<int>();
        int savedBasePointer = stack.AsSpan()[(basePointer + stackOffsets.Value.SavedBasePointer)..].To<int>();

        if (savedBasePointer == basePointer || callTrace.Contains(savedCodePointer)) return;

        callTrace.Add(savedCodePointer);

        TraceCalls(stack, savedBasePointer, stackOffsets, callTrace);
    }

    static void TraceBasePointers(ImmutableArray<byte> stack, int basePointer, StackOffsets? stackOffsets, List<int> result)
    {
        if (!CanTraceBPsWith(stack, basePointer, stackOffsets)) return;

        int newBasePointer = stack.AsSpan()[(basePointer + stackOffsets.Value.SavedBasePointer)..].To<int>();

        result.Add(newBasePointer);

        if (newBasePointer == basePointer || result.Contains(newBasePointer)) return;

        TraceBasePointers(stack, newBasePointer, stackOffsets, result);
    }

    public static ReadOnlySpan<int> TraceCalls(ImmutableArray<byte> stack, int basePointer, StackOffsets? stackOffsets)
    {
        if (!CanTraceCallsWith(stack, basePointer, stackOffsets))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceCalls(stack, basePointer, stackOffsets, result);

        Span<int> callTraceResult = CollectionsMarshal.AsSpan(result);
        callTraceResult.Reverse();
        return callTraceResult;
    }

    public static ReadOnlySpan<int> TraceBasePointers(ImmutableArray<byte> stack, int basePointer, StackOffsets? stackOffsets)
    {
        if (!CanTraceBPsWith(stack, basePointer, stackOffsets))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceBasePointers(stack, basePointer, stackOffsets, result);

        return CollectionsMarshal.AsSpan(result);
    }
}
