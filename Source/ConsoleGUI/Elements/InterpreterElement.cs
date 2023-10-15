using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using LanguageCore;
using LanguageCore.Runtime;
using Win32;

namespace ConsoleGUI
{
    internal sealed class InterpreterElement : WindowElement
    {
        public string File;
        InterpreterDebuggabble Interpreter;

        struct ConsoleLine
        {
            internal string Data;
            internal ForegroundColor Color;

            public ConsoleLine(string data, ForegroundColor color)
            {
                Data = data;
                Color = color;
            }
            public ConsoleLine(string data)
            {
                Data = data;
                Color = ForegroundColor.Default;
            }
        }

        readonly List<ConsoleLine> ConsoleLines = new();

        int ConsoleScrollOffset = 0;
        int NextCodeJumpCount = 1;
        int CurrentlyJumping = 0;
        MainThreadTimer InterpreterTimer;

        InterpreterElement() : base()
        {
            ClearBuffer();
            InitElements();
            HasBorder = false;
        }

        public InterpreterElement(string file, LanguageCore.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, LanguageCore.Parser.ParserSettings parserSettings, BytecodeInterpreterSettings interpreterSettings, bool handleErrors, string basePath) : this()
        {
            this.File = file;
            SetupInterpreter(compilerSettings, parserSettings, interpreterSettings, handleErrors, basePath);
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

        /*
        static void CalculateLayoutBoxes(
            out Rectangle StatePanelRect,
            out Rectangle ConsolePanelRect,
            out Rectangle CodePanelRect,
            out Rectangle StackPanelRect,
            out Rectangle HeapPanelRect,
            out Rectangle CallstackPanelRect
            )
        {
            Rectangle left = new(0, 0, Console.WindowWidth / 2, Console.WindowHeight);
            Rectangle right = new(left.Right + 1, 0, Console.WindowWidth - left.Right - 2, Console.WindowHeight);

            StatePanelRect = left;
            StatePanelRect.Height = 3;

            ConsolePanelRect = left;
            ConsolePanelRect.Y = StatePanelRect.Bottom + 1;
            ConsolePanelRect.Height = (left.Height - StatePanelRect.Height) / 2;

            CodePanelRect = left;
            CodePanelRect.Y = ConsolePanelRect.Bottom + 1;
            CodePanelRect.Height = left.Height - CodePanelRect.Y - 1;

            StackPanelRect = right;
            StackPanelRect.Height = right.Height / 3;

            HeapPanelRect = right;
            HeapPanelRect.Y = StackPanelRect.Bottom + 1;
            HeapPanelRect.Height = right.Height / 3;

            CallstackPanelRect = right;
            CallstackPanelRect.Y = HeapPanelRect.Bottom + 1;
            CallstackPanelRect.Height = right.Height - CallstackPanelRect.Y - 1;
        }
        */

        void InitElements()
        {
            /*
            CalculateLayoutBoxes(
                out Rectangle StatePanelRect,
                out Rectangle ConsolePanelRect,
                out Rectangle CodePanelRect,
                out Rectangle StackPanelRect,
                out Rectangle HeapPanelRect,
                out Rectangle CallstackPanelRect
                );

            Rectangle left = new(0, 0, Console.WindowWidth / 2, Console.WindowHeight);
            Rectangle right = new(left.Right + 1, 0, Console.WindowWidth - (left.Right + 1), Console.WindowHeight);

            var leftWidth = Console.WindowWidth / 2;

            Rectangle StatePanelRect = left;
            StatePanelRect.Height = 3;

            Rectangle ConsolePanelRect = left;
            ConsolePanelRect.Y = StatePanelRect.Bottom + 1;
            ConsolePanelRect.Height = (left.Height - StatePanelRect.Height) / 2;

            Rectangle CodePanelRect = left;
            CodePanelRect.Y = ConsolePanelRect.Bottom + 1;
            CodePanelRect.Height = left.Height - CodePanelRect.Y;

            var StackPanelRect = new Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth,
                (Console.WindowHeight - 1) / 2);
            var HeapPanelRect = new Rectangle(leftWidth + 1, StackPanelRect.Bottom + 1, Console.WindowWidth - 2 - leftWidth,
                (Console.WindowHeight - 2) - StackPanelRect.Bottom - 20);
            var CallstackPanelRect = new Rectangle(leftWidth + 1, StackPanelRect.Bottom + 1, Console.WindowWidth - 2 - leftWidth,
                (Console.WindowHeight - 3) - HeapPanelRect.Bottom);
            */

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

            var ConsolePanel = new InlineElement
            {
                HasBorder = true,
                Title = "Console",
            };
            ConsolePanel.OnBeforeDraw += ConsolePanel_OnBeforeDraw;
            ConsolePanel.OnMouseEventInvoked += (sender, e) =>
            {
                int a = -(ConsoleLines.Count - sender.Rect.Height);
                int b = 3;
                int min = Math.Min(a, b);
                int max = Math.Max(a, b);
                if (e.ButtonState == MouseButton.ScrollDown)
                {
                    ConsoleScrollOffset++;
                    ConsoleScrollOffset = Math.Clamp(ConsoleScrollOffset, min, max);
                }
                else if (e.ButtonState == MouseButton.ScrollUp)
                {
                    ConsoleScrollOffset--;
                    ConsoleScrollOffset = Math.Clamp(ConsoleScrollOffset, min, max);
                }
            };

            var StackPanel = new InlineElement
            {
                HasBorder = true,
                Title = "Stack",
            };
            StackPanel.OnBeforeDraw += StackElement_OnBeforeDraw;

            var HeapPanel = new InlineElement
            {
                HasBorder = true,
                Title = "HEAP",
                Layout = InlineLayout.Stretchy(150),
            };
            HeapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;

            var CallstackPanel = new InlineElement
            {
                HasBorder = true,
                Title = "Call Stack",
            };
            CallstackPanel.OnBeforeDraw += CallstackElement_OnBeforeDraw;

            this.Elements = new IElement[]{
            new HorizontalLayoutElement()
                {
                    Rect = new Rectangle(0, 0, Console.WindowWidth, Console.WindowHeight),
                    Elements = new IElement[]
                    {
                        new VerticalLayoutElement()
                        {
                            Elements = new IElement[]
                            {
                                StatePanel,
                                ConsolePanel,
                                CodePanel,
                            }
                        },
                        new VerticalLayoutElement()
                        {
                            Elements = new IElement[]
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

        void SetupInterpreter() => SetupInterpreter(LanguageCore.BBCode.Compiler.Compiler.CompilerSettings.Default, LanguageCore.Parser.ParserSettings.Default, BytecodeInterpreterSettings.Default, false, string.Empty);
        void SetupInterpreter(LanguageCore.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, LanguageCore.Parser.ParserSettings parserSettings, BytecodeInterpreterSettings interpreterSettings, bool handleErrors, string basePath)
        {
            this.InterpreterTimer = new MainThreadTimer(200);
            this.InterpreterTimer.Elapsed += () =>
            {
                if (this.CurrentlyJumping <= 0) return;

                this.CurrentlyJumping--;
                this.Interpreter.DoUpdate();
                if (!this.Interpreter.IsExecutingCode)
                {
                    ConsoleGUI.Instance.Destroy();
                    return;
                }
                ConsoleGUI.Instance.NextRefreshConsole = true;
            };
            this.InterpreterTimer.Enabled = true;

            var fileInfo = new FileInfo(File);
            var code = System.IO.File.ReadAllText(fileInfo.FullName);
            this.Interpreter = new InterpreterDebuggabble();

            void PrintOutput(string message, LogType logType)
            {
                ConsoleLines.Add(new ConsoleLine(message + "\n", logType switch
                {
                    LogType.System => ForegroundColor.Default,
                    LogType.Normal => ForegroundColor.Default,
                    LogType.Warning => ForegroundColor.Yellow,
                    LogType.Error => ForegroundColor.Red,
                    LogType.Debug => ForegroundColor.Gray,
                    _ => ForegroundColor.Default,
                }));
            }

            Interpreter.OnOutput += (_, p1, p2) => PrintOutput(p1, p2);

            Interpreter.OnStdOut += (sender, data) => ConsoleLines.Add(new ConsoleLine(data));
            Interpreter.OnStdError += (sender, data) => ConsoleLines.Add(new ConsoleLine(data, ForegroundColor.Red));

            Interpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey();
                sender.OnInput(input.KeyChar);
            };

            if (Interpreter.Initialize())
            {
                LanguageCore.BBCode.Compiler.CodeGenerator.Result? compiledCode = LanguageCore.BBCode.EasyCompiler.Compile(
                    fileInfo,
                    Interpreter.GenerateExternalFunctions(),
                    LanguageCore.Tokenizing.TokenizerSettings.Default,
                    parserSettings,
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

            int[] calltraceRaw = this.Interpreter.BytecodeInterpreter.GetContext().CallTrace;

            FunctionInformations[] callstack = this.Interpreter.CompilerResult.DebugInfo.GetFunctionInformations(calltraceRaw);

            int i;
            for (i = 0; i < callstack.Length; i++)
            {
                FunctionInformations callframe = callstack[i];

                sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                sender.DrawBuffer.AddText(" ");

                sender.DrawBuffer.AddText(new string(' ', 3 - i.ToString().Length));

                sender.DrawBuffer.AddText(i.ToString());
                sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                sender.DrawBuffer.BackgroundColor = BackgroundColor.Black;

                if (!callframe.IsValid)
                {
                    sender.DrawBuffer.ForegroundColor = ForegroundColor.Cyan;
                    sender.DrawBuffer.AddText(calltraceRaw[i].ToString());
                }
                else
                {
                    if (callframe.IsMacro)
                    {
                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Blue;
                        sender.DrawBuffer.AddText("macro ");
                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                    }

                    if (callframe.ReadableIdentifier.Contains('('))
                    {
                        string functionName = callframe.ReadableIdentifier[..callframe.ReadableIdentifier.IndexOf('(')];

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Yellow;
                        sender.DrawBuffer.AddText($"{functionName}");

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
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
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                                sender.DrawBuffer.AddText($", ");
                            }

                            string param = parameters2[j];
                            if (LanguageCore.Constants.BuiltinTypes.Contains(param))
                            {
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Blue;
                            }
                            else
                            {
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                            }
                            sender.DrawBuffer.AddText($"{param}");
                        }

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                        sender.DrawBuffer.AddChar(')');

                        sender.DrawBuffer.ResetColor();
                    }
                    else
                    {
                        sender.DrawBuffer.AddText($"{callframe.ReadableIdentifier}");
                    }
                }

                sender.DrawBuffer.BackgroundColor = BackgroundColor.Black;
                sender.DrawBuffer.FinishLine(sender.Rect.Width);
                sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
            }

            {
                FunctionInformations callframe = this.Interpreter.CompilerResult.DebugInfo.GetFunctionInformations(this.Interpreter.BytecodeInterpreter.CodePointer);

                if (callframe.IsValid)
                {
                    sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                    sender.DrawBuffer.AddText(" ");

                    sender.DrawBuffer.AddText(new string(' ', 3 - (i + 1).ToString().Length));

                    sender.DrawBuffer.AddText((i + 1).ToString());
                    sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                    sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                    sender.DrawBuffer.BackgroundColor = BackgroundColor.Black;

                    if (callframe.IsMacro)
                    {
                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Blue;
                        sender.DrawBuffer.AddText("macro ");
                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                    }

                    if (callframe.ReadableIdentifier.Contains('('))
                    {
                        string functionName = callframe.ReadableIdentifier[..callframe.ReadableIdentifier.IndexOf('(')];

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Yellow;
                        sender.DrawBuffer.AddText($"{functionName}");

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
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
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                                sender.DrawBuffer.AddText($", ");
                            }

                            string param = parameters2[j];
                            if (LanguageCore.Constants.BuiltinTypes.Contains(param))
                            {
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Blue;
                            }
                            else
                            {
                                sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                            }
                            sender.DrawBuffer.AddText($"{param}");
                        }

                        sender.DrawBuffer.ForegroundColor = ForegroundColor.Gray;
                        sender.DrawBuffer.AddChar(')');

                        sender.DrawBuffer.ResetColor();
                    }
                    else
                    {
                        sender.DrawBuffer.AddText($"{callframe.ReadableIdentifier}");
                    }

                    sender.DrawBuffer.ForegroundColor = ForegroundColor.DarkGray;
                    sender.DrawBuffer.AddText($" (current)");

                    sender.DrawBuffer.BackgroundColor = BackgroundColor.Black;
                    sender.DrawBuffer.FinishLine(sender.Rect.Width);
                    sender.DrawBuffer.ForegroundColor = ForegroundColor.Default;
                }
            }

        }

        private void ConsolePanel_OnBeforeDraw(InlineElement sender)
        {
            DrawBuffer b = sender.DrawBuffer;
            b.StepTo(0);

            b.ResetColor();

            bool lineFinished = true;
            for (int i = Math.Max(0, ConsoleLines.Count - sender.Rect.Height + ConsoleScrollOffset); i < ConsoleLines.Count; i++)
            {
                var line = ConsoleLines[i];

                if (lineFinished) b.AddChar(' ');
                b.ForegroundColor = line.Color;
                b.AddText(line.Data.TrimEnd());
                b.ForegroundColor = ForegroundColor.Default;
                b.BackgroundColor = BackgroundColor.Black;

                lineFinished = line.Data.EndsWith('\n');

                if (lineFinished) b.FinishLine(sender.Rect.Width);
            }
        }

        private void StateElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            b.AddText("  ");
            b.AddText($"IsRunning: {!this.Interpreter.BytecodeInterpreter.IsDone}");
            b.BackgroundColor = BackgroundColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ForegroundColor.Default;

            b.AddText("  ");
            if (this.Interpreter.BytecodeInterpreter.CodePointer == this.Interpreter.CompilerResult.Code.Length)
            {
                b.AddText($"State: {this.Interpreter.State}");
            }
            else
            {
                b.AddText($"State: Running...");
            }
            b.BackgroundColor = BackgroundColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ForegroundColor.Default;

            b.AddText("  ");


            if (this.Interpreter.StackOperation)
            {
                b.BackgroundColor = BackgroundColor.White;
                b.ForegroundColor = ForegroundColor.Black;
                b.AddText($"STACK");
                b.BackgroundColor = BackgroundColor.Black;
                b.ForegroundColor = ForegroundColor.Default;
            }
            else
            {
                b.AddText($"STACK");
            }

            b.AddText("  ");


            if (this.Interpreter.HeapOperation)
            {
                b.BackgroundColor = BackgroundColor.White;
                b.ForegroundColor = ForegroundColor.Black;
                b.AddText($"HEAP");
                b.BackgroundColor = BackgroundColor.Black;
                b.ForegroundColor = ForegroundColor.Default;
            }
            else
            {
                b.AddText($"HEAP");
            }

            b.AddText("  ");


            if (this.Interpreter.AluOperation)
            {
                b.BackgroundColor = BackgroundColor.White;
                b.ForegroundColor = ForegroundColor.Black;
                b.AddText($"ALU");
                b.BackgroundColor = BackgroundColor.Black;
                b.ForegroundColor = ForegroundColor.Default;
            }
            else
            {
                b.AddText($"ALU");
            }

            b.AddText("  ");


            if (this.Interpreter.ExternalFunctionOperation)
            {
                b.BackgroundColor = BackgroundColor.White;
                b.ForegroundColor = ForegroundColor.Black;
                b.AddText($"EXTERN F.");
                b.BackgroundColor = BackgroundColor.Black;
                b.ForegroundColor = ForegroundColor.Default;
            }
            else
            {
                b.AddText($"EXTERN F.");
            }

            b.AddText("  ");

            b.BackgroundColor = BackgroundColor.Black;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = ForegroundColor.Default;
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
                    { storeIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].ValueInt); }
                    else
                    { storeIndicators.Add(instruction.ParameterInt); }
                }

