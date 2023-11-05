﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LanguageCore.Brainfuck
{
    [DebuggerDisplay($"{{{nameof(GetDebuggerDisplay)}(),nq}}")]
    public readonly struct CompactCodeSegment
    {
        public readonly ushort Count;
        public readonly byte OpCode;

        public CompactCodeSegment(ushort count, byte opCode)
        {
            Count = count;
            OpCode = opCode;
        }

        public CompactCodeSegment(ushort count, char opCode)
        {
            Count = count;
            OpCode = CompactCode.OpCode(opCode);
        }

        public override string? ToString() => new(CompactCode.OpCode(OpCode), Count);
        private string GetDebuggerDisplay() => $"{CompactCode.OpCode(OpCode)} x{Count}";
    }

    public readonly struct OpCodes
    {
        public const int POINTER_R = 1;
        public const int POINTER_L = 2;
        public const int ADD = 3;
        public const int SUB = 4;
        public const int BRANCH_START = 5;
        public const int BRANCH_END = 6;
        public const int OUT = 7;
        public const int IN = 8;
        public const int DEBUG = 9;
    }

    public readonly struct OpCodesCompact
    {
        public const int CLEAR = 10;
    }

    public class CompactCode
    {
        public static byte OpCode(char c) => c switch
        {
            '>' => 1,
            '<' => 2,
            '+' => 3,
            '-' => 4,
            '[' => 5,
            ']' => 6,
            '.' => 7,
            ',' => 8,
            '$' => 9,
            _ => 0,
        };
        public static char OpCode(byte c) => c switch
        {
            1 => '>',
            2 => '<',
            3 => '+',
            4 => '-',
            5 => '[',
            6 => ']',
            7 => '.',
            8 => ',',
            9 => '$',
            _ => '\0',
        };

        public static byte[] OpCode(string c)
        {
            byte[] result = new byte[c.Length];
            for (int i = 0; i < c.Length; i++)
            { result[i] = CompactCode.OpCode(c[i]); }
            return result;
        }
        public static byte[] OpCode(char[] c)
        {
            byte[] result = new byte[c.Length];
            for (int i = 0; i < c.Length; i++)
            { result[i] = CompactCode.OpCode(c[i]); }
            return result;
        }
        public static char[] OpCode(byte[] c)
        {
            char[] result = new char[c.Length];
            for (int i = 0; i < c.Length; i++)
            { result[i] = CompactCode.OpCode(c[i]); }
            return result;
        }

        static readonly char[] Duplicatable = new char[] { '>', '<', '+', '-' };

        public static CompactCodeSegment[] Generate(char[] code) => CompactCode.Generate(new string(code));
        public static CompactCodeSegment[] Generate(string code)
        {
            List<(byte OpCode, int Count)> result = new();

            for (int i = 0; i < code.Length; i++)
            {
                var c = OpCode(code[i]);

                if (i < code.Length - 3 && code.Substring(i, 3) == "[-]")
                {
                    result.Add((OpCodesCompact.CLEAR, 1));
                    i += 2;
                }
                else if (result.Count == 0)
                {
                    result.Add((c, 1));
                }
                else if (result[^1].OpCode == c && Duplicatable.Contains(OpCode(c)))
                {
                    result[^1] = (c, result[^1].Count + 1);
                }
                else
                {
                    result.Add((c, 1));
                }
            }

            CompactCodeSegment[] result2 = new CompactCodeSegment[result.Count];
            for (int i = 0; i < result2.Length; i++)
            { result2[i] = new CompactCodeSegment((ushort)result[i].Count, result[i].OpCode); }
            return result2;
        }
    }
}