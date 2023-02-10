using System;
using System.IO;

namespace TheProgram
{
    using Communicating;

    using IngameCoding.BBCode;
    using IngameCoding.BBCode.Compiler;
    using IngameCoding.Bytecode;
    using IngameCoding.Core;

    using System.Linq;

    internal static class DebugTest
    {
        public static void Run(ArgumentParser.Settings settings_)
        {
            ArgumentParser.Settings settings = settings_;
            settings.bytecodeInterpreterSettings.ClockCyclesPerUpdate = 1;

            var ipc = new InterProcessCommunication();

            var code = File.ReadAllText(settings.File.FullName);
            var interpreter = new Interpreter();

            bool needStdin = false;

            ipc.OnRecived += (manager, message) =>
            {
                if (interpreter == null) return;

                switch (message.type)
                {
                    case "intp/step":
                        {
                            interpreter.Update();
                        }
                        break;
                    case "comp/res":
                        {
                            manager.Reply("comp/res", new Data_CompilerResult(interpreter.Details.CompilerResult), message.id);
                        }
                        break;
                    case "get-intp-data":
                        {
                            if (interpreter.Details.Interpreter == null) return;
                            manager.Reply("intp-data", new Data_BytecodeInterpreterDetails(interpreter.Details.Interpreter.Details), message.id);
                        }
                        break;
                    case "get-intp2-data":
                        {
                            manager.Reply("intp2-data", new Data_CodeInterpreterDetails(interpreter.Details), message.id);
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
                ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(sender.Details)));
            };

            interpreter.OnStdOut += (_, message) => ipc.Send("stdout", message);
            interpreter.OnStdError += (_, message) => ipc.Send("stderr", message);
            interpreter.OnNeedInput += _ => needStdin = true;

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
        public string[] CallStack { get; set; }
        public Data_StackItem[] Stack { get; set; }
        public Data_StackItem[] Heap { get; set; }

        internal Data_BytecodeInterpreterDetails(BytecodeInterpreter.InterpreterDetails v) : base(v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
            CallStack = v.CallStack;
            StackMemorySize = v.StackMemorySize;
            Stack = v.Stack.ToData(v => new Data_StackItem(v));
            Heap = v.Heap.ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter.InterpreterDetails v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<DataItem>
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Tag { get; set; }
        public bool IsHeapAddress { get; set; }

        public Data_StackItem(DataItem v) : base(v)
        {
            var v_v = v.Value();
            Type = v.type.ToString();
            Value = v_v == null ? "null" : v_v.ToString();
            Tag = v.Tag;
            IsHeapAddress = v.IsHeapAddress;
        }
    }

    public class Data_CompilerResult : Data_Serializable<Compiler.CompilerResult>
    {
        public Data_Instruction[] CompiledCode { get; set; }
        public CompiledFunction[] Functions { get; set; }
        public int SetGlobalVariablesInstruction { get; set; }
        public int ClearGlobalVariablesInstruction { get; set; }
        public Data_DebugInfo[] DebugInfo { get; set; }

        public Data_CompilerResult(Compiler.CompilerResult v) : base(v)
        {
            ClearGlobalVariablesInstruction = v.clearGlobalVariablesInstruction;
            SetGlobalVariablesInstruction = v.setGlobalVariablesInstruction;
            DebugInfo = (v.debugInfo == null) ? Array.Empty<Data_DebugInfo>() : v.debugInfo.ToData(v => new Data_DebugInfo(v));
            CompiledCode = (v.compiledCode == null) ? Array.Empty<Data_Instruction>() : v.compiledCode.ToData(v => new Data_Instruction(v));
            Functions = v.compiledFunctions.Values.ToArray();
        }
    }

    public class Data_Function : Data_Serializable<CompiledFunction>
    {
        public string FullName { get; set; }
        public Data_Position Position { get; set; }

        public Data_Function(CompiledFunction v) : base(v)
        {
            this.FullName = v.FullName;
            this.Position = new Data_Position(v.Name.GetPosition());
        }
    }

    public class Data_Position : Data_Serializable<Position>
    {
        public int StartLine { get; set; }
        public int StartChar { get; set; }
        public int EndLine { get; set; }
        public int EndChar { get; set; }
        public int StartTotal { get; set; }
        public int EndTotal { get; set; }

        public Data_Position(Position v) : base(v)
        {
            StartLine = v.Start.Line;
            StartChar = v.Start.Character;

            EndLine = v.End.Line;
            EndChar = v.End.Character;

            StartTotal = v.AbsolutePosition.Start;
            EndTotal = v.AbsolutePosition.End;
        }
    }

    public class Data_DebugInfo : Data_Serializable<DebugInfo>
    {
        public int StartOffset { get; set; }
        public int EndOffset { get; set; }
        public Data_Position Position { get; set; }

        public Data_DebugInfo(DebugInfo v) : base(v)
        {
            StartOffset = v.InstructionStart;
            EndOffset = v.InstructionEnd;
            Position = new Data_Position(v.Position);
        }
    }

    public class Data_Instruction : Data_Serializable<Instruction>
    {
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
            else if (v.parameter is Struct)
            {
                Parameter = "{ ... }";
                ParameterIsComplicated = true;
            }
            else if (v.parameter is DataItem v2)
            {
                switch (v2.type)
                {
                    case DataType.INT:
                        Parameter = "INT";
                        break;
                    case DataType.FLOAT:
                        Parameter = "FLOAT";
                        break;
                    case DataType.STRING:
                        Parameter = "STRING";
                        break;
                    case DataType.BOOLEAN:
                        Parameter = "BOOLEAN";
                        break;
                    case DataType.STRUCT:
                        Parameter = "{ ... }";
                        break;
                    case DataType.LIST:
                        Parameter = "[ ... ]";
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
        }
    }

    public class Data_Log
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public Data_Context Context { get; set; }

        public Data_Log(IngameCoding.Output.LogType type, string message, Data_Context context)
        {
            Type = type.ToString();
            Message = message;
            Context = context;
        }
    }

    public class Data_Context : Data_Serializable<Interpreter.InterpreterDetails>
    {
        public int CodePointer { get; set; } = -1;
        public string[] CallStack { get; set; } = Array.Empty<string>();

        public Data_Context(Interpreter.InterpreterDetails v) : base(v)
        {
            try
            {
                if (v == null) return;
                if (v.Interpreter == null) return;
                if (v.Interpreter.Details == null) return;
            }
            catch (NullReferenceException)
            {
                return;
            }

            CodePointer = v.Interpreter.Details.CodePointer;
            CallStack = v.Interpreter.Details.CallStack;
        }
    }
}
