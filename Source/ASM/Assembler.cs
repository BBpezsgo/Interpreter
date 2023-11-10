using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace LanguageCore.ASM
{
    [Serializable]
    public class ProcessException : Exception
    {
        readonly string processName;
        readonly int exitCode;
        readonly string stdOutput;
        readonly string stdError;

        public override string Message => $"Process \"{processName}\" exited with code {exitCode}";
        public string StandardOutput => stdOutput;
        public string StandardError => stdError;

        public ProcessException(string processName, int exitCode, string stdOutput, string stdError) : base()
        {
            this.processName = processName;
            this.exitCode = exitCode;
            this.stdOutput = stdOutput;
            this.stdError = stdError;
        }
        protected ProcessException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            this.exitCode = info.GetInt32("exitCode");
            this.stdOutput = info.GetString("stdOutput") ?? string.Empty;
            this.stdError = info.GetString("stdError") ?? string.Empty;
            this.processName = info.GetString("processName") ?? string.Empty;
        }
    }

    [System.Serializable]
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
        protected NasmException(
            System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context)
        {
            File = info.GetString("File");
            LineNumber = info.GetInt32("LineNumber");
            OriginalMessage = info.GetString("OriginalMessage") ?? string.Empty;
        }
        
        public override string ToString()
        {
            StringBuilder result = new(OriginalMessage);

            result.Append($" (at line {LineNumber})");

            if (File != null)
            { result.Append($" (in {File})"); }

            return result.ToString();
        }

        public static NasmException? Parse(string text, Exception? innerException = null)
        {
            if (!text.Contains(':')) return null;

            string potentialFileName = text.Split(':')[0];
            text = text[(potentialFileName.Length + 1)..];

            int lineNumber = -1;

            if (text.Contains(':'))
            {
                string potentialLine = text.Split(':')[0];
                if (int.TryParse(potentialLine, out lineNumber))
                {
                    text = text[(potentialLine.Length + 1)..].TrimStart();
                    if (text.StartsWith("error:"))
                    {
                        text = text["error:".Length..].TrimStart();
                    }
                }
            }
            
            return new NasmException(potentialFileName, lineNumber, text, innerException);
        }
    }

    public static class Assembler
    {
        static void Nasm(string input, string output)
        {
            string nasm = @$"C:\users\{Environment.UserName}\nasm\nasm.exe";

            if (!File.Exists(input))
            { return; }

            if (File.Exists(output))
            {
                File.Delete(output);
                Thread.Sleep(100);
            }

            Process? process = Process.Start(new ProcessStartInfo(nasm, $"-f win32 {input} -o {output}")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            { throw new Exception($"Failed to start process \"{nasm}\""); }

            process.WaitForExit();

            if (process.ExitCode == 0) {
                Thread.Sleep(200);
                return;
            }

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            ProcessException processException = new(nasm, process.ExitCode, stdOutput, stdError);

            string[] errorLines = stdError.Replace("\r\n", "\n").Replace("\r", "").Split('\n');

            for (int i = 0; i < errorLines.Length; i++)
            {
                string errorLine = errorLines[i].Trim();
                if (string.IsNullOrWhiteSpace(errorLine)) continue;
                NasmException? nasmException = NasmException.Parse(errorLine);
                if (nasmException != null) throw nasmException;
                else throw new NotImplementedException();
            }

            throw processException;
        }

        static void Masm(string input, string output)
        {
            const string masm = @"C:\masm32\bin\";

            if (!File.Exists(input))
            { return; }

            if (File.Exists(output))
            {
                File.Delete(output);
                Thread.Sleep(100);
            }

            Process? process = Process.Start(new ProcessStartInfo(masm + "ml", $"/c /Zd /Fo \"{output}\" /coff \"{input}\""));
            process?.WaitForExit();
            Console.WriteLine();
            Console.WriteLine($"Exit code: {process?.ExitCode}");
            Thread.Sleep(200);
        }

        static void Ln(string input, string output)
        {
            string ld = @"C:\MinGW\bin\ld.exe"; // @$"C:\users\{Environment.UserName}\MinGW\bin\ld.exe";

            if (!File.Exists(input))
            { return; }

            if (File.Exists(output))
            {
                File.Delete(output);
                Thread.Sleep(100);
            }

            // Process? process = Process.Start(new ProcessStartInfo(masm + "Link", $"/SUBSYSTEM:CONSOLE /OUT:\"{OutputFileExe}\" \"{OutputFileObject}\""));
            Process? process = Process.Start(new ProcessStartInfo(ld, $"{input} -o {output} -L \"C:\\Windows\\System32\" -l \"kernel32\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            { throw new Exception($"Failed to start process \"{ld}\""); }

            process.WaitForExit();

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            { throw new ProcessException(ld, process.ExitCode, stdOutput, stdError); }

            Thread.Sleep(200);
        }

        static void Batch(string bat)
        {
            Process? process = Process.Start(new ProcessStartInfo(bat)
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            { throw new Exception($"Failed to start process \"{bat}\""); }

            process.WaitForExit();

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            { throw new ProcessException(bat, process.ExitCode, stdOutput, stdError); }

            Thread.Sleep(200);
        }

        public static void Assemble(string asmSourceCode, string outputFile, bool throwErrors)
        {
            string outputFilename = Path.GetFileName(outputFile);

            string fileAsmTemp = outputFilename + ".asm";
            string fileObjTemp = outputFilename + ".obj";
            string fileExeTemp = outputFilename + ".exe";
            string fileExeFinal = outputFile + ".exe";

            if (File.Exists(fileAsmTemp))
            { Output.LogWarning($"File \"{fileAsmTemp}\" will be overridden"); }

            if (File.Exists(fileObjTemp))
            { Output.LogWarning($"File \"{fileObjTemp}\" will be overridden"); }

            if (File.Exists(fileExeTemp))
            { Output.LogWarning($"File \"{fileExeTemp}\" will be overridden"); }

            try
            {
                File.WriteAllText(fileAsmTemp, asmSourceCode);
                Thread.Sleep(100);

                Nasm(fileAsmTemp, fileObjTemp);

                Ln(fileObjTemp, fileExeTemp);

                if (File.Exists(fileExeTemp))
                { File.Copy(fileExeTemp, fileExeFinal, true); }
                Thread.Sleep(200);
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                // if (File.Exists(fileAsmTemp))
                // { File.Delete(fileAsmTemp); }
                if (File.Exists(fileObjTemp))
                { File.Delete(fileObjTemp); }
                if (File.Exists(fileExeTemp))
                { File.Delete(fileExeTemp); }
            }
        }
    }
}
