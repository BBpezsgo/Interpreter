
namespace LanguageCore;

public enum DiagnosticsLevel
{
    Error,
    Warning,
    Information,
    Hint,
    OptimizationNotice,
}

public interface IDiagnostic
{
    DiagnosticsLevel Level { get; }
    string Message { get; }
}
