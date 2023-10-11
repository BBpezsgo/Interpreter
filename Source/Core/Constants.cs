using System.Collections.Generic;
using LanguageCore.BBCode.Compiler;
using LanguageCore.Runtime;

namespace LanguageCore
{
    public static partial class Constants
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
            { "byte", RuntimeType.BYTE },
            { "int", RuntimeType.INT },
            { "float", RuntimeType.FLOAT },
            { "char", RuntimeType.CHAR },
        };

        public static readonly Dictionary<string, Type> BuiltinTypeMap3 = new()
        {
            { "byte", Type.BYTE },
            { "int", Type.INT },
            { "float", Type.FLOAT },
            { "char", Type.CHAR },
            { "void", Type.VOID },
        };

        public static class Operators
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
                { "<<", Opcode.BITSHIFT_LEFT },
                { ">>", Opcode.BITSHIFT_RIGHT },
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
}
