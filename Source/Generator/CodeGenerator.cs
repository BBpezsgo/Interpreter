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
    protected readonly Stack<CompiledVariableDeclaration> CompiledLocalVariables;
    protected readonly Stack<CompiledVariableDeclaration> CompiledGlobalVariables;
    protected readonly Stack<CompiledInstructionLabelDeclaration> CompiledInstructionLabels;

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
        CompiledLocalVariables = new Stack<CompiledVariableDeclaration>();
        CompiledGlobalVariables = new Stack<CompiledVariableDeclaration>();
        CompiledInstructionLabels = new Stack<CompiledInstructionLabelDeclaration>();

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

    protected abstract bool FindSize(PointerType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error);

    protected virtual bool FindSize(ArrayType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error)
    {
        size = default;

        if (type.Length is null)
        {
            error = new PossibleDiagnostic($"Array type doesn't have a size");
            return false;
        }

        if (!FindSize(type.Of, out int elementSize, out error)) return false;

        if (type.Length is not CompiledEvaluatedValue evaluatedStatement)
        {
            error = new PossibleDiagnostic($"Can't compute the array type's length");
            return false;
        }

        size = elementSize * (int)evaluatedStatement.Value;
        error = null;
        return true;
    }

    protected abstract bool FindSize(FunctionType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error);

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

    protected virtual bool FindSize(GenericType type, out int size, [NotNullWhen(false)] out PossibleDiagnostic? error) => throw new InvalidOperationException($"Generic type doesn't have a size");

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

    #endregion

    public int GetParameterIndex(CompiledParameter parameter)
    {
        for (int i = 0; i < CompiledParameters.Count; i++)
        {
            if (CompiledParameters[i] != parameter) continue;
            return i;
        }
        throw null;
    }
}
