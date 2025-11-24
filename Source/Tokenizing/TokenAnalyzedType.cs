namespace LanguageCore.Tokenizing;

public enum TokenAnalyzedType
{
    None,
    Attribute,
    Type,
    Struct,
    Keyword,
    FunctionName,
    VariableName,
    FieldName,
    ParameterName,
    CompileTag,
    CompileTagParameter,
    Statement,
    BuiltinType,
    TypeParameter,
    ConstantName,
    MathOperator,
    OtherOperator,
    TypeModifier,
    InstructionLabel,
}
