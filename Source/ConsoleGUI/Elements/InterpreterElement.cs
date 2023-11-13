using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using LanguageCore;
using LanguageCore.Runtime;
using Win32;

#nullable disable

namespace ConsoleGUI
{
    public sealed class InterpreterElement : WindowElement
    {
        public string File;
        InterpreterDebuggabble Interpreter;

        ScrollBar HeapScrollBar;
        ScrollBar StackScrollBar;

        int NextCodeJumpCount;
        int CurrentlyJumping;
        MainThreadTimer InterpreterTimer;
        StandardIOElement ConsolePanel;
        bool StackAutoScroll;

        InterpreterElement() : base()
        {
            ClearBuffer();
            InitElements();
            HasBorder = false;

            NextCodeJumpCount = 1;
            CurrentlyJumping = 0;
            StackAutoScroll = true;
        }

        public InterpreterElement(string file, LanguageCore.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, BytecodeInterpreterSettings interpreterSettings, bool handleErrors, string basePath) : this()
        {
            this.File = file;
            SetupInterpreter(compilerSettings, interpreterSettings, handleErrors, basePath);
        }

        public InterpreterElement(string file) : this()
        {
            this.File = file;
            SetupInterpreter();
        }

        public override void Tick(double deltaTime)
        {
            this.InterpreterTimer?.Tick(deltaTime);
        }

        void InitElements()
        {
            var StatePanel = new InlineElement
            {
                HasBorder = true,
                Title = "State",
                Layout = new InlineLayout(InlineLayoutSizeMode.Fixed, 4),
            };
            StatePanel.OnBeforeDraw += StateElement_OnBeforeDraw;

            var CodePanel = new InlineElement
            {
                HasBorder = true,
                Title = "Code",
            };
            CodePanel.OnBeforeDraw += SourceCodeElement_OnBeforeDraw;

            ConsolePanel = new StandardIOElement
            {
                HasBorder = true,
                Title = "Console",
            };

            var StackPanel = new InlineElement
            {
                HasBorder = true,
                Title = "Stack",
                Layout = InlineLayout.Stretchy(130),
            };
            StackPanel.OnBeforeDraw += StackElement_OnBeforeDraw;

            StackScrollBar = new ScrollBar((sender) => (0, Interpreter.BytecodeInterpreter.Memory.Stack.Count + 30), StackPanel);

            StackPanel.OnMouseEventInvoked += StackScrollBar.FeedEvent;
            StackPanel.OnKeyEventInvoked += StackScrollBar.FeedEvent;

            var HeapPanel = new InlineElement
            {
                HasBorder = true,
                Title = "HEAP",
            };

            HeapScrollBar = new ScrollBar((sender) => (0, Interpreter.BytecodeInterpreter.Memory.Heap.Size - 3), HeapPanel);

            HeapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;
            HeapPanel.OnMouseEventInvoked += HeapScrollBar.FeedEvent;
            HeapPanel.OnKeyEventInvoked += HeapScrollBar.FeedEvent;

            var CallstackPanel = new InlineElement
            {
                HasBorder = true,
                Title = "Call Stack",
            };
            CallstackPanel.OnBeforeDraw += CallstackElement_OnBeforeDraw;

            this.Elements = new Element[]{
            new HorizontalLayoutElement()
                {
                    Rect = new Rectangle(0, 0, Console.WindowWidth, Console.WindowHeight),
                    Elements = new Element[]
                    {
                        new VerticalLayoutElement()
                        {
                            Elements = new Element[]
                            {
                                StatePanel,
                                ConsolePanel,
                                CodePanel,
                            }
                        },
                        new VerticalLayoutElement()
                        {
                            Elements = new Element[]
                            {
                                StackPanel,
                                HeapPanel,
                                CallstackPanel,
                            }
                        },
                    },
                },
            };
        }

