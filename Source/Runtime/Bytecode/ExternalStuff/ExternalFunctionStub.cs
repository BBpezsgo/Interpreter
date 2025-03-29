namespace LanguageCore.Runtime;

public class ExternalFunctionStub : IExternalFunction
{
    public int Id { get; }
    public string Name { get; }
    public int ParametersSize { get; }
    public int ReturnValueSize { get; }

    public ExternalFunctionStub(int id, string name, int parametersSize, int returnValueSize)
    {
        Id = id;
        Name = name;
        ParametersSize = parametersSize;
        ReturnValueSize = returnValueSize;
    }

    public override string ToString() => $"<{ReturnValueSize}b> {Name ?? Id.ToString()}(<{ParametersSize}b>)";
}
