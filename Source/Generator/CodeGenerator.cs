using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

public abstract class CodeGenerator : IRuntimeInfoProvider
{
    public readonly struct CompliableTemplate<T> where T : ITemplateable<T>
    {
        public readonly T OriginalFunction;
        public readonly T Function;
        public readonly Dictionary<string, GeneralType> TypeArguments;

        public CompliableTemplate(T function, Dictionary<string, GeneralType> typeArguments)
        {
            OriginalFunction = function;
            TypeArguments = typeArguments;

            foreach (GeneralType argument in TypeArguments.Values)
            {
                if (argument.Is<GenericType>())
                { throw new InternalExceptionWithoutContext($"{argument} is generic"); }
            }

            Function = OriginalFunction.InstantiateTemplate(typeArguments);
        }

        public override string ToString() => Function?.ToString() ?? "null";
    }

    public readonly struct ControlFlowBlock
    {
        public int? FlagAddress { get; }
        public Stack<int> PendingJumps { get; }
        public Stack<bool> Doings { get; }
        public ILocated Location { get; }

        public ControlFlowBlock(int? flagAddress, ILocated location)
        {
            FlagAddress = flagAddress;
            PendingJumps = new Stack<int>();
            Doings = new Stack<bool>();

            PendingJumps.Push(0);
            Doings.Push(false);
            Location = location;
        }
    }

    #region Protected Fields

    protected readonly Stack<CompiledParameter> CompiledParameters;
    protected readonly Stack<CompiledVariableDefinition> CompiledLocalVariables;
    protected readonly Stack<CompiledVariableDefinition> CompiledGlobalVariables;
    protected readonly Stack<CompiledLabelDeclaration> CompiledInstructionLabels;

    protected bool InFunction;
    protected readonly Dictionary<string, GeneralType> TypeArguments;
    protected DebugInformation? DebugInfo;

    internal readonly DiagnosticsCollection Diagnostics;

    public abstract int PointerSize { get; }
    public BitWidth PointerBitWidth => (BitWidth)PointerSize;

    public abstract BuiltinType BooleanType { get; }
    public abstract BuiltinType SizeofStatementType { get; }
    public abstract BuiltinType ArrayLengthType { get; }

    public readonly ImmutableArray<CompiledStatement> TopLevelStatements;
    public readonly ImmutableArray<CompiledFunction> Functions;

    #endregion

    protected CodeGenerator(CompilerResult compilerResult, DiagnosticsCollection diagnostics)
    {
        Functions = compilerResult.Functions;
        TopLevelStatements = compilerResult.Statements;

        CompiledParameters = new Stack<CompiledParameter>();
        CompiledLocalVariables = new Stack<CompiledVariableDefinition>();
        CompiledGlobalVariables = new Stack<CompiledVariableDefinition>();
        CompiledInstructionLabels = new Stack<CompiledLabelDeclaration>();

        InFunction = false;
        TypeArguments = new Dictionary<string, GeneralType>();

        Diagnostics = diagnostics;
    }

    #region SetTypeArguments()

    protected void SetTypeArguments(ImmutableDictionary<string, GeneralType>? arguments)
    {
        TypeArguments.Clear();
        if (arguments is not null) TypeArguments.AddRange(arguments);
    }

    protected void SetTypeArguments(Dictionary<string, GeneralType>? arguments, out Dictionary<string, GeneralType> old)
    {
        old = new Dictionary<string, GeneralType>(TypeArguments);
        TypeArguments.Clear();
        if (arguments is not null) TypeArguments.AddRange(arguments);
    }

    protected TypeArgumentsScope SetTypeArgumentsScope(Dictionary<string, GeneralType>? arguments)
    {
        TypeArgumentsScope scope = new(this, TypeArguments);
        TypeArguments.Clear();
        if (arguments is not null) TypeArguments.AddRange(arguments);
        return scope;
    }

    protected readonly struct TypeArgumentsScope : IDisposable
    {
        readonly CodeGenerator _codeGenerator;
        readonly ImmutableDictionary<string, GeneralType> _prev;

        public TypeArgumentsScope(CodeGenerator codeGenerator, IDictionary<string, GeneralType>? prev)
        {
            _codeGenerator = codeGenerator;
            _prev = prev?.ToImmutableDictionary() ?? ImmutableDictionary<string, GeneralType>.Empty;
        }

        public void Dispose() => _codeGenerator.SetTypeArguments(_prev);
    }

    #endregion

    #region Find Size

    protected BitWidth FindBitWidth(GeneralType type)
    {
        if (!FindBitWidth(type, out BitWidth size, out PossibleDiagnostic? error)) error.Throw();
        return size;
    }
    protected BitWidth FindBitWidth(GeneralType type, ILocated location)
    {
        if (!FindBitWidth(type, out BitWidth size, out PossibleDiagnostic? error)) error.ToError(location).Throw();
        return size;
    }
    protected bool FindBitWidth(GeneralType type, out BitWidth size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        if (!FindSize(type, out int s, out error)) return false;
        switch (s)
        {
            case 1: size = BitWidth._8; return true;
            case 2: size = BitWidth._16; return true;
            case 4: size = BitWidth._32; return true;
            case 8: size = BitWidth._64; return true;
            default:
                error = new PossibleDiagnostic($"E");
                return false;
        }
    }

