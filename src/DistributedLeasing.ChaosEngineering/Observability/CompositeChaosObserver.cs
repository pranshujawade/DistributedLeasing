using System;
using System.Collections.Generic;
using System.Linq;
using DistributedLeasing.ChaosEngineering.Faults.Abstractions;
using DistributedLeasing.ChaosEngineering.Policies.Abstractions;

namespace DistributedLeasing.ChaosEngineering.Observability
{
    /// <summary>
    /// Composite observer that forwards events to multiple registered observers.
    /// Implements the Composite pattern for observer management.
    /// </summary>
    public class CompositeChaosObserver : IChaosObserver
    {
        private readonly List<IChaosObserver> _observers = new List<IChaosObserver>();
        private readonly object _lock = new object();

        /// <summary>
        /// Initializes a new instance of the CompositeChaosObserver class.
        /// </summary>
        /// <param name="observers">Initial collection of observers (optional).</param>
        public CompositeChaosObserver(params IChaosObserver[] observers)
        {
            if (observers != null)
            {
                _observers.AddRange(observers.Where(o => o != null));
            }
        }

        /// <summary>
        /// Adds an observer to the composite.
        /// </summary>
        /// <param name="observer">The observer to add.</param>
        public void AddObserver(IChaosObserver observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_lock)
            {
                if (!_observers.Contains(observer))
                {
                    _observers.Add(observer);
                }
            }
        }

        /// <summary>
        /// Removes an observer from the composite.
        /// </summary>
        /// <param name="observer">The observer to remove.</param>
        /// <returns>True if the observer was removed; false if it wasn't found.</returns>
        public bool RemoveObserver(IChaosObserver observer)
        {
            if (observer == null)
            {
                throw new ArgumentNullException(nameof(observer));
            }

            lock (_lock)
            {
                return _observers.Remove(observer);
            }
        }

        /// <summary>
        /// Gets the current count of registered observers.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _observers.Count;
                }
            }
        }

        /// <inheritdoc/>
        public void OnFaultDecisionMade(FaultContext context, FaultDecision decision, string policyName)
        {
            ForEachObserver(o => o.OnFaultDecisionMade(context, decision, policyName));
        }

        /// <inheritdoc/>
        public void OnFaultExecuting(FaultContext context, IFaultStrategy strategy)
        {
            ForEachObserver(o => o.OnFaultExecuting(context, strategy));
        }

        /// <inheritdoc/>
        public void OnFaultExecuted(FaultContext context, IFaultStrategy strategy, TimeSpan duration)
        {
            ForEachObserver(o => o.OnFaultExecuted(context, strategy, duration));
        }

        /// <inheritdoc/>
        public void OnFaultExecutionFailed(FaultContext context, IFaultStrategy strategy, Exception exception)
        {
            ForEachObserver(o => o.OnFaultExecutionFailed(context, strategy, exception));
        }

        /// <inheritdoc/>
        public void OnFaultSkipped(FaultContext context, string reason)
        {
            ForEachObserver(o => o.OnFaultSkipped(context, reason));
        }

        private void ForEachObserver(Action<IChaosObserver> action)
        {
            IChaosObserver[] observersCopy;
            lock (_lock)
            {
                observersCopy = _observers.ToArray();
            }

            foreach (var observer in observersCopy)
            {
                try
                {
                    action(observer);
                }
                catch
                {
                    // Suppress exceptions from individual observers to prevent cascading failures
                    // Consider logging this in production scenarios
                }
            }
        }
    }
}
