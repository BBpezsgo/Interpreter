
namespace LanguageCore.Runtime;

public sealed class VoidIO : IO
{
#pragma warning disable CS0618
    public static readonly VoidIO Instance = new();
#pragma warning restore CS0618

    [Obsolete($"Use {nameof(Instance)} instead")]
    public VoidIO()
    {

    }

    public override void Register(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, static () => throw new InvalidOperationException("Trying to read from void")));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, static () => { }));
    }
}
