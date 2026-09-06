using System;
using System.Collections.Generic;
using RadialLauncher.Models;

namespace RadialLauncher.Services.Context
{
    public class ItemContextAction
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string Payload { get; set; } = string.Empty;
    }

    public interface IContextualActionService
    {
        List<LauncherItem> GetContextualItems(string processName);
        List<ItemContextAction> GetItemQuickActions(LauncherItem item);
        bool ExecuteItemQuickAction(LauncherItem item, ItemContextAction action);
        void Reload();
    }
}
