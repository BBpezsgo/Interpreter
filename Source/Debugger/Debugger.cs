﻿using System;
using System.IO;
using Communicating;

using ProgrammingLanguage.BBCode.Compiler;
using ProgrammingLanguage.Bytecode;
using ProgrammingLanguage.Core;
using ProgrammingLanguage.Errors;

namespace TheProgram
{
    internal class Debugger
    {
        readonly InterProcessCommunication Ipc;
        readonly string SourceCode;
        InterpreterDebuggabble Interpreter;

        int CurrentLine
        {
            get
            {
                int result = -1;

                for (int i = 0; i < Interpreter.Details.CompilerResult.DebugInfo.Length; i++)
                {
                    DebugInfo item = Interpreter.Details.CompilerResult.DebugInfo[i];
                    if (item.InstructionStart > Interpreter.Details.Interpreter.CodePointer) continue;
                    if (item.InstructionEnd < Interpreter.Details.Interpreter.CodePointer) continue;

                    result = item.Position.Start.Line;
                }

                return result;
            }
        }

        private bool NeedStdin;

        internal Debugger(ArgumentParser.Settings settings_)
        {
            Ipc = new InterProcessCommunication();
            Ipc.OnRecived += (manager, message) => { if (Interpreter == null) return; OnMessage(message); };

            ArgumentParser.Settings settings = ModifySettings(settings_);
            SourceCode = File.ReadAllText(settings.File.FullName);
            NeedStdin = false;
            InitInterpreter(settings);
            Ipc.Start();
        }

        void InitInterpreter(ArgumentParser.Settings settings)
        {
            Interpreter?.Destroy();
            Interpreter = new InterpreterDebuggabble();

            Interpreter.OnOutput += (sender, message, logType) => Ipc.Send("console/out", new Data_Log(logType, message, new Data_Context(sender.Details)));
            Interpreter.OnStdOut += (_, message) => Ipc.Send("stdout", message);
            Interpreter.OnStdError += (_, message) => Ipc.Send("stderr", message);
            Interpreter.OnNeedInput += _ => NeedStdin = true;

            if (!Interpreter.Initialize()) return;

            Instruction[] compiledCode = Interpreter.CompileCode(settings.File, settings.compilerSettings, settings.parserSettings, settings.HandleErrors);
            if (compiledCode == null) return;

            Interpreter.ExecuteProgram(compiledCode, settings.bytecodeInterpreterSettings);
        }

        static ArgumentParser.Settings ModifySettings(ArgumentParser.Settings settings)
        {
            ArgumentParser.Settings result = settings;
            return result;
        }

        void OnMessage(IPCMessage<object> message)
        {
            switch (message.Type)
            {
                case "debug/step":
                    {
                        Ipc.Log($"Abs Breakpoint: {Interpreter.AbsoluteBreakpoint}");
                        Ipc.Log($"Breakpoint: {Interpreter.Breakpoint}");

                        Interpreter.Breakpoint = Math.Max(1, Interpreter.Breakpoint + 1);

                        Ipc.Log($"Abs Breakpoint: {Interpreter.AbsoluteBreakpoint}");
                        Ipc.Log($"Breakpoint: {Interpreter.Breakpoint}");

                        try
                        { Interpreter.Continue(2); }
                        catch (EndlessLoopException) { }
                        Ipc.Reply("interpreter/updated", "null", message);
                    }
                    break;
                case "debug/stepinto":
                    {
                        Interpreter.StepInto();
                        Ipc.Reply("interpreter/updated", "null", message);
                    }
                    break;

                case "interpreter/tick":
                    {
                        Interpreter.Update();
                        Ipc.Reply("interpreter/updated", "null", message);
                    }
                    break;

                case "compiler/debuginfo":
                    {
                        Ipc.Reply("compiler/debuginfo",
                            (Interpreter.Details.CompilerResult.DebugInfo == null) ?
                                Array.Empty<Data_DebugInfo>() :
                                Interpreter.Details.CompilerResult.DebugInfo.ToData(v => new Data_DebugInfo(v)),
                            message);
                    }
                    break;
                case "compiler/code":
                    {
                        Ipc.Reply("compiler/code",
                            (Interpreter.Details.CompilerResult.Code == null) ?
                                Array.Empty<Data_Instruction>() :
                                Interpreter.Details.CompilerResult.Code.ToData(v => new Data_Instruction(v)),
                            message);
                    }
                    break;

                case "interpreter/details":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("interpreter/details", new Data_BytecodeInterpreterDetails(Interpreter.Details.Interpreter), message);
                    }
                    break;
                case "interpreter/registers":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("interpreter/registers", new BytecodeProcessorRegisters(Interpreter.Details.Interpreter), message);
                    }
                    break;
                case "interpreter/stack":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                        Ipc.Reply("interpreter/stack", new Stack_(Interpreter.Details.Interpreter), message);
                    }
                    break;
                case "interpreter/state":
                    {
                        Ipc.Reply("interpreter/state", Interpreter.Details.State.ToString(), message);
                    }
                    break;
                case "interpreter/callstack":
                    {
                        Ipc.Reply("interpreter/callstack", Interpreter.Details.Interpreter.CallStack, message);
                    }
                    break;

