using LanguageCore.Compiler;
using LanguageCore.Parser.Statements;

namespace LanguageCore;

public static class DeclarationKeywords
{
    public const string Struct = "struct";
    public const string Using = "using";
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
    public const string U64 = "u64";
    public const string I64 = "i64";
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
        TypeKeywords.U64,
        TypeKeywords.I64,
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
        { TypeKeywords.U64, BasicType.U64 },
        { TypeKeywords.I64, BasicType.I64 },
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
    public const string Yield = "yield";
    public const string Goto = "goto";
    public const string Crash = "crash";
    public const string Delete = "delete";
    public const string New = "new";
    public const string Type = "type";
    public const string This = "this";
    public const string As = "as";
    public const string Var = "var";
    public const string Break = "break";
    public const string Sizeof = "sizeof";
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
        TypeKeywords.U64,
        TypeKeywords.I64,
        TypeKeywords.F32,

        ProtectionKeywords.Private,
        ProtectionKeywords.Export,

        StatementKeywords.As,
        StatementKeywords.New,
        StatementKeywords.Delete,
        StatementKeywords.Sizeof
    );
}

public static class LanguageOperators
{
    public static HashSet<string> UnaryOperators { get; } = new HashSet<string>()
    {
        UnaryOperatorCallExpression.LogicalNOT,
        UnaryOperatorCallExpression.BinaryNOT,
        UnaryOperatorCallExpression.UnaryPlus,
        UnaryOperatorCallExpression.UnaryMinus,
    };

    public static HashSet<string> BinaryOperators { get; } = new HashSet<string>()
    {
        BinaryOperatorCallExpression.CompLT,
        BinaryOperatorCallExpression.CompGT,
        BinaryOperatorCallExpression.CompEQ,
        BinaryOperatorCallExpression.CompNEQ,
        BinaryOperatorCallExpression.CompLEQ,
        BinaryOperatorCallExpression.CompGEQ,

        BinaryOperatorCallExpression.Addition,
        BinaryOperatorCallExpression.Subtraction,
        BinaryOperatorCallExpression.Multiplication,
        BinaryOperatorCallExpression.Division,
        BinaryOperatorCallExpression.Modulo,
        BinaryOperatorCallExpression.LogicalAND,
        BinaryOperatorCallExpression.LogicalOR,
        BinaryOperatorCallExpression.BitwiseAND,
        BinaryOperatorCallExpression.BitwiseOR,
        BinaryOperatorCallExpression.BitwiseXOR,
        BinaryOperatorCallExpression.BitshiftLeft,
        BinaryOperatorCallExpression.BitshiftRight,
    };

    public static ImmutableDictionary<string, int> Precedencies { get; } = new Dictionary<string, int>()
    {
        { BinaryOperatorCallExpression.BitwiseOR, 4 },
        { BinaryOperatorCallExpression.BitwiseAND, 4 },
        { BinaryOperatorCallExpression.BitwiseXOR, 4 },
        { BinaryOperatorCallExpression.BitshiftLeft, 4 },
        { BinaryOperatorCallExpression.BitshiftRight, 4 },

        { BinaryOperatorCallExpression.CompEQ, 5 },
        { BinaryOperatorCallExpression.CompNEQ, 5 },
        { BinaryOperatorCallExpression.CompLEQ, 5 },
        { BinaryOperatorCallExpression.CompGEQ, 5 },

        { "=", 10 },

        { "+=", 11 },
        { "-=", 11 },

        { "*=", 12 },
        { "/=", 12 },
        { "%=", 12 },

        { BinaryOperatorCallExpression.CompLT, 20 },
        { BinaryOperatorCallExpression.CompGT, 20 },

        { BinaryOperatorCallExpression.Addition, 30 },
        { BinaryOperatorCallExpression.Subtraction, 30 },

        { BinaryOperatorCallExpression.Multiplication, 31 },
        { BinaryOperatorCallExpression.Division, 31 },
        { BinaryOperatorCallExpression.Modulo, 31 },

        { "++", 40 },
        { "--", 40 },

        { BinaryOperatorCallExpression.LogicalAND, 2 },
        { BinaryOperatorCallExpression.LogicalOR, 2 },
    }.ToImmutableDictionary();
}

public static class AttributeConstants
{
    public const string ExternalIdentifier = "External";
    public const string ExposeIdentifier = "Expose";
    public const string BuiltinIdentifier = "Builtin";
    public const string MSILIncompatibleIdentifier = "MSILIncompatible";
    public const string InternalType = "InternalType";

    public static readonly ImmutableArray<string> List = ImmutableArray.Create(
        ExternalIdentifier,
        ExposeIdentifier,
        BuiltinIdentifier,
        MSILIncompatibleIdentifier,
        InternalType
    );
}

public static class BuiltinFunctions
{
    public static ImmutableDictionary<string, BuiltinFunction> Prototypes { get; } = new Dictionary<string, BuiltinFunction>()
    {
        {
            Allocate, new BuiltinFunction(
                v => v.SameAs(PointerType.Any),
                v => v.SameAs(BasicType.U8)
                  || v.SameAs(BasicType.I8)
                  || v.SameAs(BasicType.U16)
                  || v.SameAs(BasicType.I16)
                  || v.SameAs(BasicType.U32)
                  || v.SameAs(BasicType.I32)
                  || v.SameAs(BasicType.U64)
                  || v.SameAs(BasicType.I64)
            )
        },
        {
            Free, new BuiltinFunction(
                v => v.SameAs(BuiltinType.Void),
                v => v.SameAs(PointerType.Any)
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
