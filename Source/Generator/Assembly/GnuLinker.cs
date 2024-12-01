using System.IO;

namespace LanguageCore.Assembly;

[ExcludeFromCodeCoverage]
public class GnuLinkerException : Exception
{
    public GnuLinkerException(string message, Exception? inner) : base(message, inner)
    { }
}

[ExcludeFromCodeCoverage]
public static class GnuLinker
{
    public static void Link(string inputFile, string outputFile)
    {
        if (!File.Exists(inputFile))
        { throw new FileNotFoundException($"Input file not found", inputFile); }

        if (File.Exists(outputFile))
        { File.Delete(outputFile); }

        // -m i386pe 
        // -L \"C:\\Windows\\System32\" -l \"kernel32\"
        using Process? process = Process.Start(new ProcessStartInfo("ld", $"{inputFile} -o {outputFile}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new ProcessNotStartedException("ld");
        process.WaitForExit();

        string stdOutput = process.StandardOutput.ReadToEnd();
        string stdError = process.StandardError.ReadToEnd();

        if (process.ExitCode != 0)
        {
            List<GnuLinkerException> linkerExceptions = new();

            string[] lines = stdError.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Replace("ld" + ": ", null).Trim();
                if (string.IsNullOrEmpty(line)) continue;
                linkerExceptions.Add(new GnuLinkerException(line, null));
            }

            if (linkerExceptions.Count > 0)
            { throw linkerExceptions[0]; }

            throw new ProcessException("ld", process.ExitCode, stdOutput, stdError);
        }
    }
}
