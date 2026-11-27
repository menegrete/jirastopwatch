using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using StopWatch.Plugin;

namespace StopWatch
{
    /// <summary>A replacement handler together with the plugin that registered it.</summary>
    internal class RegisteredHandler
    {
        public RegisteredHandler(string pluginId, Func<TimeLoadRequest, Task<InsteadOfResult>> handler)
        {
            PluginId = pluginId;
            Handler = handler;
        }

        public string PluginId { get; private set; }

        public Func<TimeLoadRequest, Task<InsteadOfResult>> Handler { get; private set; }
    }


    /// <summary>
    /// Who takes part in time loading: at most one replacement handler per
    /// plugin, and any number of observers. Handlers come back in the order
    /// they are consulted - by plugin id, ordinal and ignoring case - so the
    /// outcome never depends on which plugin happened to load first.
    /// </summary>
    internal class TimeLoadRegistry
    {
        private readonly object gate = new object();
        private readonly List<RegisteredHandler> handlers = new List<RegisteredHandler>();
        private readonly List<EventHandler<TimeLoadedEventArgs>> observers = new List<EventHandler<TimeLoadedEventArgs>>();


        public void RegisterInsteadOf(string pluginId, Func<TimeLoadRequest, Task<InsteadOfResult>> handler)
        {
            if (string.IsNullOrEmpty(pluginId))
                throw new ArgumentException("A plugin id is required.", nameof(pluginId));
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            lock (gate)
            {
                if (handlers.Any(h => string.Equals(h.PluginId, pluginId, StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException($"Plugin {pluginId} already registered a time-load handler.");

                handlers.Add(new RegisteredHandler(pluginId, handler));
            }
        }


        public void AddObserver(EventHandler<TimeLoadedEventArgs> observer)
        {
            if (observer == null)
                throw new ArgumentNullException(nameof(observer));

            lock (gate)
                observers.Add(observer);
        }


        public void RemoveObserver(EventHandler<TimeLoadedEventArgs> observer)
        {
            lock (gate)
                observers.Remove(observer);
        }


        /// <summary>The handlers in consultation order.</summary>
        public IReadOnlyList<RegisteredHandler> Handlers
        {
            get
            {
                lock (gate)
                    return handlers.OrderBy(h => h.PluginId, StringComparer.OrdinalIgnoreCase).ToList();
            }
        }


        /// <summary>The observers in the order they subscribed.</summary>
        public IReadOnlyList<EventHandler<TimeLoadedEventArgs>> Observers
        {
            get
            {
                lock (gate)
                    return observers.ToList();
            }
        }
    }
}
