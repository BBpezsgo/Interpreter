using LanguageCore.Compiler;

namespace LanguageCore.Runtime;

[ExcludeFromCodeCoverage]
[DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
public struct SourceCodeLocation
{
    public MutableRange<int> Instructions;
    public Position SourcePosition;
    public Uri Uri;

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

        result.Append(LanguageException.Format(ReadableIdentifier, SourcePosition, File));

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
public readonly struct CompiledDebugInformation
{
    public readonly bool HasValue;
    public bool IsEmpty => !HasValue;
    public readonly ImmutableArray<SourceCodeLocation> SourceCodeLocations;
    public readonly ImmutableArray<FunctionInformation> FunctionInformation;
    public readonly ImmutableArray<ScopeInformation> ScopeInformation;
    public readonly FrozenDictionary<int, ImmutableArray<string>> CodeComments;
    public readonly FrozenDictionary<Uri, ImmutableArray<Tokenizing.Token>> OriginalFiles;
    public readonly StackOffsets StackOffsets;

    public CompiledDebugInformation(DebugInformation? debugInformation)
    {
        if (debugInformation is null)
        {
            SourceCodeLocations = ImmutableArray<SourceCodeLocation>.Empty;
            FunctionInformation = ImmutableArray<FunctionInformation>.Empty;
            ScopeInformation = ImmutableArray<ScopeInformation>.Empty;
            CodeComments = FrozenDictionary<int, ImmutableArray<string>>.Empty;
            OriginalFiles = FrozenDictionary<Uri, ImmutableArray<Tokenizing.Token>>.Empty;
            StackOffsets = new StackOffsets()
            {
                SavedBasePointer = 0,
                SavedCodePointer = 0,
            };
            HasValue = false;
        }
        else
        {
            SourceCodeLocations = debugInformation.SourceCodeLocations.ToImmutableArray();
            FunctionInformation = debugInformation.FunctionInformation.ToImmutableArray();
            ScopeInformation = debugInformation.ScopeInformation.ToImmutableArray();
            CodeComments = debugInformation.CodeComments.Select(v => new KeyValuePair<int, ImmutableArray<string>>(v.Key, v.Value.ToImmutableArray())).ToFrozenDictionary();
            OriginalFiles = debugInformation.OriginalFiles.ToFrozenDictionary();
            StackOffsets = new StackOffsets()
            {
                SavedBasePointer = 0,
                SavedCodePointer = 0,
            };
            HasValue = true;
        }
    }

    public ImmutableArray<SourceCodeLocation> GetSourceLocations(int instruction) => GetSourceLocations(SourceCodeLocations.AsSpan(), instruction);
    public static ImmutableArray<SourceCodeLocation> GetSourceLocations(ReadOnlySpan<SourceCodeLocation> sourceCodeLocations, int instruction)
    {
        List<SourceCodeLocation> result = new();
        for (int i = 0; i < sourceCodeLocations.Length; i++)
        {
            SourceCodeLocation sourceLocation = sourceCodeLocations[i];
            if (!sourceLocation.Contains(instruction))
            { continue; }
            result.Add(sourceLocation);
        }
        return result.ToImmutableArray();
    }

    public bool TryGetSourceLocation(int instruction, out SourceCodeLocation sourceLocation)
        => TryGetSourceLocation(SourceCodeLocations.AsSpan(), instruction, out sourceLocation);
    public static bool TryGetSourceLocation(ReadOnlySpan<SourceCodeLocation> sourceCodeLocations, int instruction, out SourceCodeLocation sourceLocation)
    {
        sourceLocation = default;
        bool success = false;

        for (int i = 0; i < sourceCodeLocations.Length; i++)
        {
            SourceCodeLocation _sourceLocation = sourceCodeLocations[i];
            if (!_sourceLocation.Contains(instruction))
            { continue; }
            if (success && sourceLocation.Instructions.Size() < _sourceLocation.Instructions.Size())
            { continue; }
            sourceLocation = _sourceLocation;
            success = true;
        }

        return success;
    }

    public ImmutableArray<FunctionInformation> GetFunctionInformation(ReadOnlySpan<int> codePointers)
        => GetFunctionInformation(FunctionInformation.AsSpan(), codePointers);
    public static ImmutableArray<FunctionInformation> GetFunctionInformation(ReadOnlySpan<FunctionInformation> functionInformation, ReadOnlySpan<int> codePointers)
    {
        FunctionInformation[] result = new FunctionInformation[functionInformation.Length];
        for (int i = 0; i < codePointers.Length; i++)
        {
            result[i] = GetFunctionInformation(functionInformation, codePointers[i]);
        }
        return result.ToImmutableArray();
    }

    public FunctionInformation GetFunctionInformation(int codePointer) => GetFunctionInformation(FunctionInformation.AsSpan(), codePointer);
    public static FunctionInformation GetFunctionInformation(ReadOnlySpan<FunctionInformation> functionInformation, int codePointer)
    {
        for (int i = 0; i < functionInformation.Length; i++)
        {
            FunctionInformation function = functionInformation[i];

            if (function.Contains(codePointer))
            { return function; }
        }
        return default;
    }

    public ImmutableArray<FunctionInformation> GetFunctionInformationNested(int codePointer)
        => GetFunctionInformationNested(FunctionInformation.AsSpan(), codePointer);
    public static ImmutableArray<FunctionInformation> GetFunctionInformationNested(ReadOnlySpan<FunctionInformation> functionInformation, int codePointer)
    {
        List<FunctionInformation> result = new();
        for (int i = 0; i < functionInformation.Length; i++)
        {
            FunctionInformation info = functionInformation[i];

            if (info.Contains(codePointer))
            { result.Add(info); }
        }
        result.Sort((a, b) => a.Instructions.Size() - b.Instructions.Size());
        return result.ToImmutableArray();
    }

    public ImmutableArray<ScopeInformation> GetScopes(int codePointer)
        => GetScopes(ScopeInformation.AsSpan(), codePointer);
    public static ImmutableArray<ScopeInformation> GetScopes(ReadOnlySpan<ScopeInformation> scopeInformation, int codePointer)
    {
        List<ScopeInformation> result = new();
        foreach (ScopeInformation scope in scopeInformation)
        {
            if (!scope.Location.Contains(codePointer)) continue;
            result.Add(scope);
        }
        return result.ToImmutableArray();
    }

    public CollectedScopeInfo GetScopeInformation(int codePointer)
        => GetScopeInformation(ScopeInformation.AsSpan(), codePointer);
    public static CollectedScopeInfo GetScopeInformation(ReadOnlySpan<ScopeInformation> scopeInformation, int codePointer)
    {
        List<StackElementInformation> result = new();
        foreach (ScopeInformation scope in GetScopes(scopeInformation, codePointer))
        { result.AddRange(scope.Stack); }
        return new CollectedScopeInfo(result);
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