                case "eval":
                    {
                        if (Interpreter.Details.Interpreter == null) return;
                    }
                    break;
                case "stdin":
                    if (!NeedStdin) break;
                    Interpreter.OnInput(message.Data.ToString()[0]);
                    NeedStdin = false;
                    break;
            }
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

    public struct BytecodeProcessorRegisters
    {
        public int BasePointer { get; set; }
        public int CodePointer { get; set; }

        internal BytecodeProcessorRegisters(BytecodeInterpreter v)
        {
            BasePointer = v.BasePointer;
            CodePointer = v.CodePointer;
        }
    }

    public struct Stack_
    {
        public Data_StackItem[] Stack { get; set; }

        internal Stack_(BytecodeInterpreter v)
        {
            Stack = v.Stack.ToArray().ToData(v => new Data_StackItem(v));
        }
    }

    internal class Data_BytecodeInterpreterDetails : Data_Serializable<BytecodeInterpreter>
    {
        public Data_StackItem[] Heap { get; set; }

        internal Data_BytecodeInterpreterDetails(BytecodeInterpreter v) : base(v)
        {
            Heap = v.Heap.ToArray().ToData(v => new Data_StackItem(v));
        }

        public static Data_BytecodeInterpreterDetails Make(BytecodeInterpreter v) => new(v);
    }

    public class Data_StackItem : Data_Serializable<DataItem>
    {
        public string Type { get; set; }
        public string Value { get; set; }
        public string Tag { get; set; }
        public bool IsHeapAddress { get; set; }

        public Data_StackItem(DataItem data) : base(data)
        {
            if (data.IsNull)
            {
                Type = "BYTE";
                Value = "null";
            }
            else
            {
                object value = data.GetValue();
                Type = data.Type.ToString();
                Value = value == null ? "null" : value.ToString();
            }
            Tag = data.Tag;
        }
    }

    public class Data_Function : Data_Serializable<CompiledFunction>
    {
        public string FullName { get; set; }
        public Data_Position Position { get; set; }

        public Data_Function(CompiledFunction v) : base(v)
        {
            this.FullName = v.Identifier.Content;
            this.Position = new Data_Position(v.Identifier.GetPosition());
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
        public string Opcode { get; set; }
        public Data_StackItem Parameter { get; set; }
        public string Tag { get; set; }

        public Data_Instruction(Instruction v) : base(v)
        {
            Opcode = v.opcode.ToString();
            Parameter = new Data_StackItem(v.Parameter);
            Tag = v.tag;
        }
    }

    public class Data_Log
    {
        public string Message { get; set; }
        public string Type { get; set; }
        public Data_Context Context { get; set; }

        public Data_Log(ProgrammingLanguage.Output.LogType type, string message, Data_Context context)
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
            }
            catch (NullReferenceException)
            {
                return;
            }

            CodePointer = v.Interpreter.CodePointer;
            CallStack = v.Interpreter.CallStack;
        }
    }
}
