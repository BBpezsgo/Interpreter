using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledLambda : CompiledStatementWithValue,
    ICompiledFunctionDefinition,
    IHaveCompiledType,
    IHaveInstructionOffset
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;
    public bool IsMsilCompatible { get; set; } = true;

    public ImmutableArray<CompiledParameter> Parameters { get; }
    public ParameterDefinitionCollection ParameterDefinitions { get; }
    public CompiledBlock Block { get; }
    public ImmutableArray<CapturedLocal> CapturedLocals { get; }
    public CompiledStatementWithValue? Allocator { get; init; }
    public Uri File { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);

    public CompiledLambda(GeneralType type, ImmutableArray<CompiledParameter> parameters, CompiledBlock block, ParameterDefinitionCollection parameterDefinitions, ImmutableArray<CapturedLocal> capturedLocals, Uri file)
    {
        Type = type;
        Parameters = parameters;
        Block = block;
        ParameterDefinitions = parameterDefinitions;
        CapturedLocals = capturedLocals;
        File = file;
    }

    public override string ToString()
    {
        return "nig";
    }

    public string ToReadable() => Type.ToString();
    public override string Stringify(int depth = 0) => $"({string.Join(", ", Parameters.Select(v => $"{v.Type} {v.Identifier}"))}) => {Block.Stringify(depth)}";
}
