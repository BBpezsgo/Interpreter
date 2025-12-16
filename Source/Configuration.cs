using LanguageCore;
using LanguageCore.Runtime;

namespace LanguageCore.Workspaces;

public sealed class Configuration
{
    public const string FileName = "bbl.conf";

    public required IReadOnlyList<string> ExtraDirectories { get; init; }
    public required IReadOnlyList<string> AdditionalImports { get; init; }
    public required IReadOnlyList<ExternalFunctionStub> ExternalFunctions { get; init; }
    public required IReadOnlyList<ExternalConstant> ExternalConstants { get; init; }

    delegate void DeclarationParser(ReadOnlySpan<char> key, ReadOnlySpan<char> value, LanguageCore.Location location);

    static void Parse(IReadOnlyCollection<(Uri Uri, string Content)> configurations, DeclarationParser parser, DiagnosticsCollection diagnostics)
    {
        foreach ((Uri uri, string configuration) in configurations)
        {
            string[] values = configuration.Split('\n');
            for (int line = 0; line < values.Length; line++)
            {
                ReadOnlySpan<char> decl = values[line];
                int i = decl.IndexOf('#');
                if (i != -1) decl = decl[..i];
                decl = decl.Trim();
                if (decl.IsEmpty) continue;

                LanguageCore.Location location = new(new LanguageCore.Position((new SinglePosition(line, 0), new SinglePosition(line, decl.Length - 1)), (-1, -1)), uri);

                i = decl.IndexOf('=');
                if (i == -1)
                {
                    diagnostics.Add(Diagnostic.Error($"Invalid configuration", location));
                    continue;
                }

                ReadOnlySpan<char> key = decl[..i].Trim();
                ReadOnlySpan<char> value = decl[(i + 1)..].Trim();

                parser.Invoke(key, value, location);
            }
        }
    }

    public static Configuration Parse(IReadOnlyCollection<(Uri Uri, string Content)> configurations, DiagnosticsCollection diagnostics)
    {
        List<string> extraDirectories = new();
        List<string> additionalImports = new();
        List<ExternalFunctionStub> externalFunctions = new()
        {
            new ExternalFunctionStub(-1, "stdin", 0, 2),
            new ExternalFunctionStub(-2, "stdout", 2, 0),
        };
        List<ExternalConstant> externalConstants = new();

        Parse(configurations, (key, value, location) =>
        {
            if (key.Equals("searchin", StringComparison.InvariantCultureIgnoreCase))
            {
                extraDirectories.Add(value.ToString());
            }
            else if (key.Equals("include", StringComparison.InvariantCultureIgnoreCase))
            {
                additionalImports.Add(value.ToString());
            }
            else if (key.Equals("externalfunc", StringComparison.InvariantCultureIgnoreCase))
            {
                string? name = null;
                int returnValueSize = 0;
                int parametersSize = 0;
                int i = -1;
                foreach (System.Range argR in value.Split(' '))
                {
                    i++;
                    ReadOnlySpan<char> arg = value[argR].Trim();
                    if (arg.IsEmpty) continue;
                    if (i == 0)
                    {
                        name = arg.ToString();
                    }
                    else if (i == 1)
                    {
                        if (int.TryParse(arg, out int v))
                        {
                            returnValueSize = v;
                        }
                        else
                        {
                            diagnostics.Add(Diagnostic.Error($"Invalid integer `{arg}`", location));
                        }
                    }
                    else
                    {
                        if (int.TryParse(arg, out int v))
                        {
                            parametersSize += v;
                        }
                        else
                        {
                            diagnostics.Add(Diagnostic.Error($"Invalid integer `{arg}`", location));
                        }
                    }
                }
                if (name is not null)
                {
                    if (!externalFunctions.Any(v => v.Name == name))
                    {
                        externalFunctions.Add(new ExternalFunctionStub(
                            externalFunctions.GenerateId(name),
                            name,
                            parametersSize,
                            returnValueSize
                        ));
                    }
                    else
                    {
                        diagnostics.Add(Diagnostic.Error($"[Configuration]: External function {name} already exists", location));
                    }
                }
                else
                {
                    diagnostics.Add(Diagnostic.Error($"[Configuration]: Invalid config", location));
                }
            }
            else
            {
                diagnostics.Add(Diagnostic.Error($"Invalid configuration key `{key}`", location));
            }
        }, diagnostics);

        return new Configuration()
        {
            AdditionalImports = additionalImports,
            ExtraDirectories = extraDirectories,
            ExternalFunctions = externalFunctions,
            ExternalConstants = externalConstants,
        };
    }
}

