using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace RadialLauncher.Services.Audio;

public sealed record AppAudioSessionInfo(
    int ProcessId,
    string ProcessName,
    string DisplayName,
    int VolumePercent,
    bool IsMuted);

public sealed record AudioOutputDeviceInfo(
    string Id,
    string Name,
    bool IsDefault);

public interface IPerAppAudioAndDeviceService
{
    List<AppAudioSessionInfo> GetActiveAppAudioSessions();
    int AdjustAppVolume(int processId, int deltaPercent);
    bool ToggleAppMute(int processId);
    List<AudioOutputDeviceInfo> GetPlaybackDevices();
    string CycleDefaultPlaybackDevice();
}

public sealed class PerAppAudioAndDeviceService : IPerAppAudioAndDeviceService
{
    private static readonly string[] FallbackCandidateProcessNames = new[]
    {
        "Spotify",
        "chrome",
        "msedge",
        "discord",
        "vlc"
    };

    private static readonly Dictionary<string, string> FriendlyProcessNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Spotify"] = "Spotify",
        ["chrome"] = "Google Chrome",
        ["msedge"] = "Microsoft Edge",
        ["discord"] = "Discord",
        ["vlc"] = "VLC Media Player",
        ["firefox"] = "Mozilla Firefox",
        ["brave"] = "Brave"
    };

    private readonly ConcurrentDictionary<int, int> _cachedVolumes = new();
    private readonly ConcurrentDictionary<int, bool> _cachedMutes = new();

    public List<AppAudioSessionInfo> GetActiveAppAudioSessions()
    {
        var results = new Dictionary<int, AppAudioSessionInfo>();
        int currentPid = Environment.ProcessId;

        try
        {
            EnumerateMultimediaAudioSessions((ctl2, simpleVol) =>
            {
                if (ctl2.GetProcessId(out uint pid) != 0 || pid == 0 || (int)pid == currentPid)
                {
                    return;
                }

                int processId = (int)pid;
                if (results.ContainsKey(processId))
                {
                    return;
                }

                string procName;
                string mainTitle = string.Empty;
                try
                {
                    using var proc = Process.GetProcessById(processId);
                    procName = proc.ProcessName;
                    mainTitle = proc.MainWindowTitle ?? string.Empty;
                }
                catch
                {
                    return;
                }

                if ( string.IsNullOrWhiteSpace(procName))
                {
                    return;
                }

                float level = 1.0f;
                bool muted = false;
                try { simpleVol.GetMasterVolume(out level); } catch { }
                try { simpleVol.GetMute(out muted); } catch { }

                int volPercent = Math.Clamp((int)Math.Round(level * 100f), 0, 100);
                _cachedVolumes[processId] = volPercent;
                _cachedMutes[processId] = muted;

                ctl2.GetDisplayName(out string? sessionDisplay);
                string displayName = ResolveDisplayName(procName, sessionDisplay, mainTitle);

                results[processId] = new AppAudioSessionInfo(
                    processId,
                    procName,
                    displayName,
                    volPercent,
                    muted);
            });
        }
        catch
        {
            // Fallback to open candidate processes below
        }

        if (results.Count < 1)
        {
            AppendFallbackAppSessions(results, currentPid);
        }

        return results.Values
            .OrderBy(s => s.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public int AdjustAppVolume(int processId, int deltaPercent)
    {
        int current = _cachedVolumes.GetOrAdd(processId, 100);
        int target = Math.Clamp(current + deltaPercent, 0, 100);

        try
        {
            bool foundFirst = false;
            EnumerateMultimediaAudioSessions((ctl2, simpleVol) =>
            {
                if (ctl2.GetProcessId(out uint pid) != 0 || (int)pid != processId)
                {
                    return;
                }

                if (!foundFirst)
                {
                    try
                    {
                        if (simpleVol.GetMasterVolume(out float existingLevel) == 0)
                        {
                            current = Math.Clamp((int)Math.Round(existingLevel * 100f), 0, 100);
                            target = Math.Clamp(current + deltaPercent, 0, 100);
                        }
                    }
                    catch { }
                    foundFirst = true;
                }

                try
                {
                    Guid ctx = Guid.Empty;
                    simpleVol.SetMasterVolume(target / 100f, ref ctx);
                }
                catch { }
            });
        }
        catch { }

        _cachedVolumes[processId] = target;
        return target;
    }

    public bool ToggleAppMute(int processId)
    {
        bool currentMute = _cachedMutes.GetOrAdd(processId, false);
        bool targetMute = !currentMute;

        try
        {
            bool foundFirst = false;
            EnumerateMultimediaAudioSessions((ctl2, simpleVol) =>
            {
                if (ctl2.GetProcessId(out uint pid) != 0 || (int)pid != processId)
                {
                    return;
                }

                if (!foundFirst)
                {
                    try
                    {
                        if (simpleVol.GetMute(out bool existingMute) == 0)
                        {
                            targetMute = !existingMute;
                        }
                    }
                    catch { }
                    foundFirst = true;
                }

                try
                {
                    Guid ctx = Guid.Empty;
                    simpleVol.SetMute(targetMute, ref ctx);
                }
                catch { }
            });
        }
        catch { }

        _cachedMutes[processId] = targetMute;
        return targetMute;
    }

    public List<AudioOutputDeviceInfo> GetPlaybackDevices()
    {
        var devices = new List<AudioOutputDeviceInfo>();
        try
        {
            var enumerator = CreateDeviceEnumerator();
            if (enumerator == null)
            {
                return devices;
            }

            string defaultId = string.Empty;
            try
            {
                if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice defaultDev) == 0 && defaultDev != null)
                {
                    defaultDev.GetId(out defaultId);
                }
            }
            catch { }

            if (enumerator.EnumAudioEndpoints(0, 1, out IMMDeviceCollection collection) == 0 && collection != null)
            {
                if (collection.GetCount(out int count) == 0)
                {
                    for (int i = 0; i < count; i++)
                    {
                        try
                        {
                            if (collection.Item(i, out IMMDevice dev) == 0 && dev != null)
                            {
                                dev.GetId(out string id);
                                if (string.IsNullOrWhiteSpace(id))
                                {
                                    continue;
                                }

                                string friendlyName = ReadDeviceFriendlyName(dev, i + 1);
                                bool isDefault = string.Equals(id, defaultId, StringComparison.OrdinalIgnoreCase);
                                devices.Add(new AudioOutputDeviceInfo(id, friendlyName, isDefault));
                            }
                        }
                        catch { }
                    }
                }
            }
        }
        catch { }

        return devices;
    }

    public string CycleDefaultPlaybackDevice()
    {
        try
        {
            var devices = GetPlaybackDevices();
            if (devices.Count == 0)
            {
                return "🎧 Ses Çıkışı: Varsayılan Cihaz";
            }

            if (devices.Count == 1)
            {
                return $"🎧 Ses Çıkışı: {devices[0].Name}";
            }

            int currentIndex = devices.FindIndex(d => d.IsDefault);
            int nextIndex = (currentIndex + 1) % devices.Count;
            var nextDevice = devices[nextIndex];

            SetDefaultAudioEndpoint(nextDevice.Id);
            return $"🎧 Ses Çıkışı: {nextDevice.Name}";
        }
        catch
        {
            return "🎧 Ses Çıkışı: Varsayılan Cihaz";
        }
    }

    private void AppendFallbackAppSessions(Dictionary<int, AppAudioSessionInfo> results, int currentPid)
    {
        foreach (string candidateName in FallbackCandidateProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(candidateName);
                foreach (var proc in processes)
                {
                    using (proc)
                    {
                        if (proc.Id == 0 || proc.Id == currentPid || results.ContainsKey(proc.Id))
                        {
                            continue;
                        }

                        string title = string.Empty;
                        try { title = proc.MainWindowTitle ?? string.Empty; } catch { }

                        int vol = _cachedVolumes.GetOrAdd(proc.Id, 100);
                        bool muted = _cachedMutes.GetOrAdd(proc.Id, false);
                        string display = ResolveDisplayName(proc.ProcessName, null, title);

                        results[proc.Id] = new AppAudioSessionInfo(
                            proc.Id,
                            proc.ProcessName,
                            display,
                            vol,
                            muted);
                        break;
                    }
                }
            }
            catch { }
        }
    }

    private static string ResolveDisplayName(string processName, string? sessionDisplayName, string mainWindowTitle)
    {
        if (FriendlyProcessNames.TryGetValue(processName, out string? mapped))
        {
            return mapped;
        }

        if (!string.IsNullOrWhiteSpace(sessionDisplayName) &&
            !sessionDisplayName.StartsWith("@%", StringComparison.OrdinalIgnoreCase))
        {
            return sessionDisplayName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(mainWindowTitle))
        {
            return mainWindowTitle.Length > 28
                ? mainWindowTitle[..28].Trim() + "…"
                : mainWindowTitle.Trim();
        }

        return processName;
    }

    private static void EnumerateMultimediaAudioSessions(Action<IAudioSessionControl2, ISimpleAudioVolume> callback)
    {
        var enumerator = CreateDeviceEnumerator();
        if (enumerator == null)
        {
            return;
        }

        if (enumerator.GetDefaultAudioEndpoint(0, 1, out IMMDevice endpoint) != 0 || endpoint == null)
        {
            return;
        }

        Guid iidSessionMgr2 = new("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        if (endpoint.Activate(ref iidSessionMgr2, 23, IntPtr.Zero, out object mgrObj) != 0 ||
            mgrObj is not IAudioSessionManager2 sessionManager)
        {
            return;
        }

        if (sessionManager.GetSessionEnumerator(out IAudioSessionEnumerator sessionEnumerator) != 0 ||
            sessionEnumerator == null)
        {
            return;
        }

        if (sessionEnumerator.GetCount(out int count) != 0)
        {
            return;
        }

        for (int i = 0; i < count; i++)
        {
            try
            {
                if (sessionEnumerator.GetSession(i, out IAudioSessionControl ctl) == 0 &&
                    ctl is IAudioSessionControl2 ctl2 &&
                    ctl is ISimpleAudioVolume simpleVol)
                {
                    callback(ctl2, simpleVol);
                }
            }
            catch { }
        }
    }

    private static IMMDeviceEnumerator? CreateDeviceEnumerator()
    {
        try
        {
            var type = Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"));
            return type != null ? Activator.CreateInstance(type) as IMMDeviceEnumerator : null;
        }
        catch
        {
            return null;
        }
    }

    private static string ReadDeviceFriendlyName(IMMDevice device, int index)
    {
        try
        {
            if (device.OpenPropertyStore(0, out IPropertyStore store) == 0 && store != null)
            {
                var pkey = new PropertyKey
                {
                    fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"),
                    pid = 14
                };

                if (store.GetValue(ref pkey, out PropVariant pv) == 0)
                {
                    try
                    {
                        if (pv.vt == 31 && pv.p != IntPtr.Zero)
                        {
                            string? name = Marshal.PtrToStringUni(pv.p);
                            if (!string.IsNullOrWhiteSpace(name))
                            {
                                return name.Trim();
                            }
                        }
                    }
                    finally
                    {
                        try { PropVariantClear(ref pv); } catch { }
                    }
                }
            }
        }
        catch { }

        return $"Ses Cihazı #{index}";
    }

    private static void SetDefaultAudioEndpoint(string deviceId)
    {
        try
        {
            var policyType = Type.GetTypeFromCLSID(new Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9"));
            if (policyType != null)
            {
                object? obj = Activator.CreateInstance(policyType);
                if (obj is IPolicyConfig policyConfig)
                {
                    for (int role = 0; role <= 2; role++)
                    {
                        policyConfig.SetDefaultEndpoint(deviceId, role);
                    }
                    return;
                }
                if (obj is IPolicyConfigVista policyVista)
                {
                    for (int role = 0; role <= 2; role++)
                    {
                        policyVista.SetDefaultEndpoint(deviceId, role);
                    }
                    return;
                }
            }
        }
        catch { }

        try
        {
            var vistaType = Type.GetTypeFromCLSID(new Guid("294935CE-F637-4E7C-A41B-AB255460B862"));
            if (vistaType != null && Activator.CreateInstance(vistaType) is IPolicyConfigVista vistaConfig)
            {
                for (int role = 0; role <= 2; role++)
                {
                    vistaConfig.SetDefaultEndpoint(deviceId, role);
                }
            }
        }
        catch { }
    }

    [DllImport("ole32.dll")]
    private static extern int PropVariantClear(ref PropVariant pvar);

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyKey
    {
        public Guid fmtid;
        public int pid;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropVariant
    {
        public ushort vt;
        public ushort wReserved1;
        public ushort wReserved2;
        public ushort wReserved3;
        public IntPtr p;
        public int p2;
    }

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IMMDeviceCollection ppDevices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
    }

    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out int pcDevices);
        [PreserveSig] int Item(int nDevice, out IMMDevice ppDevice);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
        [PreserveSig] int GetState(out int pdwState);
    }

    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out int cProps);
        [PreserveSig] int GetAt(int iProp, out PropertyKey pkey);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant propvar);
        [PreserveSig] int Commit();
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig] int GetAudioSessionControl(IntPtr AudioSessionGuid, int StreamFlags, out IAudioSessionControl SessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr AudioSessionGuid, int StreamFlags, out ISimpleAudioVolume AudioVolume);
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator SessionEnum);
        [PreserveSig] int RegisterSessionNotification(IntPtr IAudioSessionNotification);
        [PreserveSig] int UnregisterSessionNotification(IntPtr IAudioSessionNotification);
        [PreserveSig] int RegisterDuckNotification([MarshalAs(UnmanagedType.LPWStr)] string sessionID, IntPtr IAudioVolumeDuckNotification);
        [PreserveSig] int UnregisterDuckNotification(IntPtr IAudioVolumeDuckNotification);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int SessionCount);
        [PreserveSig] int GetSession(int SessionCount, out IAudioSessionControl Session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
    }

    [ComImport, Guid("bfb7ff88-7239-4fc9-8fa2-07c950be9c6d"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig] int GetState(out int pRetVal);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string? pRetVal);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string Value, ref Guid EventContext);
        [PreserveSig] int GetGroupingParam(out Guid pRetVal);
        [PreserveSig] int SetGroupingParam(ref Guid Override, ref Guid EventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr NewNotifications);
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string pRetVal);
        [PreserveSig] int GetProcessId(out uint pRetVal);
        [PreserveSig] int IsSystemSoundsSession();
        [PreserveSig] int SetDuckingPreference([MarshalAs(UnmanagedType.Bool)] bool optOut);
    }

    [ComImport, Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig] int SetMasterVolume(float fLevel, ref Guid EventContext);
        [PreserveSig] int GetMasterVolume(out float pfLevel);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, ref Guid EventContext);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
    }

    [ComImport, Guid("F8679F50-850A-41CF-9C72-430F290290C8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }

    [ComImport, Guid("568B9108-44BF-40B4-9006-86AFE5B5A620"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfigVista
    {
        [PreserveSig] int GetMixFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out IntPtr ppFormat);
        [PreserveSig] int SetDeviceFormat([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr pEndpointFormat, IntPtr MixFormat);
        [PreserveSig] int GetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bDefault, out long pmftDefaultPeriod, out long pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, ref long pmftPeriod);
        [PreserveSig] int GetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, out IntPtr pMode);
        [PreserveSig] int SetShareMode([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, out PropVariant pv);
        [PreserveSig] int SetPropertyValue([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bFxStore, ref PropertyKey key, ref PropVariant pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string wszDeviceId, int eRole);
        [PreserveSig] int SetEndpointVisibility([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int bVisible);
    }
}
