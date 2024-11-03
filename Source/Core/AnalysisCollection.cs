
namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class Diagnostics : List<Diagnostic>
{
    public void Throw()
    {
        for (int i = 0; i < Count; i++)
        {
            if (this[i].Level != DiagnosticsLevel.Error) continue;
            this[i].Throw();
        }
    }

    public void Print()
    {
        foreach (Diagnostic diagnostic in this)
        {
            switch (diagnostic.Level)
            {
                case DiagnosticsLevel.Error:
                    Output.LogError(diagnostic);
                    break;
                case DiagnosticsLevel.Warning:
                    Output.LogWarning(diagnostic);
                    break;
                case DiagnosticsLevel.Information:
                    Output.LogInfo(diagnostic);
                    break;
                case DiagnosticsLevel.Hint:
                    Output.LogInfo(diagnostic);
                    break;
            }
        }
    }
}
