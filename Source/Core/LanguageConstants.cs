namespace LanguageCore;

using Compiler;
using LanguageCore.Tokenizing;
using Runtime;

public static class DeclarationKeywords
{
    public const string Struct = "struct";
    public const string Enum = "enum";
    public const string Macro = "macro";
    public const string Using = "using";
    public const string Template = "template";
}

public static class TypeKeywords
{
    public const string Void = "void";
    public const string Byte = "byte";
    public const string Int = "int";
    public const string Float = "float";
    public const string Char = "char";

    public static readonly ImmutableArray<string> List = ImmutableArray.Create
    (
        TypeKeywords.Void,
        TypeKeywords.Byte,
        TypeKeywords.Int,
        TypeKeywords.Float,
        TypeKeywords.Char
    );

    public static readonly ImmutableDictionary<string, RuntimeType> RuntimeTypes = new Dictionary<string, RuntimeType>()
    {
        { TypeKeywords.Byte, RuntimeType.Byte },
        { TypeKeywords.Int, RuntimeType.Integer },
        { TypeKeywords.Float, RuntimeType.Single },
        { TypeKeywords.Char, RuntimeType.Char },
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, BasicType> BasicTypes = new Dictionary<string, BasicType>()
    {
        { TypeKeywords.Byte, BasicType.Byte },
        { TypeKeywords.Int, BasicType.Integer },
        { TypeKeywords.Float, BasicType.Float },
        { TypeKeywords.Char, BasicType.Char },
        { TypeKeywords.Void, BasicType.Void },
    }.ToImmutableDictionary();
}

public static class ProtectionKeywords
{
    public const string Private = "private";
    public const string Export = "export";
}

public static class ModifierKeywords
{
    public const string Temp = "temp";
    public const string Ref = "ref";
    public const string This = "this";
    public const string Const = "const";
    public const string Inline = "inline";
}

public static class StatementKeywords
{
    public const string If = "if";
    public const string ElseIf = "elseif";
    public const string Else = "else";
    public const string While = "while";
    public const string For = "for";
    public const string Return = "return";
    public const string Throw = "throw";
    public const string Delete = "delete";
    public const string New = "new";
    public const string Type = "type";
    public const string This = "this";
    public const string As = "as";
    public const string Var = "var";
    public const string Break = "break";
}

public static class LanguageConstants
{
    public static readonly ImmutableArray<string> KeywordList = ImmutableArray.Create
    (
        DeclarationKeywords.Struct,
        DeclarationKeywords.Enum,
        DeclarationKeywords.Macro,
        DeclarationKeywords.Using,
        DeclarationKeywords.Template,

        TypeKeywords.Void,
        TypeKeywords.Byte,
        TypeKeywords.Int,
        TypeKeywords.Float,
        TypeKeywords.Char,

        ProtectionKeywords.Private,
        ProtectionKeywords.Export,

        StatementKeywords.As
    );
}

public static class LanguageOperators
{
    public static readonly ImmutableDictionary<string, Opcode> OpCodes = new Dictionary<string, Opcode>()
    {
        { "!", Opcode.LOGIC_NOT },
        { "+", Opcode.MATH_ADD },
        { "<", Opcode.LOGIC_LT },
        { ">", Opcode.LOGIC_MT },
        { "-", Opcode.MATH_SUB },
        { "*", Opcode.MATH_MULT },
        { "/", Opcode.MATH_DIV },
        { "%", Opcode.MATH_MOD },
        { "==", Opcode.LOGIC_EQ },
        { "!=", Opcode.LOGIC_NEQ },
        { "&&", Opcode.LOGIC_AND },
        { "||", Opcode.LOGIC_OR },
        { "&", Opcode.BITS_AND },
        { "|", Opcode.BITS_OR },
        { "^", Opcode.BITS_XOR },
        { "<=", Opcode.LOGIC_LTEQ },
        { ">=", Opcode.LOGIC_MTEQ },
        { "<<", Opcode.BITS_SHIFT_LEFT },
        { ">>", Opcode.BITS_SHIFT_RIGHT },
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, int> ParameterCounts = new Dictionary<string, int>()
    {
        { "|", 2 },
        { "&", 2 },
        { "^", 2 },
        { "<<", 2 },
        { ">>", 2 },
        { "!", 1 },

        { "==", 2 },
        { "!=", 2 },
        { "<=", 2 },
        { ">=", 2 },

        { "<", 2 },
        { ">", 2 },

        { "+", 2 },
        { "-", 2 },

        { "*", 2 },
        { "/", 2 },
        { "%", 2 },

        { "&&", 2 },
        { "||", 2 },
    }.ToImmutableDictionary();

    public static readonly ImmutableDictionary<string, int> Precedencies = new Dictionary<string, int>()
    {
        { "|", 4 },
        { "&", 4 },
        { "^", 4 },
        { "<<", 4 },
        { ">>", 4 },

        { "==", 5 },
        { "!=", 5 },
        { "<=", 5 },
        { ">=", 5 },

        { "=", 10 },

        { "+=", 11 },
        { "-=", 11 },

        { "*=", 12 },
        { "/=", 12 },
        { "%=", 12 },

        { "<", 20 },
        { ">", 20 },

        { "+", 30 },
        { "-", 30 },

        { "*", 31 },
        { "/", 31 },
        { "%", 31 },

        { "++", 40 },
        { "--", 40 },

        { "&&", 2 },
        { "||", 2 },
    }.ToImmutableDictionary();
}
