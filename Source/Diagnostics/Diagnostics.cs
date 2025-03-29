namespace LanguageCore;

public interface IReadOnlyDiagnosticsCollection
{
    IReadOnlyCollection<Diagnostic> Diagnostics { get; }
    IReadOnlyCollection<DiagnosticWithoutContext> DiagnosticsWithoutContext { get; }
    bool HasErrors { get; }

    void Throw();
}

[ExcludeFromCodeCoverage]
public class DiagnosticsCollection : IReadOnlyDiagnosticsCollection
{
    readonly List<Diagnostic> _diagnostics;
    readonly List<DiagnosticWithoutContext> _diagnosticsWithoutContext;

    public IReadOnlyCollection<Diagnostic> Diagnostics => _diagnostics;
    public IReadOnlyCollection<DiagnosticWithoutContext> DiagnosticsWithoutContext => _diagnosticsWithoutContext;

    public bool HasErrors =>
        _diagnostics.Any(v => v.Level == DiagnosticsLevel.Error) ||
        _diagnosticsWithoutContext.Any(v => v.Level == DiagnosticsLevel.Error);

    public bool Has(DiagnosticsLevel level) =>
        _diagnostics.Any(v => v.Level == level) ||
        _diagnosticsWithoutContext.Any(v => v.Level == level);

    public DiagnosticsCollection()
    {
        _diagnostics = new();
        _diagnosticsWithoutContext = new();
    }

    public DiagnosticsCollection(DiagnosticsCollection other)
    {
        _diagnostics = new(other._diagnostics);
        _diagnosticsWithoutContext = new(other._diagnosticsWithoutContext);
    }

    public void Throw()
    {
        for (int i = 0; i < _diagnostics.Count; i++)
        {
            if (_diagnostics[i].Level != DiagnosticsLevel.Error) continue;
            _diagnostics[i].Throw();
        }

        for (int i = 0; i < _diagnosticsWithoutContext.Count; i++)
        {
            if (_diagnosticsWithoutContext[i].Level != DiagnosticsLevel.Error) continue;
            _diagnosticsWithoutContext[i].Throw();
        }
    }

    public void Clear()
    {
        _diagnostics.Clear();
        _diagnosticsWithoutContext.Clear();
    }

    public void AddRange(DiagnosticsCollection other)
    {
        AddRange(other._diagnostics);
        AddRange(other._diagnosticsWithoutContext);
    }

    public void Add(Diagnostic? diagnostic)
    {
        if (diagnostic is null) return;

        if (_diagnostics.Any(v => v.Equals(diagnostic)))
        { return; }
        _diagnostics.Add(diagnostic);
    }

    public void AddRange(IEnumerable<Diagnostic> diagnostic)
    { foreach (Diagnostic item in diagnostic) Add(item); }

    public void Add(DiagnosticWithoutContext diagnostic)
    {
        if (_diagnosticsWithoutContext.Any(v => v.Equals(diagnostic)))
        { return; }
        _diagnosticsWithoutContext.Add(diagnostic);
    }

    public void AddRange(IEnumerable<DiagnosticWithoutContext> diagnostic)
    { foreach (DiagnosticWithoutContext item in diagnostic) Add(item); }
}

public static class DiagnosticsCollectionExtensions
{
    public static void Print(this IReadOnlyDiagnosticsCollection diagnosticsCollection, IEnumerable<ISourceProvider>? sourceProviders = null)
    {
        foreach (DiagnosticWithoutContext diagnostic in diagnosticsCollection.DiagnosticsWithoutContext)
        {
            switch (diagnostic.Level)
            {
                case DiagnosticsLevel.Error:
                    Output.LogError(diagnostic.Message);
                    break;
                case DiagnosticsLevel.Warning:
                    Output.LogWarning(diagnostic.Message);
                    break;
                case DiagnosticsLevel.Information:
                    Output.LogInfo(diagnostic.Message);
                    break;
                case DiagnosticsLevel.Hint:
                    Output.LogInfo(diagnostic.Message);
                    break;
            }
        }

        foreach (Diagnostic diagnostic in diagnosticsCollection.Diagnostics)
        { Output.LogDiagnostic(diagnostic, sourceProviders); }
    }
}
