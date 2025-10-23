using System;
using System.Collections.Generic;
using System.Threading;

namespace RepoDash.App.Services;

/// <summary>
/// Central helper that allows view models to register layout refresh callbacks and
/// provides a single entry point for triggering layout invalidation across the app.
/// </summary>
public sealed class LayoutRefreshCoordinator
{
    private readonly object _gate = new();
    private readonly List<WeakReference<Action>> _listeners = new();

    public static LayoutRefreshCoordinator Default { get; } = new();

    private LayoutRefreshCoordinator()
    {
    }

    /// <summary>
    /// Registers a callback that will be invoked whenever <see cref="Refresh"/> is called.
    /// The registration keeps only a weak reference to the delegate to avoid leaks.
    /// </summary>
    public IDisposable Register(Action callback)
    {
        if (callback is null) throw new ArgumentNullException(nameof(callback));

        lock (_gate)
        {
            _listeners.Add(new WeakReference<Action>(callback));
        }

        return new Subscription(this, callback);
    }

    /// <summary>
    /// Triggers all registered callbacks, cleaning up entries whose targets were collected.
    /// </summary>
    public void Refresh()
    {
        List<Action> snapshot = new();

        lock (_gate)
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
            {
                var weak = _listeners[i];
                if (weak.TryGetTarget(out var target))
                {
                    snapshot.Add(target);
                }
                else
                {
                    _listeners.RemoveAt(i);
                }
            }
        }

        foreach (var action in snapshot)
        {
            action();
        }
    }

    private void Unregister(Action callback)
    {
        lock (_gate)
        {
            for (var i = _listeners.Count - 1; i >= 0; i--)
            {
                var weak = _listeners[i];
                if (!weak.TryGetTarget(out var target) || target == callback)
                {
                    _listeners.RemoveAt(i);
                }
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private LayoutRefreshCoordinator? _owner;
        private Action? _callback;

        public Subscription(LayoutRefreshCoordinator owner, Action callback)
        {
            _owner = owner;
            _callback = callback;
        }

        public void Dispose()
        {
            var owner = Interlocked.Exchange(ref _owner, null);
            var callback = Interlocked.Exchange(ref _callback, null);

            if (owner is not null && callback is not null)
            {
                owner.Unregister(callback);
            }
        }
    }
}

