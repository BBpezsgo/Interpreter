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

    public static class Assembler
    {
        static void Nasm(string input, string output)
        {
            const string nasm = @"nasm";

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

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            { throw new ProcessException(nasm, process.ExitCode, stdOutput, stdError); }

            Thread.Sleep(200);
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
            const string ld = @"C:\MinGW\bin\ld";

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

        public static void Assemble(string asmSourceCode, string outputFile)
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
                if (File.Exists(fileAsmTemp))
                { File.Delete(fileAsmTemp); }
                if (File.Exists(fileObjTemp))
                { File.Delete(fileObjTemp); }
                if (File.Exists(fileExeTemp))
                { File.Delete(fileExeTemp); }
            }
        }
    }
}
