
namespace LanguageCore;

public enum DiagnosticsLevel
{
    Error,
    Warning,
    Information,
    Hint,
    OptimizationNotice,
    FailedOptimization,
}

public interface IDiagnostic
{
    DiagnosticsLevel Level { get; }
    string Message { get; }
    IEnumerable<IDiagnostic> SubErrors { get; }
}
