using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace ConsoleGUI
{
    using IngameCoding.Core;
    using IngameCoding.Output.Debug;

    using System.Timers;

    internal sealed class InterpreterElement : WindowElement
    {
        public string File;
        InterpreterDebuggabble Interpreter;

        struct ConsoleLine
        {
            internal string Data;
            internal CharColors Color;

            public ConsoleLine(string data, CharColors color)
            {
                Data = data;
                Color = color;
            }
            public ConsoleLine(string data)
            {
                Data = data;
                Color = CharColors.FgDefault;
            }
        }

        readonly List<ConsoleLine> ConsoleLines = new();

        int ConsoleScrollOffset = 0;
        int NextCodeJumpCount = 1;
        int CurrentlyJumping = 0;
        private Timer InterpreterTimer;

        InterpreterElement() : base()
        {
            ClearBuffer();
            InitElements();
            HasBorder = false;
        }

        public InterpreterElement(string file, IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors) : this()
        {
            this.File = file;
            SetupInterpreter(compilerSettings, parserSettings, interpreterSettings, handleErrors);
        }

        public InterpreterElement(string file) : this()
        {
            this.File = file;
            SetupInterpreter();
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
                Layout = new InlineLayout(InlineLayoutSizeMode.Fixed, 3),
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
                if (e.ButtonState == MouseButtonState.ScrollDown)
                {
                    ConsoleScrollOffset++;
                    ConsoleScrollOffset = Math.Clamp(ConsoleScrollOffset, min, max);
                }
                else if (e.ButtonState == MouseButtonState.ScrollUp)
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
            return;
            this.Elements = new InlineElement[]
            {
                CodePanel,
                StackPanel,
                HeapPanel,
                StatePanel,
                ConsolePanel,
                CallstackPanel,
            };
        }

        void SetupInterpreter() => SetupInterpreter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings.Default, IngameCoding.BBCode.Parser.ParserSettings.Default, IngameCoding.Bytecode.BytecodeInterpreterSettings.Default, false);
        void SetupInterpreter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors)
        {
            this.InterpreterTimer = new Timer(200);
            this.InterpreterTimer.Elapsed += (sender, e) =>
            {
                if (this.CurrentlyJumping <= 0) return;

                this.CurrentlyJumping--;
                this.Interpreter.Update();
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

            Interpreter.OnOutput += (sender, message, logType) => ConsoleLines.Add(new ConsoleLine(message + "\n", logType switch
            {
                IngameCoding.Output.LogType.System => CharColors.FgDefault,
                IngameCoding.Output.LogType.Normal => CharColors.FgDefault,
                IngameCoding.Output.LogType.Warning => CharColors.FgYellow,
                IngameCoding.Output.LogType.Error => CharColors.FgRed,
                IngameCoding.Output.LogType.Debug => CharColors.FgGray,
                _ => CharColors.FgDefault,
            }));

            Interpreter.OnStdOut += (sender, data) => ConsoleLines.Add(new ConsoleLine(data));
            Interpreter.OnStdError += (sender, data) => ConsoleLines.Add(new ConsoleLine(data, CharColors.FgRed));

            Interpreter.OnNeedInput += (sender) =>
            {
                var input = Console.ReadKey();
                sender.OnInput(input.KeyChar);
            };

            if (Interpreter.Initialize())
            {
                var compiledCode = Interpreter.CompileCode(code, fileInfo, compilerSettings, parserSettings, handleErrors);

                if (compiledCode != null)
                {
                    Interpreter.RunCode(compiledCode, new IngameCoding.Bytecode.BytecodeInterpreterSettings()
                    {
                        ClockCyclesPerUpdate = 1,
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

            if (this.Interpreter.Details.Interpreter == null) return;

            sender.DrawBuffer.ResetColor();

            IngameCoding.Bytecode.Instruction instruction = this.Interpreter.Details.NextInstruction;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.CS_POP)
                {
                    loadIndicators.Add(this.Interpreter.Details.Interpreter.CallStack.Length - 1);
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.CS_PUSH)
                {
                    storeIndicators.Add(this.Interpreter.Details.Interpreter.CallStack.Length);
                }
            }

            int i;
            for (i = 0; i < this.Interpreter.Details.Interpreter.CallStack.Length; i++)
            {
                IngameCoding.Bytecode.CallStackFrame frame = new(this.Interpreter.Details.Interpreter.CallStack[i]);

                sender.DrawBuffer.ForegroundColor = CharColors.FgGray;
                sender.DrawBuffer.AddText(" ");

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    sender.DrawBuffer.ForegroundColor = CharColors.FgRed;
                    sender.DrawBuffer.AddText("○");
                    sender.DrawBuffer.ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    sender.DrawBuffer.ForegroundColor = CharColors.FgRed;
                    sender.DrawBuffer.AddText("●");
                    sender.DrawBuffer.ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                sender.DrawBuffer.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                sender.DrawBuffer.AddText(i.ToString());
                sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                sender.DrawBuffer.ForegroundColor = CharColors.FgDefault;
                sender.DrawBuffer.BackgroundColor = CharColors.BgBlack;

                sender.DrawBuffer.AddText($"{frame.Function}");

                sender.DrawBuffer.BackgroundColor = CharColors.BgBlack;
                sender.DrawBuffer.FinishLine(sender.Rect.Width);
                sender.DrawBuffer.ForegroundColor = CharColors.FgDefault;
            }

            while (loadIndicators.Count > 0 || storeIndicators.Count > 0)
            {
                sender.DrawBuffer.ForegroundColor = CharColors.FgDefault;
                sender.DrawBuffer.AddText(" ");

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    sender.DrawBuffer.ForegroundColor = CharColors.FgRed;
                    sender.DrawBuffer.AddText("○");
                    sender.DrawBuffer.ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    sender.DrawBuffer.ForegroundColor = CharColors.FgRed;
                    sender.DrawBuffer.AddText("●");
                    sender.DrawBuffer.ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                sender.DrawBuffer.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                sender.DrawBuffer.AddText(i.ToString());
                sender.DrawBuffer.AddSpace(5, sender.Rect.Width);

                sender.DrawBuffer.ResetColor();

                sender.DrawBuffer.FinishLine(sender.Rect.Width);
                break;
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
                b.ForegroundColor = CharColors.FgDefault;
                b.BackgroundColor = CharColors.BgBlack;

                lineFinished = line.Data.EndsWith('\n');

                if (lineFinished) b.FinishLine(sender.Rect.Width);
            }
        }

        private void StateElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.Details.Interpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            b.AddText("  ");
            b.AddText($"IsRunning: {this.Interpreter.Details.Interpreter.IsRunning}");
            b.BackgroundColor = CharColors.BgBlack;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = CharColors.FgDefault;

            b.AddText("  ");
            if (this.Interpreter.Details.Interpreter.CodePointer == this.Interpreter.Details.CompilerResult.compiledCode.Length)
            {
                b.AddText($"State: {this.Interpreter.Details.State}");
            }
            else
            {
                b.AddText($"State: Running...");
            }
            b.BackgroundColor = CharColors.BgBlack;
            b.FinishLine(sender.Rect.Width);
            b.ForegroundColor = CharColors.FgDefault;
        }

        private void HeapElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.Details.Interpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            IngameCoding.Bytecode.Instruction instruction = this.Interpreter.Details.NextInstruction;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_SET)
                {
                    if (instruction.AddressingMode == IngameCoding.Bytecode.AddressingMode.RUNTIME)
                    { storeIndicators.Add(this.Interpreter.Details.Interpreter.Stack[^1].ValueInt); }
                    else
                    { storeIndicators.Add(instruction.ParameterInt); }
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_GET)
                {
                    if (instruction.AddressingMode == IngameCoding.Bytecode.AddressingMode.RUNTIME)
                    { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack[^1].ValueInt); }
                    else
                    { loadIndicators.Add(instruction.ParameterInt); }
                }
            }

            for (int i = 0; i < this.Interpreter.Details.Interpreter.Heap.Length; i++)
            {
                var item = this.Interpreter.Details.Interpreter.Heap[i];

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("○");
                    b.ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("●");
                    b.ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                if (((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length > 0) b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));


                b.ForegroundColor = CharColors.FgGray;
                b.AddText(i.ToString());
                b.ForegroundColor = CharColors.FgDefault;
                b.AddSpace(5, sender.Rect.Width);

                if (item.IsNull)
                {
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText("<null>");
                }
                else
                {
                    switch (item.type)
                    {
                        case IngameCoding.Bytecode.RuntimeType.INT:
                            b.ForegroundColor = CharColors.FgCyan;
                            b.AddText($"{item.ValueInt}");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.FLOAT:
                            b.ForegroundColor = CharColors.FgCyan;
                            b.AddText($"{item.ValueFloat}f");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.CHAR:
                            b.ForegroundColor = CharColors.FgYellow;
                            b.AddText($"'{item.ValueChar.Escape()}'");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.BOOLEAN:
                            b.ForegroundColor = CharColors.FgDarkBlue;
                            b.AddText($"{item.ValueBoolean}");
                            break;
                        default:
                            b.ForegroundColor = CharColors.FgGray;
                            b.AddText("?");
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(item.Tag))
                {
                    b.AddText($" ");
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText(item.Tag);
                }

                b.BackgroundColor = CharColors.BgBlack;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = CharColors.FgDefault;
            }
        }
        private void StackElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.Details.Interpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            IngameCoding.Bytecode.Instruction instruction = this.Interpreter.Details.NextInstruction;

            List<int> savedBasePointers = new();

            for (int j = 0; j < this.Interpreter.Details.Interpreter.Stack.Length; j++)
            {
                var item = this.Interpreter.Details.Interpreter.Stack[j];
                if (item.type != IngameCoding.Bytecode.RuntimeType.INT) continue;
                if (item.Tag != "saved base pointer") continue;
                savedBasePointers.Add(item.ValueInt);
            }

            bool basepointerShown = false;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE)
                {
                    storeIndicators.Add(this.Interpreter.Details.Interpreter.GetAddress((int)(instruction.Parameter ?? 0), instruction.AddressingMode));
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_SET)
                {
                    if (instruction.AddressingMode == IngameCoding.Bytecode.AddressingMode.RUNTIME)
                    { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 2); }
                    else
                    { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 1); }
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_VALUE)
                {
                    loadIndicators.Add(this.Interpreter.Details.Interpreter.GetAddress((int)(instruction.Parameter ?? 0), instruction.AddressingMode));
                    storeIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length);
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.PUSH_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.GET_BASEPOINTER ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_GET)
                { storeIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length); }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.POP_VALUE)
                { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 1); }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.MATH_ADD ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.MATH_DIV ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.MATH_MOD ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.MATH_MULT ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.MATH_SUB ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.LOGIC_AND ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.LOGIC_OR)
                {
                    loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 1);
                    storeIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 2);
                }
            }

            int i;
            for (i = 0; i < this.Interpreter.Details.Interpreter.Stack.Length; i++)
            {
                var item = this.Interpreter.Details.Interpreter.Stack[i];

                if (this.Interpreter.Details.Interpreter.BasePointer == i)
                {
                    b.ForegroundColor = CharColors.FgLightBlue;
                    b.AddText("►");
                    basepointerShown = true;
                    b.ForegroundColor = CharColors.FgGray;
                }
                else if (savedBasePointers.Contains(i))
                {
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText("►");
                    b.ForegroundColor = CharColors.FgGray;
                }
                else
                {
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("○");
                    b.ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("●");
                    b.ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                b.AddText(i.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.ForegroundColor = CharColors.FgDefault;
                b.BackgroundColor = CharColors.BgBlack;

                if (item.IsNull)
                {
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText("<null>");
                }
                else
                {
                    switch (item.type)
                    {
                        case IngameCoding.Bytecode.RuntimeType.INT:
                            b.ForegroundColor = CharColors.FgCyan;
                            b.AddText($"{item.ValueInt}");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.FLOAT:
                            b.ForegroundColor = CharColors.FgCyan;
                            b.AddText($"{item.ValueFloat}f");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.CHAR:
                            b.ForegroundColor = CharColors.FgYellow;
                            b.AddText($"'{item.ValueChar.Escape()}'");
                            break;
                        case IngameCoding.Bytecode.RuntimeType.BOOLEAN:
                            b.ForegroundColor = CharColors.FgLightBlue;
                            b.AddText($"{item.ValueBoolean}");
                            break;
                        default:
                            b.ForegroundColor = CharColors.FgGray;
                            b.AddText("?");
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(item.Tag))
                {
                    b.AddText($" ");
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText(item.Tag);
                }

                if (this.Interpreter.Details.Interpreter.BasePointer == i && b.ForegroundColor == CharColors.FgGray)
                {
                    b.ForegroundColor = CharColors.FgBlack;
                }

                b.BackgroundColor = CharColors.BgBlack;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = CharColors.FgDefault;
            }

            while ((basepointerShown == false && i <= this.Interpreter.Details.Interpreter.BasePointer) || loadIndicators.Count > 0 || storeIndicators.Count > 0)
            {
                if (this.Interpreter.Details.Interpreter.BasePointer == i)
                {
                    b.ForegroundColor = CharColors.FgLightBlue;
                    b.AddText("►");
                    basepointerShown = true;
                    b.ForegroundColor = CharColors.FgGray;
                }
                else
                {
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("○");
                    b.ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    b.ForegroundColor = CharColors.FgRed;
                    b.AddText("●");
                    b.ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                b.AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                b.AddText(i.ToString());
                b.AddSpace(5, sender.Rect.Width);

                b.BackgroundColor = CharColors.BgBlack;
                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = CharColors.FgDefault;

                i++;

                break;
            }

        }
        private void SourceCodeElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();
            sender.DrawBuffer.StepTo(0);

            if (this.Interpreter.Details.Interpreter == null) return;

            DrawBuffer b = sender.DrawBuffer;

            b.ResetColor();

            void LinePrefix(string lineNumber = "")
            {
                b.AddText(new string(' ', 4 - lineNumber.Length));
                b.ForegroundColor = CharColors.FgGray;
                b.AddText(lineNumber);
                b.ForegroundColor = CharColors.FgDefault;
                b.AddSpace(5, sender.Rect.Width);
            }

            int indent = 0;
            for (int i = 0; i < this.Interpreter.Details.Interpreter.CodePointer - 5; i++)
            {
                var instruction = this.Interpreter.Details.CompilerResult.compiledCode[i];
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.COMMENT)
                {
                    if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("}"))
                    { indent--; }
                    if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("{"))
                    { indent++; }

                    continue;
                }
            }

            bool IsNextInstruction = false;
            for (int i = Math.Max(0, this.Interpreter.Details.Interpreter.CodePointer - 5); i < this.Interpreter.Details.CompilerResult.compiledCode.Length; i++)
            {
                if (Interpreter.Details.Interpreter != null) if (Interpreter.Details.Interpreter.CodePointer == i) IsNextInstruction = true;

                var instruction = this.Interpreter.Details.CompilerResult.compiledCode[i];
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.COMMENT)
                {
                    if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("}"))
                    {
                        indent--;
                    }

                    LinePrefix((i + 1).ToString());
                    b.ForegroundColor = CharColors.FgGray;
                    b.AddText($"{new string(' ', Math.Max(0, indent * 2))}{instruction.tag}");
                    b.ForegroundColor = CharColors.FgDefault;
                    b.BackgroundColor = CharColors.BgBlack;
                    b.FinishLine(sender.Rect.Width);

                    if (!instruction.tag.EndsWith("{ }") && instruction.tag.EndsWith("{"))
                    {
                        indent++;
                    }

                    continue;
                }

                LinePrefix((i + 1).ToString());
                b.ForegroundColor = CharColors.FgOrange;
                b.AddText($"{new string(' ', Math.Max(0, indent * 2))} ");
                if (IsNextInstruction)
                {
                    IsNextInstruction = false;
                    b.BackgroundColor = CharColors.BgRed;
                }
                b.AddText($"{instruction.opcode}");
                b.AddText($" ");

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_GET ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_SET)
                {
                    b.AddText($"{instruction.AddressingMode}");
                    b.AddText($" ");
                }

                if (instruction.Parameter is int)
                {
                    b.ForegroundColor = CharColors.FgCyan;
                    b.AddText($"{instruction.Parameter}");
                    b.AddText($" ");
                }
                else if (instruction.Parameter is float)
                {
                    b.ForegroundColor = CharColors.FgCyan;
                    b.AddText($"{instruction.Parameter}f");
                    b.AddText($" ");
                }
                else if (instruction.Parameter is bool)
                {
                    b.ForegroundColor = CharColors.FgDarkBlue;
                    b.AddText($"{instruction.Parameter}");
                    b.AddText($" ");
                }
                else if (instruction.Parameter is string @string)
                {
                    b.ForegroundColor = CharColors.FgYellow;
                    b.AddText($"\"{@string.Escape()}\"");
                    b.AddText($" ");
                }
                else
                {
                    b.ForegroundColor = CharColors.FgWhite;
                    // b.AddText($"{instruction.Parameter}");
                    b.AddText($" ");
                }

                if (!string.IsNullOrEmpty(instruction.tag))
                {
                    b.ForegroundColor = IsNextInstruction ? CharColors.FgBlack : CharColors.FgGray;
                    b.AddText($"{instruction.tag}");
                }

                b.BackgroundColor = CharColors.BgBlack;

                b.FinishLine(sender.Rect.Width);
                b.ForegroundColor = CharColors.FgDefault;
            }

            {
                string t = CurrentlyJumping == 0 ? $" Next Jump Count: {NextCodeJumpCount} " : $" Jumping: {CurrentlyJumping} ";
                b.ForegroundColor = CharColors.FgBlack;
                b.BackgroundColor = CharColors.BgWhite;
                b.SetText(t, sender.Rect.Right - (2 + t.Length));
            }
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

            if (!e.KeyDown && e.AsciiChar == 9)
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

            if (e.KeyDown && e.AsciiChar == 43)
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

            if (e.KeyDown && e.AsciiChar == 45)
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
        }

        public override void RefreshSize()
        {
            base.RefreshSize();
            Elements[0].Rect = Rect;
            Elements[0].RefreshSize();
        }
    }
}
