using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public readonly struct StackOffsets
{
    public required int SavedCodePointer { get; init; }
    public required int SavedBasePointer { get; init; }
}

public record struct CallTraceItem(int BasePointer, int InstructionPointer);

[ExcludeFromCodeCoverage]
public static class DebugUtils
{
    static bool CanTraceStackWith(ReadOnlySpan<byte> stack, int basePointer, [NotNullWhen(true)] StackOffsets? stackOffsets)
    {
        if (!stackOffsets.HasValue) { return false; }

        int savedCodePointerAddress = basePointer + stackOffsets.Value.SavedCodePointer;
        int savedBasePointerAddress = basePointer + stackOffsets.Value.SavedBasePointer;

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static void TraceStack(ReadOnlySpan<byte> stack, int basePointer, StackOffsets? stackOffsets, List<CallTraceItem> callTrace)
    {
        if (!CanTraceStackWith(stack, basePointer, stackOffsets)) return;

        int savedCodePointer = stack[(basePointer + stackOffsets.Value.SavedCodePointer)..].To<int>();
        int savedBasePointer = stack[(basePointer + stackOffsets.Value.SavedBasePointer)..].To<int>();

        CallTraceItem scope = new(savedBasePointer, savedCodePointer);

        if (savedBasePointer == basePointer || callTrace.Contains(scope)) return;

        callTrace.Add(scope);

        TraceStack(stack, savedBasePointer, stackOffsets, callTrace);
    }

    public static ReadOnlySpan<CallTraceItem> TraceStack(ReadOnlySpan<byte> stack, int basePointer, StackOffsets? stackOffsets)
    {
        if (!CanTraceStackWith(stack, basePointer, stackOffsets))
        { return ReadOnlySpan<CallTraceItem>.Empty; }

        List<CallTraceItem> result = new();

        TraceStack(stack, basePointer, stackOffsets, result);

        Span<CallTraceItem> callTraceResult = CollectionsMarshal.AsSpan(result);
        callTraceResult.Reverse();
        return callTraceResult;
    }
}
