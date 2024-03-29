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
    Enum,
    EnumMember,
    TypeParameter,
    ConstantName,
}
