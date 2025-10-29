
using System.Collections.Immutable;
using System.Diagnostics;

namespace LanguageCore.TUI;

enum FlowDirection
{
    Horizontal,
    Vertical,
}

class Container : Element
{
    public readonly FlowDirection FlowDirection;
    public readonly ImmutableArray<Element> Children;

    [DebuggerStepThrough]
    public Container(FlowDirection flowDirection, ElementSize size, ImmutableArray<Element> children) : base(size)
    {
        FlowDirection = flowDirection;
        Children = children;
    }

    [DebuggerStepThrough]
    public Container(FlowDirection flowDirection, ElementSize size, params Element[] children) : base(size)
    {
        FlowDirection = flowDirection;
        Children = children.ToImmutableArray();
    }

    public override void Render(in AnsiBufferSlice buffer)
    {
        int avaliableSize = FlowDirection == FlowDirection.Horizontal ? buffer.Width : buffer.Height;
        int autoElements = 0;

        foreach (Element child in Children)
        {
            if (!child.Visible) continue;
            if (child.Size.Kind == ElementSizeKind.Fixed)
            {
                avaliableSize -= (int)child.Size.Value;
            }
            else if (child.Size.Kind == ElementSizeKind.Auto)
            {
                autoElements++;
            }
        }

        if (avaliableSize <= 0)
        {
            return;
        }

        int currentOffset = 0;
        for (int i = 0; i < Children.Length; i++)
        {
            Element child = Children[i];
            if (!child.Visible) continue;
            int width;
            int height;
            int offsetX;
            int offsetY;
            if (child.Size.Kind == ElementSizeKind.Fixed)
            {
                width = FlowDirection == FlowDirection.Vertical ? buffer.Width : (int)child.Size.Value;
                height = FlowDirection == FlowDirection.Horizontal ? buffer.Height : (int)child.Size.Value;
                offsetX = FlowDirection == FlowDirection.Vertical ? 0 : currentOffset;
                offsetY = FlowDirection == FlowDirection.Horizontal ? 0 : currentOffset;
            }
            else if (child.Size.Kind == ElementSizeKind.Percentage)
            {
                width = FlowDirection == FlowDirection.Vertical ? buffer.Width : (int)(avaliableSize * child.Size.Value);
                height = FlowDirection == FlowDirection.Horizontal ? buffer.Height : (int)(avaliableSize * child.Size.Value);
                offsetX = FlowDirection == FlowDirection.Vertical ? 0 : currentOffset;
                offsetY = FlowDirection == FlowDirection.Horizontal ? 0 : currentOffset;
            }
            else if (child.Size.Kind == ElementSizeKind.Auto)
            {
                width = FlowDirection == FlowDirection.Vertical ? buffer.Width : (avaliableSize / autoElements);
                height = FlowDirection == FlowDirection.Horizontal ? buffer.Height : (avaliableSize / autoElements);
                offsetX = FlowDirection == FlowDirection.Vertical ? 0 : currentOffset;
                offsetY = FlowDirection == FlowDirection.Horizontal ? 0 : currentOffset;
            }
            else
            {
                continue;
            }

            currentOffset += FlowDirection == FlowDirection.Horizontal ? width : height;
            child.Render(new AnsiBufferSlice(buffer, width, height, offsetX, offsetY));
        }
    }
}
