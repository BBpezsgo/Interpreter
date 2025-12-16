using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public readonly record struct StackOffsets(int SavedCodePointer, int SavedBasePointer);

public record struct CallTraceItem(int BasePointer, int InstructionPointer);

[ExcludeFromCodeCoverage]
public static class DebugUtils
{
    public static void TraceStack(ReadOnlySpan<byte> stack, int basePointer, StackOffsets stackOffsets, List<CallTraceItem> callTrace)
    {
        int savedCodePointerAddress = basePointer + stackOffsets.SavedCodePointer;
        int savedBasePointerAddress = basePointer + stackOffsets.SavedBasePointer;

        if (savedCodePointerAddress <= 0 || savedCodePointerAddress >= stack.Length) return;
        if (savedBasePointerAddress <= 0 || savedBasePointerAddress >= stack.Length) return;

        int savedCodePointer = stack[savedCodePointerAddress..].To<int>();
        int savedBasePointer = stack[savedBasePointerAddress..].To<int>();

        CallTraceItem scope = new(savedBasePointer, savedCodePointer);

        if (savedBasePointer == basePointer || callTrace.Contains(scope)) return;

        callTrace.Add(scope);

        TraceStack(stack, savedBasePointer, stackOffsets, callTrace);
    }

    public static ReadOnlySpan<CallTraceItem> TraceStack(ReadOnlySpan<byte> stack, int basePointer, StackOffsets? stackOffsets)
    {
        if (!stackOffsets.HasValue) return ReadOnlySpan<CallTraceItem>.Empty;

        List<CallTraceItem> result = new();

        TraceStack(stack, basePointer, stackOffsets.Value, result);

        Span<CallTraceItem> callTraceResult = CollectionsMarshal.AsSpan(result);
        callTraceResult.Reverse();
        return callTraceResult;
    }
}
