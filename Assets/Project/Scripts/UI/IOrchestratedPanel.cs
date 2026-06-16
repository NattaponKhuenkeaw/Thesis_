namespace Project.UI
{
    public interface IOrchestratedPanel
    {
        void OnPanelOpened();
        void OnPanelClosed();
        bool TryRequestClose(System.Action closeAction);
    }
}
