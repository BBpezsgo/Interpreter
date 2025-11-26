
namespace LanguageCore.Runtime;

public sealed class StandardIO : IO
{
    public static readonly StandardIO Instance = new();

    public override void Register(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, static () => (char)Console.Read()));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, (Action<char>)Console.Write));
    }
}
