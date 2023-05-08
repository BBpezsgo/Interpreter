using IngameCoding.Core;

using System;
using System.IO;

namespace ConsoleGUI
{
    using ConsoleLib;

    using System.Collections.Generic;

    internal class InterpreterElement : BaseWindowElement
    {
        public string File;
        int Scroll;
        Interpreter Interpreter;

        string ConsoleText = "";

        int ConsoleScrollOffset = 0;

        public InterpreterElement(string file, IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors) : base()
        {
            ClearBuffer();
            this.File = file;
            SetupInterpreter(compilerSettings, parserSettings, interpreterSettings, handleErrors);
            InitElements();
        }

        public InterpreterElement(string file) : base()
        {
            ClearBuffer();
            this.File = file;
            SetupInterpreter();
            InitElements();
        }

        void InitElements()
        {
            var leftWidth = Console.WindowWidth / 2;

            var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
            var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
            var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

            var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, (Console.WindowHeight - 1) / 2);
            var HeapPanelRect = new System.Drawing.Rectangle(leftWidth + 1, StackPanelRect.Bottom + 1, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 2 - StackPanelRect.Bottom);

            var StatePanel = new BaseInlineElement
            {
                Rect = StatePanelRect
            };
            StatePanel.OnBeforeDraw += StateElement_OnBeforeDraw;
            StatePanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                sender.Rect = StatePanelRect;
            };