        void SetupInterpreter() => SetupInterpreter(LanguageCore.BBCode.Compiler.Compiler.CompilerSettings.Default, BytecodeInterpreterSettings.Default, false, string.Empty);
        void SetupInterpreter(LanguageCore.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, BytecodeInterpreterSettings interpreterSettings, bool handleErrors, string basePath)
        {
            this.InterpreterTimer = new MainThreadTimer(200);
            this.InterpreterTimer.Elapsed += () =>
            {
                if (this.CurrentlyJumping <= 0) return;

                this.CurrentlyJumping--;
                this.Interpreter.DoUpdate();
                if (!this.Interpreter.IsExecutingCode)
                {
                    ConsoleGUI.Instance?.Destroy();
                    return;
                }
                if (ConsoleGUI.Instance != null) ConsoleGUI.Instance.NextRefreshConsole = true;
            };
            this.InterpreterTimer.Enabled = true;

            var fileInfo = new FileInfo(File);
            var code = System.IO.File.ReadAllText(fileInfo.FullName);
            this.Interpreter = new InterpreterDebuggabble();

            void PrintOutput(string message, LogType logType)
            {
                ConsolePanel.Write(message, logType switch
                {
                    LogType.System => ByteColor.Silver,
                    LogType.Normal => ByteColor.Silver,
                    LogType.Warning => ByteColor.BrightYellow,
                    LogType.Error => ByteColor.BrightRed,
                    LogType.Debug => ByteColor.Silver,
                    _ => ByteColor.Silver,
                });
                ConsolePanel.Write("\n");
            }

            Interpreter.OnOutput += (_, p1, p2) => PrintOutput(p1, p2);

            Interpreter.OnStdOut += (sender, data) => ConsolePanel.Write(data);
            Interpreter.OnStdError += (sender, data) => ConsolePanel.Write(data, ByteColor.BrightRed);

            Interpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey();
                sender.OnInput(input.KeyChar);
            };

            if (Interpreter.Initialize())
            {
                LanguageCore.BBCode.Compiler.CodeGeneratorForMain.Result? compiledCode = LanguageCore.BBCode.EasyCompiler.Compile(
                    fileInfo,
                    Interpreter.GenerateExternalFunctions(),
                    LanguageCore.Tokenizing.TokenizerSettings.Default,
                    compilerSettings,
                    handleErrors,
                    PrintOutput,
                    basePath
                    );

                if (compiledCode.HasValue)
                {
                    Interpreter.CompilerResult = compiledCode.Value;
                    Interpreter.ExecuteProgram(compiledCode.Value.Code, new BytecodeInterpreterSettings()
                    {
                        InstructionLimit = interpreterSettings.InstructionLimit,
                        StackMaxSize = interpreterSettings.StackMaxSize,
                        HeapSize = interpreterSettings.HeapSize,
                    });
                }
            }
        }

        private void CallstackElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            sender.DrawBuffer.ResetColor();

            int[] calltraceRaw = this.Interpreter.BytecodeInterpreter.TraceCalls();

            FunctionInformations[] callstack = this.Interpreter.CompilerResult.DebugInfo.GetFunctionInformations(calltraceRaw);

            int i;
            for (i = 0; i < callstack.Length; i++)
            {
                FunctionInformations callframe = callstack[i];

                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                sender.DrawBuffer.AddText(' ');

                sender.DrawBuffer.AddText(' ', 3 - i.ToString().Length);

                sender.DrawBuffer.AddText(i.ToString());
                sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                sender.DrawBuffer.BackgroundColor = ByteColor.Black;

                if (!callframe.IsValid)
                {
                    sender.DrawBuffer.ForegroundColor = ByteColor.BrightCyan;
                    sender.DrawBuffer.AddText(calltraceRaw[i].ToString());
                }
                else
                {
                    if (callframe.IsMacro)
                    {
                        sender.DrawBuffer.ForegroundColor = ByteColor.BrightBlue;
                        sender.DrawBuffer.AddText("macro ");
                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                    }

                    if (callframe.ReadableIdentifier.Contains('('))
                    {
                        string functionName = callframe.ReadableIdentifier[..callframe.ReadableIdentifier.IndexOf('(')];

                        sender.DrawBuffer.ForegroundColor = ByteColor.BrightYellow;
                        sender.DrawBuffer.AddText(functionName);

                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                        sender.DrawBuffer.AddChar('(');

                        string parameters = callframe.ReadableIdentifier[(callframe.ReadableIdentifier.IndexOf('(') + 1)..callframe.ReadableIdentifier.IndexOf(')')];

                        List<string> parameters2;
                        if (!parameters.Contains(','))
                        {
                            parameters2 = new List<string>() { parameters };
                        }
                        else
                        {
                            parameters2 = new List<string>();
                            string[] splitted = parameters.Split(',');
                            for (int j = 0; j < splitted.Length; j++)
                            { parameters2.Add(splitted[j].Trim()); }
                        }

                        for (int j = 0; j < parameters2.Count; j++)
                        {
                            if (j > 0)
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                                sender.DrawBuffer.AddText(',');
                                sender.DrawBuffer.AddText(' ');
                            }

                            string param = parameters2[j];
                            if (LanguageConstants.BuiltinTypes.Contains(param))
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.BrightBlue;
                            }
                            else
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                            }
                            sender.DrawBuffer.AddText(param);
                        }

                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                        sender.DrawBuffer.AddChar(')');

