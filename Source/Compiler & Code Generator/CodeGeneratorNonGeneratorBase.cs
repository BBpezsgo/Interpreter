namespace LanguageCore.Compiler;

using BBCode.Generator;

public class CodeGeneratorNonGeneratorBase : CodeGenerator
{
    public CodeGeneratorNonGeneratorBase() : base()
    { }

    public CodeGeneratorNonGeneratorBase(CompilerResult compilerResult, GeneratorSettings settings, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, settings, analysisCollection, print)
    { }

    protected override ValueAddress GetBaseAddress(CompiledParameter parameter) => throw new NotImplementedException();
    protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset) => throw new NotImplementedException();
    protected override ValueAddress GetGlobalVariableAddress(CompiledVariable variable) => throw new NotImplementedException();
    protected override void StackLoad(ValueAddress address) => throw new NotImplementedException();
    protected override void StackStore(ValueAddress address) => throw new NotImplementedException();
}
