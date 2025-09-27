
namespace LanguageCore.Runtime;

public sealed class StandardIO : IO
{
    public static readonly StandardIO Instance = new();

    private StandardIO()
    {

    }

    public override void Register(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, Console.ReadKey));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, (Action<char>)Console.Write));
    }
}
