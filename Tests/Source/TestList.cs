using System.Diagnostics.CodeAnalysis;

namespace LanguageCore.Tests;

[TestClass]
public class SetupAssemblyInitializer
{
    [AssemblyInitialize]
    public static void AssemblyInit(TestContext _)
    {
        TestList.ReadCases();
    }
}

static class TestList
{
    static readonly string TestFilesPath = $"{LanguageCore.Program.ProjectPath}/TestFiles/";

    const int TestCount = 92;
    static readonly int TestFileNameWidth = (int)Math.Floor(Math.Log10(TestCount)) + 1;

    public static void GenerateTestFiles()
    {
        Directory.CreateDirectory(TestFilesPath);

        for (int i = 1; i <= TestCount; i++)
        {
            string sourceFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.{LanguageConstants.LanguageExtension}";
            string resultFile = $"{TestFilesPath}{i.ToString().PadLeft(TestFileNameWidth, '0')}.result";

            if (!File.Exists(sourceFile))
            { File.CreateText(sourceFile); }

            if (!File.Exists(resultFile))
            { File.CreateText(resultFile); }
        }
    }

    [NotNull] static TestFileCase?[]? Cases = null;

    public static void ReadCases()
    {
        int maxTestIndex = 0;
        List<(int Index, string SourceFile, string ResultFile, string? InputFile)> cases = new();
        foreach (string sourceFile in Directory.GetFiles(TestFilesPath, $"*.{LanguageConstants.LanguageExtension}"))
        {
            string name = Path.GetFileNameWithoutExtension(sourceFile);
            if (!int.TryParse(name, out int i)) continue;
            maxTestIndex = Math.Max(maxTestIndex, i);

            string resultFile = $"{TestFilesPath}{name}.result";
            if (!File.Exists(resultFile)) continue;

            string? inputFile = $"{TestFilesPath}{name}.txt";
            if (!File.Exists(inputFile)) inputFile = null;

            cases.Add((i, sourceFile, resultFile, inputFile));
        }
        Cases = new TestFileCase?[maxTestIndex + 1];
        foreach ((int i, string sourceFile, string resultFile, string? inputFile) in cases)
        {
            Cases[i] = new TestFileCase(sourceFile, resultFile, inputFile);
        }
    }

    public static TestFileCase GetTest(int i)
    {
        TestFileCase? f = Cases[i];
        if (f is null) Assert.Inconclusive();
        return f;
    }
}
