namespace TDFMAUI.Services
{
    public class PanelStateService
    {
        private UsersRightPanel? _activePanel;

        public UsersRightPanel? GetActivePanel() => _activePanel;

        public void RegisterPanel(UsersRightPanel panel)
        {
            _activePanel = panel;
        }

        public void UnregisterPanel(UsersRightPanel panel)
        {
            if (_activePanel == panel)
            {
                _activePanel = null;
            }
        }
    }
}