            var CodePanel = new BaseInlineElement
            {
                Rect = CodePanelRect
            };
            CodePanel.OnBeforeDraw += SourceCodeElement_OnBeforeDraw;
            CodePanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                sender.Rect = CodePanelRect;
            };

            var ConsolePanel = new BaseInlineElement
            {
                Rect = ConsolePanelRect
            };
            ConsolePanel.OnBeforeDraw += ConsolePanel_OnBeforeDraw;
            ConsolePanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                sender.Rect = ConsolePanelRect;
            };
            ConsolePanel.OnMouseEventInvoked += (sender, e) =>
            {
                if (e.ButtonState == MouseInfo.ButtonStateEnum.ScrollDown)
                {
                    ConsoleScrollOffset++;
                    ConsoleScrollOffset = Math.Clamp(ConsoleScrollOffset, -(ConsoleText.Trim().Split('\n').Length - sender.Rect.Height), 3);
                }
                else if (e.ButtonState == MouseInfo.ButtonStateEnum.ScrollUp)
                {
                    ConsoleScrollOffset--;
                    ConsoleScrollOffset = Math.Clamp(ConsoleScrollOffset, -(ConsoleText.Trim().Split('\n').Length - sender.Rect.Height), 3);
                }
            };

            var StackPanel = new BaseInlineElement
            {
                Rect = StackPanelRect
            };
            StackPanel.OnBeforeDraw += StackElement_OnBeforeDraw;
            StackPanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, (Console.WindowHeight - 1) / 2);

                sender.Rect = StackPanelRect;
            };

            var HeapPanel = new BaseInlineElement
            {
                Rect = HeapPanelRect
            };
            HeapPanel.OnBeforeDraw += HeapElement_OnBeforeDraw;
            HeapPanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, (Console.WindowHeight - 1) / 2);
                var HeapPanelRect = new System.Drawing.Rectangle(leftWidth + 1, StackPanelRect.Bottom + 1, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 2 - StackPanelRect.Bottom);

                sender.Rect = HeapPanelRect;
            };


            this.Elements = new BaseInlineElement[]
            {
                CodePanel,
                StackPanel,
                HeapPanel,
                StatePanel,
                ConsolePanel
            };
        }

        void SetupInterpreter() => SetupInterpreter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings.Default, IngameCoding.BBCode.Parser.ParserSettings.Default, IngameCoding.Bytecode.BytecodeInterpreterSettings.Default, false);
        void SetupInterpreter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors)
        {
            var fileInfo = new FileInfo(File);
            var code = System.IO.File.ReadAllText(fileInfo.FullName);
            this.Interpreter = new Interpreter();

            Interpreter.OnOutput += (sender, message, logType) => { ConsoleText += message + "\n"; };

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

        private void ConsolePanel_OnBeforeDraw(BaseInlineElement sender)
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

            var lines = ConsoleText.Trim().Split('\n');

            for (int i = Math.Max(0, lines.Length - sender.Rect.Height + ConsoleScrollOffset); i < lines.Length; i++)
            {
                var line = lines[i];

                AddText("  ");
                AddText(line);

                FinishLine();
            }
        }

        private void StateElement_OnBeforeDraw(BaseInlineElement sender)
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

        private void HeapElement_OnBeforeDraw(BaseInlineElement sender)
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
                AddText(" ".Repeat(4 - lineNumber.Length));
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

            for (int i = 0; i < this.Interpreter.Details.Interpreter.Heap.Length; i++)
            {
                var item = this.Interpreter.Details.Interpreter.Heap[i];

                LinePrefix(i.ToString());

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
                        AddText("<null>");
                        break;
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
        private void StackElement_OnBeforeDraw(BaseInlineElement sender)
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

            IngameCoding.Bytecode.Instruction instruction = null;
            for (int cp = this.Interpreter.Details.Interpreter.CodePointer; cp < this.Interpreter.Details.CompilerResult.compiledCode.Length; cp++)
            {
                instruction = this.Interpreter.Details.CompilerResult.compiledCode[cp];
                if (instruction.opcode == IngameCoding.Bytecode.Opcode.COMMENT) continue;
                break;
            }

            List<int> savedBasePointers = new();

            for (int j = 0; j < this.Interpreter.Details.Interpreter.Stack.Length; j++)
            {
                var item = this.Interpreter.Details.Interpreter.Stack[j];
                if (item.type != IngameCoding.Bytecode.DataType.INT) continue;
                if (item.Tag != "saved base pointer") continue;
                savedBasePointers.Add(item.ValueInt);
            }

            bool basepointerShown = false;
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

                bool addIndicator = false;
                if (instruction != null)
                {
                    if (instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_VALUE ||
                        instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_VALUE ||
                        instruction.opcode == IngameCoding.Bytecode.Opcode.LOAD_FIELD ||
                        instruction.opcode == IngameCoding.Bytecode.Opcode.STORE_FIELD)
                    {
                        addIndicator = this.Interpreter.Details.Interpreter.GetAddress((int)(instruction.parameter ?? 0), instruction.AddressingMode) == i;
                    }
                }

                if (addIndicator)
                {
                    ForegroundColor = CharColors.FgRed;
                    AddText("●");
                    ForegroundColor = CharColors.FgGray;
                }

                AddText(" ".Repeat((addIndicator ? 2 : 3) - i.ToString().Length));

                AddText(i.ToString());
                AddSpace(5);

                ForegroundColor = CharColors.FgDefault;
                BackgroundColor = CharColors.BgBlack;

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
                        AddText("<null>");
                        break;
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
            while (basepointerShown == false && i <= this.Interpreter.Details.Interpreter.BasePointer)
            {
                if (this.Interpreter.Details.Interpreter.BasePointer == i)
                {
                    ForegroundColor = CharColors.FgLightBlue;
                    AddText("► " + " ".Repeat(2 - i.ToString().Length));
                    basepointerShown = true;
                    ForegroundColor = CharColors.FgGray;
                }
                else
                {
                    ForegroundColor = CharColors.FgGray;
                    AddText(" ".Repeat(4 - i.ToString().Length));
                }

                AddText(i.ToString());
                AddSpace(5);

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }

        }
        private void SourceCodeElement_OnBeforeDraw(BaseInlineElement sender)
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
                AddText(" ".Repeat(4 - lineNumber.Length));
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
                    if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("}"))
                    {
                        indent--;
                    }

                    LinePrefix((i + 1).ToString());
                    ForegroundColor = CharColors.FgGray;
                    AddText($"{"  ".Repeat(indent)}{instruction.parameter}");
                    ForegroundColor = CharColors.FgDefault;
                    BackgroundColor = CharColors.BgBlack;
                    FinishLine();

                    if (!instruction.parameter.ToString().EndsWith("{ }") && instruction.parameter.ToString().EndsWith("{"))
                    {
                        indent++;
                    }

                    continue;
                }

                LinePrefix((i + 1).ToString());
                ForegroundColor = CharColors.FgOrange;
                AddText($"{"  ".Repeat(indent)} ");
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

                if (instruction.parameter is int || instruction.parameter is float)
                {
                    ForegroundColor = CharColors.FgCyan;
                    AddText($"{instruction.parameter}");
                    AddText($" ");
                }
                else if (instruction.parameter is bool)
                {
                    ForegroundColor = CharColors.FgDarkBlue;
                    AddText($"{instruction.parameter}");
                    AddText($" ");
                }
                else if (instruction.parameter is string)
                {
                    ForegroundColor = CharColors.FgYellow;
                    AddText($"\"{instruction.parameter}\"");
                    AddText($" ");
                }
                else
                {
                    ForegroundColor = CharColors.FgWhite;
                    AddText($"{instruction.parameter}");
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

        internal override void BeforeDraw()
        {
            base.BeforeDraw();
        }

        internal override void OnMouseEvent(MouseInfo e)
        {
            if (e.ButtonState == MouseInfo.ButtonStateEnum.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (e.ButtonState == MouseInfo.ButtonStateEnum.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }

            // if ((e.Flags & MouseInfo.FlagsValues.MOUSE_MOVED) == 0) Debug.WriteLine($"Mouse Event {{ ButtonState: {e.ButtonState}, ControlKeyState: {e.ControlKeyState}, Flags: {e.Flags}, Pos: {{ {e.X} : {e.Y} }} }}");
        }

        internal override void OnKeyEvent(NativeMethods.KEY_EVENT_RECORD e)
        {
            if (e.bKeyDown) return;

            if (e.AsciiChar == 9)
            {
                this.Interpreter.Update();
                if (!this.Interpreter.IsExecutingCode)
                {
                    ConsoleGUI.Instance.Destroy();
                }
            }

            // Debug.WriteLine($"Key Event {{ ASCII: {e.AsciiChar} }}");
        }

        void ScrollTo(int value) => Scroll = 0; // Math.Clamp(value, 0, File.Split('\n').Length - Rect.Height + 1);
    }
}
