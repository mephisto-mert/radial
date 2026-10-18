using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace RadialLauncher.Services.Input;

public interface IGamepadRadialService
{
    event Action<double, bool>? OnStickAimChanged;
    event Action? OnConfirmPressed;
    event Action? OnCancelPressed;
    event Action<int>? OnShoulderPressed;

    void StartPolling();
    void StopPolling();
}

public class GamepadRadialService : IGamepadRadialService
{
    private const uint ERROR_SUCCESS = 0;
    private const ushort XINPUT_GAMEPAD_LEFT_SHOULDER = 0x0100;
    private const ushort XINPUT_GAMEPAD_RIGHT_SHOULDER = 0x0200;
    private const ushort XINPUT_GAMEPAD_A = 0x1000;
    private const ushort XINPUT_GAMEPAD_B = 0x2000;
    private const double StickDeadzoneThreshold = 16000.0;

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_GAMEPAD
    {
        public ushort wButtons;
        public byte bLeftTrigger;
        public byte bRightTrigger;
        public short sThumbLX;
        public short sThumbLY;
        public short sThumbRX;
        public short sThumbRY;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct XINPUT_STATE
    {
        public uint dwPacketNumber;
        public XINPUT_GAMEPAD Gamepad;
    }

    [DllImport("xinput1_4.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState14(uint dwUserIndex, ref XINPUT_STATE pState);

    [DllImport("xinput9_1_0.dll", EntryPoint = "XInputGetState")]
    private static extern uint XInputGetState910(uint dwUserIndex, ref XINPUT_STATE pState);

    private readonly DispatcherTimer _pollTimer;
    private bool _useFallbackDll;
    private bool _xinputUnavailable;
    private bool _hasPreviousState;
    private ushort _previousButtons;
    private bool _wasStickActive;
    private double _lastAngle;

    public event Action<double, bool>? OnStickAimChanged;
    public event Action? OnConfirmPressed;
    public event Action? OnCancelPressed;
    public event Action<int>? OnShoulderPressed;

    public GamepadRadialService()
    {
        _pollTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(33)
        };
        _pollTimer.Tick += OnPollTimerTick;
    }

    public void StartPolling()
    {
        if (_xinputUnavailable || _pollTimer.IsEnabled)
        {
            return;
        }

        _hasPreviousState = false;
        _previousButtons = 0;
        _wasStickActive = false;
        _pollTimer.Start();
    }

    public void StopPolling()
    {
        if (_pollTimer.IsEnabled)
        {
            _pollTimer.Stop();
        }

        _hasPreviousState = false;
        _previousButtons = 0;
        _wasStickActive = false;
    }

    private void OnPollTimerTick(object? sender, EventArgs e)
    {
        if (!TryGetConnectedState(out XINPUT_STATE state))
        {
            _hasPreviousState = false;
            _previousButtons = 0;
            return;
        }

        EvaluateThumbstick(state.Gamepad);
        EvaluateButtons(state.Gamepad.wButtons);
    }

    private void EvaluateThumbstick(XINPUT_GAMEPAD gamepad)
    {
        double lx = gamepad.sThumbLX;
        double ly = gamepad.sThumbLY;
        double rx = gamepad.sThumbRX;
        double ry = gamepad.sThumbRY;

        double leftMag = Math.Sqrt(lx * lx + ly * ly);
        double rightMag = Math.Sqrt(rx * rx + ry * ry);

        double stickX = rightMag > leftMag ? rx : lx;
        double stickY = rightMag > leftMag ? ry : ly;
        double magnitude = Math.Max(leftMag, rightMag);

        bool isActive = magnitude > StickDeadzoneThreshold;
        if (isActive)
        {
            double angleRadians = Math.Atan2(-stickY, stickX);
            if (!_wasStickActive || Math.Abs(angleRadians - _lastAngle) > 0.01)
            {
                _lastAngle = angleRadians;
                _wasStickActive = true;
                OnStickAimChanged?.Invoke(angleRadians, true);
            }
        }
        else if (_wasStickActive)
        {
            _wasStickActive = false;
            OnStickAimChanged?.Invoke(_lastAngle, false);
        }
    }

    private void EvaluateButtons(ushort currentButtons)
    {
        if (!_hasPreviousState)
        {
            _hasPreviousState = true;
            _previousButtons = currentButtons;
            return;
        }

        ushort risingEdges = (ushort)(currentButtons & ~_previousButtons);
        _previousButtons = currentButtons;

        if ((risingEdges & XINPUT_GAMEPAD_A) != 0)
        {
            OnConfirmPressed?.Invoke();
        }

        if ((risingEdges & XINPUT_GAMEPAD_B) != 0)
        {
            OnCancelPressed?.Invoke();
        }

        if ((risingEdges & XINPUT_GAMEPAD_LEFT_SHOULDER) != 0)
        {
            OnShoulderPressed?.Invoke(-1);
        }

        if ((risingEdges & XINPUT_GAMEPAD_RIGHT_SHOULDER) != 0)
        {
            OnShoulderPressed?.Invoke(1);
        }
    }

    private bool TryGetConnectedState(out XINPUT_STATE state)
    {
        state = default;
        if (_xinputUnavailable)
        {
            return false;
        }

        for (int i = 0; i < 4; i++)
        {
            try
            {
                uint result = _useFallbackDll
                    ? XInputGetState910((uint)i, ref state)
                    : XInputGetState14((uint)i, ref state);

                if (result == ERROR_SUCCESS)
                {
                    return true;
                }
            }
            catch (DllNotFoundException) when (!_useFallbackDll)
            {
                _useFallbackDll = true;
                i--;
            }
            catch
            {
                _xinputUnavailable = true;
                _pollTimer.Stop();
                return false;
            }
        }

        return false;
    }
}
