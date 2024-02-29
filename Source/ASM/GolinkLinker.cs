using System.IO;

namespace LanguageCore.ASM;

public class GolinkLinkerException : Exception
{
    public GolinkLinkerException(string message, Exception? inner) : base(message, inner)
    {

    }
}

public static class GolinkLinker
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

        if (!Utils.GetFullPath("GoLink.exe", out string? golink))
        { throw new FileNotFoundException($"GoLink not found", "GoLink.exe"); }

        using Process? process = Process.Start(new ProcessStartInfo(golink, $"/entry:_main {inputFile} kernel32.dll")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new ProcessNotStartedException(golink);
        process.WaitForExit();

        string stdOutput = process.StandardOutput.ReadToEnd();
        string stdError = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            List<GnuLinkerException> linkerExceptions = new();

            string[] lines = stdError.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace(golink + ": ", null).Trim();
                if (string.IsNullOrEmpty(line)) continue;
                linkerExceptions.Add(new GnuLinkerException(line, null));
            }

            if (linkerExceptions.Count > 0)
            { throw linkerExceptions[0]; }

            throw new ProcessException(golink, process.ExitCode, stdOutput, stdError);
        }
    }
}
