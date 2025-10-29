using System.Diagnostics;

namespace LanguageCore.TUI;

delegate void CustomPanelRenderer(in AnsiBufferSlice buffer);

class Panel : Element
{
    readonly CustomPanelRenderer Renderer;

    [DebuggerStepThrough]
    public Panel(CustomPanelRenderer renderer, ElementSize size) : base(size)
    {
        Renderer = renderer;
    }

    public override void Render(in AnsiBufferSlice buffer)
    {
        Renderer.Invoke(buffer);
    }
}
