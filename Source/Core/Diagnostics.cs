namespace LanguageCore;

public interface IReadOnlyDiagnosticsCollection
{
    public IReadOnlyCollection<Diagnostic> Diagnostics { get; }
    public IReadOnlyCollection<DiagnosticWithoutContext> DiagnosticsWithoutContext { get; }
    public bool HasErrors { get; }

    public void Throw();
    public void Print();
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
        { Output.LogDiagnostic(diagnostic); }
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

    public void Add(Diagnostic diagnostic)
    {
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
