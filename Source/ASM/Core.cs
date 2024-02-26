using System;

namespace LanguageCore.ASM;

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
}

public class ProcessNotStartedException : Exception
{
    readonly string processName;

    public override string Message => $"Failed to start process \"{processName}\"";

    public ProcessNotStartedException(string processName) : base()
    {
        this.processName = processName;
    }
}
