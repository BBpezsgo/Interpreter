using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace LanguageCore.Runtime;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SourceCodeLocation
{
    public MutableRange<int> Instructions;
    public Position SourcePosition;

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End >= instruction;

    public override readonly string ToString() => $"({Instructions} -> {SourcePosition.ToStringRange()})";
    readonly string GetDebuggerDisplay() => ToString();
}

public enum StackElementKind
{
    Internal,
    Variable,
    Parameter,
}

public enum StackElementType
{
    Value,
    HeapPointer,
    StackPointer,
}

public struct StackElementInformations
{
    public StackElementKind Kind;
    public StackElementType Type;
    public string Tag;

    public int Address;
    public bool BasepointerRelative;
    public int Size;

    public readonly MutableRange<int> GetRange(int basepointer)
    {
        int itemStart = this.Address;
        if (this.BasepointerRelative) itemStart += basepointer;
        int itemEnd = itemStart + this.Size - 1;
        return new MutableRange<int>(itemStart, itemEnd);
    }
}

public struct ScopeInformations
{
    public SourceCodeLocation Location;
    public List<StackElementInformations> Stack;
}

public readonly struct CollectedScopeInfo
{
    public readonly StackElementInformations[] Stack;

    public CollectedScopeInfo(StackElementInformations[] stack)
    {
        Stack = stack;
    }

    public bool TryGet(int basePointer, int stackAddress, out StackElementInformations result)
    {
        for (int i = 0; i < Stack.Length; i++)
        {
            StackElementInformations item = Stack[i];
            MutableRange<int> range = item.GetRange(basePointer);

            if (range.Contains(stackAddress))
            {
                result = item;
                return true;
            }
        }
        result = default;
        return false;
    }
}

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct FunctionInformations
{
    public bool IsValid;
    public Position SourcePosition;
    public string Identifier;
    public Uri? File;
    public string ReadableIdentifier;
    public bool IsMacro;
    public MutableRange<int> Instructions;

    public override readonly string ToString()
    {
        if (!IsValid) return "<unknown>";
        StringBuilder result = new();

        if (IsMacro)
        { result.Append("macro "); }

        result.Append(ReadableIdentifier);

        return result.ToString();
    }

    readonly string GetDebuggerDisplay()
    {
        if (!IsValid) return "<unknown>";
        StringBuilder result = new();

        result.Append(Instructions.ToString());

        if (IsMacro)
        { result.Append("macro "); }

        result.Append(ReadableIdentifier);

        return result.ToString();
    }
}

public class DebugInformation : IDuplicatable<DebugInformation>
{
    public readonly List<SourceCodeLocation> SourceCodeLocations;
    public readonly List<FunctionInformations> FunctionInformations;
    public readonly List<ScopeInformations> ScopeInformations;
    public readonly Dictionary<int, List<string>> CodeComments;

    public DebugInformation()
    {
        SourceCodeLocations = new List<SourceCodeLocation>();
        FunctionInformations = new List<FunctionInformations>();
        ScopeInformations = new List<ScopeInformations>();
        CodeComments = new Dictionary<int, List<string>>();
    }

    public static int[] TraceBasePointers(DataItem[] stack, int basePointer)
    {
        if (!CanTraceBPsWith(basePointer))
        { return System.Array.Empty<int>(); }

        List<int> result = new();
        TraceBasePointers(result, stack, basePointer);
        return result.ToArray();
    }

    static bool CanTraceBPsWith(int basePointer) =>
        basePointer >= 1;

    static void TraceBasePointers(List<int> result, DataItem[] stack, int basePointer)
    {
        if (!CanTraceBPsWith(basePointer)) return;
        if (basePointer - 1 >= stack.Length) return;
        DataItem item = stack[basePointer - 1];
        if (item.Type != RuntimeType.SInt32) return;
        int num = item.ValueSInt32;
        result.Add(num);
        if (num == basePointer) return;
        TraceBasePointers(result, stack, num);
    }

