namespace LanguageCore.Compiler
{
    using Parser;

    public class CompiledConstructorTemplateInstance : CompiledConstructor
    {
        public readonly CompiledConstructor Template;

        public CompiledConstructorTemplateInstance(CompiledType type, CompiledType[] parameterTypes, CompiledConstructor template, ConstructorDefinition functionDefinition)
            : base(type, parameterTypes, functionDefinition)
        {
            Template = template;
        }
    }
}
