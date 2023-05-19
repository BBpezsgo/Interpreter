using IngameCoding.Core;

using System;
using System.IO;

namespace ConsoleGUI
{
    using System.Collections.Generic;
    using System.Drawing;

    internal sealed class InterpreterElement : WindowElement
    {
        public string File;
        Interpreter Interpreter;

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

        void CalculateLayoutBoxes(
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

        void InitElements()
        {
            CalculateLayoutBoxes(
                out Rectangle StatePanelRect,
                out Rectangle ConsolePanelRect,
                out Rectangle CodePanelRect,
                out Rectangle StackPanelRect,
                out Rectangle HeapPanelRect,
                out Rectangle CallstackPanelRect
                );

            /*
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
                Rect = StatePanelRect,
                Title = "State",
            };
            StatePanel.OnBeforeDraw += StateElement_OnBeforeDraw;
            StatePanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = StatePanelRect;
            };

            var CodePanel = new InlineElement
            {
                HasBorder = true,
                Rect = CodePanelRect,
                Title = "Code",
            };
            CodePanel.OnBeforeDraw += SourceCodeElement_OnBeforeDraw;
            CodePanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = CodePanelRect;
            };

            var ConsolePanel = new InlineElement
            {
                HasBorder = true,
                Rect = ConsolePanelRect,
                Title = "Console",
            };
            ConsolePanel.OnBeforeDraw += ConsolePanel_OnBeforeDraw;
            ConsolePanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = ConsolePanelRect;
            };
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
                Rect = StackPanelRect,
                Title = "Stack",
            };
            StackPanel.OnBeforeDraw += StackElement_OnBeforeDraw;
            StackPanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = StackPanelRect;
            };

            var HeapPanel = new InlineElement
            {
                HasBorder = true,
                Rect = HeapPanelRect,
                Title = "HEAP",
            };
            HeapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;
            HeapPanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = HeapPanelRect;
            };

            var CallstackPanel = new InlineElement
            {
                HasBorder = true,
                Rect = HeapPanelRect,
                Title = "Call Stack",
            };
            CallstackPanel.OnBeforeDraw += CallstackElement_OnBeforeDraw;
            CallstackPanel.OnRefreshSize += (sender) =>
            {
                CalculateLayoutBoxes(
                    out Rectangle StatePanelRect,
                    out Rectangle ConsolePanelRect,
                    out Rectangle CodePanelRect,
                    out Rectangle StackPanelRect,
                    out Rectangle HeapPanelRect,
                    out Rectangle CallstackPanelRect
                    );
                sender.Rect = CallstackPanelRect;
            };


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
            var fileInfo = new FileInfo(File);
            var code = System.IO.File.ReadAllText(fileInfo.FullName);
            this.Interpreter = new Interpreter();

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
                    });
                }
            }
        }

        private void CallstackElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpreter == null) return;

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

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

                ForegroundColor = CharColors.FgGray;
                AddText(" ");

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("○");
                    ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                AddText(i.ToString());
                AddSpace(5);

                ForegroundColor = CharColors.FgDefault;
                BackgroundColor = CharColors.BgBlack;

                AddText($"{frame.Function}");

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }

            while (loadIndicators.Count > 0 || storeIndicators.Count > 0)
            {
                ForegroundColor = CharColors.FgDefault;
                AddText(" ");

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("○");
                    ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                AddText(i.ToString());
                AddSpace(5);

                BackgroundColor = CharColors.BgBlack;
                ForegroundColor = CharColors.FgDefault;

                FinishLine();
                break;
            }

        }

        private void ConsolePanel_OnBeforeDraw(InlineElement sender)
        {
            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

            bool lineFinished = true;
            for (int i = Math.Max(0, ConsoleLines.Count - sender.Rect.Height + ConsoleScrollOffset); i < ConsoleLines.Count; i++)
            {
                var line = ConsoleLines[i];

                if (lineFinished) AddChar(' ');
                ForegroundColor = line.Color;
                AddText(line.Data.TrimEnd());
                ForegroundColor = CharColors.FgDefault;
                BackgroundColor = CharColors.BgBlack;

                lineFinished = line.Data.EndsWith('\n');

                if (lineFinished) FinishLine();
            }
        }

        private void StateElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpreter == null) return;

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

            AddText("  ");
            AddText($"IsRunning: {this.Interpreter.Details.Interpreter.IsRunning}");
            BackgroundColor = CharColors.BgBlack;
            FinishLine();
            ForegroundColor = CharColors.FgDefault;

            AddText("  ");
            if (this.Interpreter.Details.Interpreter.CodePointer == this.Interpreter.Details.CompilerResult.compiledCode.Length)
            {
                AddText($"State: {this.Interpreter.Details.State}");
            }
            else
            {
                AddText($"State: Running...");
            }
            BackgroundColor = CharColors.BgBlack;
            FinishLine();
            ForegroundColor = CharColors.FgDefault;
        }

        private void HeapElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpreter == null) return;

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

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
                    ForegroundColor = CharColors.FgRed;
                    AddText("○");
                    ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));


                ForegroundColor = CharColors.FgGray;
                AddText(i.ToString());
                ForegroundColor = CharColors.FgDefault;
                AddSpace(5);

                if (item.IsNull)
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText("<null>");
                }
                else
                {
                    switch (item.type)
                    {
                        case IngameCoding.Bytecode.DataType.INT:
                            ForegroundColor = CharColors.FgCyan;
                            AddText($"{item.ValueInt}");
                            break;
                        case IngameCoding.Bytecode.DataType.FLOAT:
                            ForegroundColor = CharColors.FgCyan;
                            AddText($"{item.ValueFloat}");
                            break;
                        case IngameCoding.Bytecode.DataType.STRING:
                            ForegroundColor = CharColors.FgYellow;
                            AddText($"\"{item.ValueString}\"");
                            break;
                        case IngameCoding.Bytecode.DataType.BOOLEAN:
                            ForegroundColor = CharColors.FgDarkBlue;
                            AddText($"{item.ValueBoolean}");
                            break;
                        case IngameCoding.Bytecode.DataType.STRUCT:
                            ForegroundColor = CharColors.FgWhite;
                            var @struct = item.ValueStruct;
                            var fields = @struct.GetFields();
                            string text = "{";
                            for (int j = 0; j < fields.Length; j++)
                            {
                                var field = fields[j];
                                var text_ = $" {field}: {@struct.GetField(field)}";

                                if ((text + text_).Length > 10)
                                {
                                    text += " ...";
                                    break;
                                }
                                text += text_;
                            }
                            text += " }";
                            AddText(text);
                            break;
                        case IngameCoding.Bytecode.DataType.LIST:
                            AddText($"{item.ValueList.itemTypes.ToString().ToLower()} [ ... ]");
                            break;
                        default:
                            ForegroundColor = CharColors.FgGray;
                            AddText("?");
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(item.Tag))
                {
                    AddText($" ");
                    ForegroundColor = CharColors.FgGray;
                    AddText(item.Tag);
                }

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }
        }
        private void StackElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpreter == null) return;

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

            IngameCoding.Bytecode.Instruction instruction = this.Interpreter.Details.NextInstruction;

            List<int> savedBasePointers = new();

            for (int j = 0; j < this.Interpreter.Details.Interpreter.Stack.Length; j++)
            {
                var item = this.Interpreter.Details.Interpreter.Stack[j];
                if (item.type != IngameCoding.Bytecode.DataType.INT) continue;
                if (item.Tag != "saved base pointer") continue;
                savedBasePointers.Add(item.ValueInt);
            }

            bool basepointerShown = false;

            List<int> loadIndicators = new();
            List<int> storeIndicators = new();

            if (instruction != null)
            {
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_FIELD)
                {
                    storeIndicators.Add(this.Interpreter.Details.Interpreter.GetAddress((int)(instruction.Parameter ?? 0), instruction.AddressingMode));
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_FIELD ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.HEAP_SET)
                {
                    if (instruction.AddressingMode == IngameCoding.Bytecode.AddressingMode.RUNTIME)
                    { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 2); }
                    else
                    { loadIndicators.Add(this.Interpreter.Details.Interpreter.Stack.Length - 1); }
                }

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_FIELD)
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
                    ForegroundColor = CharColors.FgLightBlue;
                    AddText("►");
                    basepointerShown = true;
                    ForegroundColor = CharColors.FgGray;
                }
                else if (savedBasePointers.Contains(i))
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText("►");
                    ForegroundColor = CharColors.FgGray;
                }
                else
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("○");
                    ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                AddText(i.ToString());
                AddSpace(5);

                ForegroundColor = CharColors.FgDefault;
                BackgroundColor = CharColors.BgBlack;

                if (item.IsNull)
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText("<null>");
                }
                else
                {
                    switch (item.type)
                    {
                        case IngameCoding.Bytecode.DataType.INT:
                            ForegroundColor = CharColors.FgCyan;
                            AddText($"{item.ValueInt}");
                            break;
                        case IngameCoding.Bytecode.DataType.FLOAT:
                            ForegroundColor = CharColors.FgCyan;
                            AddText($"{item.ValueFloat}");
                            break;
                        case IngameCoding.Bytecode.DataType.STRING:
                            ForegroundColor = CharColors.FgYellow;
                            AddText($"\"{item.ValueString.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t").Replace("\0", "\\0")}\"");
                            break;
                        case IngameCoding.Bytecode.DataType.BOOLEAN:
                            ForegroundColor = CharColors.FgDarkBlue;
                            AddText($"{item.ValueBoolean}");
                            break;
                        case IngameCoding.Bytecode.DataType.STRUCT:
                            ForegroundColor = CharColors.FgWhite;
                            {
                                var @struct = item.ValueStruct;
                                var fields = @struct.GetFields();

                                string text = "{";

                                int j = 0;
                                while (text.Length < sender.Rect.Width - 10 && j < fields.Length)
                                {
                                    if (j > 0)
                                    {
                                        text += ";";
                                    }

                                    text += $" {fields[j]}: {@struct.GetField(fields[j])}";

                                    j++;
                                }
                                if (j < fields.Length)
                                {
                                    if (j > 0)
                                    {
                                        text += ";";
                                    }
                                    text += " ...";
                                }

                                text += " }";

                                AddText(text);
                            }
                            break;
                        case IngameCoding.Bytecode.DataType.LIST:
                            {
                                var valueList = item.ValueList;
                                string text = $"{valueList.itemTypes.ToString().ToLower()}";
                                text += " [";

                                int j = 0;
                                while (text.Length < sender.Rect.Width - 10 && j < valueList.items.Count)
                                {
                                    if (j > 0)
                                    {
                                        text += ",";
                                    }

                                    text += $" {valueList.items[j]}";

                                    j++;
                                }
                                if (j < valueList.items.Count)
                                {
                                    if (j > 0)
                                    {
                                        text += ",";
                                    }
                                    text += " ...";
                                }

                                text += " ]";

                                AddText(text);
                            }
                            break;
                        default:
                            ForegroundColor = CharColors.FgGray;
                            AddText("?");
                            break;
                    }
                }

                if (!string.IsNullOrEmpty(item.Tag))
                {
                    AddText($" ");
                    ForegroundColor = CharColors.FgGray;
                    AddText(item.Tag);
                }

                if (this.Interpreter.Details.Interpreter.BasePointer == i && ForegroundColor == CharColors.FgGray)
                {
                    ForegroundColor = CharColors.FgBlack;
                }

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }

            while ((basepointerShown == false && i <= this.Interpreter.Details.Interpreter.BasePointer) || loadIndicators.Count > 0 || storeIndicators.Count > 0)
            {
                if (this.Interpreter.Details.Interpreter.BasePointer == i)
                {
                    ForegroundColor = CharColors.FgLightBlue;
                    AddText("►");
                    basepointerShown = true;
                    ForegroundColor = CharColors.FgGray;
                }
                else
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText(" ");
                }

                bool addLoadIndicator = false;
                bool addStoreIndicator = false;

                for (int j = loadIndicators.Count - 1; j >= 0; j--)
                {
                    if (loadIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("○");
                    ForegroundColor = CharColors.FgGray;
                    loadIndicators.RemoveAt(j);
                    addLoadIndicator = true;
                    break;
                }

                for (int j = storeIndicators.Count - 1; j >= 0; j--)
                {
                    if (storeIndicators[j] != i) continue;
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                    storeIndicators.RemoveAt(j);
                    addStoreIndicator = true;
                    break;
                }

                AddText(new string(' ', ((addStoreIndicator || addLoadIndicator) ? 2 : 3) - i.ToString().Length));

                AddText(i.ToString());
                AddSpace(5);

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;

                i++;

                break;
            }

        }
        private void SourceCodeElement_OnBeforeDraw(InlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpreter == null) return;

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                sender.DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                sender.DrawBuffer[BufferIndex].Char = data;

                BufferIndex++;
                if (BufferIndex >= sender.DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string text)
            {
                for (int i = 0; i < text.Length; i++)
                {
                    if (!AddChar(text[i])) break;
                }
            }
            void AddSpace(int to)
            {
                while (BufferIndex % sender.Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

            void LinePrefix(string lineNumber = "")
            {
                AddText(new string(' ', 4 - lineNumber.Length));
                ForegroundColor = CharColors.FgGray;
                AddText(lineNumber);
                ForegroundColor = CharColors.FgDefault;
                AddSpace(5);
            }
            void FinishLine()
            {
                AddSpace(sender.Rect.Width - 1);
                AddChar(' ');
            }

            int indent = 0;
            for (int i = 0; i < this.Interpreter.Details.Interpreter.CodePointer - 5; i++)
            {
                var instruction = this.Interpreter.Details.CompilerResult.compiledCode[i];
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.COMMENT)
                {
                    if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("}"))
                    { indent--; }
                    if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("{"))
                    { indent++; }

                    continue;
                }
            }

            bool IsNextInstruction = false;
            for (int i = Math.Max(0, this.Interpreter.Details.Interpreter.CodePointer - 5); i < this.Interpreter.Details.CompilerResult.compiledCode.Length; i++)
            {
                if (this.Interpreter.Details.CompilerResult.clearGlobalVariablesInstruction == i)
                {
                    LinePrefix();
                    ForegroundColor = CharColors.FgMagenta;
                    AddText("ClearGlobalVariables:");
                    ForegroundColor = CharColors.FgDefault;
                    FinishLine();
                }
                if (this.Interpreter.Details.CompilerResult.setGlobalVariablesInstruction == i)
                {
                    LinePrefix();
                    ForegroundColor = CharColors.FgMagenta;
                    AddText("SetGlobalVariables:");
                    ForegroundColor = CharColors.FgDefault;
                    FinishLine();
                }

                if (Interpreter.Details.Interpreter != null) if (Interpreter.Details.Interpreter.CodePointer == i) IsNextInstruction = true;

                var instruction = this.Interpreter.Details.CompilerResult.compiledCode[i];
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.COMMENT)
                {
                    if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("}"))
                    {
                        indent--;
                    }

                    LinePrefix((i + 1).ToString());
                    ForegroundColor = CharColors.FgGray;
                    AddText($"{new string(' ', Math.Max(0, indent * 2))}{instruction.Parameter}");
                    ForegroundColor = CharColors.FgDefault;
                    BackgroundColor = CharColors.BgBlack;
                    FinishLine();

                    if (!instruction.ParameterString.EndsWith("{ }") && instruction.ParameterString.EndsWith("{"))
                    {
                        indent++;
                    }

                    continue;
                }

                LinePrefix((i + 1).ToString());
                ForegroundColor = CharColors.FgOrange;
                AddText($"{new string(' ', Math.Max(0, indent * 2))} ");
                if (IsNextInstruction)
                {
                    IsNextInstruction = false;
                    BackgroundColor = CharColors.BgRed;
                }
                AddText($"{instruction.opcode}");
                AddText($" ");

                if (instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_FIELD ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_VALUE ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_FIELD ||
                    instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE)
                {
                    AddText($"{instruction.AddressingMode}");
                    AddText($" ");
                }

                if (instruction.Parameter is int || instruction.Parameter is float)
                {
                    ForegroundColor = CharColors.FgCyan;
                    AddText($"{instruction.Parameter}");
                    AddText($" ");
                }
                else if (instruction.Parameter is bool)
                {
                    ForegroundColor = CharColors.FgDarkBlue;
                    AddText($"{instruction.Parameter}");
                    AddText($" ");
                }
                else if (instruction.Parameter is string)
                {
                    ForegroundColor = CharColors.FgYellow;
                    AddText($"\"{instruction.Parameter}\"");
                    AddText($" ");
                }
                else
                {
                    ForegroundColor = CharColors.FgWhite;
                    AddText($"{instruction.Parameter}");
                    AddText($" ");
                }

                if (!string.IsNullOrEmpty(instruction.tag))
                {
                    ForegroundColor = IsNextInstruction ? CharColors.FgBlack : CharColors.FgGray;
                    AddText($"{instruction.tag}");
                }

                BackgroundColor = CharColors.BgBlack;

                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }
        }

        public override void OnMouseEvent(MouseEvent e)
        {
            base.OnMouseEvent(e);
            Elements.OnMouseEvent(e);
        }

        public override void OnKeyEvent(KeyEvent e)
        {
            base.OnKeyEvent(e);
            Elements.OnKeyEvent(e);

            if (e.KeyDown) return;

            if (e.AsciiChar == 9)
            {
                this.Interpreter.Update();
                if (!this.Interpreter.IsExecutingCode)
                {
                    ConsoleGUI.Instance.Destroy();
                }
            }
        }

        public override void RefreshSize()
        {
            base.RefreshSize();
            Elements[0].Rect = Rect;
            Elements.RefreshSize();
        }
    }
}