    public SourceCodeLocation[] GetSourceLocations(int instruction)
    {
        List<SourceCodeLocation> result = new();
        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation sourceLocation = SourceCodeLocations[i];
            if (!sourceLocation.Contains(instruction))
            { continue; }
            result.Add(sourceLocation);
        }
        return result.ToArray();
    }

    public bool TryGetSourceLocation(int instruction, out SourceCodeLocation sourceLocation)
    {
        sourceLocation = default;
        bool success = false;

        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation _sourceLocation = SourceCodeLocations[i];
            if (!_sourceLocation.Instructions.Contains(instruction))
            { continue; }
            if (success && sourceLocation.Instructions.Size() < _sourceLocation.Instructions.Size())
            { continue; }
            sourceLocation = _sourceLocation;
            success = true;
        }

        return success;
    }

    public FunctionInformations[] GetFunctionInformations(int[] callstack)
    {
        if (callstack.Length == 0)
        { return System.Array.Empty<FunctionInformations>(); }

        FunctionInformations[] result = new FunctionInformations[callstack.Length];
        for (int i = 0; i < callstack.Length; i++)
        { result[i] = GetFunctionInformations(callstack[i]); }
        return result;
    }

    public FunctionInformations GetFunctionInformations(int codePointer)
    {
        for (int j = 0; j < FunctionInformations.Count; j++)
        {
            FunctionInformations info = FunctionInformations[j];

            if (info.Instructions.Contains(codePointer))
            { return info; }
        }
        return default;
    }

    public FunctionInformations[] GetFunctionInformationsNested(int codePointer)
    {
        List<FunctionInformations> result = new();
        for (int j = 0; j < FunctionInformations.Count; j++)
        {
            FunctionInformations info = FunctionInformations[j];

            if (info.Instructions.Contains(codePointer))
            { result.Add(info); }
        }
        result.Sort((a, b) => a.Instructions.Size() - b.Instructions.Size());
        return result.ToArray();
    }

    public ScopeInformations[] GetScopes(int codePointer)
    {
        List<ScopeInformations> result = new();

        for (int i = 0; i < ScopeInformations.Count; i++)
        {
            ScopeInformations scope = ScopeInformations[i];
            if (!scope.Location.Contains(codePointer)) continue;
            result.Add(scope);
        }

        return result.ToArray();
    }

    public CollectedScopeInfo GetScopeInformations(int codePointer)
    {
        ScopeInformations[] scopes = GetScopes(codePointer);
        List<StackElementInformations> result = new();

        for (int i = 0; i < scopes.Length; i++)
        {
            if (scopes[i].Stack == null) continue;
            for (int j = 0; j < scopes[i].Stack.Count; j++)
            {
                StackElementInformations item = scopes[i].Stack[j];
                result.Add(item);
            }
        }

        return new CollectedScopeInfo(result.ToArray());
    }

    public DebugInformation Duplicate()
    {
        DebugInformation copy = new();

        copy.SourceCodeLocations.AddRange(SourceCodeLocations);
        copy.FunctionInformations.AddRange(FunctionInformations);
        copy.ScopeInformations.AddRange(ScopeInformations);
        copy.CodeComments.AddRange(CodeComments);

        return copy;
    }

    /*
    public void RemoveCode(int start, int count)
    {
        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation item = SourceCodeLocations[i];

            // Before
            if (item.Instructions.End <= start) continue;

            // After
            if (item.Instructions.Start > start + count)
            {
                item.Instructions = new Range<int>(item.Instructions.Start - count, item.Instructions.End - count);
                goto Finish;
            }

            // Inside
            if (item.Instructions.Contains(start) && item.Instructions.Contains(start + count))
            {
                item.Instructions = new Range<int>(item.Instructions.Start, item.Instructions.End - count);
                goto Finish;
            }

            item.Instructions = new Range<int>(item.Instructions.Start, item.Instructions.End - count);

        Finish:
            SourceCodeLocations[i] = item;
        }

        for (int i = 0; i < FunctionInformations.Count; i++)
        {
            FunctionInformations item = FunctionInformations[i];

            // Before
            if (item.Instructions.End <= start) continue;

            // After
            if (item.Instructions.Start > start + count)
            {
                item.Instructions = new Range<int>(item.Instructions.Start - count, item.Instructions.End - count);
                goto Finish;
            }

            // Inside
            if (item.Instructions.Contains(start) && item.Instructions.Contains(start + count))
            {
                item.Instructions = new Range<int>(item.Instructions.Start, item.Instructions.End - count);
                goto Finish;
            }

            item.Instructions = new Range<int>(item.Instructions.Start, item.Instructions.End - count);

        Finish:
            FunctionInformations[i] = item;
        }

        KeyValuePair<int, List<string>>[] codeComments = CodeComments.ToArray();
        for (int i = 0; i < codeComments.Length; i++)
        {
            KeyValuePair<int, List<string>> item = codeComments[i];
            if (item.Key <= start) continue;
            codeComments[i] = new KeyValuePair<int, List<string>>(item.Key - count, item.Value);
        }
        CodeComments.Clear();
        CodeComments.AddRange(codeComments);
    }
    */
}
