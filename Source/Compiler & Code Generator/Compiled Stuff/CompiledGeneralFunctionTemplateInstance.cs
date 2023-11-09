using LanguageCore.Parser;

namespace LanguageCore.BBCode.Compiler
{
    public class CompiledGeneralFunctionTemplateInstance : CompiledGeneralFunction
    {
        public readonly CompiledGeneralFunction Template;

        public CompiledGeneralFunctionTemplateInstance(CompiledType type, CompiledType[] parameterTypes, CompiledGeneralFunction template, GeneralFunctionDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        {
            Template = template;
        }
    }
}
