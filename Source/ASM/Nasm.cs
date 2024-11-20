using System.IO;

namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
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

        result.Append($" (at line {LineNumber})");

        if (File != null)
        { result.Append($" (in {File})"); }

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

        string[] lines = text.Split('\n');

        if (LineNumber - 1 > lines.Length)
        { return null; }

        string line = lines[LineNumber - 1];

        StringBuilder result = new();

        result.Append(line.Replace('\t', ' '));
        result.AppendLine();

        int leadingWhitespace = 0;
        for (int i = 0; i < line.Length; i++)
        {
            if (char.IsWhiteSpace(line[i]))
            { leadingWhitespace++; }
            else
            { break; }
        }

        int trailingWhitespace = 0;
        for (int i = line.Length - 1; i >= 0; i--)
        {
            if (char.IsWhiteSpace(line[i]))
            { trailingWhitespace++; }
            else
            { break; }
        }

        result.Append(' ', leadingWhitespace);
        result.Append('^', Math.Max(1, line.Length - leadingWhitespace - trailingWhitespace));
        return result.ToString();
    }
}

[ExcludeFromCodeCoverage]
public static class Nasm
{
    public static void Assemble(string inputFile, string outputFile)
    {
        if (!File.Exists(inputFile))
        { throw new FileNotFoundException($"Input file not found", inputFile); }

        if (File.Exists(outputFile))
        { File.Delete(outputFile); }

        // -gcv8 -f win32

        using Process? process = Process.Start(new ProcessStartInfo("nasm", $"-f elf64 -g -F dwarf {inputFile} -o {outputFile}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new ProcessNotStartedException("nasm");
        process.WaitForExit();

        if (process.ExitCode == 0)
        { return; }

        string stdOutput = process.StandardOutput.ReadToEnd();
        string stdError = process.StandardError.ReadToEnd();

        string[] errorLines = stdError.ReplaceLineEndings("\n").Split('\n');

        for (int i = 0; i < errorLines.Length; i++)
        {
            string errorLine = errorLines[i].Trim();
            if (string.IsNullOrWhiteSpace(errorLine)) continue;
            NasmException? nasmException = NasmException.Parse(errorLine);
            if (nasmException != null) throw nasmException;
            else throw new NotImplementedException();
        }

        throw new ProcessException("nasm", process.ExitCode, stdOutput, stdError);
    }

    public static void AssembleRaw(string inputFile, string outputFile)
    {
        if (!File.Exists(inputFile))
        { throw new FileNotFoundException($"Input file not found", inputFile); }

        if (File.Exists(outputFile))
        { File.Delete(outputFile); }

        using Process? process = Process.Start(new ProcessStartInfo("nasm", $"{inputFile} -f bin -o {outputFile}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        }) ?? throw new ProcessNotStartedException("nasm");
        process.WaitForExit();

        if (process.ExitCode == 0)
        { return; }

        string stdOutput = process.StandardOutput.ReadToEnd();
        string stdError = process.StandardError.ReadToEnd();

        string[] errorLines = stdError.ReplaceLineEndings("\n").Split('\n');

        for (int i = 0; i < errorLines.Length; i++)
        {
            string errorLine = errorLines[i].Trim();
            if (string.IsNullOrWhiteSpace(errorLine)) continue;
            NasmException? nasmException = NasmException.Parse(errorLine);
            if (nasmException != null) throw nasmException;
            else throw new NotImplementedException();
        }

        throw new ProcessException("nasm", process.ExitCode, stdOutput, stdError);
    }
}
