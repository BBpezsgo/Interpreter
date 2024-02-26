using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LanguageCore.ASM;

public class GnuLinkerException : Exception
{
    public GnuLinkerException(string message, Exception? inner) : base(message, inner)
    {

    }
}

public static class GnuLinker
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
        { throw new FileNotFoundException($"LD not found", "ld.exe"); }

        using Process? process = Process.Start(new ProcessStartInfo(ld, $"{inputFile} -m i386pe -o {outputFile} -L \"C:\\Windows\\System32\" -l \"kernel32\"")
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
        {
            List<GnuLinkerException> linkerExceptions = new();

            string[] lines = stdError.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace(ld + ": ", null).Trim();
                if (string.IsNullOrEmpty(line)) continue;
                linkerExceptions.Add(new GnuLinkerException(line, null));
            }

            if (linkerExceptions.Count > 0)
            { throw linkerExceptions[0]; }

            throw new ProcessException(ld, process.ExitCode, stdOutput, stdError);
        }
    }
}
