namespace ProgrammingLanguage.Bytecode
{
    static class Extensions
    {
        internal static string GetTypeText(this DataItem val) => val.Type.ToString();
    }
}
