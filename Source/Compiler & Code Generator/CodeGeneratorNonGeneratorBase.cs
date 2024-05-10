namespace LanguageCore.Compiler;

public class CodeGeneratorNonGeneratorBase : CodeGenerator
{
    public CodeGeneratorNonGeneratorBase(CompilerResult compilerResult, AnalysisCollection? analysisCollection, PrintCallback? print) : base(compilerResult, analysisCollection, print)
    { }

    protected override ValueAddress GetBaseAddress(CompiledParameter parameter) => throw new NotImplementedException();
    protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset) => throw new NotImplementedException();
    protected override ValueAddress GetGlobalVariableAddress(CompiledVariable variable) => throw new NotImplementedException();
    protected override void StackLoad(ValueAddress address) => throw new NotImplementedException();
    protected override void StackStore(ValueAddress address) => throw new NotImplementedException();
}
