namespace LanguageCore.Compiler;

using Parser;

public class CompiledGeneralFunctionTemplateInstance : CompiledGeneralFunction
{
    public readonly CompiledGeneralFunction Template;

    public CompiledGeneralFunctionTemplateInstance(CompiledType type, CompiledType[] parameterTypes, CompiledGeneralFunction template, GeneralFunctionDefinition functionDefinition)
        : base(type, parameterTypes, functionDefinition)
    {
        Template = template;
    }
}
