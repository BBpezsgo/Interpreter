using LanguageCore;
using LanguageCore.Runtime;

namespace LanguageCore.Workspaces;

public sealed class Configuration
{
    public const string FileName = "bbl.conf";

    public required ImmutableArray<string> ExtraDirectories { get; init; }
    public required ImmutableArray<string> AdditionalImports { get; init; }
    public required ImmutableArray<ExternalFunctionStub> ExternalFunctions { get; init; }
    public required ImmutableArray<ExternalConstant> ExternalConstants { get; init; }

    public delegate void DeclarationParser(ReadOnlySpan<char> key, ReadOnlySpan<char> value, Location location);

    public class Parser
    {
        public readonly DiagnosticsCollection diagnostics;

        public readonly List<string> extraDirectories = new();
        public readonly List<string> additionalImports = new();
        public readonly List<ExternalFunctionStub> externalFunctions = new();
        public readonly List<ExternalConstant> externalConstants = new();

        public Parser(DiagnosticsCollection diagnostics)
        {
            this.diagnostics = diagnostics;
        }

        public void Parse(ReadOnlySpan<char> key, ReadOnlySpan<char> value, Location location)
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
                foreach (Range argR in value.Split(' '))
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
                            diagnostics.Add(Diagnostic.Error($"Invalid integer `{arg.ToString()}`", location));
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
                            diagnostics.Add(Diagnostic.Error($"Invalid integer `{arg.ToString()}`", location));
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
                diagnostics.Add(Diagnostic.Error($"Invalid configuration key `{key.ToString()}`", location));
            }
        }

        public Configuration Compile()
        {
            return new Configuration()
            {
                AdditionalImports = additionalImports.ToImmutableArray(),
                ExtraDirectories = extraDirectories.ToImmutableArray(),
                ExternalFunctions = externalFunctions.ToImmutableArray(),
                ExternalConstants = externalConstants.ToImmutableArray(),
            };
        }
    }

    public static void Parse(Uri uri, string content, DeclarationParser parser, DiagnosticsCollection diagnostics)
    {
        string[] values = content.Split('\n');
        for (int line = 0; line < values.Length; line++)
        {
            ReadOnlySpan<char> decl = values[line];
            int i = decl.IndexOf('#');
            if (i != -1) decl = decl[..i];
            decl = decl.Trim();
            if (decl.IsEmpty) continue;

            Location location = new(new Position((new SinglePosition(line, 0), new SinglePosition(line, decl.Length - 1)), (-1, -1)), uri);

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

    public static void Parse(IReadOnlyCollection<(Uri Uri, string Content)> configurations, DeclarationParser parser, DiagnosticsCollection diagnostics)
    {
        foreach ((Uri uri, string content) in configurations)
        {
            Parse(uri, content, parser, diagnostics);
        }
    }

    public static Configuration Parse(IReadOnlyCollection<(Uri Uri, string Content)> configurations, DiagnosticsCollection diagnostics)
    {
        Parser parser = new(diagnostics);
        Parse(configurations, parser.Parse, diagnostics);
        return parser.Compile();
    }

    public static Configuration Parse(Uri uri, string content, DiagnosticsCollection diagnostics)
    {
        Parser parser = new(diagnostics);
        Parse(uri, content, parser.Parse, diagnostics);
        return parser.Compile();
    }
}

