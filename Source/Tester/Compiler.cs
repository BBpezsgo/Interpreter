using IngameCoding.BBCode;
using IngameCoding.Core;
using IngameCoding.Errors;

using System.Collections.Generic;
using System.IO;

namespace IngameCoding.Tester
{
    using Parser;

    using System.Linq;

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
            internal string[] Files;
        }

        internal static CompilerResult Compile(ParserResult parserResult, List<Warning> warnings, DirectoryInfo directory, string path)
        {
            List<CompiledTestDefinition> compiledTestDefinitions = new();

            List<string> disabled = new();
            List<string> names = new();

            foreach (var item in parserResult.TestDefinitions)
            {
                if (names.Contains(item.Name.Content))
                { throw new CompilerException($"Test '{item.Name.Content}' is already defined", item.Name, path); }

                if (string.IsNullOrEmpty(item.Name.Content) || string.IsNullOrWhiteSpace(item.Name.Content) || (item.Name.Content == "test" && item.Name.TokenType != TokenType.LITERAL_STRING))
                { throw new CompilerException($"Invalid test name '{item.Name.Content}'", item.Name, path); }

                names.Add(item.Name.Content);

                if (item.TryGetAttribute("File", out var attrFile))
                {
                    if (attrFile.Parameters.Length != 1)
                    { throw new CompilerException("Attribute 'File' requies 1 string parameter", attrFile.Name, path); }

                    if (attrFile.Parameters[0].TokenType != TokenType.LITERAL_STRING)
                    { throw new CompilerException($"Attribute 'File' requies 1 string parameter, passed {attrFile.Parameters[0].TokenType}", attrFile.Name, path); }

                    if (!File.Exists(Path.Combine(directory.FullName, attrFile.Parameters[0].Content)))
                    { throw new CompilerException($"File '{Path.Combine(directory.FullName, attrFile.Parameters[0].Content)} does not exists'", attrFile.Parameters[0]); }

                    compiledTestDefinitions.Add(new CompiledTestDefinition()
                    {
                        Disabled = false,
                        Name = item.Name.Content,
                        Files = new string[1] { Path.Combine(directory.FullName, attrFile.Parameters[0].Content) },
                    });
                }
                else if (item.TryGetAttribute("All", out var attributeAll))
                {
                    if (attributeAll.Parameters.Length != 0)
                    { throw new CompilerException("Attribute 'All' requies 0 parameter", attrFile.Name, path); }

                    var files = directory.GetFiles("*.bbc");
                    compiledTestDefinitions.Add(new CompiledTestDefinition()
                    {
                        Disabled = false,
                        Name = item.Name.Content,
                        Files = files.Select(v => v.FullName).ToArray(),
                    });
                }
                else
                { throw new CompilerException("Attribute 'File' is requied for test definition", item.Keyword, path); }
            }

            foreach (var item in parserResult.Disabled)
            {
                if (item.Parameters.Length == 0)
                { throw new CompilerException("Expected 1 or more parameter after keyword 'disable'", item.Keyword, path); }
                foreach (var param in item.Parameters)
                {
                    if (disabled.Contains(param.Content))
                    { warnings.Add(new Warning($"Test '{param.Content}' already disabled", param, path)); }
                    if (!names.Contains(param.Content))
                    { warnings.Add(new Warning($"Test '{param.Content}' not found", param, path)); }
                    disabled.Add(param.Content);
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
