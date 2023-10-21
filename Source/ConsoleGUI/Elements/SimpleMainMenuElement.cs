namespace ConsoleGUI
{
    using Win32;

    internal class SimpleMainMenuElement : WindowElement
    {
        int Scroll;

        bool VersionDownloadDone;
        string? VersionDownloadState;

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

            byte foregroundColor;
            byte backgroundColor;

            int BufferIndex = 0;

            bool AddChar(char data)
            {
                if (BufferIndex >= DrawBuffer.Length) return false;

                DrawBuffer[BufferIndex] = new CharInfo()
                {
                    Foreground = foregroundColor,
                    Background = backgroundColor,
                    Char = data,
                };

                BufferIndex++;
                if (BufferIndex >= DrawBuffer.Length) return false;

                return true;
            }
            void AddText(string? text)
            {
                if (text == null) return;
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

            foregroundColor = ByteColor.Silver;
            backgroundColor = ByteColor.Black;

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
                    AddText(" New version available");
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
            if (mouse.ButtonState == MouseButton.ScrollUp)
            {
                ClearBuffer();
                ScrollTo(Scroll - 1);
            }
            else if (mouse.ButtonState == MouseButton.ScrollDown)
            {
                ClearBuffer();
                ScrollTo(Scroll + 1);
            }
        }

        void ScrollTo(int value) => Scroll = 0; // Math.Clamp(value, 0, File.Split('\n').Length - Rect.Height + 1);
    }
}
