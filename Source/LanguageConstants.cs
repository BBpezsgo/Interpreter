using LanguageCore.Compiler;
using LanguageCore.Parser.Statement;

namespace LanguageCore;

public static class DeclarationKeywords
{
    public const string Struct = "struct";
    public const string Using = "using";
    public const string Template = "template";
    public const string Alias = "alias";
}

public static class TypeKeywords
{
    public const string Void = "void";
    public const string Any = "any";

    public const string U8 = "u8";
    public const string I8 = "i8";
    public const string U16 = "u16";
    public const string I16 = "i16";
    public const string U32 = "u32";
    public const string I32 = "i32";
    public const string F32 = "f32";

    public static ImmutableArray<string> List { get; } = ImmutableArray.Create
    (
        TypeKeywords.Void,
        TypeKeywords.Any,
        TypeKeywords.U8,
        TypeKeywords.I8,
        TypeKeywords.U16,
        TypeKeywords.I16,
        TypeKeywords.U32,
        TypeKeywords.I32,
        TypeKeywords.F32
    );

    public static ImmutableDictionary<string, BasicType> BasicTypes { get; } = new Dictionary<string, BasicType>()
    {
        { TypeKeywords.Void, BasicType.Void },
        { TypeKeywords.Any, BasicType.Any },

        { TypeKeywords.U8, BasicType.U8 },
        { TypeKeywords.I8, BasicType.I8 },
        { TypeKeywords.U16, BasicType.U16 },
        { TypeKeywords.I16, BasicType.I16 },
        { TypeKeywords.U32, BasicType.U32 },
        { TypeKeywords.I32, BasicType.I32 },
        { TypeKeywords.F32, BasicType.F32 },
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
    public const string Crash = "crash";
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
    public const string LanguageName = "BBLang";
    public const string LanguageId = "bbc";
    public const string LanguageExtension = "bbc";

    public static ImmutableArray<string> KeywordList { get; } = ImmutableArray.Create
    (
        DeclarationKeywords.Struct,
        DeclarationKeywords.Using,
        DeclarationKeywords.Template,
        DeclarationKeywords.Alias,

        ModifierKeywords.Temp,
        ModifierKeywords.Ref,
        ModifierKeywords.This,
        ModifierKeywords.Const,
        ModifierKeywords.Inline,

        TypeKeywords.Void,
        TypeKeywords.Any,

        TypeKeywords.U8,
        TypeKeywords.I8,
        TypeKeywords.U16,
        TypeKeywords.I16,
        TypeKeywords.U32,
        TypeKeywords.I32,
        TypeKeywords.F32,

        ProtectionKeywords.Private,
        ProtectionKeywords.Export,

        StatementKeywords.As
    );
}

public static class LanguageOperators
{
    public static HashSet<string> UnaryOperators { get; } = new HashSet<string>()
    {
        UnaryOperatorCall.LogicalNOT,
        UnaryOperatorCall.BinaryNOT,
    };

    public static HashSet<string> BinaryOperators { get; } = new HashSet<string>()
    {
        BinaryOperatorCall.CompLT,
        BinaryOperatorCall.CompGT,
        BinaryOperatorCall.CompEQ,
        BinaryOperatorCall.CompNEQ,
        BinaryOperatorCall.CompLEQ,
        BinaryOperatorCall.CompGEQ,

        BinaryOperatorCall.Addition,
        BinaryOperatorCall.Subtraction,
        BinaryOperatorCall.Multiplication,
        BinaryOperatorCall.Division,
        BinaryOperatorCall.Modulo,
        BinaryOperatorCall.LogicalAND,
        BinaryOperatorCall.LogicalOR,
        BinaryOperatorCall.BitwiseAND,
        BinaryOperatorCall.BitwiseOR,
        BinaryOperatorCall.BitwiseXOR,
        BinaryOperatorCall.BitshiftLeft,
        BinaryOperatorCall.BitshiftRight,
    };

    public static ImmutableDictionary<string, int> Precedencies { get; } = new Dictionary<string, int>()
    {
        { BinaryOperatorCall.BitwiseOR, 4 },
        { BinaryOperatorCall.BitwiseAND, 4 },
        { BinaryOperatorCall.BitwiseXOR, 4 },
        { BinaryOperatorCall.BitshiftLeft, 4 },
        { BinaryOperatorCall.BitshiftRight, 4 },

        { BinaryOperatorCall.CompEQ, 5 },
        { BinaryOperatorCall.CompNEQ, 5 },
        { BinaryOperatorCall.CompLEQ, 5 },
        { BinaryOperatorCall.CompGEQ, 5 },

        { "=", 10 },

        { "+=", 11 },
        { "-=", 11 },

        { "*=", 12 },
        { "/=", 12 },
        { "%=", 12 },

        { BinaryOperatorCall.CompLT, 20 },
        { BinaryOperatorCall.CompGT, 20 },

        { BinaryOperatorCall.Addition, 30 },
        { BinaryOperatorCall.Subtraction, 30 },

        { BinaryOperatorCall.Multiplication, 31 },
        { BinaryOperatorCall.Division, 31 },
        { BinaryOperatorCall.Modulo, 31 },

        { "++", 40 },
        { "--", 40 },

        { BinaryOperatorCall.LogicalAND, 2 },
        { BinaryOperatorCall.LogicalOR, 2 },
    }.ToImmutableDictionary();
}

public static class AttributeConstants
{
    public const string ExternalIdentifier = "External";
    public const string ExposeIdentifier = "Expose";
    public const string BuiltinIdentifier = "Builtin";
}

public static class BuiltinFunctions
{
    public static ImmutableDictionary<string, BuiltinFunction> Prototypes { get; } = new Dictionary<string, BuiltinFunction>()
    {
        {
            Allocate, new BuiltinFunction(
                v => v.SameAs(new PointerType(BuiltinType.Any)),
                v => v.SameAs(BasicType.U8)
                  || v.SameAs(BasicType.I8)
                  || v.SameAs(BasicType.U16)
                  || v.SameAs(BasicType.I16)
                  || v.SameAs(BasicType.U32)
                  || v.SameAs(BasicType.I32)
            )
        },
        {
            Free, new BuiltinFunction(
                v => v.SameAs(BuiltinType.Void),
                v => v.SameAs(new PointerType(BuiltinType.Any))
            )
        },
    }.ToImmutableDictionary();

    public const string Allocate = "alloc";
    public const string Free = "free";
}

public static class BuiltinFunctionIdentifiers
{
    public const string Destructor = "destructor";
    public const string IndexerGet = "indexer_get";
    public const string IndexerSet = "indexer_set";
}

public static class ExternalFunctionNames
{
    public const string StdOut = "stdout";
    public const string StdIn = "stdin";
}
