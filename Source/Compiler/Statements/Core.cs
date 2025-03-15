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

    readonly StatementCompilerSettings Settings;
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

    public StatementCompiler(CompilerResult compilerResult, StatementCompilerSettings settings, DiagnosticsCollection diagnostics, PrintCallback? print)
    {
        CompiledParameters = new Stack<CompiledParameter>();

        compilableFunctions = new List<CompliableTemplate<CompiledFunction>>();
        compilableOperators = new List<CompliableTemplate<CompiledOperator>>();
        compilableGeneralFunctions = new List<CompliableTemplate<CompiledGeneralFunction>>();

        TypeArguments = new Dictionary<string, GeneralType>();

        CompiledStructs = compilerResult.Structs;
        CompiledFunctions = compilerResult.Functions;
        CompiledOperators = compilerResult.Operators;
        CompiledConstructors = compilerResult.Constructors;
        CompiledGeneralFunctions = compilerResult.GeneralFunctions;
        CompiledAliases = compilerResult.Aliases;

        Diagnostics = diagnostics;
        Print = print;

        ImmutableArray<IExternalFunction>.Builder externalFunctions = ImmutableArray.CreateBuilder<IExternalFunction>();
        externalFunctions.AddRange(compilerResult.ExternalFunctions);
        if (!settings.ExternalFunctions.IsDefaultOrEmpty) externalFunctions.AddRange(settings.ExternalFunctions);
        ExternalFunctions = externalFunctions.ToImmutable();
        Settings = settings;
    }

    public static CompilerResult2 Compile(
        CompilerResult compilerResult,
        StatementCompilerSettings settings,
        PrintCallback? printCallback,
        DiagnosticsCollection diagnostics)
    {
        return new StatementCompiler(compilerResult, settings, diagnostics, printCallback).GenerateCode(compilerResult);
    }
}

public struct CompilerResult2
{
    public ImmutableArray<CompiledStatement> Statements;
    public ImmutableArray<CompiledFunction2> Functions;

    public readonly string Stringify()
    {
        StringBuilder res = new();

        foreach ((ICompiledFunction function, CompiledBlock body) in Functions)
        {
            res.Append(function.Type.ToString());
            res.Append(' ');
            res.Append(function switch
            {
                CompiledFunction v => v.Identifier.Content,
                CompiledOperator v => v.Identifier.Content,
                CompiledGeneralFunction v => v.Identifier.Content,
                CompiledConstructor v => v.Type.ToString(),
                _ => "???",
            });
            res.Append('(');
            for (int i = 0; i < function.Parameters.Count; i++)
            {
                if (i > 0) res.Append(", ");
                res.Append(function.ParameterTypes[i].ToString());
                res.Append(' ');
                res.Append(function.Parameters[i].Identifier.Content);
            }
            res.Append(')');
            res.Append(body.Stringify(0));
            res.AppendLine();
        }

        res.AppendLine("// Top level statements");
        foreach (CompiledStatement statement in Statements)
        {
            if (statement is EmptyStatement) continue;
            res.Append(statement.Stringify(0));
            res.Append(';');
            res.AppendLine();
        }

        return res.ToString();
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
}
