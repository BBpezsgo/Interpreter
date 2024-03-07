namespace LanguageCore.Runtime;

[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SourceCodeLocation
{
    public MutableRange<int> Instructions;
    public Position SourcePosition;
    public Uri? Uri;

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
        int itemStart = Address;
        if (BasepointerRelative) itemStart += basepointer;
        int itemEnd = itemStart + Size - 1;
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
    public readonly ImmutableArray<StackElementInformations> Stack;

    public CollectedScopeInfo(IEnumerable<StackElementInformations> stack)
    {
        Stack = stack.ToImmutableArray();
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
    public readonly Dictionary<Uri, ImmutableArray<Tokenizing.Token>> OriginalFiles;

    public DebugInformation(IEnumerable<KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>> originalFiles)
    {
        SourceCodeLocations = new List<SourceCodeLocation>();
        FunctionInformations = new List<FunctionInformations>();
        ScopeInformations = new List<ScopeInformations>();
        CodeComments = new Dictionary<int, List<string>>();
        OriginalFiles = new Dictionary<Uri, ImmutableArray<Tokenizing.Token>>(originalFiles);
    }

    public static int[] TraceBasePointers(DataItem[] stack, int basePointer)
    {
        if (!CanTraceBPsWith(basePointer))
        { return Array.Empty<int>(); }

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
        if (item.Type != RuntimeType.Integer) return;
        int num = item.VInt;
        result.Add(num);
        if (num == basePointer) return;
        TraceBasePointers(result, stack, num);
    }

    public IEnumerable<SourceCodeLocation> GetSourceLocations(int instruction)
    {
        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation sourceLocation = SourceCodeLocations[i];
            if (!sourceLocation.Contains(instruction))
            { continue; }
            yield return sourceLocation;
        }
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

    public IEnumerable<FunctionInformations> GetFunctionInformations(IEnumerable<int> callstack)
    {
        foreach (int item in callstack)
        { yield return GetFunctionInformations(item); }
    }

    public FunctionInformations GetFunctionInformations(int codePointer)
    {
        for (int i = 0; i < FunctionInformations.Count; i++)
        {
            FunctionInformations function = FunctionInformations[i];

            if (function.Instructions.Contains(codePointer))
            { return function; }
        }
        return default;
    }

    public ImmutableArray<FunctionInformations> GetFunctionInformationsNested(int codePointer)
    {
        List<FunctionInformations> result = new();
        for (int i = 0; i < FunctionInformations.Count; i++)
        {
            FunctionInformations info = FunctionInformations[i];

            if (info.Instructions.Contains(codePointer))
            { result.Add(info); }
        }
        result.Sort((a, b) => a.Instructions.Size() - b.Instructions.Size());
        return result.ToImmutableArray();
    }

    public IEnumerable<ScopeInformations> GetScopes(int codePointer)
    {
        for (int i = 0; i < ScopeInformations.Count; i++)
        {
            ScopeInformations scope = ScopeInformations[i];
            if (!scope.Location.Contains(codePointer)) continue;
            yield return scope;
        }
    }

    public CollectedScopeInfo GetScopeInformations(int codePointer)
    {
        List<StackElementInformations> result = new();
        foreach (ScopeInformations scope in GetScopes(codePointer))
        { result.AddRange(scope.Stack); }
        return new CollectedScopeInfo(result);
    }

    public void OffsetCodeFrom(int from, int offset)
    {
        {
            Dictionary<int, List<string>> newCodeComments = new();
            foreach (KeyValuePair<int, List<string>> item in CodeComments)
            {
                if (item.Key > from)
                { newCodeComments.Add(item.Key + offset, item.Value); }
                else
                { newCodeComments.Add(item.Key, item.Value); }
            }
            CodeComments.Clear();
            CodeComments.AddRange(newCodeComments);
        }

        for (int i = 0; i < SourceCodeLocations.Count; i++)
        {
            SourceCodeLocation loc = SourceCodeLocations[i];

            if (loc.Instructions.Start > offset)
            { loc.Instructions.Start += offset; }
            if (loc.Instructions.End > offset)
            { loc.Instructions.End += offset; }

            SourceCodeLocations[i] = loc;
        }

        for (int i = 0; i < FunctionInformations.Count; i++)
        {
            FunctionInformations func = FunctionInformations[i];

            if (func.Instructions.Start > offset)
            { func.Instructions.Start += offset; }
            if (func.Instructions.End > offset)
            { func.Instructions.End += offset; }

            FunctionInformations[i] = func;
        }

        for (int i = 0; i < ScopeInformations.Count; i++)
        {
            ScopeInformations scope = ScopeInformations[i];

            if (scope.Location.Instructions.Start > offset)
            { scope.Location.Instructions.Start += offset; }
            if (scope.Location.Instructions.End > offset)
            { scope.Location.Instructions.End += offset; }

            ScopeInformations[i] = scope;
        }
    }

    public DebugInformation Duplicate()
    {
        DebugInformation copy = new(OriginalFiles);

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
