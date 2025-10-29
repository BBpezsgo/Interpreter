using System.Diagnostics;

namespace LanguageCore.TUI;

enum ElementSizeKind
{
    Fixed,
    Percentage,
    Auto,
}

readonly struct ElementSize
{
    public readonly ElementSizeKind Kind;
    public readonly float Value;

    [DebuggerStepThrough]
    public static ElementSize Auto() => new(ElementSizeKind.Auto, default);
    [DebuggerStepThrough]
    public static ElementSize Fixed(int size) => new(ElementSizeKind.Fixed, size);
    [DebuggerStepThrough]
    public static ElementSize Percentage(float value) => new(ElementSizeKind.Percentage, value);

    [DebuggerStepThrough]
    public ElementSize(ElementSizeKind kind, float value)
    {
        Kind = kind;
        Value = value;
    }
}

abstract class Element
{
    public readonly ElementSize Size;
    public bool Visible;

    [DebuggerStepThrough]
    protected Element(ElementSize size)
    {
        Size = size;
        Visible = true;
    }

    public abstract void Render(in AnsiBufferSlice buffer);

    [DebuggerStepThrough]
    public Borders WithBorders(string? label) => new(this, label, Size);
}

static class ElementExtensions
{
    [DebuggerStepThrough]
    public static T Invisible<T>(this T e) where T : Element
    {
        e.Visible = false;
        return e;
    }
}
