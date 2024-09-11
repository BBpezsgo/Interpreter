using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SourceCodeLocation
{
    public MutableRange<int> Instructions;
    public Position SourcePosition;
    public Uri? Uri;

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End > instruction;

    public override readonly string ToString() => $"({Instructions} -> {SourcePosition.ToStringRange()})";
    readonly string GetDebuggerDisplay() => ToString();
}

public enum StackElementKind
{
    Internal,
    Variable,
    Parameter,
}

[ExcludeFromCodeCoverage]
public struct StackElementInformation
{
    public StackElementKind Kind;
    public GeneralType Type;
    public string Tag;

    public int Address;
    public bool BasePointerRelative;
    public int Size;

    public readonly Range<int> GetRange(int basePointer, int absoluteOffset)
    {
        int itemStart = Address;

        if (BasePointerRelative) itemStart += basePointer;
        else itemStart += absoluteOffset;

        if (BytecodeProcessor.StackDirection < 0)
        { itemStart += Size - 1; }

        int itemEnd = itemStart + ((Size - 1) * BytecodeProcessor.StackDirection);

        return new Range<int>(itemStart, itemEnd);
    }
}

[ExcludeFromCodeCoverage]
public struct ScopeInformation
{
    public SourceCodeLocation Location;
    public List<StackElementInformation> Stack;
}

[ExcludeFromCodeCoverage]
public readonly struct CollectedScopeInfo
{
    public readonly ImmutableArray<StackElementInformation> Stack;

    public static CollectedScopeInfo Empty => new(Enumerable.Empty<StackElementInformation>());

    public CollectedScopeInfo(IEnumerable<StackElementInformation> stack)
    {
        Stack = stack.ToImmutableArray();
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

[ExcludeFromCodeCoverage]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct FunctionInformation
{
    public bool IsValid;
    public Parser.FunctionThingDefinition? Function;
    public ImmutableDictionary<string, GeneralType>? TypeArguments;
    public MutableRange<int> Instructions;

    public readonly Position SourcePosition => Function?.Identifier.Position ?? default;
    public readonly string? Identifier => Function?.Identifier.Content;
    public readonly Uri? File => Function?.File;
    public readonly string? ReadableIdentifier => Function?.ToReadable();

    public readonly bool Contains(int instruction) =>
        Instructions.Start <= instruction &&
        Instructions.End > instruction;

    public override readonly string? ToString()
    {
        if (!IsValid) return null;

        StringBuilder result = new();

        result.Append(ReadableIdentifier);

        if (SourcePosition != Position.UnknownPosition)
        { result.Append(SourcePosition.ToStringCool().Surround(" (at ", ")")); }

        if (File != null)
        { result.Append($" (in {File})"); }

        return result.ToString();
    }

    readonly string GetDebuggerDisplay()
    {
        if (!IsValid) return "<unknown>";
        StringBuilder result = new();

        result.Append(Instructions.ToString());

        result.Append(ReadableIdentifier);

        return result.ToString();
    }
}

[ExcludeFromCodeCoverage]
public class DebugInformation : IDuplicatable<DebugInformation>
{
    public readonly List<SourceCodeLocation> SourceCodeLocations;
    public readonly List<FunctionInformation> FunctionInformation;
    public readonly List<ScopeInformation> ScopeInformation;
    public readonly Dictionary<int, List<string>> CodeComments;
    public readonly Dictionary<Uri, ImmutableArray<Tokenizing.Token>> OriginalFiles;
    public StackOffsets StackOffsets;

    public DebugInformation(IEnumerable<KeyValuePair<Uri, ImmutableArray<Tokenizing.Token>>> originalFiles)
    {
        SourceCodeLocations = new List<SourceCodeLocation>();
        FunctionInformation = new List<FunctionInformation>();
        ScopeInformation = new List<ScopeInformation>();
        CodeComments = new Dictionary<int, List<string>>();
        OriginalFiles = new Dictionary<Uri, ImmutableArray<Tokenizing.Token>>(originalFiles);
        StackOffsets = new StackOffsets()
        {
            SavedBasePointer = 0,
            SavedCodePointer = 0,
        };
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
            if (!_sourceLocation.Contains(instruction))
            { continue; }
            if (success && sourceLocation.Instructions.Size() < _sourceLocation.Instructions.Size())
            { continue; }
            sourceLocation = _sourceLocation;
            success = true;
        }

        return success;
    }

    public IEnumerable<FunctionInformation> GetFunctionInformation(IEnumerable<int> codePointers)
    {
        foreach (int item in codePointers)
        { yield return GetFunctionInformation(item); }
    }

    public FunctionInformation GetFunctionInformation(int codePointer)
    {
        for (int i = 0; i < FunctionInformation.Count; i++)
        {
            FunctionInformation function = FunctionInformation[i];

            if (function.Contains(codePointer))
            { return function; }
        }
        return default;
    }

    public ImmutableArray<FunctionInformation> GetFunctionInformationNested(int codePointer)
    {
        List<FunctionInformation> result = new();
        for (int i = 0; i < FunctionInformation.Count; i++)
        {
            FunctionInformation info = FunctionInformation[i];

            if (info.Contains(codePointer))
            { result.Add(info); }
        }
        result.Sort((a, b) => a.Instructions.Size() - b.Instructions.Size());
        return result.ToImmutableArray();
    }

    public IEnumerable<ScopeInformation> GetScopes(int codePointer)
    {
        foreach (ScopeInformation scope in ScopeInformation)
        {
            if (!scope.Location.Contains(codePointer)) continue;
            yield return scope;
        }
    }

    public CollectedScopeInfo GetScopeInformation(int codePointer)
    {
        List<StackElementInformation> result = new();
        foreach (ScopeInformation scope in GetScopes(codePointer))
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

            if (loc.Instructions.Start > from)
            { loc.Instructions.Start += offset; }
            if (loc.Instructions.End > from)
            { loc.Instructions.End += offset; }

            SourceCodeLocations[i] = loc;
        }

        for (int i = 0; i < FunctionInformation.Count; i++)
        {
            FunctionInformation func = FunctionInformation[i];

            if (func.Instructions.Start > from)
            { func.Instructions.Start += offset; }
            if (func.Instructions.End > from)
            { func.Instructions.End += offset; }

            FunctionInformation[i] = func;
        }

        for (int i = 0; i < ScopeInformation.Count; i++)
        {
            ScopeInformation scope = ScopeInformation[i];

            if (scope.Location.Instructions.Start > from)
            { scope.Location.Instructions.Start += offset; }
            if (scope.Location.Instructions.End > from)
            { scope.Location.Instructions.End += offset; }

            ScopeInformation[i] = scope;
        }
    }

    public DebugInformation Duplicate()
    {
        DebugInformation copy = new(OriginalFiles);

        copy.SourceCodeLocations.AddRange(SourceCodeLocations);
        copy.FunctionInformation.AddRange(FunctionInformation);
        copy.ScopeInformation.AddRange(ScopeInformation);
        copy.CodeComments.AddRange(CodeComments);

        return copy;
    }
}
