using LanguageCore.Runtime;

namespace LanguageCore.Compiler;

class Scope
{
    public readonly Stack<CompiledVariableDeclaration> Variables;
    public readonly ImmutableArray<CompiledVariableConstant> Constants;
    public readonly ImmutableArray<CompiledInstructionLabelDeclaration> InstructionLabels;

    public Scope(ImmutableArray<CompiledVariableConstant> constants, ImmutableArray<CompiledInstructionLabelDeclaration> instructionLabels)
    {
        Variables = new();
        Constants = constants;
        InstructionLabels = instructionLabels;
    }
}

public partial class StatementCompiler
{
    #region Fields

    public const int InvalidFunctionAddress = int.MinValue;

    readonly ImmutableArray<IExternalFunction> ExternalFunctions;

    public BuiltinType ArrayLengthType => Settings.ArrayLengthType;
    public BuiltinType BooleanType => Settings.BooleanType;
    public int PointerSize => Settings.PointerSize;
    public BuiltinType SizeofStatementType => Settings.SizeofStatementType;
    public BuiltinType ExitCodeType => Settings.ExitCodeType;

    GeneralType? CurrentReturnType;

    readonly CompilerSettings Settings;
    readonly List<CompiledFunction2> GeneratedFunctions = new();

    #endregion

    public static readonly ImmutableDictionary<string, (Register Register, BuiltinType Type)> RegisterKeywords = new Dictionary<string, (Register Register, BuiltinType Type)>()
    {
        { "IP", (Register.CodePointer, BuiltinType.I32) },
        { "SP", (Register.CodePointer, BuiltinType.I32) },
        { "BP", (Register.CodePointer, BuiltinType.I32) },

        { "EAX", (Register.EAX, BuiltinType.I32) },
        { "EBX", (Register.EBX, BuiltinType.I32) },
        { "ECX", (Register.ECX, BuiltinType.I32) },
        { "EDX", (Register.EDX, BuiltinType.I32) },

        { "AX", (Register.AX, BuiltinType.I16) },
        { "BX", (Register.BX, BuiltinType.I16) },
        { "CX", (Register.CX, BuiltinType.I16) },
        { "DX", (Register.DX, BuiltinType.I16) },

        { "AH", (Register.AH, BuiltinType.I8) },
        { "BH", (Register.BH, BuiltinType.I8) },
        { "CH", (Register.CH, BuiltinType.I8) },
        { "DH", (Register.DH, BuiltinType.I8) },

        { "AL", (Register.AL, BuiltinType.I8) },
        { "BL", (Register.BL, BuiltinType.I8) },
        { "CL", (Register.CL, BuiltinType.I8) },
        { "DL", (Register.DL, BuiltinType.I8) },
    }.ToImmutableDictionary();

    public StatementCompiler(CompilerResult compilerResult, CompilerSettings settings, DiagnosticsCollection diagnostics, PrintCallback? print)
    {
        CompiledParameters = new();

        CompilableFunctions = new();
        CompilableOperators = new();
        CompilableGeneralFunctions = new();

        TypeArguments = new Dictionary<string, GeneralType>();

        CompiledStructs = compilerResult.Structs;
        CompiledFunctions = compilerResult.Functions;
        CompiledOperators = compilerResult.Operators;
        CompiledConstructors = compilerResult.Constructors;
        CompiledGeneralFunctions = compilerResult.GeneralFunctions;
        CompiledAliases = compilerResult.Aliases;

        Diagnostics = diagnostics;
        Print = print;
        ExternalFunctions = compilerResult.ExternalFunctions;
        Settings = settings;
    }

    public static CompilerResult2 Compile(
        CompilerResult compilerResult,
        CompilerSettings settings,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
    {
        return new StatementCompiler(compilerResult, settings, diagnostics, printCallback).GenerateCode(compilerResult);
    }
}

public class CompiledFunction2
{
    public ICompiledFunction Function;
    public CompiledBlock Body;

    public CompiledFunction2(ICompiledFunction function, CompiledBlock body)
    {
        Function = function;
        Body = body;
    }

    public void Deconstruct(out ICompiledFunction function, out CompiledBlock body)
    {
        function = Function;
        body = Body;
    }

    public override string? ToString() => Function.ToString() ?? base.ToString();
}