                if (instruction.opcode == Opcode.HEAP_GET)
                {
                    if (instruction.AddressingMode == AddressingMode.RUNTIME)
                    {
                        if (this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].Type == RuntimeType.INT)
                        { loadIndicators.Add(this.Interpreter.BytecodeInterpreter.Memory.Stack[^1].ValueInt); }
                    }
                    else
                    { loadIndicators.Add(instruction.ParameterInt); }
                }
            }

            int nextHeader = 0;
            for (int i = 0; i < this.Interpreter.BytecodeInterpreter.Memory.Heap.Size; i++)
            {
                var item = this.Interpreter.BytecodeInterpreter.Memory.Heap[i];
                bool isHeader = (nextHeader == i) && (!this.Interpreter.BytecodeInterpreter.Memory.Heap[i].IsNull) && (this.Interpreter.BytecodeInterpreter.Memory.Heap is not null);
                (int, bool) header = (default, default);

                if (isHeader)
                {
                    header = HEAP.GetHeader(item);
                    nextHeader += header.Item1 + HEAP.BLOCK_HEADER_SIZE;
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("○");
                    b.ForegroundColor = ForegroundColor.Gray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("●");
                    b.ForegroundColor = ForegroundColor.Gray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                if (((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length > 0) b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                b.ForegroundColor = ForegroundColor.Gray;
                b.AddText(i.ToString());
                b.ForegroundColor = ForegroundColor.White;
                b.AddSpace(5, sender.Rect.Width);

                if (isHeader)
                {
                    b.BackgroundColor = BackgroundColor.DarkGray;
                    b.AddText("HEADER | ");
                    b.AddText(header.Item1.ToString());
                    b.AddText(" | ");
                    if (header.Item2)
                    {
                        b.BackgroundColor = BackgroundColor.Yellow;
                        b.ForegroundColor = ForegroundColor.Black;
                    }
                    else
                    {
                        b.BackgroundColor = BackgroundColor.Green;
                        b.ForegroundColor = ForegroundColor.White;
                    }
                    b.AddText(header.Item2 ? "USED" : "FREE");
                }
                else
                {
                    if (item.IsNull)
                    {
                        b.ForegroundColor = ForegroundColor.DarkGray;
                        b.AddText("<null>");
                    }
                    else
                    {
                        switch (item.Type)
                        {
                            case RuntimeType.BYTE:
                                b.ForegroundColor = ForegroundColor.Cyan;
                                b.AddText($"{item.ValueByte}");
                                break;
                            case RuntimeType.INT:
                                b.ForegroundColor = ForegroundColor.Cyan;
                                b.AddText($"{item.ValueInt}");
                                break;
                            case RuntimeType.FLOAT:
                                b.ForegroundColor = ForegroundColor.Cyan;
                                b.AddText($"{item.ValueFloat}f");
                                break;
                            case RuntimeType.CHAR:
                                b.ForegroundColor = ForegroundColor.Yellow;
                                b.AddText($"'{item.ValueChar.Escape()}'");
                                break;
                            default:
                                b.ForegroundColor = ForegroundColor.Gray;
                                b.AddText("?");
                                break;
                        }
                    }
                }

                b.BackgroundColor = BackgroundColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ForegroundColor.Default;
            }
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

            int stackDrawStart = 0;
            int stackDrawEnd = stackSize;

            int notVisible = Math.Max(stackDrawEnd - (sender.Rect.Height - 3), 0);

            stackDrawStart += notVisible;

            void DrawElement(int i, DataItem item)
            {
                if (this.Interpreter.BytecodeInterpreter.BasePointer == i)
                {
                    b.ForegroundColor = ForegroundColor.Blue;
                    b.AddText("►");
                    basepointerShown = true;
                    b.ForegroundColor = ForegroundColor.Gray;
                }
                else if (savedBasePointers.Contains(i))
                {
                    b.ForegroundColor = ForegroundColor.Gray;
                    b.AddText("►");
                    b.ForegroundColor = ForegroundColor.Gray;
                }
                else
                {
                    b.ForegroundColor = ForegroundColor.Gray;
                    b.AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("○");
                    b.ForegroundColor = ForegroundColor.Gray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("●");
                    b.ForegroundColor = ForegroundColor.Gray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                b.AddText(i.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.ForegroundColor = ForegroundColor.Default;
                b.BackgroundColor = BackgroundColor.Black;

                if (item.IsNull)
                {
                    b.ForegroundColor = ForegroundColor.DarkGray;
                    b.AddText("<null>");
                }
                else
                {
                    switch (item.Type)
                    {
                        case RuntimeType.BYTE:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{item.ValueByte}");
                            break;
                        case RuntimeType.INT:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{item.ValueInt}");
                            break;
                        case RuntimeType.FLOAT:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{item.ValueFloat}f");
                            break;
                        case RuntimeType.CHAR:
                            b.ForegroundColor = ForegroundColor.Yellow;
                            b.AddText($"'{item.ValueChar.Escape()}'");
                            break;
                        default:
                            b.ForegroundColor = ForegroundColor.Gray;
                            b.AddText("?");
                            break;
                    }
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
                            b.ForegroundColor = ForegroundColor.Default;

                            DrawElement(i, item);

                            b.ForegroundColor = ForegroundColor.DarkGray;
                            b.AddText($" ({itemDebugInfo.Kind.ToString().ToLowerInvariant()}) {itemDebugInfo.Tag}");
                        }
                        else if (range.Start == i)
                        {
                            b.ForegroundColor = ForegroundColor.DarkGray;
                            b.AddText($" ({itemDebugInfo.Kind.ToString().ToLowerInvariant()}) {itemDebugInfo.Tag} {{");

                            b.BackgroundColor = BackgroundColor.Black;
                            b.FinishLine(sender.Rect.Width);
                            b.ForegroundColor = ForegroundColor.Default;

                            DrawElement(i, item);
                        }
                        else if (range.End == i)
                        {
                            DrawElement(i, item);

                            b.BackgroundColor = BackgroundColor.Black;
                            b.FinishLine(sender.Rect.Width);
                            b.ForegroundColor = ForegroundColor.DarkGray;
                            b.AddText($" }}");
                        }
                        else
                        {
                            DrawElement(i, item);
                        }
                    }
                    else if (itemDebugInfo.Kind == StackElementKind.Internal)
                    {
                        DrawElement(i, item);

                        b.ForegroundColor = ForegroundColor.DarkGray;
                        b.AddText($" {itemDebugInfo.Tag}");
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

                b.BackgroundColor = BackgroundColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ForegroundColor.Default;
            }

            while ((basepointerShown == false && i <= this.Interpreter.BytecodeInterpreter.BasePointer) || loadIndicators.Count > 0 || storeIndicators.Count > 0)
            {
                if (this.Interpreter.BytecodeInterpreter.BasePointer == i)
                {
                    b.ForegroundColor = ForegroundColor.Blue;
                    b.AddText("►");
                    basepointerShown = true;
                    b.ForegroundColor = ForegroundColor.Gray;
                }
                else
                {
                    b.ForegroundColor = ForegroundColor.Gray;
                    b.AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("○");
                    b.ForegroundColor = ForegroundColor.Gray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = ForegroundColor.Red;
                    b.AddText("●");
                    b.ForegroundColor = ForegroundColor.Gray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                b.AddText(i.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.BackgroundColor = BackgroundColor.Black;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ForegroundColor.Default;

                i++;

                break;
            }

        }
        private void SourceCodeElement_OnBeforeDraw(InlineElement sender)
        {
            // sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.BytecodeInterpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            void LinePrefix(string lineNumber = "")
            {
                b.AddText(new string(' ', 4 - lineNumber.Length));
                b.ForegroundColor = ForegroundColor.Gray;
                b.AddText(lineNumber);
                b.ForegroundColor = ForegroundColor.Default;
                b.AddSpace(5, sender.Rect.Width);
            }

            int indent = 0;
            for (int i = 0; i < this.Interpreter.BytecodeInterpreter.CodePointer - 5; i++)
            {
                if (Interpreter.CompilerResult.DebugInfo.CodeComments.TryGetValue(i, out var comments))
                {
                    for (int j = 0; j < comments.Count; j++)
                    {
                        if (!comments[j].EndsWith("{ }") && comments[j].EndsWith("}"))
                        { indent--; }
                        if (!comments[j].EndsWith("{ }") && comments[j].EndsWith("{"))
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

                        if (!comment.EndsWith("{ }") && comment.EndsWith("}"))
                        {
                            indent--;
                        }

                        LinePrefix();
                        b.ForegroundColor = ForegroundColor.DarkGray;
                        b.AddText($"{new string(' ', Math.Max(0, indent * 2))}{comment}");
                        b.ForegroundColor = ForegroundColor.Default;
                        b.BackgroundColor = BackgroundColor.Black;
                        b.FinishLine(sender.Rect.Width);

                        if (!comment.EndsWith("{ }") && comment.EndsWith("{"))
                        {
                            indent++;
                        }
                    }
                }

                LinePrefix((i + 1).ToString());
                b.ForegroundColor = ForegroundColor.Yellow;
                b.AddText($"{new string(' ', Math.Max(0, indent * 2))} ");
                if (IsNextInstruction)
                {
                    IsNextInstruction = false;
                    b.BackgroundColor = BackgroundColor.Red;
                }
                b.AddText($"{instruction.opcode}");
                b.AddText($" ");

                if (instruction.opcode == Opcode.LOAD_VALUE ||
                    instruction.opcode == Opcode.STORE_VALUE ||
                    instruction.opcode == Opcode.HEAP_GET ||
                    instruction.opcode == Opcode.HEAP_SET)
                {
                    b.AddText($"{instruction.AddressingMode}");
                    b.AddText($" ");
                }

                if (!instruction.Parameter.IsNull) switch (instruction.Parameter.Type)
                    {
                        case RuntimeType.BYTE:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{instruction.Parameter.ValueByte}");
                            b.AddText($" ");
                            break;
                        case RuntimeType.INT:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{instruction.Parameter.ValueInt}");
                            b.AddText($" ");
                            break;
                        case RuntimeType.FLOAT:
                            b.ForegroundColor = ForegroundColor.Cyan;
                            b.AddText($"{instruction.Parameter.ValueFloat}f");
                            b.AddText($" ");
                            break;
                        case RuntimeType.CHAR:
                            b.ForegroundColor = ForegroundColor.Yellow;
                            b.AddText($"'{instruction.Parameter.ValueChar.Escape()}'");
                            b.AddText($" ");
                            break;
                        default:
                            b.ForegroundColor = ForegroundColor.White;
                            b.AddText($"{instruction.Parameter}");
                            b.AddText($" ");
                            break;
                    }

                /*
                if (!string.IsNullOrEmpty(instruction.tag))
                {
                    b.ForegroundColor = IsNextInstruction ? ForegroundColor.Black : ForegroundColor.Gray;
                    b.AddText($"{instruction.tag}");
                }
                */

                b.BackgroundColor = BackgroundColor.Black;

                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = ForegroundColor.Default;
            }

            {
                string t = CurrentlyJumping == 0 ? $" Next Jump Count: {NextCodeJumpCount} " : $" Jumping: {CurrentlyJumping} ";
                b.ForegroundColor = ForegroundColor.Black;
                b.BackgroundColor = BackgroundColor.White;
                b.SetText(t, sender.Rect.Right - (2 + t.Length));
            }

            b.FillRemaing();
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
                {
                    this.CurrentlyJumping++;
                }
                else
                {
                    this.NextCodeJumpCount++;
                }
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
                {
                    this.CurrentlyJumping = int.MaxValue;
                }
                else
                {
                    this.NextCodeJumpCount = int.MaxValue;
                }
                return;
            }

            if (e.IsDown != 0 && e.AsciiChar == 47)
            {
                if (this.CurrentlyJumping > 0)
                {
                    this.CurrentlyJumping = 0;
                }
                else
                {
                    this.NextCodeJumpCount = 1;
                }
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
