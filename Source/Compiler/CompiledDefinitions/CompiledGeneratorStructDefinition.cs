namespace LanguageCore.Compiler;

class CompiledGeneratorStructDefinition
{
    public CompiledStruct Struct { get; }
    public CompiledFunctionDefinition NextFunction { get; }
    public CompiledField StateField { get; }
    public CompiledField FunctionField { get; }
    public CompiledConstructorDefinition Constructor { get; }

    public CompiledGeneratorStructDefinition(CompiledStruct @struct, CompiledFunctionDefinition nextFunction, CompiledField stateField, CompiledField functionField, CompiledConstructorDefinition constructor)
    {
        Struct = @struct;
        NextFunction = nextFunction;
        StateField = stateField;
        FunctionField = functionField;
        Constructor = constructor;
    }
}
