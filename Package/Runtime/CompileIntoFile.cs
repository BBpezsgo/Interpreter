using IngameCoding.BBCode.Compiler;
using IngameCoding.Bytecode;
using IngameCoding.Core;
using IngameCoding.Errors;
using IngameCoding.Serialization;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IngameCoding
{
    internal class CompileIntoFile
    {
        internal static void Compile(TheProgram.ArgumentParser.Settings settings)
        {
            var output = settings.CompileOutput;
            var file = settings.File;

            var code = File.ReadAllText(file.FullName);
            var compilerResult = CompileCode(code, file, new List<Warning>(), settings.compilerSettings, settings.parserSettings);

            Serializer serializer = new();
            serializer.SerializeObject(new SerializableCode(compilerResult));
            File.WriteAllBytes(output, serializer.Result);
        }

        internal static SerializableCode Decompile(byte[] data)
        {
            Deserializer serializer = new(data);
            return (SerializableCode)serializer.DeserializeObject<SerializableCode>();
        }

        internal class SerializableLiteral : ISerializable<SerializableLiteral>
        {
            internal Literal.Type Type;
            internal bool ValueBool;
            internal float ValueFloat;
            internal int ValueInt;
            internal string ValueString;

            public static SerializableLiteral Create(Literal v) => new()
            {
                Type = v.type,
                ValueBool = v.ValueBool,
                ValueFloat = v.ValueFloat,
                ValueInt = v.ValueInt,
                ValueString = v.ValueString,
            };

            public void Deserialize(Deserializer deserializer)
            {
                this.Type = (Literal.Type)deserializer.DeserializeInt32();
                switch (this.Type)
                {
                    case Literal.Type.Integer:
                        ValueInt = deserializer.DeserializeInt32();
                        break;
                    case Literal.Type.Float:
                        ValueFloat = deserializer.DeserializeFloat();
                        break;
                    case Literal.Type.String:
                        ValueString = deserializer.DeserializeString();
                        break;
                    case Literal.Type.Boolean:
                        ValueBool = deserializer.DeserializeBoolean();
                        break;
                }
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize((int)this.Type);
                switch (this.Type)
                {
                    case Literal.Type.Integer:
                        serializer.Serialize(ValueInt);
                        break;
                    case Literal.Type.Float:
                        serializer.Serialize(ValueFloat);
                        break;
                    case Literal.Type.String:
                        serializer.Serialize(ValueString);
                        break;
                    case Literal.Type.Boolean:
                        serializer.Serialize(ValueBool);
                        break;
                }
            }
        }

        internal class SerializableAttribute : ISerializable<SerializableAttribute>
        {
            internal string Name;
            internal SerializableLiteral[] parameters;

            public static SerializableAttribute Create(AttributeValues v)
            {
                SerializableAttribute result = new()
                {
                    Name = v.NameToken.text,
                    parameters = new SerializableLiteral[v.parameters.Count],
                };
                for (int i = 0; i < v.parameters.Count; i++)
                { result.parameters[i] = SerializableLiteral.Create(v.parameters[i]); }
                return result;
            }

            public void Deserialize(Deserializer deserializer)
            {
                this.Name = deserializer.DeserializeString();
                this.parameters = deserializer.DeserializeObjectArray<SerializableLiteral>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(this.Name);
                serializer.SerializeObjectArray(this.parameters);
            }


            public bool TryGetValue(int index, out string value)
            {
                value = string.Empty;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.String)
                {
                    value = parameters[index].ValueString;
                }
                return true;
            }
            public bool TryGetValue(int index, out int value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Integer)
                {
                    value = parameters[index].ValueInt;
                }
                return true;
            }
            public bool TryGetValue(int index, out float value)
            {
                value = 0;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Float)
                {
                    value = parameters[index].ValueFloat;
                }
                return true;
            }
            public bool TryGetValue(int index, out bool value)
            {
                value = false;
                if (parameters == null) return false;
                if (parameters.Length <= index) return false;
                if (parameters[index].Type == Literal.Type.Boolean)
                {
                    value = parameters[index].ValueBool;
                }
                return true;
            }
        }

        internal class SerializableFunctionDef : ISerializable<SerializableFunctionDef>
        {
            internal string Name;
            internal string[] NamespacePath;
            internal SerializableAttribute[] Attributes;

            /// <summary>
            /// <c>[Namespace].[...].Name</c>
            /// </summary>
            public string FullName => NamespacePathString + Name;
            /// <summary>
            /// <c>[Namespace].[...].</c>
            /// </summary>
            string NamespacePathString
            {
                get
                {
                    string val = "";
                    for (int i = 0; i < NamespacePath.Length; i++)
                    {
                        if (val.Length > 0)
                        {
                            val += "." + NamespacePath[i].ToString();
                        }
                        else
                        {
                            val = NamespacePath[i].ToString();
                        }
                    }
                    if (val.Length > 0)
                    {
                        val += ".";
                    }
                    return val;
                }
            }

            public SerializableFunctionDef() { }

            public SerializableFunctionDef(CompiledFunction funcDef)
            {
                this.Name = funcDef.Name.text;
                this.NamespacePath = funcDef.NamespacePath;
                this.Attributes = new SerializableAttribute[funcDef.CompiledAttributes.Count];
                for (int i = 0; i < Attributes.Length; i++)
                {
                    Attributes[i] = SerializableAttribute.Create(funcDef.CompiledAttributes.ElementAt(i).Value);
                }
            }

            public void Deserialize(Deserializer deserializer)
            {
                this.Name = deserializer.DeserializeString();
                this.NamespacePath = deserializer.DeserializeArray<string>();
                this.Attributes = deserializer.DeserializeObjectArray<SerializableAttribute>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(this.Name);
                serializer.Serialize(this.NamespacePath);
                serializer.SerializeObjectArray(this.Attributes);
            }

            internal bool TryGetAttribute(string name, out SerializableAttribute attriute)
            {
                attriute = null;
                foreach (var item in Attributes)
                {
                    if (item.Name == name)
                    {
                        attriute = item;
                        return true;
                    }
                }
                return false;
            }
        }

        internal class SerializableCode : ISerializable<SerializableCode>
        {
            internal Instruction[] Instructions;
            internal Dictionary<string, int> FunctionOffsets;
            internal SerializableFunctionDef[] CompiledFunctions;
            internal int OffsetSetGlobalVariables;
            internal int OffsetClearGlobalVariables;

            public SerializableCode() { }

            public SerializableCode(Compiler.CompilerResult compilerResult)
            {
                Instructions = compilerResult.compiledCode;
                OffsetClearGlobalVariables = compilerResult.clearGlobalVariablesInstruction;
                OffsetSetGlobalVariables = compilerResult.setGlobalVariablesInstruction;
                FunctionOffsets = compilerResult.functionOffsets;
                CompiledFunctions = new SerializableFunctionDef[compilerResult.compiledFunctions.Count];
                for (int i = 0; i < compilerResult.compiledFunctions.Count; i++)
                {
                    CompiledFunctions[i] = new SerializableFunctionDef(compilerResult.compiledFunctions.ElementAt(i).Value);
                }
            }

            public void Deserialize(Deserializer deserializer)
            {
                OffsetSetGlobalVariables = deserializer.DeserializeInt32();
                OffsetClearGlobalVariables = deserializer.DeserializeInt32();
                FunctionOffsets = deserializer.DeserializeDictionary<string, int>(false, false);
                Instructions = deserializer.DeserializeObjectArray<Instruction>();
                CompiledFunctions = deserializer.DeserializeObjectArray<SerializableFunctionDef>();
            }

            public void Serialize(Serializer serializer)
            {
                serializer.Serialize(OffsetSetGlobalVariables);
                serializer.Serialize(OffsetClearGlobalVariables);
                serializer.Serialize(FunctionOffsets, false);
                serializer.SerializeObjectArray(Instructions);
                serializer.SerializeObjectArray(CompiledFunctions);
            }

            internal bool GetFunctionOffset(SerializableFunctionDef compiledFunction, out int functionOffset)
            {
                if (FunctionOffsets.TryGetValue(compiledFunction.Name, out functionOffset))
                {
                    return true;
                }
                else if (FunctionOffsets.TryGetValue(compiledFunction.FullName, out functionOffset))
                {
                    return true;
                }
                functionOffset = -1;
                return false;
            }
        }

        static Compiler.CompilerResult CompileCode(
            string sourceCode,
            FileInfo file,
            List<Warning> warnings,
            Compiler.CompilerSettings compilerSettings,
            BBCode.Parser.ParserSettings parserSettings)
        {
            List<Error> errors = new();

            var parserResult = BBCode.Parser.Parser.Parse(sourceCode, warnings);
            parserResult.SetFile(file.FullName);
            var compilerResult = Compiler.CompileCode(
                parserResult,
                new Dictionary<string, BuiltinFunction>(),
                new Dictionary<string, Func<IStruct>>(),
                file,
                warnings,
                errors,
                compilerSettings,
                parserSettings);

            if (errors.Count > 0)
            { throw new System.Exception("Failed to compile", errors[0].ToException()); }

            return compilerResult;
        }
    }
}