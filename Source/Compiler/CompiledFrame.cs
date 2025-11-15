namespace LanguageCore.Compiler;

class CompiledFrame
{
    public required ImmutableDictionary<string, GeneralType> TypeArguments { get; init; }
    public required ImmutableArray<CompiledParameter> CompiledParameters { get; init; }
    public required Stack<Scope> Scopes { get; init; }
    public required CompiledGeneratorContext? CompiledGeneratorContext { get; init; }
    public required GeneralType? CurrentReturnType { get; set; }
    public HashSet<CompiledVariableDeclaration> CapturedVariables { get; } = new();
    public HashSet<CompiledParameter> CapturedParameters { get; } = new();
    public bool IsMsilCompatible { get; set; } = true;

    public static CompiledFrame Empty => new()
    {
        TypeArguments = ImmutableDictionary<string, GeneralType>.Empty,
        CompiledParameters = ImmutableArray<CompiledParameter>.Empty,
        Scopes = new(),
        CompiledGeneratorContext = null,
        CurrentReturnType = BuiltinType.Void,
    };
}
