using System.Diagnostics;
using System.IO;

namespace LanguageCore.ASM
{
    public static class Linker
    {
        /// <exception cref="ProcessException"/>
        /// <exception cref="FileNotFoundException"/>
        /// <exception cref="ProcessNotStartedException"/>
        public static void Link(string inputFile, string outputFile)
        {
            if (!File.Exists(inputFile))
            { throw new FileNotFoundException($"Input file not found", inputFile); }

            if (File.Exists(outputFile))
            { File.Delete(outputFile); }

            if (!Utils.GetFullPath("ld.exe", out string? ld))
            { throw new FileNotFoundException($"Linker not found", "ld.exe"); }

            // // https://deac-riga.dl.sourceforge.net/project/gnuwin32/sed/4.2.1/sed-4.2.1-setup.exe
            // string ld = @"C:\MinGW\bin\ld.exe"; // @$"C:\users\{Environment.UserName}\MinGW\bin\ld.exe";
            using Process? process = Process.Start(new ProcessStartInfo(ld, $"{inputFile} -o {outputFile} -L \"C:\\Windows\\System32\" -l \"kernel32\"")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });

            if (process == null)
            { throw new ProcessNotStartedException(ld); }

            process.WaitForExit();

            string stdOutput = process.StandardOutput.ReadToEnd();
            string stdError = process.StandardError.ReadToEnd();

            if (process.ExitCode != 0)
            { throw new ProcessException(ld, process.ExitCode, stdOutput, stdError); }
        }
    }
}
