using System;
using System.Diagnostics;

namespace ConsoleGUI
{
    using ConsoleLib;

    using System.Timers;

    internal class SimpleMainMenuElement : BaseWindowElement
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

        internal override void OnStart()
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

        internal override void BeforeDraw()
        {
            base.BeforeDraw();

            CharColors ForegroundColor;
            CharColors BackgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= DrawBuffer.Length) return false;

                DrawBuffer[BufferIndex].Color = ForegroundColor | BackgroundColor;
                DrawBuffer[BufferIndex].Char = data;

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

            ForegroundColor = CharColors.FgDefault;
            BackgroundColor = CharColors.BgBlack;

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

        internal override void OnMouseEvent(MouseInfo mouse)
        {
            if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == MouseInfo.ButtonStateEnum.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        internal override void OnKeyEvent(NativeMethods.KEY_EVENT_RECORD e)
        {
            if (e.bKeyDown) return;

            Debug.WriteLine(e.AsciiChar);
        }

        void ScrollTo(int value) => Scroll = 0; // Math.Clamp(value, 0, File.Split('\n').Length - Rect.Height + 1);
    }
}
