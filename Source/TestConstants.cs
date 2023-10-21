namespace TheProgram
{
    public static class TestConstants
    {
        public static string TestFilesPath => AssemblyPath + @"\TestFiles\";
        public static string ExampleFilesPath => AssemblyPath + @"\Examples\";
        public static string AssemblyPath => new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly()!.Location).Directory!.Parent!.Parent!.Parent!.FullName;
        public const string TheProjectPath = @"D:\Program Files\BBCodeProject\BBCode";
    }
}
