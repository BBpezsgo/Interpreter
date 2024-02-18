using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace LanguageCore.ASM
{
    public class NasmException : Exception
    {
        public readonly string? File;
        public readonly int LineNumber;
        public readonly string OriginalMessage;

        public NasmException(string file, int lineNumber, string originalMessage, Exception? inner) : base(originalMessage, inner)
        {
            this.File = file;
            this.LineNumber = lineNumber;
            this.OriginalMessage = originalMessage;
        }

        public override string ToString()
        {
            StringBuilder result = new(OriginalMessage);

            result.Append(CultureInfo.InvariantCulture, $" (at line {LineNumber})");

            if (File != null)
            { result.Append(CultureInfo.InvariantCulture, $" (in {File})"); }

            return result.ToString();
        }

        public static NasmException? Parse(string text, Exception? innerException = null)
        {
            if (!text.Contains(':', StringComparison.Ordinal)) return null;

            string potentialFileName = text.Split(':')[0];
            text = text[(potentialFileName.Length + 1)..];

            int lineNumber = -1;

            if (text.Contains(':', StringComparison.Ordinal))
            {
                string potentialLine = text.Split(':')[0];
                if (int.TryParse(potentialLine, out lineNumber))
                {
                    text = text[(potentialLine.Length + 1)..].TrimStart();
                    if (text.StartsWith("error:", StringComparison.Ordinal))
                    {
                        text = text["error:".Length..].TrimStart();
                    }
                }
            }

            return new NasmException(potentialFileName, lineNumber, text, innerException);
        }

        public string? GetArrows()
        {
            if (File == null) return null;
            string text = System.IO.File.ReadAllText(File);

            string[] lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');

            if (LineNumber > lines.Length)
            { return null; }

            string line = lines[LineNumber];

            StringBuilder result = new();

            result.Append(line.Replace('\t', ' '));
            result.AppendLine();
            result.Append('^', Math.Max(1, line.Length));
            return result.ToString();
        }
    }

    public static class Nasm
    {
        /// <exception cref="ProcessNotStartedException"/>
        /// <exception cref="NotImplementedException"/>
        /// <exception cref="NasmException"/>
        /// <exception cref="FileNotFoundException"/>
        /// <exception cref="ProcessException"/>
        public static void Assemble(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
            { throw new FileNotFoundException($"Input file not found", inputFile); }

            if (File.Exists(outputFile))
            { File.Delete(outputFile); }

            if (!Utils.GetFullPath("nasm.exe", out string? nasm))
            { throw new FileNotFoundException($"NASM not found", "nasm.exe"); }

            using Process? process = Process.Start(new ProcessStartInfo(nasm, $"-gcv8 -f win32 {inputFile} -o {outputFile}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            { throw new ProcessNotStartedException(nasm); }

            process.WaitForExit();

            if (process.ExitCode == 0)
            { return; }

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            string[] errorLines = stdError.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", string.Empty, StringComparison.Ordinal).Split('\n');

            for (int i = 0; i < errorLines.Length; i++)
            {
                string errorLine = errorLines[i].Trim();
                if (string.IsNullOrWhiteSpace(errorLine)) continue;
                NasmException? nasmException = NasmException.Parse(errorLine);
                if (nasmException != null) throw nasmException;
                else throw new NotImplementedException();
            }

            throw new ProcessException(nasm, process.ExitCode, stdOutput, stdError);
        }
    }
}
