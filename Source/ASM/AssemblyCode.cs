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
                while (HasLabel(result.ToString() + newChar))
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
            AppendTextLine($"  db \"{data}\", 0");
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
        public readonly TextSectionBuilder CodeBuilder;
        public readonly DataSectionBuilder DataBuilder;

        const string EOL = "\r\n";

        public AssemblyCode()
        {
            CodeBuilder = new TextSectionBuilder();
            DataBuilder = new DataSectionBuilder();
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
