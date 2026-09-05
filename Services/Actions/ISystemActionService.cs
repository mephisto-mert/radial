using System.Collections.Generic;

namespace RadialLauncher.Services.Actions
{
    public class SystemActionInfo
    {
        public string ActionKey { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string IconSymbol { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
    }

    public interface ISystemActionService
    {
        List<SystemActionInfo> GetAvailableActions();
        void ExecuteAction(string actionKey);
        string GetIconForAction(string actionKey);
    }
}
