using LanguageCore.Compiler;

namespace LanguageCore;

public class BuiltinFunction
{
    public Predicate<GeneralType> Type { get; }
    public ImmutableArray<Predicate<GeneralType>> Parameters { get; }

    public bool ReturnSomething => !Type.Equals(BasicType.Void);

    public BuiltinFunction(Predicate<GeneralType> type, params ImmutableArray<Predicate<GeneralType>> parameters)
    {
        Type = type;
        Parameters = parameters;
    }

    public BuiltinFunction(Predicate<GeneralType> type, IEnumerable<Predicate<GeneralType>> parameters)
    {
        Type = type;
        Parameters = parameters.ToImmutableArray();
    }
}
