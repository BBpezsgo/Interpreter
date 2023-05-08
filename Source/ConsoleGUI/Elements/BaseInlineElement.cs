namespace ConsoleGUI
{
    internal class BaseInlineElement : BaseElement
    {
        public class BeforeDrawEvent { }

        public delegate void MouseEvent(BaseInlineElement sender, MouseInfo e);
        public delegate void KeyEvent(BaseInlineElement sender, ConsoleLib.NativeMethods.KEY_EVENT_RECORD e);
        public delegate void SimpleEvent(BaseInlineElement sender);

        public event SimpleEvent OnBeforeDraw;
        public event SimpleEvent OnRefreshSize;

        public event MouseEvent OnMouseEventInvoked;
        public event KeyEvent OnKeyEventInvoked;

        internal override void BeforeDraw()
        {
            base.BeforeDraw();
            OnBeforeDraw?.Invoke(this);
        }
        internal override void RefreshSize()
        {
            base.RefreshSize();
            OnRefreshSize?.Invoke(this);
        }
        internal override void OnMouseEvent(MouseInfo e)
        {
            base.OnMouseEvent(e);
            OnMouseEventInvoked?.Invoke(this, e);
        }
        internal override void OnKeyEvent(ConsoleLib.NativeMethods.KEY_EVENT_RECORD e)
        {
            base.OnKeyEvent(e);
            OnKeyEventInvoked?.Invoke(this, e);
        }
    }
}
