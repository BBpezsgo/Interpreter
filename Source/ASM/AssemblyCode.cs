using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.ASM
{
    using Runtime;

    public struct AssemblyHeader
    {
        public List<string> Externs;
    }

    public enum Instruction
    {
        /// <summary> </summary>
        MOV,
        /// <summary>  </summary>
        PUSH,
        /// <summary>  </summary>
        CALL,
        /// <summary>  </summary>
        HALT,
        /// <summary>
        /// <para> <c>add A B</c> </para>
        /// <para> <c>A = A + B</c> </para>
        /// </summary>
        ADD,
        /// <summary>
        /// <para> <c>sub A B</c> </para>
        /// <para> <c>A = A - B</c> </para>
        /// </summary>
        SUB,
        /// <summary> Load Effective Address </summary>
        LEA,
        /// <summary>  </summary>
        POP,
        /// <summary>  </summary>
        MUL,
        /// <summary>  </summary>
        DIV,
        /// <summary>  </summary>
        IMUL,
        /// <summary>  </summary>
        IDIV,
        /// <summary>  </summary>
        TEST,
        /// <summary>  </summary>
        JZ,
        /// <summary>  </summary>
        JMP,
        /// <summary>  </summary>
        AND,
        /// <summary>  </summary>
        XOR,
        /// <summary>  </summary>
        OR,
        /// <summary>  </summary>
        POR,
        /// <summary>  </summary>
        PAND,
        RET,
    }

    public struct Registers
    {
        /// <summary>Accumulator register. Used in arithmetic operations.</summary>
        public const string RAX = "rax", EAX = "eax", AX = "ax", AL = "al";
        /// <summary>Base register (BX). Used as a pointer to data (located in segment register DS, when in segmented mode).</summary>
        public const string RBX = "rbx", EBX = "ebx", BX = "bx", BL = "bl";
        /// <summary>Counter register (CX). Used in shift/rotate instructions and loops.</summary>
        public const string RCX = "rcx", ECX = "ecx", CX = "cx", CL = "cl";
        /// <summary>Data register (DX). Used in arithmetic operations and I/O operations.</summary>
        public const string RDX = "rdx", EDX = "edx", DX = "dx", DL = "dl";
        /// <summary>Source Index register (SI). Used as a pointer to a source in stream operations.</summary>
        public const string RSI = "rsi", ESI = "esi", SI = "si", SIL = "sil";
        /// <summary>Destination Index register (DI). Used as a pointer to a destination in stream operations.</summary>
        public const string RDI = "rdi", EDI = "edi", DI = "di", DIL = "dil";
        /// <summary>Stack Base Pointer register (BP). Used to point to the base of the stack.</summary>
        public const string RBP = "rbp", EBP = "ebp", BP = "bp", BPL = "bpl";
        /// <summary>Stack Pointer register (SP). Pointer to the top of the stack.</summary>
        public const string RSP = "rsp", ESP = "esp", SP = "sp", SPL = "spl";
        public const string R8 = "r8", R8d = "r8d", R8w = "r8w", R8b = "r8b";
        public const string R9 = "r9", R9d = "r9d", R9w = "r9w", R9b = "r9b";
        public const string R10 = "r10", R10d = "r10d   ", R10w = "r10w", R10b = "r10b";
        public const string R11 = "r11", R11d = "r11d   ", R11w = "r11w", R11b = "r11b";
        public const string R12 = "r12", R12d = "r12d   ", R12w = "r12w", R12b = "r12b";
        public const string R13 = "r13", R13d = "r13d   ", R13w = "r13w", R13b = "r13b";
        public const string R14 = "r14", R14d = "r14d   ", R14w = "r14w", R14b = "r14b";
        public const string R15 = "r15", R15d = "r15d   ", R15w = "r15w", R15b = "r15b";
    }

    public class SectionBuilder
    {
        public const string EOL = "\r\n";

        public readonly StringBuilder Builder;
        public int Indent;
        public const int IndentIncrement = 2;

        public SectionBuilder()
        {
            this.Builder = new StringBuilder();
            this.Indent = 0;
        }

        public void AppendText(char text) => Builder.Append(text);
        public void AppendText(char text, int repeatCount) => Builder.Append(text, repeatCount);
        public void AppendText(string text) => Builder.Append(text);
        public void AppendTextLine() => Builder.Append(EOL);
        public void AppendTextLine(string text) { Builder.Append(text); Builder.Append(EOL); }

        public void AppendComment(string? comment)
        {
            Builder.Append(' ', Indent);
            Builder.Append(';');
            if (!string.IsNullOrWhiteSpace(comment))
            {
                Builder.Append(' ');
                Builder.Append(comment);
            }
        }
        public void AppendCommentLine(string? comment)
        {
            AppendComment(comment);
            Builder.Append(EOL);
        }

        public IndentBlock Block() => new(this);
    }

    public readonly struct IndentBlock : IDisposable
    {
        readonly SectionBuilder Builder;

        public IndentBlock(SectionBuilder builder)
        {
            Builder = builder;
            Builder.Indent += SectionBuilder.IndentIncrement;
        }

        public void Dispose()
        {
            Builder.Indent -= SectionBuilder.IndentIncrement;
        }
    }

    public class DataSectionBuilder : SectionBuilder
    {
        readonly List<string> DataLabels;

        public DataSectionBuilder() : base()
        {
            this.DataLabels = new List<string>();
        }

        bool HasLabel(string dataLabel)
        {
            for (int i = 0; i < DataLabels.Count; i++)
            {
                if (string.Equals(DataLabels[i], dataLabel))
                {
                    return true;
                }
            }
            return false;
        }

        public string NewString(string data, string? name = null, int labelLength = 16)
        {
            string label = AssemblyCode.GenerateLabel("d_" + name + "_", labelLength, HasLabel);
            DataLabels.Add(label);
            AppendText(' ', Indent);
            AppendTextLine($"{label}:");
            AppendText(' ', Indent + IndentIncrement);
            AppendTextLine($"db \"{data}\", 0");
            return label;
        }
    }

    public class TextSectionBuilder : SectionBuilder
    {
        readonly List<string> Labels;

        public TextSectionBuilder() : base()
        {
            this.Labels = new List<string>();
        }

        bool HasLabel(string dataLabel)
        {
            for (int i = 0; i < Labels.Count; i++)
            {
                if (string.Equals(Labels[i], dataLabel))
                {
                    return true;
                }
            }
            return false;
        }

        public string NewLabel(string? name = null, int labelLength = 16)
        {
            string label = AssemblyCode.GenerateLabel("t_" + name + "_", labelLength, HasLabel);
            Labels.Add(label);
            return label;
        }

        public void AppendLabel(string label)
        {
            AppendText(' ', Indent);
            AppendTextLine($"{label}:");
        }

        public static string StringifyInstruction(Instruction instruction) => instruction switch
        {
            Instruction.MOV => "mov",
            Instruction.PUSH => "push",
            Instruction.CALL => "call",
            Instruction.HALT => "hlt",
            Instruction.ADD => "add",
            Instruction.SUB => "sub",
            Instruction.LEA => "lea",
            Instruction.POP => "pop",
            Instruction.MUL => "mul",
            Instruction.DIV => "div",
            Instruction.IMUL => "imul",
            Instruction.IDIV => "idiv",
            Instruction.TEST => "test",
            Instruction.JZ => "jz",
            Instruction.JMP => "jmp",
            Instruction.AND => "and",
            Instruction.XOR => "xor",
            Instruction.OR => "or",
            Instruction.POR => "por",
            Instruction.PAND => "pand",
            Instruction.RET => "ret",
            _ => throw new ImpossibleException(),
        };

        public void AppendInstruction(Instruction keyword)
        {
            AppendText(' ', Indent);
            AppendText(StringifyInstruction(keyword));
            AppendText(EOL);
        }

        public void AppendInstruction(string keyword)
        {
            AppendText(' ', Indent);
            AppendText(keyword);
            AppendText(EOL);
        }

        public void AppendInstruction(Instruction keyword, params string[] operands)
        {
            AppendText(' ', Indent);
            AppendText(StringifyInstruction(keyword));
            if (operands.Length > 0)
            {
                AppendText(' ');
                for (int i = 0; i < operands.Length; i++)
                {
                    string operand = operands[i];
                    if (i > 0)
                    { AppendText(", "); }
                    AppendText(operand);
                }
            }
            AppendText(EOL);
        }

        public void AppendInstruction(Instruction keyword, params DataItem[] operands)
        {
            AppendText(' ', Indent);
            AppendText(StringifyInstruction(keyword));
            if (operands.Length > 0)
            {
                AppendText(' ');
                for (int i = 0; i < operands.Length; i++)
                {
                    DataItem operand = operands[i];
                    if (i > 0)
                    { AppendText(", "); }
                    switch (operand.Type)
                    {
                        case RuntimeType.Null:
                            throw new InternalException($"Operand value is null");
                        case RuntimeType.UInt8:
                            AppendText(operand.ValueUInt8.ToString());
                            break;
                        case RuntimeType.SInt32:
                            AppendText(operand.ValueSInt32.ToString());
                            break;
                        case RuntimeType.Single:
                            throw new NotImplementedException();
                        case RuntimeType.UInt16:
                            AppendText(operand.ValueUInt16.ToString());
                            break;
                        default:
                            throw new ImpossibleException();
                    }
                }
            }
            AppendText(EOL);
        }
    }

    public class AssemblyCode
    {
        public static readonly string[] ReservedWords = new string[] {
            "$",
            "DF",
            "GROUP",
            "ORG",
            "*",
            "DGROUP",
            "GT",
            "%OUT",
            "+",
            "DOSSEG",
            "HIGH",
            "PAGE",
            "_",
            "DQ",
            "IF",
            "PARA",
            ".",
            "DS",
            "IF1",
            "PROC",
            "/",
            "DT",
            "IF2",
            "PTR",
            "=",
            "DUP",
            "IFB",
            "PUBLIC",
            "?",
            "DW",
            "IFDEF",
            "PURGE",
            "[  ]",
            "DWORD",
            "IFGIF",
            "QWORD",
            ".186",
            "ELSE",
            "IFDE",
            ".RADIX",
            ".286",
            "END",
            "IFIDN",
            "RECORD",
            ".286P",
            "ENDIF",
            "IFNB",
            "REPT",
            ".287",
            "ENDM",
            "IFNDEF",
            ".SALL",
            ".386",
            "ENDP",
            "INCLUDE",
            "SEG",
            ".386P",
            "ENDS",
            "INCLUDELIB",
            "SEGMENT",
            ".387",
            "EQ",
            "IRP",
            ".SEQ",
            ".8086",
            "EQU",
            "IRPC",
            ".SFCOND",
            ".8087",
            ".ERR",
            "LABEL",
            "SHL",
            "ALIGN",
            ".ERR1",
            ".LALL",
            "SHORT",
            ".ALPHA",
            ".ERR2",
            "LARGE",
            "SHR",
            "AND",
            ".ERRB",
            "LE",
            "SIZE",
            "ASSUME",
            ".ERRDEF",
            "LENGTH",
            "SMALL",
            "AT",
            ".ERRDIF",
            ".LFCOND",
            "STACK",
            "BYTE",
            ".ERRE",
            ".LIST",
            "@STACK",
            ".CODE",
            ".ERRIDN",
            "LOCAL",
            ".STACK",
            "@CODE",
            ".ERRNB",
            "LOW",
            "STRUC",
            "@CODESIZE",
            ".ERRNDEF",
            "LT",
            "SUBTTL",
            "COMM",
            ".ERRNZ",
            "MACRO",
            "TBYTE",
            "COMMENT",
            "EVEN",
            "MASK",
            ".TFCOND",
            ".CONST",
            "EXITM",
            "MEDIUM",
            "THIS",
            ".CREF",
            "EXTRN",
            "MOD",
            "TITLE",
            "@CURSEG",
            "FAR",
            ".MODEL",
            "TYPE",
            "@DATA",
            "@FARDATA",
            "NAME",
            ".TYPE",
            ".DATA",
            ".FARDATA",
            "NE",
            "WIDTH",
            "@DATA?",
            "@FARDATA?",
            "NEAR",
            "WORD",
            ".DATA?",
            ".FARDATA?",
            "NOT",
            "@WORDSIZE",
                        "@DATASIZE",
            "@FILENAME",
            "NOTHING",
            ".XALL",
            "DB",
            "FWORD",
            "OFFSET",
            ".XCREP",
            "DD",
            "GE",
            "OR",
            ".XLIST",
            "XOR",
        };
        public const string ValidIdentifierCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public readonly TextSectionBuilder CodeBuilder;
        public readonly DataSectionBuilder DataBuilder;

        const string EOL = "\r\n";

        public AssemblyCode()
        {
            CodeBuilder = new TextSectionBuilder()
            {
                Indent = SectionBuilder.IndentIncrement,
            };
            DataBuilder = new DataSectionBuilder()
            {
                Indent = SectionBuilder.IndentIncrement,
            };
        }

        public static string GenerateLabel(string? prefix, int length, Func<string, bool>? isValidCallback)
        {
            StringBuilder result = new(prefix, length + (prefix?.Length ?? 0));

            int endlessSafe = 128;
            while (result.Length < length)
            {
                char newChar = ValidIdentifierCharacters[Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
                while (AssemblyCode.IsReserved(result.ToString() + newChar) ||
                       isValidCallback == null ||
                       isValidCallback.Invoke(result.ToString() + newChar))
                {
                    newChar = ValidIdentifierCharacters[Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
                    if (endlessSafe-- < 0) throw new EndlessLoopException();
                }
                result.Append(newChar);
            }

            return result.ToString();
        }

        public static bool IsReserved(string word)
        {
            for (int i = 0; i < ReservedWords.Length; i++)
            {
                if (string.Equals(ReservedWords[i], word, StringComparison.InvariantCultureIgnoreCase))
                { return true; }
            }
            return false;
        }

        public string Make(AssemblyHeader header)
        {
            StringBuilder builder = new();

            builder.Append(";" + EOL);
            builder.Append("; WARNING: Generated by BB's compiler!" + EOL);
            builder.Append(";" + EOL);
            builder.Append(EOL);

            builder.Append(";" + EOL);
            builder.Append("; External functions" + EOL);
            builder.Append(";" + EOL);
            for (int i = 0; i < header.Externs.Count; i++)
            {
                builder.Append($"extern {header.Externs[i]}" + EOL);
            }
            builder.Append(EOL);

            builder.Append(";" + EOL);
            builder.Append("; Global functions" + EOL);
            builder.Append(";" + EOL);
            builder.Append("global _main" + EOL);
            builder.Append(EOL);

            builder.Append(";" + EOL);
            builder.Append("; Code Section" + EOL);
            builder.Append(";" + EOL);
            builder.Append("section .text" + EOL);
            builder.Append(EOL);
            builder.Append("_main:" + EOL);

            builder.Append(CodeBuilder.Builder);
            builder.Append(EOL);

            builder.Append(";" + EOL);
            builder.Append("; Data Section" + EOL);
            builder.Append(";" + EOL);
            builder.Append("section .rodata" + EOL);
            builder.Append(DataBuilder.Builder);
            builder.Append(EOL);

            return builder.ToString();
        }
    }
}
