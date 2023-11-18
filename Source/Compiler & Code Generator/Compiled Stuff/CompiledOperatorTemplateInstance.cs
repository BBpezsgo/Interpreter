namespace LanguageCore.Compiler
{
    using Parser;

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
