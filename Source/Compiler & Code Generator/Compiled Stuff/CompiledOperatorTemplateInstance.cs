using LanguageCore.Parser;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledOperatorTemplateInstance : CompiledOperator
    {
        public readonly CompiledOperator Template;

        public CompiledOperatorTemplateInstance(CompiledType type, CompiledType[] parameterTypes, CompiledOperator template, FunctionDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        {
            Template = template;
        }
    }
}
