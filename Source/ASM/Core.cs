namespace LanguageCore.ASM;

[ExcludeFromCodeCoverage]
public class ProcessException : Exception
{
    readonly string processName;
    readonly int exitCode;

    public override string Message => $"Process \"{processName}\" exited with code {exitCode}";
    public string StandardOutput { get; }
    public string StandardError { get; }

    public ProcessException(string processName, int exitCode, string stdOutput, string stdError) : base()
    {
        this.processName = processName;
        this.exitCode = exitCode;
        this.StandardOutput = stdOutput;
        this.StandardError = stdError;
    }
}

[ExcludeFromCodeCoverage]
public class ProcessNotStartedException : Exception
{
    readonly string processName;

    public override string Message => $"Failed to start process \"{processName}\"";

    public ProcessNotStartedException(string processName) : base()
    {
        this.processName = processName;
    }
}
