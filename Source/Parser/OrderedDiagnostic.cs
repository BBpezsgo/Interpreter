using System.Linq;

namespace LanguageCore.Parser;

class OrderedDiagnosticCollection : IEnumerable<OrderedDiagnostic>
{
    readonly List<OrderedDiagnostic> _diagnostics;

    public OrderedDiagnosticCollection()
    {
        _diagnostics = new();
    }

    public void Add(int importance, Diagnostic diagnostic, IEnumerable<OrderedDiagnostic> subdiagnostic)
    {
        Add(new OrderedDiagnostic(importance, diagnostic, subdiagnostic.ToImmutableArray()));
    }

    public void Add(int importance, Diagnostic diagnostic, params OrderedDiagnostic[] subdiagnostic)
    {
        Add(new OrderedDiagnostic(importance, diagnostic, subdiagnostic));
    }

    public void Add(OrderedDiagnostic diagnostic)
    {
        int index = _diagnostics.BinarySearch(diagnostic);
        _diagnostics.Insert(index < 0 ? ~index : index, diagnostic);
    }

    static Diagnostic Compile(OrderedDiagnostic diagnostic) => new(
        diagnostic.Diagnostic.Level,
        diagnostic.Diagnostic.Message,
        diagnostic.Diagnostic.Position,
        diagnostic.Diagnostic.File,
        false,
        diagnostic.SubDiagnostics.ToImmutableArray(Compile)
    );

    public ImmutableArray<Diagnostic> Compile()
    {
        if (_diagnostics.Count == 0) return ImmutableArray<Diagnostic>.Empty;
        ImmutableArray<Diagnostic>.Builder result = ImmutableArray.CreateBuilder<Diagnostic>(_diagnostics.Count);
        for (int i = 0; i < _diagnostics.Count; i++)
        {
            result.Add(Compile(_diagnostics[i]));
        }
        return result.MoveToImmutable();
    }

    public IEnumerator<OrderedDiagnostic> GetEnumerator() => _diagnostics.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _diagnostics.GetEnumerator();
}

readonly struct OrderedDiagnostic : IComparable<OrderedDiagnostic>
{
    public int Importance { get; }
    public Diagnostic Diagnostic { get; }
    public ImmutableArray<OrderedDiagnostic> SubDiagnostics { get; }

    public OrderedDiagnostic(int importance, Diagnostic diagnostic)
    {
        Importance = importance;
        Diagnostic = diagnostic;
        SubDiagnostics = ImmutableArray<OrderedDiagnostic>.Empty;
    }

    public OrderedDiagnostic(int importance, Diagnostic diagnostic, ImmutableArray<OrderedDiagnostic> subdiagnostics)
    {
        Importance = importance;
        Diagnostic = diagnostic;
        SubDiagnostics = subdiagnostics;
    }

    public OrderedDiagnostic(int importance, Diagnostic diagnostic, params OrderedDiagnostic[] subdiagnostic)
        : this(importance, diagnostic, subdiagnostic.ToImmutableArray()) { }

    public int CompareTo(OrderedDiagnostic other) => Importance.CompareTo(other.Importance);
}
