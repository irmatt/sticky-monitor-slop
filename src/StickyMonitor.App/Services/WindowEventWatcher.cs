using StickyMonitor.App.Native;

namespace StickyMonitor.App.Services;

public sealed class WindowEventWatcher : IDisposable
{
    private static readonly uint[] WatchedEvents =
    {
        NativeMethods.EventSystemMoveSizeEnd,
        NativeMethods.EventSystemMinimizeStart,
        NativeMethods.EventSystemMinimizeEnd,
        NativeMethods.EventObjectCreate,
        NativeMethods.EventObjectDestroy
    };

    private readonly Action _onChange;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly List<IntPtr> _hooks = [];
    private bool _started;

    public WindowEventWatcher(Action onChange)
    {
        _onChange = onChange;
        _callback = OnWinEvent;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        foreach (var eventId in WatchedEvents)
        {
            var hook = NativeMethods.SetWinEventHook(
                eventId,
                eventId,
                IntPtr.Zero,
                _callback,
                0,
                0,
                NativeMethods.WineventOutOfContext | NativeMethods.WineventSkipOwnProcess);

            if (hook != IntPtr.Zero)
            {
                _hooks.Add(hook);
            }
        }

        _started = true;
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        _started = false;
    }

    public void Dispose()
    {
        Stop();
    }

    private void OnWinEvent(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
    {
        if (idObject != NativeMethods.ObjidWindow || hwnd == IntPtr.Zero)
        {
            return;
        }

        try
        {
            _onChange();
        }
        catch
        {
        }
    }
}