    protected int FindSize(GeneralType type)
    {
        if (!FindSize(type, out int size, out PossibleDiagnostic? sizeError)) sizeError.Throw();
        return size;
    }
    protected int FindSize(GeneralType type, ILocated location)
    {
        if (!FindSize(type, out int size, out PossibleDiagnostic? sizeError)) sizeError.ToError(location).Throw();
        return size;
    }
    protected bool FindSize(GeneralType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => type switch
    {
        PointerType v => FindSize(v, out size, out error),
        ArrayType v => FindSize(v, out size, out error),
        FunctionType v => FindSize(v, out size, out error),
        StructType v => FindSize(v, out size, out error),
        GenericType v => FindSize(v, out size, out error),
        BuiltinType v => FindSize(v, out size, out error),
        AliasType v => FindSize(v, out size, out error),
        _ => throw new NotImplementedException(),
    };
    protected virtual bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }
    protected virtual bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error)) return false;

        size = elementSize * type.Length.Value;
        error = null;
        return true;
    }
    protected virtual bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = PointerSize;
        error = null;
        return true;
    }
    protected virtual bool FindSize(StructType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = 0;

        foreach (CompiledField field in type.Struct.Fields)
        {
            GeneralType fieldType = type.ReplaceType(field.Type, out error);
            if (error is not null) return false;
            if (!FindSize(fieldType, out int fieldSize, out error)) return false;
            size += fieldSize;
        }

        error = null;
        return true;
    }
    protected virtual bool FindSize(GenericType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        error = new PossibleDiagnostic($"Generic type doesn't have a size");
        size = default;
        return false;
    }

    protected virtual bool FindSize(BuiltinType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;
        error = default;
        switch (type.Type)
        {
            case BasicType.Void: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.Any: error = new PossibleDiagnostic($"Can't get the size of type \"{type}\""); return false;
            case BasicType.U8: size = 1; return true;
            case BasicType.I8: size = 1; return true;
            case BasicType.U16: size = 2; return true;
            case BasicType.I16: size = 2; return true;
            case BasicType.U32: size = 4; return true;
            case BasicType.I32: size = 4; return true;
            case BasicType.U64: size = 8; return true;
            case BasicType.I64: size = 8; return true;
            case BasicType.F32: size = 4; return true;
            default: throw new UnreachableException();
        }
    }
    protected virtual bool FindSize(AliasType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => FindSize(type.Value, out size, out error);

    protected bool FindSize(CompiledTypeExpression type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        if (!CompileType(type, out GeneralType? compiledType, out error))
        {
            size = default;
            return false;
        }
        return FindSize(compiledType, out size, out error);
    }

    #endregion

    public bool GetFieldOffsets(StructType type, [NotNullWhen(true)] out ImmutableDictionary<CompiledField, int>? fields, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        Dictionary<CompiledField, int> result = new(type.Struct.Fields.Length);
        fields = default;

        int offset = 0;
        foreach (CompiledField field in type.Struct.Fields)
        {
            result.Add(field, offset);
            GeneralType fieldType = field.Type;
            fieldType = type.ReplaceType(fieldType, out error);
            if (error is not null) return false;
            if (!FindSize(fieldType, out int fieldSize, out error)) return false;
            offset += fieldSize;
        }

        fields = result.ToImmutableDictionary();
        error = null;
        return true;
    }

    public bool GetFieldOffset(StructType type, string name, [NotNullWhen(true)] out CompiledField? field, [NotNullWhen(true)] out int offset, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        offset = default;
        field = default;
        error = default;

        foreach (CompiledField _field in type.Struct.Fields)
        {
            if (_field.Identifier.Content == name)
            {
                field = _field;
                return true;
            }

            GeneralType fieldType = _field.Type;
            fieldType = type.ReplaceType(fieldType, out error);
            if (error is not null) return false;
            if (!FindSize(fieldType, out int fieldSize, out error)) return false;

            offset += fieldSize;
        }

        error = new PossibleDiagnostic($"Field \"{name}\" not found in struct \"{type.Struct}\"");
        return false;
    }

    protected static bool CompileType(CompiledTypeExpression typeExpression, [NotNullWhen(true)] out GeneralType? type, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        return StatementCompiler.CompileType(typeExpression, out type, out error);
    }

    public int GetParameterIndex(CompiledParameter parameter)
    {
        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            if (CompiledParameters[i] != parameter) continue;
            return i;
        }
        throw new LanguageException($"Parameter {parameter.Identifier.Content} not found", parameter.Position, parameter.File);
    }
}
