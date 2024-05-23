using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static class DebugUtils
{
    static bool CanTraceCallsWith(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        int savedCodePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * BytecodeProcessor.StackDirection);
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * BytecodeProcessor.StackDirection);

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static bool CanTraceBPsWith(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * BytecodeProcessor.StackDirection);

        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    public static void TraceCalls(ImmutableArray<RuntimeValue> stack, int basePointer, List<int> callTrace)
    {
        if (!CanTraceCallsWith(stack, basePointer)) return;

        int savedCodePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset * BytecodeProcessor.StackDirection)].Int;
        int savedBasePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * BytecodeProcessor.StackDirection)].Int;

        callTrace.Add(savedCodePointer);

        if (savedBasePointer == basePointer || callTrace.Contains(savedCodePointer)) return;

        TraceCalls(stack, savedBasePointer, callTrace);
    }

    static void TraceBasePointers(ImmutableArray<RuntimeValue> stack, int basePointer, List<int> result)
    {
        if (!CanTraceBPsWith(stack, basePointer)) return;

        int newBasePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset * BytecodeProcessor.StackDirection)].Int;

        result.Add(newBasePointer);

        if (newBasePointer == basePointer || result.Contains(newBasePointer)) return;

        TraceBasePointers(stack, newBasePointer, result);
    }

    public static ReadOnlySpan<int> TraceCalls(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        if (!CanTraceCallsWith(stack, basePointer))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceCalls(stack, basePointer, result);

        Span<int> callTraceResult = CollectionsMarshal.AsSpan(result);
        callTraceResult.Reverse();
        return callTraceResult;
    }

    public static ReadOnlySpan<int> TraceBasePointers(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        if (!CanTraceBPsWith(stack, basePointer))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceBasePointers(stack, basePointer, result);

        return CollectionsMarshal.AsSpan(result);
    }
}
