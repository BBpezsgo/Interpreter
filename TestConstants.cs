namespace TheProgram
{
    internal static class TestConstants
    {
        internal static string TestFilesPath => ProjectPath + "\\TestFiles\\";
        internal static string ProjectPath => new System.IO.FileInfo(System.Reflection.Assembly.GetEntryAssembly().Location).Directory.Parent.Parent.Parent.FullName;
    }
}
