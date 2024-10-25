
namespace LanguageCore;

[ExcludeFromCodeCoverage]
public class AnalysisCollection
{
    public readonly List<LanguageError> Errors;
    public readonly List<Warning> Warnings;
    public readonly List<Information> Informations;
    public readonly List<Hint> Hints;

    public AnalysisCollection()
    {
        Errors = new List<LanguageError>();
        Warnings = new List<Warning>();
        Informations = new List<Information>();
        Hints = new List<Hint>();
    }

    public void Throw()
    {
        if (Errors.Count == 0) return;
        throw Errors[0];
    }

    public void Print()
    {
        for (int i = 0; i < Errors.Count; i++)
        { Output.LogError(Errors[i]); }

        for (int i = 0; i < Warnings.Count; i++)
        { Output.LogWarning(Warnings[i]); }

        for (int i = 0; i < Informations.Count; i++)
        { Output.LogInfo(Informations[i]); }

        for (int i = 0; i < Hints.Count; i++)
        { Output.LogInfo(Hints[i]); }
    }

    public void Clear()
    {
        Errors.Clear();
        Warnings.Clear();
        Informations.Clear();
        Hints.Clear();
    }
}
