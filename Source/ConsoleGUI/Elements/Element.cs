using System.Drawing;
using System.Runtime.Versioning;
using Win32.Console;

namespace ConsoleGUI;

[SupportedOSPlatform("windows")]
public class Element : IMainThreadThing
{
    public Rectangle Rect { get; set; } = Rectangle.Empty;

    public bool HasBorder;
    public string? Title;

    public DrawBuffer DrawBuffer = new();
    protected MouseEvent LastMouse;

    public virtual ConsoleChar DrawContent(int x, int y) => DrawBuffer.Clamp(Utils.GetIndex(x, y, Rect.Width), ConsoleChar.Empty);

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
