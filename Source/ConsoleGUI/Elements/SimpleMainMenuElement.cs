using System;
using System.Diagnostics;

namespace ConsoleGUI
{
    using ConsoleDrawer;

    using System.Timers;
    using Win32;

    internal class SimpleMainMenuElement : WindowElement
    {
        int Scroll;

        bool VersionDownloadDone;
        string VersionDownloadState;

        public SimpleMainMenuElement() : base()
        {
            VersionDownloadDone = false;
            VersionDownloadState = "Loading";

            ClearBuffer();
        }

        public override void OnStart()
        {
            base.OnStart();

            TheProgram.Version.DownloadVersion(
                (state) =>
                {
                    VersionDownloadState = state;
                },
                () =>
                {
                    VersionDownloadState = null;
                    VersionDownloadDone = true;
                }
            );
        }

        public override void BeforeDraw()
        {
            base.BeforeDraw();

            ForegroundColor ForegroundColor;
            BackgroundColor BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= DrawBuffer.Length) return false;

                DrawBuffer[BufferIndex] = new Character()
                {
                    ForegroundColor = ForegroundColor,
                    BackgroundColor = BackgroundColor,
                    Char = data,
                };

                BufferIndex++;
                if (BufferIndex >= DrawBuffer.Length) return false;

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
                while (BufferIndex % Rect.Width < to)
                {
                    if (!AddChar(' ')) break;
                }
            }
            void FinishLine()
            {
                if (Rect.Width == 0) { return; }
                AddSpace(Rect.Width - 1);
                AddChar(' ');
            }

            ForegroundColor = ForegroundColor.Default;
            BackgroundColor = BackgroundColor.Black;

            AddText(" Current Version: ");
            AddText(TheProgram.Version.Current);
            FinishLine();

            AddText(" Newest Version: ");
            if (VersionDownloadDone)
            {
                AddText(TheProgram.Version.UploadedSaved);
                FinishLine();

                if (TheProgram.Version.HasNewVersion())
                {
                    AddText(" New version avaliable");
                }
                else
                {
                    AddText(" Latest version");
                }
                FinishLine();
            }
            else
            {
                AddText("Download... (");
                AddText(VersionDownloadState ?? "Loading");
                AddText(")");
                FinishLine();
            }
        }

        public override void OnMouseEvent(MouseEvent mouse)
        {
            if (mouse.ButtonState == (uint)MouseButtonState.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == (uint)MouseButtonState.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        void ScrollTo(int value) => Scroll = 0; // Math.Clamp(value, 0, File.Split('\n').Length - Rect.Height + 1);
    }
}
