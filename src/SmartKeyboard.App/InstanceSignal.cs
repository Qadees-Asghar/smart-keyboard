namespace SmartKeyboard.App;

/// <summary>
/// Lets something outside the running copy of SmartKeyboard ask it to do
/// something, through a named Windows event. The running copy waits on the
/// event; whoever wants something sets it.
///
/// Two requests use this:
///
///   OpenRequested  a second copy, started from the desktop icon, asks the
///                  running one to show its window, then exits. This replaced
///                  a broadcast window message, which Windows quietly failed
///                  to deliver to the running copy's hidden window, so the
///                  click was lost and nothing appeared.
///
///   ExitRequested  run.cmd, install.cmd and uninstall.cmd ask it to close,
///                  so it saves what it learned on the way out. taskkill
///                  without /F reports success but never reached it, so
///                  every rebuild ended in a forced kill and lost whatever it
///                  had learned since its last save.
/// </summary>
public sealed class InstanceSignal : IDisposable
{
    /// <summary>Show the window. Set by a second copy.</summary>
    public const string OpenRequested = "SmartKeyboard.OpenRequested.v1";

    /// <summary>Save and close. Set by tools\close_running.cmd.</summary>
    public const string ExitRequested = "SmartKeyboard.ExitRequested.v1";

    private readonly EventWaitHandle _event;
    private readonly RegisteredWaitHandle _registration;

    private InstanceSignal(EventWaitHandle signal, RegisteredWaitHandle registration)
    {
        _event = signal;
        _registration = registration;
    }

    // Starts listening for one request. onSignal runs on a thread pool
    // thread each time it is asked, so it must hand its work to the UI
    // thread. Returns null if the event could not be made, which only costs
    // that one request. Time O(1).
    public static InstanceSignal? Listen(string name, Action onSignal)
    {
        ArgumentNullException.ThrowIfNull(onSignal);

        try
        {
            var signal = new EventWaitHandle(false, EventResetMode.AutoReset, name);

            RegisteredWaitHandle registration = ThreadPool.RegisterWaitForSingleObject(
                signal,
                (_, _) => onSignal(),
                state: null,
                millisecondsTimeOutInterval: Timeout.Infinite,
                executeOnlyOnce: false);

            return new InstanceSignal(signal, registration);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // Makes a request of the running copy. Returns false when no copy is
    // listening, so the caller can fall back to something else. Time O(1).
    public static bool Send(string name)
    {
        try
        {
            if (!EventWaitHandle.TryOpenExisting(name, out EventWaitHandle? signal))
            {
                return false;
            }

            using (signal)
            {
                return signal.Set();
            }
        }
        catch (Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _registration.Unregister(null);
        _event.Dispose();
    }
}
