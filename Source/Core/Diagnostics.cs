namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class DiagnosticsCollection
{
    readonly List<Diagnostic> _diagnostics;
    readonly List<DiagnosticWithoutContext> _diagnosticsWithoutContext;

    public IReadOnlyCollection<Diagnostic> Diagnostics => _diagnostics;
    public IReadOnlyCollection<DiagnosticWithoutContext> DiagnosticsWithoutContext => _diagnosticsWithoutContext;

    public bool HasErrors =>
        _diagnostics.Any(v => v.Level == DiagnosticsLevel.Error) ||
        _diagnosticsWithoutContext.Any(v => v.Level == DiagnosticsLevel.Error);

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

    public void Print()
    {
        foreach (DiagnosticWithoutContext diagnostic in _diagnosticsWithoutContext)
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

        foreach (Diagnostic diagnostic in _diagnostics)
        {
            switch (diagnostic.Level)
            {
                case DiagnosticsLevel.Error:
                    Output.LogError(diagnostic);
                    break;
                case DiagnosticsLevel.Warning:
                    Output.LogWarning(diagnostic);
                    break;
                case DiagnosticsLevel.Information:
                    Output.LogInfo(diagnostic);
                    break;
                case DiagnosticsLevel.Hint:
                    Output.LogInfo(diagnostic);
                    break;
            }
        }
    }

    public void Clear()
    {
        _diagnostics.Clear();
        _diagnosticsWithoutContext.Clear();
    }

    public void AddRange(DiagnosticsCollection other)
    {
        _diagnostics.AddRange(other._diagnostics);
        _diagnosticsWithoutContext.AddRange(other._diagnosticsWithoutContext);
    }

    public void Add(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);
    public void AddRange(IEnumerable<Diagnostic> diagnostic) => _diagnostics.AddRange(diagnostic);

    public void Add(DiagnosticWithoutContext diagnostic) => _diagnosticsWithoutContext.Add(diagnostic);
    public void AddRange(IEnumerable<DiagnosticWithoutContext> diagnostic) => _diagnosticsWithoutContext.AddRange(diagnostic);
}
