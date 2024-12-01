namespace LanguageCore.Runtime;

public interface IExternalFunction
{
    public string? Name { get; }
    public int Id { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }
}
