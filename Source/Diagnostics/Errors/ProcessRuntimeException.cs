namespace LanguageCore;

[ExcludeFromCodeCoverage]
public sealed class ProcessRuntimeException : Exception
{
    public uint ExitCode { get; }

    ProcessRuntimeException(uint exitCode, string message) : base(message)
    {
        ExitCode = exitCode;
    }

    public static bool TryGetFromExitCode(int exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
        => ProcessRuntimeException.TryGetFromExitCode(unchecked((uint)exitCode), out processRuntimeException);

    public static bool TryGetFromExitCode(uint exitCode, [NotNullWhen(true)] out ProcessRuntimeException? processRuntimeException)
    {
        processRuntimeException = exitCode switch
        {
            0xC0000094 => new ProcessRuntimeException(exitCode, "Integer division by zero"),
            _ => null,
        };
        return processRuntimeException != null;
    }
}
