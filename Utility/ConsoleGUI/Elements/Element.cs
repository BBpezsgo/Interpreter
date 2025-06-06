﻿using System.Drawing;
using Win32.Console;

namespace ConsoleGUI;

[ExcludeFromCodeCoverage]
public class Element : IMainThreadThing
{
    public Rectangle Rect { get; set; }

    public bool HasBorder;
    public string? Title;
    public bool IsFocused;

    public DrawBuffer DrawBuffer = new();
    protected MouseEvent LastMouse;

    public virtual CLI.AnsiChar DrawContent(int x, int y) => DrawBuffer.Clamp(Utils.GetIndex(x, y, Rect.Width), CLI.AnsiChar.Empty);

    public void ClearBuffer() => DrawBuffer = new(Rect.Width, Rect.Height);

    public virtual void BeforeDraw()
    { /*if (DrawBuffer.Length == 0) ClearBuffer();*/ }

    public virtual void OnMouseEvent(MouseEvent e)
    {
        LastMouse = e;
    }
    public virtual void OnKeyEvent(KeyEvent e) { }
    public virtual void OnStart() { }
    public virtual void OnDestroy() { }

    public virtual void RefreshSize() => this.ClearBuffer();

    public virtual void Tick(double deltaTime) { }
}
