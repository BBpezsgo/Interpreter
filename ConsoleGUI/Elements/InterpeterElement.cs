using IngameCoding.Core;

using System;
using System.Diagnostics;
using System.IO;

namespace ConsoleGUI
{
    using ConsoleLib;

    internal class InterpeterElement : BaseWindowElement
    {
        public string File;
        int Scroll = 0;
        Interpreter Interpreter;

        string ConsoleText = "";

        public InterpeterElement(string file, IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors) : base()
        {
            ClearBuffer();
            this.File = file;
            SetupInterpeter(compilerSettings, parserSettings, interpreterSettings, handleErrors);
            InitElements();
        }

        public InterpeterElement(string file) : base()
        {
            ClearBuffer();
            this.File = file;
            SetupInterpeter();
            InitElements();
        }

        void InitElements()
        {
            var leftWidth = Console.WindowWidth / 2;

            var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
            var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
            var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

            var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 1);

            var StatePanel = new BaseInlineElement
            {
                Rect = StatePanelRect
            };
            StatePanel.OnBeforeDraw += NewElement3_OnBeforeDraw;
            StatePanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 1);

                sender.Rect = StatePanelRect;
            };

            var CodePanel = new BaseInlineElement
            {
                Rect = CodePanelRect
            };
            CodePanel.OnBeforeDraw += NewElement1_OnBeforeDraw;
            CodePanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 1);

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

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 1);

                sender.Rect = ConsolePanelRect;
            };

            var StackPanel = new BaseInlineElement
            {
                Rect = StackPanelRect
            };
            StackPanel.OnBeforeDraw += NewElement2_OnBeforeDraw;
            StackPanel.OnRefreshSize += (sender) =>
            {
                var leftWidth = Console.WindowWidth / 2;

                var StatePanelRect = new System.Drawing.Rectangle(0, 0, leftWidth, 3);
                var ConsolePanelRect = new System.Drawing.Rectangle(0, StatePanelRect.Bottom + 1, leftWidth, (Console.WindowHeight - StatePanelRect.Bottom - 1) / 2);
                var CodePanelRect = new System.Drawing.Rectangle(0, ConsolePanelRect.Bottom + 1, leftWidth, Console.WindowHeight - 2 - ConsolePanelRect.Bottom);

                var StackPanelRect = new System.Drawing.Rectangle(leftWidth + 1, 0, Console.WindowWidth - 2 - leftWidth, Console.WindowHeight - 1);

                sender.Rect = StackPanelRect;
            };


            this.Elements = new BaseInlineElement[]
            {
                CodePanel,
                StackPanel,
                StatePanel,
                ConsolePanel
            };
        }

        void SetupInterpeter() => SetupInterpeter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings.Default, IngameCoding.BBCode.Parser.ParserSettings.Default, IngameCoding.Bytecode.BytecodeInterpreterSettings.Default, false);
        void SetupInterpeter(IngameCoding.BBCode.Compiler.Compiler.CompilerSettings compilerSettings, IngameCoding.BBCode.Parser.ParserSettings parserSettings, IngameCoding.Bytecode.BytecodeInterpreterSettings interpreterSettings, bool handleErrors)
        {
            var fileInfo = new FileInfo(File);
            var code = System.IO.File.ReadAllText(fileInfo.FullName);
            this.Interpreter = new Interpreter();

            Interpreter.OnOutput += (sender, message, logType) => { ConsoleText += message + "\n"; };

            Interpreter.OnNeedInput += (sender, message) =>
            {
                Console.Write(message);
                var input = Console.ReadLine();
                sender.OnInput(input);
            };

            if (Interpreter.Initialize())
            {
                var compiledCode = Interpreter.CompileCode(code, fileInfo.Directory, compilerSettings, parserSettings, handleErrors);

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
            for (int i = Math.Max(0, lines.Length - sender.Rect.Height); i < lines.Length; i++)
            {
                var line = lines[i];

                AddText("  ");
                AddText(line);

                FinishLine();
            }
        }

        private void NewElement3_OnBeforeDraw(BaseInlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpeter == null) return;

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
            AddText($"IsRunning: {this.Interpreter.Details.Interpeter.IsRunning}");
            BackgroundColor = CharColors.BgBlack;
            FinishLine();
            ForegroundColor = CharColors.FgDefault;

            AddText("  ");
            if (this.Interpreter.Details.Interpeter.Details.CodePointer == this.Interpreter.Details.CompilerResult.compiledCode.Length)
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

        private void NewElement2_OnBeforeDraw(BaseInlineElement sender)
        {
            sender.ClearBuffer();

            if (this.Interpreter.Details.Interpeter == null) return;

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

            for (int i = 0; i < this.Interpreter.Details.Interpeter.Details.Stack.Length; i++)
            {
                var item = this.Interpreter.Details.Interpeter.Details.Stack[i];

                LinePrefix(i.ToString());

                if (this.Interpreter.Details.Interpeter.Details.BasePointer == i)
                {
                    BackgroundColor = CharColors.BgGray;
                }

                switch (item.type)
                {
                    case IngameCoding.Bytecode.Stack.Item.Type.INT:
                        ForegroundColor = CharColors.FgCyan;
                        AddText($"{item.ValueInt}");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.FLOAT:
                        ForegroundColor = CharColors.FgCyan;
                        AddText($"{item.ValueFloat}");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.STRING:
                        ForegroundColor = CharColors.FgYellow;
                        AddText($"\"{item.ValueString}\"");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.BOOLEAN:
                        ForegroundColor = CharColors.FgDarkBlue;
                        AddText($"{item.ValueBoolean}");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.STRUCT:
                        ForegroundColor = CharColors.FgWhite;
                        AddText("{ ... }");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.LIST:
                        AddText($"{item.ValueList.itemTypes.ToString().ToLower()} [ ... ]");
                        break;
                    case IngameCoding.Bytecode.Stack.Item.Type.RUNTIME:
                        ForegroundColor = CharColors.FgGray;
                        AddText("<runtime>");
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

                if (this.Interpreter.Details.Interpeter.Details.BasePointer == i && ForegroundColor == CharColors.FgGray)
                {
                    ForegroundColor = CharColors.FgBlack;
                }

                BackgroundColor = CharColors.BgBlack;
                FinishLine();
                ForegroundColor = CharColors.FgDefault;
            }
        }
        private void NewElement1_OnBeforeDraw(BaseInlineElement sender)
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
            for (int i = 0; i < this.Interpreter.Details.CompilerResult.compiledCode.Length; i++)
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

                if (Interpreter.Details.Interpeter != null) if (Interpreter.Details.Interpeter.Details.CodePointer == i) IsNextInstruction = true;

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

                if (!string.IsNullOrEmpty(instruction.additionParameter))
                {
                    ForegroundColor = CharColors.FgYellow;
                    AddText($"\"{instruction.additionParameter}\"");
                    AddText($" ");
                }

                if (instruction.additionParameter2 != -1)
                {
                    ForegroundColor = IsNextInstruction ? CharColors.FgBlack : CharColors.FgCyan;
                    AddText($"{instruction.additionParameter2}");
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

        bool first = true;

        internal override void BeforeDraw()
        {
            base.BeforeDraw();

            first = false;
        }

        internal override void OnMouseEvent(MouseInfo mouse)
        {
            if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        internal override void OnKeyEvent(NativeMethods.KEY_EVENT_RECORD e)
        {
            if (e.bKeyDown) return;

            if (e.AsciiChar == 9)
            {
                this.Interpreter.Update();
            }
            else
            {
                Debug.WriteLine(e.AsciiChar);
            }
        }

        void ScrollTo(int value) => Scroll = 0; // Math.Clamp(value, 0, File.Split('\n').Length - Rect.Height + 1);
    }
}
