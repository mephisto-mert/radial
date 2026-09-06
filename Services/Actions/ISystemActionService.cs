using System;
using System.Collections.Generic;

namespace RadialLauncher.Services.Actions
{
    public class SystemActionInfo
    {
        public string ActionKey { get; set; } = string.Empty;
        private string _displayName = string.Empty;
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(ActionKey))
                {
                    return Localization.LocalizationService.Instance.GetString($"SysAction_{ActionKey}", _displayName);
                }
                return _displayName;
            }
            set => _displayName = value;
        }
        public string IconSymbol { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        public string LocalizedCategory
        {
            get
            {
                if (!string.IsNullOrEmpty(Category))
                {
                    return Localization.LocalizationService.Instance.GetString($"SysCat_{Category}", Category);
                }
                return string.Empty;
            }
        }

        public string Description
        {
            get
            {
                if (!string.IsNullOrEmpty(ActionKey))
                {
                    return Localization.LocalizationService.Instance.GetString($"SysDesc_{ActionKey}", DisplayName);
                }
                return DisplayName;
            }
        }
    }

    public interface ISystemActionService
    {
        List<SystemActionInfo> GetAvailableActions();
        void ExecuteAction(string actionKey);
        string GetIconForAction(string actionKey);
        
        event System.Action<TimeSpan>? FocusTimerTick;
        event System.Action? FocusTimerCompleted;
        bool IsFocusTimerRunning { get; }
        TimeSpan FocusTimerRemaining { get; }
        void StartFocusTimer(int minutes = 25);
        void StopFocusTimer();
    }
}
