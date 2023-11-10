using System;
using System.Collections.Generic;
using System.Text;
using LanguageCore.Runtime;

namespace LanguageCore.ASM
{
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
    }

    public struct Registers
    {
        public const string EAX = "eax";
        public const string EBX = "ebx";

        public const string StackPointer = "esp";
        public const string BasePointer = "ebp";
    }

    public class SectionBuilder
    {
        public const string EOL = "\r\n";

        public readonly StringBuilder Builder;
        public int Indent = 0;
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
    }

    public class DataSectionBuilder : SectionBuilder
    {
        const string ValidIdentifierCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        readonly List<string> DataLabels;

        public DataSectionBuilder() : base()
        {
            this.DataLabels = new List<string>();
        }

        string GenerateLabel(int length = 16)
        {
            StringBuilder result = new(length);

            int endlessSafe = 128;
            while (result.Length < length)
            {
                char newChar = ValidIdentifierCharacters[Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
                while (HasLabel(result.ToString() + newChar) || AssemblyCode.IsReserved(result.ToString() + newChar))
                {
                    newChar = ValidIdentifierCharacters[Random.Shared.Next(0, ValidIdentifierCharacters.Length)];
                    if (endlessSafe-- < 0) throw new EndlessLoopException();
                }
                result.Append(newChar);
            }

            return result.ToString();
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

        public string NewString(string data, int labelLength = 16)
        {
            string label = GenerateLabel(labelLength);
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
        public static string StringifyInstruction(Instruction instruction)
        {
            return instruction switch
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
                _ => throw new ImpossibleException(),
            };
        }

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

            for (int i = 0; i < header.Externs.Count; i++)
            {
                builder.Append($"extern {header.Externs[i]}" + EOL);
            }
            builder.Append(EOL);

            builder.Append("global _main" + EOL);
            builder.Append(EOL);

            builder.Append("section .text" + EOL);
            builder.Append(EOL);
            builder.Append("_main:" + EOL);

            builder.Append(CodeBuilder.Builder);
            builder.Append(EOL);

            builder.Append("section .rodata" + EOL);
            builder.Append(DataBuilder.Builder);
            builder.Append(EOL);


            return builder.ToString();
        }
    }
}
