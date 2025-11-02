using LanguageCore.Parser;

namespace LanguageCore.Compiler;

public class CompiledLambda : CompiledStatementWithValue,
    ICompiledFunctionDefinition,
    IHaveCompiledType,
    IHaveInstructionOffset
{
    public int InstructionOffset { get; set; } = BBLang.Generator.CodeGeneratorForMain.InvalidFunctionAddress;
    public bool IsMsilCompatible { get; set; } = true;

    public ImmutableArray<GeneralType> ParameterTypes { get; }
    public CompiledBlock Block { get; }
    public ParameterDefinitionCollection Parameters { get; }
    public Uri File { get; }

    public bool ReturnSomething => !Type.SameAs(BasicType.Void);
    ImmutableArray<ParameterDefinition> ICompiledFunctionDefinition.Parameters => Parameters.Parameters;
    ImmutableArray<GeneralType> ICompiledFunctionDefinition.ParameterTypes => ParameterTypes;

    public CompiledLambda(GeneralType type, ImmutableArray<GeneralType> parameterTypes, CompiledBlock block, ParameterDefinitionCollection parameters, Uri file)
    {
        Type = type;
        ParameterTypes = parameterTypes;
        Block = block;
        Parameters = parameters;
        File = file;
    }

    public override string ToString()
    {
        return "nig";
    }

    public string ToReadable() => Type.ToString();
    public override string Stringify(int depth = 0) => $"({string.Join(", ", Parameters.Parameters.Select(v => $"{v.Type} {v.Identifier}"))}) => {Block.Stringify(depth)}";
}
