using System.Diagnostics.CodeAnalysis;

namespace TheProgram
{
    public static class TestConstants
    {
        [RequiresAssemblyFiles]
        public static string TestFilesPath => AssemblyPath + @"\TestFiles\";
        [RequiresAssemblyFiles]
        public static string ExampleFilesPath => AssemblyPath + @"\Examples\";
        [RequiresAssemblyFiles]
        public static string AssemblyPath => new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly()!.Location).Directory!.Parent!.Parent!.Parent!.FullName;
        public const string TheProjectPath = @"D:\Program Files\BBCodeProject\BBCode";
    }
}
