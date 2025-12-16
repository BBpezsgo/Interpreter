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
    readonly Stack<Override> _overrides;

    public IReadOnlyCollection<Diagnostic> Diagnostics => _diagnostics;
    public IReadOnlyCollection<DiagnosticWithoutContext> DiagnosticsWithoutContext => _diagnosticsWithoutContext;

    public readonly struct Override : IDisposable
    {
        public DiagnosticsCollection Collection { get; }
        public DiagnosticsCollection Base { get; }

        public Override(DiagnosticsCollection @base, DiagnosticsCollection? collection)
        {
            Collection = collection ?? new();
            Base = @base;
        }

        public void Apply()
        {
            Base.AddRange(Collection);
            Collection.Clear();
        }

        public void Dispose()
        {
            Override r = Base._overrides.Pop();
            if (r.Collection != Collection) throw new UnreachableException();
        }
    }

    public Override MakeOverride(DiagnosticsCollection? diagnostics = null)
    {
        Override r = new(this, diagnostics);
        _overrides.Push(r);
        return r;
    }

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
        _overrides = new();
    }

    public DiagnosticsCollection(DiagnosticsCollection other)
    {
        _diagnostics = new(other._diagnostics);
        _diagnosticsWithoutContext = new(other._diagnosticsWithoutContext);
        _overrides = new();
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

    public bool Update(Diagnostic old, Diagnostic diagnostic)
    {
        if (diagnostic is null) return false;

        for (int i = 0; i < _diagnostics.Count; i++)
        {
            if (_diagnostics[i] == old)
            {
                _diagnostics[i] = diagnostic;
                return true;
            }
        }

        return false;
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

    public static void WriteErrorsTo(this IReadOnlyDiagnosticsCollection diagnosticsCollection, StringBuilder writer)
    {
        foreach (DiagnosticWithoutContext diagnostic in diagnosticsCollection.DiagnosticsWithoutContext)
        {
            if (diagnostic.Level == DiagnosticsLevel.Error)
            {
                writer.AppendLine(diagnostic.Message);
            }
        }

        foreach (Diagnostic diagnostic in diagnosticsCollection.Diagnostics)
        {
            if (diagnostic.Level == DiagnosticsLevel.Error)
            {
                writer.AppendLine(diagnostic.ToString());
            }
        }
    }
}
