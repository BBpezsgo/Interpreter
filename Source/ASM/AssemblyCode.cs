using System;
using System.Collections.Generic;
using System.Text;

namespace LanguageCore.ASM
{
    public struct AssemblyHeader
    {
        public List<string> Externs;
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
        {
            string label = GenerateDataLabel(labelLength);
            DataLabels.Add(label);
            AppendDataLine($"{label}:");
            AppendDataLine($"  db \"{data}\", 0");
            return label;
        }

        public string Make(AssemblyHeader header)
        {
            StringBuilder builder = new();

            /*
                global _main
                extern  _GetStdHandle@4
                extern  _WriteFile@20
                extern  _ExitProcess@4

                section .text
            _main:
                ; DWORD  bytes;    
                mov     ebp, esp
                sub     esp, 4

                ; hStdOut = GetstdHandle( STD_OUTPUT_HANDLE)
                push    -11
                call    _GetStdHandle@4
                mov     ebx, eax    

                ; WriteFile( hstdOut, message, length(message), &bytes, 0);
                push    0
                lea     eax, [ebp-4]
                push    eax
                push    (message_end - message)
                push    message
                push    ebx
                call    _WriteFile@20

                ; ExitProcess(0)
                push    0
                call    _ExitProcess@4

                ; never here
                hlt
            message:
                db      'Hello, World', 10
            message_end:
             */

            builder.Append("global _main" + EOL);

            for (int i = 0; i < header.Externs.Count; i++)
            {
                builder.Append($"extern {header.Externs[i]}" + EOL);
            }
            builder.Append(EOL);

            builder.Append("section .text" + EOL);
            builder.Append("_main:" + EOL);

            builder.Append(CodeBuilder);
            builder.Append(EOL);

            builder.Append("section .rodata" + EOL);
            builder.Append(DataBuilder);
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
