using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.ASM
{
    public struct AssemblyHeader
    {
        public List<string> Externs;
    }

    public class SectionBuilder
    {
        public const string EOL = "\r\n";

        public readonly StringBuilder Builder;

        public SectionBuilder()
        {
            this.Builder = new StringBuilder();
        }

        public void AppendText(char text) => Builder.Append(text);
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
            AppendTextLine($"{label}:");
            AppendTextLine($"db \"{data}\", 0");
            return label;
        }
    }

    public class TextSectionBuilder : SectionBuilder
    {
        public void AppendInstruction(string keyword, params string[] operands)
        {
            AppendText(keyword);
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
            CodeBuilder = new TextSectionBuilder();
            DataBuilder = new DataSectionBuilder();
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

            builder.Append("global _main" + EOL);

            for (int i = 0; i < header.Externs.Count; i++)
            {
                builder.Append($"extern {header.Externs[i]}" + EOL);
            }
            builder.Append(EOL);

            builder.Append("section .text" + EOL);
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
