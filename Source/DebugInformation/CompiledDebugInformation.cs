namespace LanguageCore.Runtime;

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
            StackOffsets = new StackOffsets(0, 0);
            HasValue = false;
        }
        else
        {
            List<SourceCodeLocation> sourceCodeLocations = new(debugInformation.SourceCodeLocations.Count);
            foreach (SourceCodeLocation item in debugInformation.SourceCodeLocations)
            {
                if (sourceCodeLocations.Count == 0)
                {
                    sourceCodeLocations.Add(item);
                    continue;
                }

                SourceCodeLocation l = sourceCodeLocations[^1];

                if (item.Location != l.Location)
                {
                    sourceCodeLocations.Add(item);
                    continue;
                }

                sourceCodeLocations.Add(new SourceCodeLocation()
                {
                    Instructions = (Math.Min(l.Instructions.Start, item.Instructions.Start), Math.Max(l.Instructions.End, item.Instructions.End)),
                    Location = l.Location,
                    IsSubtle = l.IsSubtle && item.IsSubtle,
                });
            }
            SourceCodeLocations = sourceCodeLocations.ToImmutableArray();
            FunctionInformation = debugInformation.FunctionInformation.ToImmutableArray();
            ScopeInformation = debugInformation.ScopeInformation.ToImmutableArray();
            CodeComments = debugInformation.CodeComments.Select(v => new KeyValuePair<int, ImmutableArray<string>>(v.Key, v.Value.ToImmutableArray())).ToFrozenDictionary();
            OriginalFiles = debugInformation.OriginalFiles.ToFrozenDictionary();
            StackOffsets = debugInformation.StackOffsets;
            HasValue = true;
        }
    }

    public ImmutableArray<SourceCodeLocation> GetSourceLocations(int instruction) => GetSourceLocations(SourceCodeLocations.AsSpan(), instruction);
    public static ImmutableArray<SourceCodeLocation> GetSourceLocations(ReadOnlySpan<SourceCodeLocation> sourceCodeLocations, int instruction)
    {
        ImmutableArray<SourceCodeLocation>.Builder result = ImmutableArray.CreateBuilder<SourceCodeLocation>();
        for (int i = 0; i < sourceCodeLocations.Length; i++)
        {
            SourceCodeLocation sourceLocation = sourceCodeLocations[i];
            if (!sourceLocation.Contains(instruction))
            { continue; }
            result.Add(sourceLocation);
        }
        return result.ToImmutable();
    }

    public bool TryGetSourceLocation(int instruction, out SourceCodeLocation sourceLocation, bool fallback = false)
        => TryGetSourceLocation(SourceCodeLocations.AsSpan(), instruction, out sourceLocation);
    public static bool TryGetSourceLocation(ReadOnlySpan<SourceCodeLocation> sourceCodeLocations, int instruction, out SourceCodeLocation sourceLocation, bool fallback = false)
    {
        sourceLocation = default;
        bool success = false;

        for (int i = 0; i < sourceCodeLocations.Length; i++)
        {
            SourceCodeLocation _sourceLocation = sourceCodeLocations[i];
            if (!_sourceLocation.Instructions.Contains(instruction))
            { continue; }
            if (success && sourceLocation.Instructions.Size() < _sourceLocation.Instructions.Size())
            { continue; }
            sourceLocation = _sourceLocation;
            success = true;
        }

        if (success) return true;
        if (!fallback) return false;

        for (int i = 0; i < sourceCodeLocations.Length; i++)
        {
            SourceCodeLocation _sourceLocation = sourceCodeLocations[i];
            if (_sourceLocation.Instructions.Contains(instruction - 1))
            {
                sourceLocation = new SourceCodeLocation()
                {
                    Instructions = (instruction, instruction + 1),
                    Location = _sourceLocation.Location.After(),
                };
                return true;
            }
        }

        return success;
    }

    public ImmutableArray<FunctionInformation> GetFunctionInformation(ReadOnlySpan<CallTraceItem> callTrace)
        => GetFunctionInformation(FunctionInformation.AsSpan(), callTrace);
    public static ImmutableArray<FunctionInformation> GetFunctionInformation(ReadOnlySpan<FunctionInformation> functionInformation, ReadOnlySpan<CallTraceItem> callTrace)
    {
        ImmutableArray<FunctionInformation>.Builder result = ImmutableArray.CreateBuilder<FunctionInformation>(callTrace.Length);
        for (int i = 0; i < callTrace.Length; i++)
        {
            result.Add(GetFunctionInformation(functionInformation, callTrace[i].InstructionPointer));
        }
        return result.MoveToImmutable();
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

    public bool TryGetFunctionInformation(int codePointer, out FunctionInformation function) => TryGetFunctionInformation(FunctionInformation.AsSpan(), codePointer, out function);
    public static bool TryGetFunctionInformation(ReadOnlySpan<FunctionInformation> functionInformation, int codePointer, out FunctionInformation function)
    {
        for (int i = 0; i < functionInformation.Length; i++)
        {
            function = functionInformation[i];

            if (function.Contains(codePointer))
            { return true; }
        }

        function = default;
        return false;
    }

    public ImmutableArray<FunctionInformation> GetFunctionInformationNested(int codePointer)
        => GetFunctionInformationNested(FunctionInformation.AsSpan(), codePointer);
    public static ImmutableArray<FunctionInformation> GetFunctionInformationNested(ReadOnlySpan<FunctionInformation> functionInformation, int codePointer)
    {
        ImmutableArray<FunctionInformation>.Builder result = ImmutableArray.CreateBuilder<FunctionInformation>();
        for (int i = 0; i < functionInformation.Length; i++)
        {
            FunctionInformation info = functionInformation[i];

            if (info.Contains(codePointer))
            { result.Add(info); }
        }
        result.Sort((a, b) => a.Instructions.Size() - b.Instructions.Size());
        return result.ToImmutable();
    }

    public ImmutableArray<ScopeInformation> GetScopes(int codePointer)
        => GetScopes(ScopeInformation.AsSpan(), codePointer);
    public static ImmutableArray<ScopeInformation> GetScopes(ReadOnlySpan<ScopeInformation> scopeInformation, int codePointer)
    {
        ImmutableArray<ScopeInformation>.Builder result = ImmutableArray.CreateBuilder<ScopeInformation>();
        for (int i = 0; i < scopeInformation.Length; i++)
        {
            if (!scopeInformation[i].Location.Contains(codePointer)) continue;
            result.Add(scopeInformation[i]);
        }
        return result.ToImmutable();
    }

    public CollectedScopeInfo GetScopeInformation(int codePointer)
    {
        return GetScopeInformation(ScopeInformation.AsSpan(), codePointer);
    }

    public CollectedScopeInfo GetAllScopeInformation(ReadOnlySpan<byte> memory, int basePointer, int codePointer)
    {
        ReadOnlySpan<CallTraceItem> callTrace = DebugUtils.TraceStack(memory, basePointer, StackOffsets);
        ImmutableArray<StackElementInformation>.Builder result = ImmutableArray.CreateBuilder<StackElementInformation>();
        foreach (ScopeInformation scope in GetScopes(ScopeInformation.AsSpan(), codePointer))
        { result.AddRange(scope.Stack); }
        foreach (CallTraceItem callFrame in callTrace)
        {
            foreach (ScopeInformation scope in GetScopes(ScopeInformation.AsSpan(), callFrame.InstructionPointer))
            { result.AddRange(scope.Stack); }
        }
        return new CollectedScopeInfo(result.ToImmutable());
    }

    public static CollectedScopeInfo GetScopeInformation(ReadOnlySpan<ScopeInformation> scopeInformation, int codePointer)
    {
        ImmutableArray<StackElementInformation>.Builder result = ImmutableArray.CreateBuilder<StackElementInformation>();
        foreach (ScopeInformation scope in GetScopes(scopeInformation, codePointer))
        { result.AddRange(scope.Stack); }
        return new CollectedScopeInfo(result.ToImmutable());
    }
}
