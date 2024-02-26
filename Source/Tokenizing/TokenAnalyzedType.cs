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
    Namespace,
    Hash,
    HashParameter,
    Library,
    Class,
    Statement,
    BuiltinType,
    Enum,
    EnumMember,
    TypeParameter,
    ConstantName,
}
