using System.IO;
using System.Runtime.CompilerServices;
using CommandLine;
using CommandLine.Text;

namespace LanguageCore;

public static class Program
{
    const string _thisFileName = nameof(Program) + ".cs";
    static string? _projectPath;
    public static string ProjectPath => _projectPath ??= GetProjectPath();

    static string GetProjectPath([CallerFilePath] string? callerFilePath = null)
    {
        if (callerFilePath is null || !callerFilePath.EndsWith(_thisFileName, StringComparison.Ordinal)) throw new Exception($"Failed to get the project path");
        return Path.GetDirectoryName(callerFilePath[..^(_thisFileName.Length + 1)])!;
    }

    public static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<Error> errs)
    {
        HelpText? helpText = null;
        if (errs.IsVersion())
        {
            helpText = HelpText.AutoBuild(result);
        }
        else
        {
            helpText = HelpText.AutoBuild(result, h =>
            {
                h.AdditionalNewLineAfterOption = false;
                h.Heading = "BBLang";
                h.Copyright = string.Empty;
                return HelpText.DefaultParsingErrorsHandler(result, h);
            }, e => e);
        }
        Console.WriteLine(helpText);
    }

    public static int Main(string[] args)
    {
#if DEBUG
        return DevelopmentEntry.Start(args);
#else
        return Entry.Run(args);
#endif
    }
}
