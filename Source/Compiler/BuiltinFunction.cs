using LanguageCore.Compiler;

namespace LanguageCore;

public class BuiltinFunction
{
    public Predicate<GeneralType> Type { get; }
    public ImmutableArray<Predicate<GeneralType>> Parameters { get; }

    public bool ReturnSomething => !Type.Equals(BasicType.Void);

#if NET_STANDARD
    public BuiltinFunction(Predicate<GeneralType> type, params Predicate<GeneralType>[] parameters)
#else
    public BuiltinFunction(Predicate<GeneralType> type, params ImmutableArray<Predicate<GeneralType>> parameters)
#endif
    {
        Type = type;
#if NET_STANDARD
        Parameters = parameters.ToImmutableArray();
#else
        Parameters = parameters;
#endif
    }

    public BuiltinFunction(Predicate<GeneralType> type, IEnumerable<Predicate<GeneralType>> parameters)
    {
        Type = type;
        Parameters = parameters.ToImmutableArray();
    }
}
