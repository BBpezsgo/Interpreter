using System.Runtime.Versioning;
using Win32.Console;

namespace ConsoleGUI;

[SupportedOSPlatform("windows")]
public class InlineElement : Element, IInlineLayoutElement
{
    public InlineLayout Layout { get; set; } = InlineLayout.Stretchy();

    public delegate void EventHandler<T>(InlineElement sender, T e);
    public delegate void EventHandler(InlineElement sender);

    public event EventHandler? OnBeforeDraw;
    public event EventHandler? OnRefreshSize;
    public event EventHandler<MouseEvent>? OnMouseEventInvoked;
    public event EventHandler<KeyEvent>? OnKeyEventInvoked;

    public override void BeforeDraw()
    {
        base.BeforeDraw();
        try { OnBeforeDraw?.Invoke(this); }
        catch (Exception) { }
    }
    public override void RefreshSize()
    {
        base.RefreshSize();
        OnRefreshSize?.Invoke(this);
    }
    public override void OnMouseEvent(MouseEvent e)
    {
        base.OnMouseEvent(e);
        OnMouseEventInvoked?.Invoke(this, e);
    }
    public override void OnKeyEvent(KeyEvent e)
    {
        base.OnKeyEvent(e);
        OnKeyEventInvoked?.Invoke(this, e);
    }
}
