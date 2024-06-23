using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

public static class DebugUtils
{
    #region Old

    static bool CanTraceCallsWith(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        int savedCodePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedCodePointerOffset);
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset);

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static bool CanTraceBPsWith(ImmutableArray<RuntimeValue> stack, int basePointer)
    {
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset);

        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    public static void TraceCalls(ImmutableArray<RuntimeValue> stack, int basePointer, List<int> callTrace)
    {
        if (!CanTraceCallsWith(stack, basePointer)) return;

        int savedCodePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedCodePointerOffset)].Int;
        int savedBasePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset)].Int;

        if (savedBasePointer == basePointer || callTrace.Contains(savedCodePointer)) return;

        callTrace.Add(savedCodePointer);

        TraceCalls(stack, savedBasePointer, callTrace);
    }

    static void TraceBasePointers(ImmutableArray<RuntimeValue> stack, int basePointer, List<int> result)
    {
        if (!CanTraceBPsWith(stack, basePointer)) return;

        int newBasePointer = stack[basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset)].Int;

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

    #endregion

    #region New

    static bool CanTraceCallsWith(ImmutableArray<byte> stack, int basePointer)
    {
        int savedCodePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedCodePointerOffset);
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset);

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static bool CanTraceBPsWith(ImmutableArray<byte> stack, int basePointer)
    {
        int savedBasePointerAddress = basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset);

        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    public static void TraceCalls(ImmutableArray<byte> stack, int basePointer, List<int> callTrace)
    {
        if (!CanTraceCallsWith(stack, basePointer)) return;

        int savedCodePointer = stack.AsSpan()[(basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedCodePointerOffset))..].To<int>();
        int savedBasePointer = stack.AsSpan()[(basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset))..].To<int>();

        if (savedBasePointer == basePointer || callTrace.Contains(savedCodePointer)) return;

        callTrace.Add(savedCodePointer);

        TraceCalls(stack, savedBasePointer, callTrace);
    }

    static void TraceBasePointers(ImmutableArray<byte> stack, int basePointer, List<int> result)
    {
        if (!CanTraceBPsWith(stack, basePointer)) return;

        int newBasePointer = stack.AsSpan()[(basePointer + (BBLang.Generator.CodeGeneratorForMain.ScaledSavedBasePointerOffset))..].To<int>();

        result.Add(newBasePointer);

        if (newBasePointer == basePointer || result.Contains(newBasePointer)) return;

        TraceBasePointers(stack, newBasePointer, result);
    }

    public static ReadOnlySpan<int> TraceCalls(ImmutableArray<byte> stack, int basePointer)
    {
        if (!CanTraceCallsWith(stack, basePointer))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceCalls(stack, basePointer, result);

        Span<int> callTraceResult = CollectionsMarshal.AsSpan(result);
        callTraceResult.Reverse();
        return callTraceResult;
    }

    public static ReadOnlySpan<int> TraceBasePointers(ImmutableArray<byte> stack, int basePointer)
    {
        if (!CanTraceBPsWith(stack, basePointer))
        { return ReadOnlySpan<int>.Empty; }

        List<int> result = new();

        TraceBasePointers(stack, basePointer, result);

        return CollectionsMarshal.AsSpan(result);
    }

    #endregion
}
