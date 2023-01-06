using IngameCoding.BBCode;
using IngameCoding.Core;
using IngameCoding.Errors;

using System.Collections.Generic;
using System.IO;

namespace IngameCoding.Tester
{
    using Parser;

    internal class Compiler
    {
        internal struct CompilerResult
        {
            internal CompiledTestDefinition[] Tests;
        }

        internal class CompiledTestDefinition
        {
            internal string Name;
            internal bool Disabled;
            internal string File;
        }

        internal static CompilerResult Compile(ParserResult parserResult, List<Warning> warnings, DirectoryInfo directory, string path)
        {
            List<CompiledTestDefinition> compiledTestDefinitions = new();

            List<string> disabled = new();
            List<string> names = new();

            foreach (var item in parserResult.TestDefinitions)
            {
                if (!item.TryGetAttribute("File", out var attrFile))
                { throw new CompilerException("Attribute 'File' is requied for test definition", item.Keyword, path); }
                if (attrFile.Parameters.Length != 1)
                { throw new CompilerException("Attribute 'File' requies 1 string parameter", attrFile.Name, path); }
                if (attrFile.Parameters[0].type != TokenType.LITERAL_STRING)
                { throw new CompilerException($"Attribute 'File' requies 1 string parameter, passed {attrFile.Parameters[0].type}", attrFile.Name, path); }
                if (!File.Exists(Path.Combine(directory.FullName, attrFile.Parameters[0].text)))
                { throw new CompilerException($"File '{Path.Combine(directory.FullName, attrFile.Parameters[0].text)} does not exists'", attrFile.Parameters[0]); }
                if (names.Contains(item.Name.text))
                { throw new CompilerException($"Test '{item.Name.text}' is already defined", item.Name, path); }
                if (string.IsNullOrEmpty(item.Name.text) || string.IsNullOrWhiteSpace(item.Name.text) || (item.Name.text == "test" && item.Name.type != TokenType.LITERAL_STRING))
                { throw new CompilerException($"Invalid test name '{item.Name.text}'", item.Name, path); }
                names.Add(item.Name.text);
                compiledTestDefinitions.Add(new CompiledTestDefinition()
                {
                    Disabled = false,
                    Name = item.Name.text,
                    File = Path.Combine(directory.FullName, attrFile.Parameters[0].text),
                });
            }

            foreach (var item in parserResult.Disabled)
            {
                if (item.Parameters.Length == 0)
                { throw new CompilerException("Expected 1 or more parameter after keyword 'disable'", item.Keyword, path); }
                foreach (var param in item.Parameters)
                {
                    if (disabled.Contains(param.text))
                    { warnings.Add(new Warning($"Test '{param.text}' already disabled", param, path)); }
                    if (!names.Contains(param.text))
                    { warnings.Add(new Warning($"Test '{param.text}' not found", param, path)); }
                    disabled.Add(param.text);
                }
            }

            for (int i = 0; i < compiledTestDefinitions.Count; i++)
            {
                if (disabled.Contains(compiledTestDefinitions[i].Name)) { compiledTestDefinitions[i].Disabled = true; }
            }

            return new CompilerResult()
            {
                Tests = compiledTestDefinitions.ToArray(),
            };
        }
    }
}
