using System.IO;
using Win32.Console;

namespace LanguageCore;

public readonly struct Option
{
    public required string Name { get; init; }
    public required ImmutableArray<string> Arguments { get; init; }
}

public class OptionSpecification
{
    public required string LongName { get; init; }
    public char ShortName { get; init; }
    public (string Name, string Description)[] Arguments { get; init; }
    public required string Help { get; init; }

    public OptionSpecification()
    {
        Arguments = Array.Empty<(string Name, string Description)>();
    }
}

public static class CommandLineParser
{
    class OptionBuilder
    {
        public string Name { get; init; }
        public bool IsShort { get; init; }
        public List<string> Arguments { get; init; }

        public OptionBuilder(string name, bool isShort)
        {
            Name = name;
            IsShort = isShort;
            Arguments = new List<string>();
        }
    }

    public static (List<Option> Options, List<string> Arguments) Parse(string[] args, OptionSpecification[] optionSpecifications)
    {
        OptionBuilder? current = null;

        List<Option> resultOptions = new();
        List<string> resultArguments = new();

        void Finish(ref OptionBuilder? current)
        {
            if (current is null) return;

            OptionSpecification? found = null;
            foreach (OptionSpecification spec in optionSpecifications)
            {
                if (current.IsShort)
                { if (current.Name[0] != spec.ShortName) continue; }
                else
                { if (current.Name != spec.LongName) continue; }

                if (found is not null)
                { throw new ArgumentException($"Unknown option \"{(current.IsShort ? $"-{current.Name}" : $"--{current.Name}")}\""); }

                found = spec;

                if (current.Arguments.Count != spec.Arguments.Length)
                { throw new ArgumentException($"Wrong number of arguments passed to option \"${(current.IsShort ? $"-{current.Name}" : $"--{current.Name}")}\": required {spec.Arguments}, passed {current.Arguments.Count}"); }
            }

            if (found is null)
            { throw new ArgumentException($"Unknown option \"{(current.IsShort ? $"-{current.Name}" : $"--{current.Name}")}\""); }

            resultOptions.Add(new Option()
            {
                Name = found.LongName,
                Arguments = current.Arguments.ToImmutableArray(),
            });
            current = null;
        }

        foreach (string arg in args)
        {
            if (arg.StartsWith("--"))
            {
                if (resultArguments.Count > 0)
                { throw new ArgumentException($"Unexpected option \"${arg}\" after argument \"${resultArguments[^1]}\""); }

                Finish(ref current);
                current = new OptionBuilder(arg[2..], false);
            }
            else if (arg.StartsWith('-'))
            {
                if (resultArguments.Count > 0)
                { throw new ArgumentException($"Unexpected option \"${arg}\" after argument \"${resultArguments[^1]}\""); }

                foreach (char c in arg[1..])
                {
                    Finish(ref current);
                    current = new OptionBuilder(c.ToString(), true);
                }
            }
            else
            {
                if (current is null)
                {
                    resultArguments.Add(arg);
                }
                else
                {
                    bool added = false;
                    foreach (OptionSpecification spec in optionSpecifications)
                    {
                        if (current.IsShort)
                        { if (current.Name[0] != spec.ShortName) continue; }
                        else
                        { if (current.Name != spec.LongName) continue; }

                        if (current.Arguments.Count >= spec.Arguments.Length)
                        {
                            Finish(ref current);
                            break;
                        }
                        else
                        {
                            current.Arguments.Add(arg);
                            added = true;
                            break;
                        }
                    }

                    if (!added)
                    {
                        resultArguments.Add(arg);
                    }
                }
            }
        }

        return (resultOptions, resultArguments);
    }

    public static void PrintHelp(OptionSpecification[] optionSpecifications, (string Name, string Help)[] argumentsHelp)
    {
        const int PaddingBefore = 4;
        const int PaddingBetween = 4;

        Console.WriteLine("About:");
        Console.Write(new string(' ', PaddingBefore));
        Console.WriteLine("A basic, compiler for a custom programming language that can generate bytecodes or brainfuck code.");
        Console.WriteLine();

        Console.WriteLine("Source:");
        Console.Write(new string(' ', PaddingBefore));
        Console.WriteLine("https://github.com/BBpezsgo/Interpreter");
        Console.WriteLine();

        Console.WriteLine("Usage:");
        Console.Write(new string(' ', PaddingBefore));
        Console.Write(Path.GetFileName(Environment.ProcessPath) ?? "this");
        Console.WriteLine();
        foreach (OptionSpecification option in optionSpecifications)
        {
            Console.Write(' ');
            Console.Write('[');
            Console.Write("--");
            Console.Write(option.LongName);
            if (option.ShortName != '\0')
            {
                Console.Write('|');
                Console.Write("-");
                Console.Write(option.ShortName);
            }
            foreach ((string argName, _) in option.Arguments)
            {
                Console.Write(' ');
                Console.Write(argName);
            }
            Console.Write(']');
        }
        foreach ((string argName, _) in argumentsHelp)
        {
            Console.Write(' ');
            Console.Write('<');
            Console.Write(argName);
            Console.Write('>');
        }
        Console.WriteLine();
        Console.WriteLine();

        Console.WriteLine("Options:");

        int maxLength = optionSpecifications.Max(static v =>
        {
            int maxArgsLength = v.Arguments.Length == 0 ? 0 : v.Arguments.Max(static v => v.Name.Length + 4);
            return Math.Max(v.LongName.Length, maxArgsLength);
        });

        foreach (OptionSpecification option in optionSpecifications)
        {
            Console.Write(new string(' ', PaddingBefore));
            Console.Write("--");
            Console.Write(option.LongName);
            Console.Write(new string(' ', maxLength - option.LongName.Length + PaddingBetween));
            Console.Write(option.Help);
            Console.WriteLine();
            foreach ((string argName, string argDescription) in option.Arguments)
            {
                Console.Write(new string(' ', PaddingBefore + 4));
                Console.Write(argName);
                Console.Write(new string(' ', maxLength - argName.Length + PaddingBetween - 2));
                Console.Write(argDescription);
                Console.WriteLine();
            }
            Console.WriteLine();
        }
        Console.WriteLine();

        Console.WriteLine("Arguments:");
        foreach ((string argName, string argHelp) in argumentsHelp)
        {
            Console.Write(new string(' ', PaddingBefore));
            Console.Write(argName);
            Console.Write(new string(' ', maxLength - argName.Length + PaddingBetween));
            Console.Write(argHelp);
            Console.WriteLine();
        }
        Console.WriteLine();
    }
}
