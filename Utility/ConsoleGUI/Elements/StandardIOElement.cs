﻿using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public class StandardIOElement : InlineElement
{
    public delegate void InputEventHandler(char input);

    readonly struct ConsoleText
    {
        public readonly string Text;
        public readonly CLI.AnsiColor Color;

        public ConsoleText(string text, CLI.AnsiColor color)
        {
            Text = text;
            Color = color;
        }
    }

    readonly ScrollBar ScrollBar;
    readonly List<ConsoleText> ConsoleTexts;
    bool IsReading;

    public event InputEventHandler? OnInput;

    public StandardIOElement() : base()
    {
        ConsoleTexts = new List<ConsoleText>();
        ScrollBar = new ScrollBar(GetScrollBarBounds, this);
        IsReading = false;
    }

    LanguageCore.Range<int> GetScrollBarBounds(Element element)
    {
        int totalLines = 0;
        for (int i = 0; i < ConsoleTexts.Count; i++)
        {
            string[] lines = ConsoleTexts[i].Text.Split('\n');
            totalLines += lines.Length - 1;

            for (int j = 0; j < lines.Length; j++)
            {
                if (lines[j].Length >= DrawBuffer.Width) totalLines++;
            }
        }

        return new LanguageCore.Range<int>(0, Math.Max(1, totalLines));
    }

    public void Write(string text, CLI.AnsiColor color = CLI.AnsiColor.Silver)
    {
        ConsoleTexts.Add(new ConsoleText(text, color));
    }

    public override void BeforeDraw()
    {
        base.BeforeDraw();

        DrawBuffer b = DrawBuffer;
        b.StepTo(0);

        b.ResetColor();

        int start = Math.Max(0, ScrollBar.Offset);

        int line = 0;
        for (int i = 0; i < ConsoleTexts.Count; i++)
        {
            ConsoleText consoleText = ConsoleTexts[i];
            string text = consoleText.Text;

            string[] lines = text.Split('\n');

            b.ForegroundColor = consoleText.Color;

            for (int j = 0; j < lines.Length; j++)
            {
                if (line + j < start) continue;
                if (j == lines.Length - 1 && string.IsNullOrEmpty(lines[j])) continue;

                string line_ = lines[j];
                b.AddText(line_);
                if (j < lines.Length - 1)
                { b.FinishLine(); }
            }

            line += lines.Length - 1;
        }

        ScrollBar.Draw(b);
    }

    public override void OnKeyEvent(KeyEvent e)
    {
        base.OnKeyEvent(e);
        ScrollBar.FeedEvent(this, e);

        if (IsReading)
        {
            OnInput?.Invoke(e.UnicodeChar);
            IsReading = false;
        }
    }

    public override void OnMouseEvent(MouseEvent e)
    {
        base.OnMouseEvent(e);
        ScrollBar.FeedEvent(this, e);
    }

    public void BeginRead() => IsReading = true;
}
