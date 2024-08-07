﻿using System.Runtime.InteropServices;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
public static class DebugUtils
{
    static bool CanTraceCallsWith(ImmutableArray<byte> stack, int basePointer)
    {
        int savedCodePointerAddress = basePointer + BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset;
        int savedBasePointerAddress = basePointer + BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset;

        if (savedCodePointerAddress < 0 || savedCodePointerAddress >= stack.Length) return false;
        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    static bool CanTraceBPsWith(ImmutableArray<byte> stack, int basePointer)
    {
        int savedBasePointerAddress = basePointer + BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset;

        if (savedBasePointerAddress < 0 || savedBasePointerAddress >= stack.Length) return false;

        return true;
    }

    public static void TraceCalls(ImmutableArray<byte> stack, int basePointer, List<int> callTrace)
    {
        if (!CanTraceCallsWith(stack, basePointer)) return;

        int savedCodePointer = stack.AsSpan()[(basePointer + BBLang.Generator.CodeGeneratorForMain.SavedCodePointerOffset)..].To<int>();
        int savedBasePointer = stack.AsSpan()[(basePointer + BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset)..].To<int>();

        if (savedBasePointer == basePointer || callTrace.Contains(savedCodePointer)) return;

        callTrace.Add(savedCodePointer);

        TraceCalls(stack, savedBasePointer, callTrace);
    }

    static void TraceBasePointers(ImmutableArray<byte> stack, int basePointer, List<int> result)
    {
        if (!CanTraceBPsWith(stack, basePointer)) return;

        int newBasePointer = stack.AsSpan()[(basePointer + BBLang.Generator.CodeGeneratorForMain.SavedBasePointerOffset)..].To<int>();

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
}
