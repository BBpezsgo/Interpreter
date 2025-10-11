namespace LanguageCore.Compiler;

public readonly struct CompliableTemplate<T> where T : ITemplateable<T>
{
    public readonly T OriginalFunction;
    public readonly T Function;
    public readonly ImmutableDictionary<string, GeneralType> TypeArguments;

    public CompliableTemplate(T function, ImmutableDictionary<string, GeneralType> typeArguments)
    {
        OriginalFunction = function;
        TypeArguments = typeArguments;

        foreach (GeneralType argument in TypeArguments.Values)
        {
            if (argument.Is<GenericType>())
            { throw new InternalExceptionWithoutContext($"{argument} is generic"); }
        }

        Function = OriginalFunction.InstantiateTemplate(typeArguments);
    }

    public override string ToString() => Function?.ToString() ?? "null";
}
