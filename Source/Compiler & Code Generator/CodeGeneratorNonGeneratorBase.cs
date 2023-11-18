using System;

namespace LanguageCore.Compiler
{
    using BBCode.Generator;

    public class CodeGeneratorNonGeneratorBase : CodeGenerator
    {
        public CodeGeneratorNonGeneratorBase() : base()
        { }

        public CodeGeneratorNonGeneratorBase(CompilerResult compilerResult) : base(compilerResult)
        { }

        protected override ValueAddress GetBaseAddress(CompiledParameter parameter) => throw new NotImplementedException();
        protected override ValueAddress GetBaseAddress(CompiledParameter parameter, int offset) => throw new NotImplementedException();
        protected override void StackLoad(ValueAddress address) => throw new NotImplementedException();
        protected override void StackStore(ValueAddress address) => throw new NotImplementedException();
    }
}
