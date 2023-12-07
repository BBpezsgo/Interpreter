using System;
using System.Text;
using Win32;
using Win32.LowLevel;

namespace LanguageCore
{
    public static class GUI
    {
        public static ConsoleRenderer? ConsoleRenderer;

        public static void Label(int x, int y, string text)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            if (y < 0 || y >= ConsoleRenderer.Height) return;

            for (int i = 0; i < text.Length; i++)
            {
                int x_ = x + i;
                if (x_ < 0) continue;
                if (x_ >= ConsoleRenderer.Width) return;

                ConsoleRenderer[x_, y].Char = text[i];
            }
        }
        public static void Label(int x, int y, string text, ushort attributes)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            if (y < 0 || y >= ConsoleRenderer.Height) return;

            for (int i = 0; i < text.Length; i++)
            {
                int x_ = x + i;
                if (x_ < 0) continue;
                if (x_ >= ConsoleRenderer.Width) return;

                ConsoleRenderer[x_, y].Char = text[i];
                ConsoleRenderer[x_, y].Attributes = attributes;
            }
        }

        public static void Label(ref int x, int y, string text)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            if (y < 0 || y >= ConsoleRenderer.Height) return;

            for (int i = 0; i < text.Length; i++)
            {
                if (x < 0) { x++; continue; }
                if (x >= ConsoleRenderer.Width) return;

                ConsoleRenderer[x, y].Char = text[i];

                x++;
            }
        }
        public static void Label(ref int x, int y, string text, ushort attributes)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            if (y < 0 || y >= ConsoleRenderer.Height) return;

            for (int i = 0; i < text.Length; i++)
            {
                if (x < 0) { x++; continue; }
                if (x >= ConsoleRenderer.Width) return;

                ConsoleRenderer[x, y].Char = text[i];
                ConsoleRenderer[x, y].Attributes = attributes;

                x++;
            }
        }

        public static bool Button(SmallRect rect, string text)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            byte fg = ByteColor.White;
            byte bg = ByteColor.Gray;

            bool clicked = false;

            if (rect.Contains(Mouse.RecordedPosition))
            {
                bg = ByteColor.Silver;
                fg = ByteColor.Black;
            }

            if (Mouse.IsPressed(MouseButton.Left) && rect.Contains(Mouse.LeftPressedAt))
            {
                fg = ByteColor.Black;
                bg = ByteColor.White;
            }

            if (Mouse.IsUp(MouseButton.Left) && rect.Contains(Mouse.LeftPressedAt))
            {
                clicked = true;
            }

            int labelOffset = (rect.Width / 2) - (text.Length / 2);

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (y >= ConsoleRenderer.Height) break;
                if (y < 0) continue;

                for (int x = rect.Left; x < rect.Right; x++)
                {
                    if (x >= ConsoleRenderer.Width) break;
                    if (x < 0) continue;

                    char c = ' ';

                    int i = x - rect.Left - labelOffset;

                    if (i >= 0 && i < text.Length)
                    {
                        c = text[i];
                    }

                    ConsoleRenderer[x, y] = new ConsoleChar(c, fg, bg);
                }
            }

            return clicked;
        }

        static readonly char[] ShiftedChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ§'\"+!%/=()?:_-+".ToCharArray();
        static readonly char[] Chars = "abcdefghijklmnopqrstuvwxyz0123456789,.-+".ToCharArray();
        static readonly char[] Keys = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789,.-+".ToCharArray();

        public static void InputField(SmallRect rect, ref bool active, StringBuilder value)
        {
            if (ConsoleRenderer == null) throw new NullReferenceException($"{nameof(ConsoleRenderer)} is null");

            byte fg = ByteColor.White;
            byte bg = ByteColor.Gray;

            if (active || rect.Contains(Mouse.RecordedPosition))
            {
                bg = ByteColor.Silver;
                fg = ByteColor.Black;
            }

            if (Mouse.IsPressed(MouseButton.Left))
            { active = rect.Contains(Mouse.LeftPressedAt); }

            if (active)
            {
                if (Keyboard.IsKeyDown(VirtualKeyCode.BACK))
                {
                    if (value.Length > 0)
                    { value.Remove(value.Length - 1, 1); }
                }
                else
                {
                    for (int i = 0; i < Keys.Length; i++)
                    {
                        if (!Keyboard.IsKeyDown(Keys[i]))
                        { continue; }

                        if (Keyboard.IsKeyPressed(VirtualKeyCode.SHIFT))
                        { value.Append(ShiftedChars[i]); }
                        else
                        { value.Append(Chars[i]); }
                    }
                }
            }

            for (int y = rect.Top; y < rect.Bottom; y++)
            {
                if (y >= ConsoleRenderer.Height) break;
                if (y < 0) continue;

                for (int x = rect.Left; x < rect.Right; x++)
                {
                    if (x >= ConsoleRenderer.Width) break;
                    if (x < 0) continue;

                    char c = ' ';

                    int i = x - rect.Left;

                    if (i >= 0 && i < value.Length)
                    {
                        c = value[i];
                    }

                    ConsoleRenderer[x, y] = new ConsoleChar(c, fg, bg);
                }
            }
        }
    }
}
