using System.Collections.Generic;

namespace LanguageCore
{
    using Compiler;
    using Runtime;

    public static partial class LanguageConstants
    {
        public static readonly string[] Keywords = new string[]
        {
            "struct",
            "class",
            "enum",
            "macro",
            "adaptive",

            "void",
            "namespace",
            "using",

            "byte",
            "int",
            "float",
            "char",

            "as",
        };

        public static readonly string[] BuiltinTypes = new string[]
        {
            "void",
            "byte",
            "int",
            "float",
            "char",
        };

        public static readonly Dictionary<string, RuntimeType> BuiltinTypeMap1 = new()
        {
            { "byte", RuntimeType.UInt8 },
            { "int", RuntimeType.SInt32 },
            { "float", RuntimeType.Single },
            { "char", RuntimeType.UInt16 },
        };

        public static readonly Dictionary<string, Type> BuiltinTypeMap3 = new()
        {
            { "byte", Type.Byte },
            { "int", Type.Integer },
            { "float", Type.Float },
            { "char", Type.Char },
            { "void", Type.Void },
        };
    }

    public static class LanguageOperators
    {
        public static readonly Dictionary<string, Opcode> OpCodes = new()
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
        };

        public static readonly Dictionary<string, int> ParameterCounts = new()
        {
            { "!", 1 },
            { "+", 2 },
            { "<", 2 },
            { ">", 2 },
            { "-", 2 },
            { "*", 2 },
            { "/", 2 },
            { "%", 2 },
            { "==", 2 },
            { "!=", 2 },
            { "&&", 2 },
            { "&", 2 },
            { "||", 2 },
            { "|", 2 },
            { "^", 2 },
            { "<=", 2 },
            { ">=", 2 },
            { "<<", 2 },
            { ">>", 2 },
        };

        public static readonly Dictionary<string, int> Precedencies = new()
        {
            { "|", 4 },
            { "&", 4 },
            { "^", 4 },
            { "<<", 4 },
            { ">>", 4 },

            { "!=", 5 },
            { ">=", 5 },
            { "<=", 5 },
            { "==", 5 },

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
        };
    }
}
