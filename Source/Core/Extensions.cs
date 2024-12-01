namespace LanguageCore.Runtime;

public static class ExternalFunctionExtensions
{
    public static string ToReadable(this IExternalFunction externalFunction) => $"<{externalFunction.ReturnValueSize}bytes> {externalFunction.Name ?? externalFunction.Id.ToString()}(<{externalFunction.ParametersSize}bytes>)";
}
