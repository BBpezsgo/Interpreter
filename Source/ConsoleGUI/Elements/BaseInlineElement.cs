namespace ConsoleGUI
{
    internal class BaseInlineElement : BaseElement
    {
        public class BeforeDrawEvent { }
        public delegate void SampleEventHandler(BaseInlineElement sender);

        public event SampleEventHandler OnBeforeDraw;
        public event SampleEventHandler OnRefreshSize;

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
    }
}
