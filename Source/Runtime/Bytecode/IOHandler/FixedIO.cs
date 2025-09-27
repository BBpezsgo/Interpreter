using System.IO;

namespace LanguageCore.Runtime;

public sealed class FixedIO : IO
{
    readonly string Input;
    int InputPosition;
    public readonly StringBuilder Output;

    public FixedIO(string input, StringBuilder? output = null)
    {
        Input = input;
        InputPosition = 0;
        Output = output ?? new StringBuilder();
    }

    public override void Register(List<IExternalFunction> externalFunctions)
    {
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdIn), ExternalFunctionNames.StdIn, () =>
        {
            if (InputPosition >= Input.Length)
            {
                throw new EndOfStreamException();
            }
            return Input[InputPosition++];
        }));
        externalFunctions.AddExternalFunction(ExternalFunctionSync.Create(externalFunctions.GenerateId(ExternalFunctionNames.StdOut), ExternalFunctionNames.StdOut, (char v) =>
        {
            Output.Append(v);
        }));
    }

    public void Reset()
    {
        InputPosition = 0;
        Output.Clear();
    }
}
