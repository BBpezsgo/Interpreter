using System.Runtime.InteropServices;

#pragma warning disable CA1069 // Enums values should not be duplicated

namespace Windows
{
    public static class Win32
    {
        internal enum MessageBoxButton : uint
        {
            /// <summary>
            /// The message box contains three push buttons: Abort, Retry, and Ignore.
            /// </summary>
            MB_ABORTRETRYIGNORE = 0x00000002,

            /// <summary>
            /// The message box contains three push buttons: Cancel, Try Again, Continue. Use this message box type instead of MB_ABORTRETRYIGNORE.
            /// </summary>
            MB_CANCELTRYCONTINUE = 0x00000006,

            /// <summary>
            /// Adds a Help button to the message box. When the user clicks the Help button or presses F1, the system sends a WM_HELP message to the owner.
            /// </summary>
            MB_HELP = 0x00004000,

            /// <summary>
            /// The message box contains one push button: OK. This is the default.
            /// </summary>
            MB_OK = 0x00000000,

            /// <summary>
            /// The message box contains two push buttons: OK and Cancel.
            /// </summary>
            MB_OKCANCEL = 0x00000001,

            /// <summary>
            /// The message box contains two push buttons: Retry and Cancel.
            /// </summary>
            MB_RETRYCANCEL = 0x00000005,

            /// <summary>
            /// The message box contains two push buttons: Yes and No.
            /// </summary>
            MB_YESNO = 0x00000004,

            /// <summary>
            /// The message box contains three push buttons: Yes, No, and Cancel.
            /// </summary>
            MB_YESNOCANCEL = 0x00000003,
        }

        internal enum MessageBoxIcon : uint
        {
            /// <summary>
            /// An exclamation-point icon appears in the message box.
            /// </summary>
            MB_ICONEXCLAMATION = 0x00000030,

            /// <summary>
            /// An exclamation-point icon appears in the message box.
            /// </summary>
            MB_ICONWARNING = 0x00000030,

            /// <summary>
            /// An icon consisting of a lowercase letter i in a circle appears in the message box.
            /// </summary>
            MB_ICONINFORMATION = 0x00000040,

            /// <summary>
            /// An icon consisting of a lowercase letter i in a circle appears in the message box.
            /// </summary>
            MB_ICONASTERISK = 0x00000040,

            /// <summary>
            /// A question-mark icon appears in the message box. The question-mark message icon is no longer recommended because it does not clearly represent a specific type of message and because the phrasing of a message as a question could apply to any message type. In addition, users can confuse the message symbol question mark with Help information. Therefore, do not use this question mark message symbol in your message boxes. The system continues to support its inclusion only for backward compatibility.
            /// </summary>
            MB_ICONQUESTION = 0x00000020,

            /// <summary>
            /// A stop-sign icon appears in the message box.
            /// </summary>
            MB_ICONSTOP = 0x00000010,

            /// <summary>
            /// A stop-sign icon appears in the message box.
            /// </summary>
            MB_ICONERROR = 0x00000010,

            /// <summary>
            /// A stop-sign icon appears in the message box.
            /// </summary>
            MB_ICONHAND = 0x00000010,
        }

        internal enum MessageBoxDefaultButton : uint
        {
            /// <summary>
            /// The first button is the default button.
            /// MB_DEFBUTTON1 is the default unless MB_DEFBUTTON2, MB_DEFBUTTON3, or MB_DEFBUTTON4 is specified.
            /// </summary>
            MB_DEFBUTTON1 = 0x00000000,

            /// <summary>
            /// The second button is the default button.
            /// </summary>
            MB_DEFBUTTON2 = 0x00000100,

            /// <summary>
            /// The third button is the default button.
            /// </summary>
            MB_DEFBUTTON3 = 0x00000200,

            /// <summary>
            /// The fourth button is the default button.
            /// </summary>
            MB_DEFBUTTON4 = 0x00000300,
        }

        internal enum MessageBoxModality : uint
        {
            /// <summary>
            /// <para>
            /// The user must respond to the message box before continuing work in the window identified by the hWnd parameter. However, the user can move to the windows of other threads and work in those windows.
            /// </para>
            /// <para>
            /// Depending on the hierarchy of windows in the application, the user may be able to move to other windows within the thread. All child windows of the parent of the message box are automatically disabled, but pop-up windows are not.
            /// </para>
            /// <para>
            /// MB_APPLMODAL is the default if neither MB_SYSTEMMODAL nor MB_TASKMODAL is specified.
            /// </para>
            /// </summary>
            MB_APPLMODAL = 0x00000000,

            /// <summary>
            /// Same as MB_APPLMODAL except that the message box has the WS_EX_TOPMOST style. Use system-modal message boxes to notify the user of serious, potentially damaging errors that require immediate attention (for example, running out of memory). This flag has no effect on the user's ability to interact with windows other than those associated with hWnd.
            /// </summary>
            MB_SYSTEMMODAL = 0x00001000,
            /// <summary>
            /// Same as MB_APPLMODAL except that all the top-level windows belonging to the current thread are disabled if the hWnd parameter is NULL.Use this flag when the calling application or library does not have a window handle available but still needs to prevent input to other windows in the calling thread without suspending other threads.
            /// </summary>
            MB_TASKMODAL = 0x00002000,
        }

        internal enum MessageBoxResult : int
        {
            /// <summary>
            /// The Abort button was selected.
            /// </summary>
            IDABORT = 3,

            /// <summary>
            /// The Cancel button was selected.
            /// </summary>
            IDCANCEL = 2,

            /// <summary>
            /// The Continue button was selected.
            /// </summary>
            IDCONTINUE = 11,

            /// <summary>
            /// The Ignore button was selected.
            /// </summary>
            IDIGNORE = 5,

            /// <summary>
            /// The No button was selected.
            /// </summary>
            IDNO = 7,

            /// <summary>
            /// The OK button was selected.
            /// </summary>
            IDOK = 1,

            /// <summary>
            /// The Retry button was selected.
            /// </summary>
            IDRETRY = 4,

            /// <summary>
            /// The Try Again button was selected.
            /// </summary>
            IDTRYAGAIN = 10,

            /// <summary>
            /// The Yes button was selected.
            /// </summary>
            IDYES = 6,
        }

        /// <summary>
        /// Displays a modal dialog box that contains a system icon, a set of buttons, and a brief application-specific message, such as status or error information. The message box returns an integer value that indicates which button the user clicked.
        /// </summary>
        /// <param name="hWnd">
        /// A handle to the owner window of the message box to be created. If this parameter is NULL, the message box has no owner window.
        /// </param>
        /// <param name="text">
        /// The message to be displayed. If the string consists of more than one line, you can separate the lines using a carriage return and/or linefeed character between each line.
        /// </param>
        /// <param name="caption">
        /// The dialog box title. If this parameter is NULL, the default title is Error.
        /// </param>
        /// <param name="type">
        /// The contents and behavior of the dialog box. This parameter can be a combination of flags from the following groups of flags.
        /// </param>
        /// <returns></returns>
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        internal static extern MessageBoxResult MessageBoxW(int hWnd, string text, string caption, uint type);
    }
}