                        sender.DrawBuffer.ResetColor();
                    }
                    else
                    {
                        sender.DrawBuffer.AddText(callframe.ReadableIdentifier);
                    }
                }

                sender.DrawBuffer.BackgroundColor = ByteColor.Black;
                sender.DrawBuffer.FinishLine(sender.Rect.Width);
                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
            }

            {
                FunctionInformations callframe = this.Interpreter.CompilerResult.DebugInfo.GetFunctionInformations(this.Interpreter.BytecodeInterpreter.CodePointer);

                if (callframe.IsValid)
                {
                    sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                    sender.DrawBuffer.AddText(' ');

                    sender.DrawBuffer.AddText(' ', 3 - (i + 1).ToString().Length);

                    sender.DrawBuffer.AddText((i + 1).ToString());
                    sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                    sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                    sender.DrawBuffer.BackgroundColor = ByteColor.Black;

                    if (callframe.IsMacro)
                    {
                        sender.DrawBuffer.ForegroundColor = ByteColor.BrightBlue;
                        sender.DrawBuffer.AddText("macro ");
                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                    }

                    if (callframe.ReadableIdentifier.Contains('('))
                    {
                        string functionName = callframe.ReadableIdentifier[..callframe.ReadableIdentifier.IndexOf('(')];

                        sender.DrawBuffer.ForegroundColor = ByteColor.BrightYellow;
                        sender.DrawBuffer.AddText(functionName);

                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                        sender.DrawBuffer.AddChar('(');

                        string parameters = callframe.ReadableIdentifier[(callframe.ReadableIdentifier.IndexOf('(') + 1)..callframe.ReadableIdentifier.IndexOf(')')];

                        List<string> parameters2;
                        if (!parameters.Contains(','))
                        {
                            parameters2 = new List<string>() { parameters };
                        }
                        else
                        {
                            parameters2 = new List<string>();
                            string[] splitted = parameters.Split(',');
                            for (int j = 0; j < splitted.Length; j++)
                            { parameters2.Add(splitted[j].Trim()); }
                        }

                        for (int j = 0; j < parameters2.Count; j++)
                        {
                            if (j > 0)
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                                sender.DrawBuffer.AddText(',');
                                sender.DrawBuffer.AddText(' ');
                            }

                            string param = parameters2[j];
                            if (LanguageConstants.BuiltinTypes.Contains(param))
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.BrightBlue;
                            }
                            else
                            {
                                sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                            }
                            sender.DrawBuffer.AddText(param);
                        }

                        sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                        sender.DrawBuffer.AddChar(')');

                        sender.DrawBuffer.ResetColor();
                    }
                    else
                    {
                        sender.DrawBuffer.AddText(callframe.ReadableIdentifier);
                    }

                    sender.DrawBuffer.ForegroundColor = ByteColor.Gray;
                    sender.DrawBuffer.AddText(" (current)");

                    sender.DrawBuffer.BackgroundColor = ByteColor.Black;
                    sender.DrawBuffer.FinishLine(sender.Rect.Width);
                    sender.DrawBuffer.ForegroundColor = ByteColor.Silver;
                }
            }

        }

        private void StateElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            b.AddText(' ', 2);
            b.AddText("IsRunning: ");
            b.AddText((!this.Interpreter.BytecodeInterpreter.IsDone).ToString());
            b.BackgroundColor = ByteColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ByteColor.Silver;

            b.AddText(' ', 2);
            if (this.Interpreter.BytecodeInterpreter.CodePointer == this.Interpreter.CompilerResult.Code.Length)
            {
                b.AddText("State: ");
                b.AddText(this.Interpreter.State.ToString());
            }
            else
            {
                b.AddText("State: Running...");
            }
            b.BackgroundColor = ByteColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ByteColor.Silver;

            b.AddText(' ', 2);


            if (this.Interpreter.StackOperation)
            {
                b.BackgroundColor = ByteColor.White;
                b.ForegroundColor = ByteColor.Black;
                b.AddText("STACK");
                b.BackgroundColor = ByteColor.Black;
                b.ForegroundColor = ByteColor.Silver;
            }
            else
            {
                b.AddText("STACK");
            }

            b.AddText(' ', 2);


            if (this.Interpreter.HeapOperation)
            {
                b.BackgroundColor = ByteColor.White;
                b.ForegroundColor = ByteColor.Black;
                b.AddText("HEAP");
                b.BackgroundColor = ByteColor.Black;
                b.ForegroundColor = ByteColor.Silver;
            }
            else
            {
                b.AddText("HEAP");
            }

            b.AddText(' ', 2);


            if (this.Interpreter.AluOperation)
            {
                b.BackgroundColor = ByteColor.White;
                b.ForegroundColor = ByteColor.Black;
                b.AddText("ALU");
                b.BackgroundColor = ByteColor.Black;
                b.ForegroundColor = ByteColor.Silver;
            }
            else
            {
                b.AddText("ALU");
            }

            b.AddText(' ', 2);


            if (this.Interpreter.ExternalFunctionOperation)
            {
                b.BackgroundColor = ByteColor.White;
                b.ForegroundColor = ByteColor.Black;
                b.AddText("EXTERNAL");
                b.BackgroundColor = ByteColor.Black;
                b.ForegroundColor = ByteColor.Silver;
            }
            else
            {
                b.AddText("EXTERNAL");
            }

            b.AddText(' ', 2);

            b.BackgroundColor = ByteColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ByteColor.Silver;
        }

        private void HeapElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            Instruction instruction = this.Interpreter.NextInstruction;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == Opcode.HEAP_SET)
                {
                    if (instruction.AddressingMode == AddressingMode.RUNTIME)
                    { storeIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].ValueSInt32); }
                    else
                    { storeIndicators.Add(instruction.ParameterInt); }
                }

                if (instruction.opcode == Opcode.HEAP_GET)
                {
                    if (instruction.AddressingMode == AddressingMode.RUNTIME)
                    {
                        if (this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].Type == RuntimeType.SInt32)
                        { loadIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].ValueSInt32); }
                    }
                    else
                    { loadIndicators.Add(instruction.ParameterInt); }
                }
            }

            int nextHeader = 0;
            for (int i = 0; i < this.Interpreter.BytecodeInterpreter.Memory.Heap!.Size; i++)
            {
                var item = this.Interpreter.BytecodeInterpreter.Memory.Heap[i];
                bool isHeader = (nextHeader == i) && (!this.Interpreter.BytecodeInterpreter.Memory.Heap[i].IsNull) && (this.Interpreter.BytecodeInterpreter.Memory.Heap is not null);
                (int, bool) header = (default, default);

                if (isHeader)
                {
                    header = HEAP.GetHeader(item);
                    nextHeader += header.Item1 + HEAP.BLOCK_HEADER_SIZE;
                }

                if (i < HeapScrollBar.Offset) continue;

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = ByteColor.Silver;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('●');
                    b.ForegroundColor = ByteColor.Silver;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                if (((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length > 0) b.AddText(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length);

                b.ForegroundColor = ByteColor.Silver;
                b.AddText(i.ToString());
                b.ForegroundColor = ByteColor.White;
                b.AddSpace(5, sender.Rect.Width);

                if (isHeader)
                {
                    b.BackgroundColor = ByteColor.Gray;
                    b.AddText("HEADER | ");
                    b.AddText(header.Item1.ToString());
                    b.AddText(" | ");
                    if (header.Item2)
                    {
                        b.BackgroundColor = ByteColor.BrightYellow;
                        b.ForegroundColor = ByteColor.Black;
                    }
                    else
                    {
                        b.BackgroundColor = ByteColor.BrightGreen;
                        b.ForegroundColor = ByteColor.White;
                    }
                    b.AddText(header.Item2 ? "USED" : "FREE");
                }
                else
                {
                    if (item.IsNull)
                    {
                        b.ForegroundColor = ByteColor.Gray;
                        b.AddText("<null>");
                    }
                    else
                    {
                        switch (item.Type)
                        {
                            case RuntimeType.UInt8:
                                b.ForegroundColor = ByteColor.BrightCyan;
                                b.AddText(item.ValueUInt8.ToString());
                                break;
                            case RuntimeType.SInt32:
                                b.ForegroundColor = ByteColor.BrightCyan;
                                b.AddText(item.ValueSInt32.ToString());
                                break;
                            case RuntimeType.Single:
                                b.ForegroundColor = ByteColor.BrightCyan;
                                b.AddText(item.ValueSingle.ToString());
                                b.AddText('f');
                                break;
                            case RuntimeType.UInt16:
                                b.ForegroundColor = ByteColor.BrightYellow;
                                b.AddText('\'');
                                b.AddText(item.ValueUInt16.Escape());
                                b.AddText('\'');
                                break;
                            default:
                                b.ForegroundColor = ByteColor.Silver;
                                b.AddText('?');
                                break;
                        }
                    }
                }

                b.BackgroundColor = ByteColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ByteColor.Silver;
            }

            HeapScrollBar.Draw(b);
        }
        private void StackElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            CollectedScopeInfo stackDebugInfo = Interpreter.CompilerResult.DebugInfo.GetScopeInformations(Interpreter.BytecodeInterpreter.CodePointer);

            Instruction instruction = this.Interpreter.NextInstruction;

            int stackSize = this.Interpreter.BytecodeInterpreter.Memory.Stack.Count;

            int[] savedBasePointers = DebugInformation.TraceBasePointers(Interpreter.BytecodeInterpreter.Memory.Stack.ToArray(), Interpreter.BytecodeInterpreter.BasePointer);

            bool basepointerShown = false;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == Opcode.STORE_VALUE)
                {
                    storeIndicators.Add(this.Interpreter.BytecodeInterpreter.GetAddress(instruction.Parameter.Integer ?? 0, instruction.AddressingMode));
                }

                if (instruction.opcode == Opcode.STORE_VALUE ||
                    instruction.opcode == Opcode.HEAP_SET)
                {
                    if (instruction.AddressingMode == AddressingMode.RUNTIME)
                    { loadIndicators.Add(stackSize - 2); }
                    else
                    { loadIndicators.Add(stackSize - 1); }
                }

                if (instruction.opcode == Opcode.LOAD_VALUE)
                {
                    loadIndicators.Add(this.Interpreter.BytecodeInterpreter.GetAddress(instruction.Parameter.Integer ?? 0, instruction.AddressingMode));
                    storeIndicators.Add(stackSize);
                }

                if (instruction.opcode == Opcode.PUSH_VALUE ||
                    instruction.opcode == Opcode.GET_BASEPOINTER ||
                    instruction.opcode == Opcode.HEAP_GET)
                { storeIndicators.Add(stackSize); }

                if (instruction.opcode == Opcode.POP_VALUE)
                { loadIndicators.Add(stackSize - 1); }

                if (instruction.opcode == Opcode.MATH_ADD ||
                    instruction.opcode == Opcode.MATH_DIV ||
                    instruction.opcode == Opcode.MATH_MOD ||
                    instruction.opcode == Opcode.MATH_MULT ||
                    instruction.opcode == Opcode.MATH_SUB ||
                    instruction.opcode == Opcode.BITS_AND ||
                    instruction.opcode == Opcode.BITS_OR ||
                    instruction.opcode == Opcode.LOGIC_AND ||
                    instruction.opcode == Opcode.LOGIC_OR)
                {
                    loadIndicators.Add(stackSize - 1);
                    storeIndicators.Add(stackSize - 2);
                }
            }

            int stackDrawStart = StackScrollBar.Offset;
            int stackDrawEnd = stackSize;

            int notVisible = Math.Max(stackDrawEnd - (sender.Rect.Height - 3), 0);

            stackDrawStart += notVisible;

            void DrawElement(int address, DataItem item)
            {
                if (this.Interpreter.BytecodeInterpreter.BasePointer == address)
                {
                    b.ForegroundColor = ByteColor.BrightBlue;
                    b.AddText('►');
                    basepointerShown = true;
                    b.ForegroundColor = ByteColor.Silver;
                }
                else if (savedBasePointers.Contains(address))
                {
                    b.ForegroundColor = ByteColor.Silver;
                    b.AddText('►');
                    b.ForegroundColor = ByteColor.Silver;
                }
                else
                {
                    b.ForegroundColor = ByteColor.Silver;
                    b.AddText(' ');
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != address) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = ByteColor.Silver;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != address) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('●');
                    b.ForegroundColor = ByteColor.Silver;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - address.ToString().Length);

                b.AddText(address.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.ForegroundColor = ByteColor.Silver;
                b.BackgroundColor = ByteColor.Black;

                if (item.IsNull)
                {
                    b.ForegroundColor = ByteColor.Gray;
                    b.AddText("<null>");
                    return;
                }

                switch (item.Type)
                {
                    case RuntimeType.UInt8:
                        b.ForegroundColor = ByteColor.BrightCyan;
                        b.AddText(item.ValueUInt8.ToString());
                        break;
                    case RuntimeType.SInt32:
                        b.ForegroundColor = ByteColor.BrightCyan;
                        b.AddText(item.ValueSInt32.ToString());
                        break;
                    case RuntimeType.Single:
                        b.ForegroundColor = ByteColor.BrightCyan;
                        b.AddText(item.ValueSingle.ToString());
                        b.AddText('f');
                        break;
                    case RuntimeType.UInt16:
                        b.ForegroundColor = ByteColor.BrightYellow;
                        b.AddText('\'');
                        b.AddText(item.ValueUInt16.Escape());
                        b.AddText('\'');
                        break;
                    default:
                        b.ForegroundColor = ByteColor.Silver;
                        b.AddText('?');
                        break;
                }
            }

            int i;
            for (i = stackDrawStart; i < stackDrawEnd; i++)
            {
                DataItem item = this.Interpreter.BytecodeInterpreter.Memory.Stack[i];

                if (stackDebugInfo.TryGet(Interpreter.BytecodeInterpreter.BasePointer, i, out StackElementInformations itemDebugInfo))
                {
                    Range<int> range = itemDebugInfo.GetRange(Interpreter.BytecodeInterpreter.BasePointer);

                    if (itemDebugInfo.Kind == StackElementKind.Variable ||
                        itemDebugInfo.Kind == StackElementKind.Parameter)
                    {
                        if (range.Start == range.End)
                        {
                            b.ForegroundColor = ByteColor.Silver;

                            DrawElement(i, item);

                            b.ForegroundColor = ByteColor.Gray;
                            b.AddText($" ({itemDebugInfo.Kind.ToString().ToLowerInvariant()}) {itemDebugInfo.Tag}");
                        }
                        else if (range.Start == i)
                        {
                            b.ForegroundColor = ByteColor.Gray;
                            b.AddText($" ({itemDebugInfo.Kind.ToString().ToLowerInvariant()}) {itemDebugInfo.Tag} {{");

                            b.BackgroundColor = ByteColor.Black;
                            b.FinishLine(sender.Rect.Width);
                            b.ForegroundColor = ByteColor.Silver;

                            DrawElement(i, item);
                        }
                        else if (range.End == i)
                        {
                            DrawElement(i, item);

                            b.BackgroundColor = ByteColor.Black;
                            b.FinishLine(sender.Rect.Width);
                            b.ForegroundColor = ByteColor.Gray;
                            b.AddText(' ');
                            b.AddText('}');
                        }
                        else
                        {
                            DrawElement(i, item);
                        }
                    }
                    else if (itemDebugInfo.Kind == StackElementKind.Internal)
                    {
                        DrawElement(i, item);

                        b.ForegroundColor = ByteColor.Gray;
                        b.AddText(' ');
                        b.AddText(itemDebugInfo.Tag);
                    }
                    else
                    {
                        DrawElement(i, item);
                    }
                }
                else
                {
                    DrawElement(i, item);
                }

                b.BackgroundColor = ByteColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ByteColor.Silver;
            }

            while (
                (
                    !basepointerShown &&
                    i <= this.Interpreter.BytecodeInterpreter.BasePointer
                ) ||
                loadIndicators.Count > 0 ||
                storeIndicators.Count > 0)
            {
                if (this.Interpreter.BytecodeInterpreter.BasePointer == i)
                {
                    b.ForegroundColor = ByteColor.BrightBlue;
                    b.AddText('►');
                    basepointerShown = true;
                    b.ForegroundColor = ByteColor.Silver;
                }
                else
                {
                    b.ForegroundColor = ByteColor.Silver;
                    b.AddText(' ');
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('○');
                    b.ForegroundColor = ByteColor.Silver;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = ByteColor.BrightRed;
                    b.AddText('●');
                    b.ForegroundColor = ByteColor.Silver;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length);

                b.AddText(i.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.BackgroundColor = ByteColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ByteColor.Silver;

                i++;

                break;
            }

            StackScrollBar.Draw(b);
        }
        void SourceCodeElement_OnBeforeDraw(InlineElement sender)
        {
            // sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            void LinePrefix(string lineNumber)
            {
                b.AddText(' ', 4 - lineNumber.Length);
                b.ForegroundColor = ByteColor.Silver;
                b.AddText(lineNumber);
                b.ForegroundColor = ByteColor.Silver;
                b.AddSpace(5, sender.Rect.Width);
            }

            int indent = 0;
            for (int i = 0; i < this.Interpreter.BytecodeInterpreter.CodePointer - 5; i++)
            {
                if (Interpreter.CompilerResult.DebugInfo.CodeComments.TryGetValue(i, out var comments))
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        if (!comments[j].EndsWith("{ }") && comments[j].EndsWith('}'))
                        { indent--; }
                        if (!comments[j].EndsWith("{ }") && comments[j].EndsWith('{'))
                        { indent++; }
                    }
                }
            }

            bool IsNextInstruction = false;
            for (int i = Math.Max(0, this.Interpreter.BytecodeInterpreter.CodePointer - 5); i < this.Interpreter.CompilerResult.Code.Length; i++)
            {
                if (Interpreter.BytecodeInterpreter != null) if (Interpreter.BytecodeInterpreter.CodePointer == i) IsNextInstruction = true;

                var instruction = this.Interpreter.CompilerResult.Code[i];

                if (this.Interpreter.CompilerResult.DebugInfo.CodeComments.TryGetValue(i, out var comments))
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        string comment = comments[j];

                        if (!comment.EndsWith("{ }") && comment.EndsWith('}'))
                        {
                            indent--;
                        }

                        LinePrefix(string.Empty);
                        b.ForegroundColor = ByteColor.Gray;
                        b.AddText(' ', Math.Max(0, indent * 2));
                        b.AddText(comment);
                        b.ForegroundColor = ByteColor.Silver;
                        b.BackgroundColor = ByteColor.Black;
                        b.FinishLine(sender.Rect.Width);

                        if (!comment.EndsWith("{ }") && comment.EndsWith('{'))
                        {
                            indent++;
                        }
                    }
                }

                LinePrefix((i + 1).ToString());
                b.ForegroundColor = ByteColor.BrightYellow;
                b.AddText(' ', Math.Max(0, indent * 2));
                b.AddText(' ');
                if (IsNextInstruction)
                {
                    IsNextInstruction = false;
                    b.BackgroundColor = ByteColor.BrightRed;
                }
                b.AddText(instruction.opcode.ToString());
                b.AddText(' ');

                if (instruction.opcode == Opcode.LOAD_VALUE ||
                    instruction.opcode == Opcode.STORE_VALUE ||
                    instruction.opcode == Opcode.HEAP_GET ||
                    instruction.opcode == Opcode.HEAP_SET)
                {
                    b.AddText(instruction.AddressingMode.ToString());
                    b.AddText(' ');
                }

                if (!instruction.Parameter.IsNull) switch (instruction.Parameter.Type)
                    {
                        case RuntimeType.UInt8:
                            b.ForegroundColor = ByteColor.BrightCyan;
                            b.AddText(instruction.Parameter.ValueUInt8.ToString());
                            b.AddText(' ');
                            break;
                        case RuntimeType.SInt32:
                            b.ForegroundColor = ByteColor.BrightCyan;
                            b.AddText(instruction.Parameter.ValueSInt32.ToString());
                            b.AddText(' ');
                            break;
                        case RuntimeType.Single:
                            b.ForegroundColor = ByteColor.BrightCyan;
                            b.AddText(instruction.Parameter.ValueSingle.ToString());
                            b.AddText('f');
                            b.AddText(' ');
                            break;
                        case RuntimeType.UInt16:
                            b.ForegroundColor = ByteColor.BrightYellow;
                            b.AddText('\'');
                            b.AddText(instruction.Parameter.ValueUInt16.Escape());
                            b.AddText('\'');
                            b.AddText(' ');
                            break;
                        default:
                            b.ForegroundColor = ByteColor.White;
                            b.AddText(instruction.Parameter.ToString());
                            b.AddText(' ');
                            break;
                    }

                b.BackgroundColor = ByteColor.Black;

                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ByteColor.Silver;
            }

            {
                string t = CurrentlyJumping == 0 ? $" Jump Count: {NextCodeJumpCount} " : $" Jumping: {CurrentlyJumping} ";
                b.ForegroundColor = ByteColor.Black;
                b.BackgroundColor = ByteColor.White;
                b.SetText(t, sender.Rect.Right - (2 + t.Length));
            }

            b.FillRemaining();
        }

        public override void OnMouseEvent(MouseEvent e)
        {
            base.OnMouseEvent(e);
            Elements.OnMouseEvent(e);
        }

        public override void OnKeyEvent(KeyEvent e)
        {
            Debug.Log(e.ToString());

            base.OnKeyEvent(e);
            Elements.OnKeyEvent(e);

            if (e.IsDown == 1 && (e.AsciiChar == 9 || e.AsciiChar == 32))
            {
                if (this.CurrentlyJumping <= 0)
                {
                    this.CurrentlyJumping = this.NextCodeJumpCount;
                    this.NextCodeJumpCount = 1;
                }
                else
                {
                    this.NextCodeJumpCount = Math.Max(1, this.CurrentlyJumping);
                    this.CurrentlyJumping = 0;
                }
                return;
            }

            if (e.IsDown != 0 && e.AsciiChar == 43)
            {
                if (this.CurrentlyJumping > 0)
                { this.CurrentlyJumping++; }
                else
                { this.NextCodeJumpCount++; }
                return;
            }

            if (e.IsDown != 0 && e.AsciiChar == 45)
            {
                if (this.CurrentlyJumping > 0)
                {
                    this.CurrentlyJumping--;
                    this.CurrentlyJumping = Math.Max(this.CurrentlyJumping, 0);
                }
                else
                {
                    this.NextCodeJumpCount--;
                    this.NextCodeJumpCount = Math.Max(this.NextCodeJumpCount, 1);
                }
                return;
            }

            if (e.IsDown != 0 && e.AsciiChar == 42)
            {
                if (this.CurrentlyJumping > 0)
                { this.CurrentlyJumping = int.MaxValue; }
                else
                { this.NextCodeJumpCount = int.MaxValue; }
                return;
            }

            if (e.IsDown != 0 && e.AsciiChar == 47)
            {
                if (this.CurrentlyJumping > 0)
                { this.CurrentlyJumping = 0; }
                else
                { this.NextCodeJumpCount = 1; }
                return;
            }
        }

        public override void OnDestroy()
        {
            this.Interpreter?.Destroy();
        }

        public override void RefreshSize()
        {
            base.RefreshSize();
            Elements[0].Rect = Rect;
            Elements[0].RefreshSize();
        }
    }
}
