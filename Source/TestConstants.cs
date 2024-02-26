using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;

namespace LanguageCore;

public static class TestConstants
{
    [RequiresAssemblyFiles] public static string TestFilesPath => AssemblyPath + @"\TestFiles\";
    [RequiresAssemblyFiles] public static string ExampleFilesPath => AssemblyPath + @"\Examples\";
    [RequiresAssemblyFiles] public static string AssemblyPath => new FileInfo(Assembly.GetEntryAssembly()!.Location).Directory!.Parent!.Parent!.Parent!.FullName;
    public const string TheProjectPath = @"D:\Program Files\BBCodeProject\BBCode";
}
