using System;
using System.IO;

namespace TheProgram
{
    using Communicating;

    using IngameCoding.BBCode;
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;

    internal static class DebugTest
    {
        public static void Run(ArgumentParser.Settings settings_)
        {
            ArgumentParser.Settings settings = settings_;
            settings.bytecodeInterpreterSettings.ClockCyclesPerUpdate = 1;

            var ipc = new IPC();

            var code = File.ReadAllText(settings.File.FullName);
            var interpreter = new Interpreter();

            bool needStdin = false;

            ipc.OnRecived += (manager, message) =>
            {
                if (interpreter == null) return;

                switch (message.type)
                {
                    case "intp-update":
                        {
                            interpreter.Update();
                        }
                        break;
                    case "get-comp-res":
                        {
                            manager.Send("comp-res", new Data_CompilerResult(interpreter.Details.CompilerResult));
                        }
                        break;
                    case "get-intp-data":
                        {
                            if (interpreter.Details.Interpreter == null) return;
                            manager.Send("intp-data", new Data_BytecodeInterpreterDetails(interpreter.Details.Interpreter.Details));
                        }
                        break;
                    case "get-intp2-data":
                        {
                            manager.Send("intp2-data", new Data_CodeInterpreterDetails(interpreter.Details));
                        }
                        break;
                    case "stdin":
                        if (!needStdin) break;
                        needStdin = false;

                        interpreter.OnInput(message.data.ToString()[0]);
                        break;
                }
            };

            interpreter.OnOutput += (sender, message, logType) =>
            {
                ipc.Send("con-out", new Data_Log((logType.ToString(), message)));
            };

            interpreter.OnStdOut += (sender, message) =>
            {
                ipc.Send("stdout", message);
            };

            interpreter.OnStdError += (sender, message) =>
            {
                ipc.Send("stderr", message);
            };

            interpreter.OnNeedInput += (sender) =>
            {
                needStdin = true;
            };

            if (interpreter.Initialize())
            {
                var compiledCode = interpreter.CompileCode(code, settings.File, settings.compilerSettings, settings.parserSettings, settings.HandleErrors);

                if (compiledCode != null)
                {
                    interpreter.RunCode(compiledCode, settings.bytecodeInterpreterSettings);
                }
            }

            ipc.Start();
        }
    }

    static class Extensions
    {
        public static double ToUnix(this DateTime v) => v.ToUniversalTime().Subtract(new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;

        public static TResult[] ToData<TOriginal, TResult>(this TOriginal[] v, Func<TOriginal, TResult> CreateCallback) where TResult : Data_Serializable<TOriginal>
        {
            TResult[] result = new TResult[v.Length];
            for (int i = 0; i < v.Length; i++)
            {
                result[i] = CreateCallback.Invoke(v[i]);
            }
            return result;
        }
    }

    public abstract class Data_Serializable<TOriginal>
    {
#pragma warning disable IDE0060 // Remove unused parameter
        internal Data_Serializable(TOriginal v) { }
#pragma warning restore IDE0060 // Remove unused parameter
    }

    internal class Data_CodeInterpreterDetails : Data_Serializable<Interpreter.InterpreterDetails>
    {
        internal Data_CodeInterpreterDetails(Interpreter.InterpreterDetails v) : base(v)
        {
            this.State = v.State.ToString();
        }

        public string State { get; private set; }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter.InterpreterDetails v) => new(v);
    }

    internal class Data_BytecodeInterpreterDetails : Data_Serializable<BytecodeInterpreter.InterpreterDetails>
    {
        public int BasePointer { get; set; }
        public int CodePointer { get; set; }
        public int StackMemorySize { get; set; }
        public Data_StackItem[] Stack { get; set; }

        internal Data_BytecodeInterpreterDetails(BytecodeInterpreter.InterpreterDetails v) : base(v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
            StackMemorySize = v.StackMemorySize;
            Stack = v.Stack.ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter.InterpreterDetails v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<DataItem>
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Tag { get; set; }

        public Data_StackItem(DataItem v) : base(v)
        {
            var v_v = v.Value();
            Type = v.type.ToString();
            Value = v_v == null ? "null" : v_v.ToString();
            Tag = v.Tag;
        }
    }

    public class Data_CompilerResult : Data_Serializable<Compiler.CompilerResult>
    {
        public Data_Instruction[] CompiledCode { get; set; }
        public int SetGlobalVariablesInstruction { get; set; }
        public int ClearGlobalVariablesInstruction { get; set; }

        public Data_CompilerResult(Compiler.CompilerResult v) : base(v)
        {
            ClearGlobalVariablesInstruction = v.clearGlobalVariablesInstruction;
            SetGlobalVariablesInstruction = v.setGlobalVariablesInstruction;
            CompiledCode = (v.compiledCode == null) ? Array.Empty<Data_Instruction>() : v.compiledCode.ToData(v => new Data_Instruction(v));
        }
    }

    public class Data_Instruction : Data_Serializable<Instruction>
    {
        public int AdditionParameter2 { get; set; }
        public string AdditionParameter { get; set; }
        public string Tag { get; set; }
        public object Parameter { get; set; }
        public bool ParameterIsComplicated { get; set; }
        public string Opcode { get; set; }
        public Data_Instruction(Instruction v) : base(v)
        {
            Opcode = v.opcode.ToString();
            if (v.parameter is IStruct)
            {
                Parameter = "IStruct { ... }";
                ParameterIsComplicated = true;
            }
            else if (v.parameter is DataItem.List)
            {
                Parameter = "[ ... ]";
                ParameterIsComplicated = true;
            }
            else if (v.parameter is DataItem.Struct)
            {
                Parameter = "{ ... }";
                ParameterIsComplicated = true;
            }
            else if (v.parameter is DataItem v2)
            {
                switch (v2.type)
                {
                    case DataItem.Type.INT:
                        Parameter = "INT";
                        break;
                    case DataItem.Type.FLOAT:
                        Parameter = "FLOAT";
                        break;
                    case DataItem.Type.STRING:
                        Parameter = "STRING";
                        break;
                    case DataItem.Type.BOOLEAN:
                        Parameter = "BOOLEAN";
                        break;
                    case DataItem.Type.STRUCT:
                        Parameter = "{ ... }";
                        break;
                    case DataItem.Type.LIST:
                        Parameter = "[ ... ]";
                        break;
                    case DataItem.Type.RUNTIME:
                        Parameter = "RUNTIME";
                        break;
                }
                ParameterIsComplicated = true;
            }
            else
            {
                Parameter = v.parameter;
                ParameterIsComplicated = false;
            }
            Tag = v.tag;
            AdditionParameter = v.additionParameter;
            AdditionParameter2 = v.additionParameter2;
        }
    }

    public class Data_Log : Data_Serializable<(string, string)>
    {
        public string Message { get; set; }
        public string Type { get; set; }

        public Data_Log((string, string) v) : base(v)
        {
            this.Type = v.Item1;
            this.Message = v.Item2;
        }
    }
}
