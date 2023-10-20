using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Win32;

namespace ConsoleGUI
{
    public class ScrollBar
    {
        Func<Element, (int Min, int Max)> GetRange;
        Element Parent;
        int offset;

        public int Offset => offset;

        float ScrollPercent
        {
            get
            {
                var range = GetRange.Invoke(Parent);
                offset -= range.Min;
                int max = range.Max - range.Min;
                return (float)offset / (float)max;
            }
        }

        public ScrollBar(Func<Element, (int Min, int Max)> getRange, Element parent)
        {
            GetRange = getRange;
            Parent = parent;
        }

        public void Draw(DrawBuffer buffer)
        {
            int height = Parent.Rect.Height - 1;
            int x = Parent.Rect.Width - 2;

            float scrollPercent = ScrollPercent;
            int scrollY = (int)MathF.Round(height * scrollPercent);

            for (int y = 0; y < height; y++)
            {
                if (y == scrollY)
                {
                    buffer[x, y] = new Character(' ', ForegroundColor.Black, BackgroundColor.White);
                }
                else
                {
                    buffer[x, y] = new Character((char)0x2592, ForegroundColor.DarkGray, BackgroundColor.Black);
                }
            }
        }

        public void FeedEvent(Element sender, MouseEvent e)
        {
            var range = GetRange.Invoke(sender);
            if (e.ButtonState == MouseButton.ScrollDown)
            {
                offset = Math.Clamp(offset + 1, range.Min, range.Max);
                return;
            }

            if (e.ButtonState == MouseButton.ScrollUp)
            {
                offset = Math.Clamp(offset - 1, range.Min, range.Max);
                return;
            }

            Coord localMousePos = e.MousePosition;
            localMousePos.X -= (short)sender.Rect.X;
            localMousePos.Y -=(short) ((short)sender.Rect.Y + (short)1);

            if (localMousePos.X != Parent.Rect.Width - 1)
            { return; }

            if (localMousePos.Y < 0 || localMousePos.Y > Parent.Rect.Height - 2)
            { return; }

            if (e.ButtonState == MouseButton.Left)
            {
                int height = Parent.Rect.Height - 1;

                float v = localMousePos.Y;
                v /= (float)height;

                v *= range.Max - range.Min;
                v += range.Min;

                offset = (int)MathF.Round(v);
            }
        }

        public void FeedEvent(Element sender, KeyEvent e)
        {

        }
    }
}
