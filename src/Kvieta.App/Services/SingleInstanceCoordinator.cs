using System.Security.Principal;

namespace Kvieta.App.Services;

public sealed class SingleInstanceCoordinator : IDisposable
{
    private readonly Mutex _mutex;
    private readonly EventWaitHandle _activationEvent;
    private RegisteredWaitHandle? _registeredWait;
    private bool _disposed;

    public SingleInstanceCoordinator(string channel = "ControlCenter")
    {
        string identity = WindowsIdentity.GetCurrent().User?.Value.Replace('-', '_') ?? Environment.UserName;
        string safeChannel = new(channel.Where(char.IsLetterOrDigit).ToArray());
        string suffix = $"Kvieta_{identity}_{safeChannel}";
        _mutex = new Mutex(initiallyOwned: true, $"Local\\{suffix}_Instance", out bool createdNew);
        IsPrimary = createdNew;
        _activationEvent = new EventWaitHandle(false, EventResetMode.AutoReset, $"Local\\{suffix}_Activate");

        if (IsPrimary)
        {
            _registeredWait = ThreadPool.RegisterWaitForSingleObject(
                _activationEvent,
                (_, timedOut) =>
                {
                    if (!timedOut && !_disposed)
                    {
                        ActivationRequested?.Invoke(this, EventArgs.Empty);
                    }
                },
                null,
                Timeout.Infinite,
                executeOnlyOnce: false);
        }
    }

    public bool IsPrimary { get; }
    public event EventHandler? ActivationRequested;

    public void SignalPrimary()
    {
        _activationEvent.Set();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _registeredWait?.Unregister(null);
        _registeredWait = null;
        if (IsPrimary)
        {
            try
            {
                _mutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // The process is already shutting down from another thread.
            }
        }

        _activationEvent.Dispose();
        _mutex.Dispose();
    }
}
