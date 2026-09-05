using System.Collections.Generic;
using RadialLauncher.Services.Actions;

namespace RadialLauncher.Services
{
    public class SystemActionInfo : Actions.SystemActionInfo
    {
    }

    public static class SystemActionService
    {
        private static ISystemActionService Service => Actions.SystemActionService.Instance;

        public static List<Actions.SystemActionInfo> AvailableActions => Actions.SystemActionService.AvailableActions;

        public static void ExecuteAction(string actionKey) => Service.ExecuteAction(actionKey);

        public static string GetIconForAction(string actionKey) => Service.GetIconForAction(actionKey);
    }
}
