using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.ASM
{
    public struct AssemblyHeader
    {
        public string MasmPath;
        public List<string> Libraries;
    }

    public class AssemblyCode
    {
        readonly StringBuilder CodeBuilder;
        readonly StringBuilder DataBuilder;
        readonly List<string> DataLabels;

        const string EOL = "\r\n";

        public AssemblyCode()
        {
            CodeBuilder = new StringBuilder();
            DataBuilder = new StringBuilder();
            DataLabels = new List<string>();
        }

        public string GenerateDataLabel(int length = 16)
        {
            char[] validCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();

            StringBuilder result = new(length);

            int endlessSafe = 128;
            while (result.Length < length)
            {
                char newChar = validCharacters[Random.Shared.Next(0, validCharacters.Length)];
                while (HasDataLabel(result.ToString() + newChar))
                {
                    newChar = validCharacters[Random.Shared.Next(0, validCharacters.Length)];
                    if (endlessSafe-- < 0) throw new EndlessLoopException();
                }
                result.Append(newChar);
            }

            return result.ToString();
        }
        public bool HasDataLabel(string dataLabel)
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

        public string NewStringDataLabel(string data, int labelLength = 16)
            => NewRawDataLabel($"\"{data}\", 0", labelLength);

        public string NewRawDataLabel(string rawData, int labelLength = 16)
        {
            string label = GenerateDataLabel(labelLength);
            DataLabels.Add(label);
            AppendDataLine($"{label} db {rawData}");
            return label;
        }

        public string Make(AssemblyHeader header)
        {
            StringBuilder builder = new();

            builder.Append(".386" + EOL);
            builder.Append(".model flat, stdcall" + EOL);
            builder.Append("option casemap:none" + EOL);
            builder.Append(EOL);

            for (int i = 0; i < header.Libraries.Count; i++)
            {
                string library = header.Libraries[i];
                builder.Append($"include {header.MasmPath}include\\{library}.inc" + EOL);
            }

            for (int i = 0; i < header.Libraries.Count; i++)
            {
                string library = header.Libraries[i];
                builder.Append($"includelib {header.MasmPath}lib\\{library}.lib" + EOL);
            }

            builder.Append(EOL);

            builder.Append(".data" + EOL);
            builder.Append(DataBuilder);

            builder.Append(EOL);

            builder.Append(".code" + EOL);
            builder.Append(CodeBuilder);

            builder.Append(EOL);

            return builder.ToString();
        }

        public void AppendCodeLine() => CodeBuilder.Append(EOL);

        public void AppendCodeLine(string line)
        {
            CodeBuilder.Append(line);
            CodeBuilder.Append(EOL);
        }

        public void AppendDataLine() => DataBuilder.Append(EOL);

        public void AppendDataLine(string line)
        {
            DataBuilder.Append(line);
            DataBuilder.Append(EOL);
        }
    }
}
