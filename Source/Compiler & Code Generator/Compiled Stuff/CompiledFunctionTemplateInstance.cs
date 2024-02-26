namespace LanguageCore.Compiler;

using Parser;

public class CompiledFunctionTemplateInstance : CompiledFunction
{
    public readonly CompiledFunction Template;

    public CompiledFunctionTemplateInstance(CompiledType type, CompiledType[] parameterTypes, CompiledFunction template, FunctionDefinition functionDefinition)
        : base(type, parameterTypes, functionDefinition)
    {
        Template = template;
    }
}
