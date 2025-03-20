namespace LanguageCore.Runtime;

public interface IExternalFunction
{
    string? Name { get; }
    int Id { get; }
    int ParametersSize { get; }
    int ReturnValueSize { get; }

    string ToString();
}
