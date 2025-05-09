using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using System.Linq;
using TDFAPI.Messaging.Interfaces;

namespace TDFAPI.Messaging
{
    /// <summary>
    /// Implementation of the event mediator that allows services to publish and subscribe to events
    /// </summary>
    public class EventMediator : IEventMediator
    {
        private readonly ILogger<EventMediator> _logger;
        private readonly ConcurrentDictionary<Type, List<Delegate>> _syncHandlers = new();
        private readonly ConcurrentDictionary<Type, List<Delegate>> _asyncHandlers = new();

        public EventMediator(ILogger<EventMediator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        public void Publish<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            
            // Process synchronous handlers
            if (_syncHandlers.TryGetValue(eventType, out var syncHandlers))
            {
                foreach (var handler in syncHandlers.ToList())
                {
                    try
                    {
                        ((Action<TEvent>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing sync handler for event {EventType}", eventType.Name);
                    }
                }
            }
            
            // Process asynchronous handlers
            if (_asyncHandlers.TryGetValue(eventType, out var asyncHandlers))
            {
                foreach (var handler in asyncHandlers.ToList())
                {
                    try
                    {
                        var task = ((Func<TEvent, Task>)handler)(eventData);
                        
                        // Fire and forget - we don't wait for the result
                        task.ContinueWith(t =>
                        {
                            if (t.IsFaulted)
                            {
                                _logger.LogError(t.Exception, "Error executing async handler for event {EventType}", eventType.Name);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing async handler for event {EventType}", eventType.Name);
                    }
                }
            }
        }
        
        /// <summary>
        /// Publish an event to all subscribers asynchronously and wait for completion
        /// </summary>
        public async Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            var tasks = new List<Task>();
            
            // Process synchronous handlers
            if (_syncHandlers.TryGetValue(eventType, out var syncHandlers))
            {
                foreach (var handler in syncHandlers.ToList())
                {
                    try
                    {
                        ((Action<TEvent>)handler)(eventData);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing sync handler for event {EventType}", eventType.Name);
                    }
                }
            }
            
            // Process asynchronous handlers
            if (_asyncHandlers.TryGetValue(eventType, out var asyncHandlers))
            {
                foreach (var handler in asyncHandlers.ToList())
                {
                    try
                    {
                        var task = ((Func<TEvent, Task>)handler)(eventData);
                        tasks.Add(task);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error executing async handler for event {EventType}", eventType.Name);
                    }
                }
            }
            
            // Wait for all async handlers to complete
            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Subscribe to an event
        /// </summary>
        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            _syncHandlers.AddOrUpdate(
                eventType,
                new List<Delegate> { handler },
                (_, handlers) =>
                {
                    handlers.Add(handler);
                    return handlers;
                });
            
            _logger.LogDebug("Registered sync handler for event {EventType}", eventType.Name);
        }

        /// <summary>
        /// Subscribe to an event with async handler
        /// </summary>
        public void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            _asyncHandlers.AddOrUpdate(
                eventType,
                new List<Delegate> { handler },
                (_, handlers) =>
                {
                    handlers.Add(handler);
                    return handlers;
                });
            
            _logger.LogDebug("Registered async handler for event {EventType}", eventType.Name);
        }

        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            if (_syncHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                _logger.LogDebug("Unregistered sync handler for event {EventType}", eventType.Name);
            }
        }

        /// <summary>
        /// Unsubscribe from an event with async handler
        /// </summary>
        public void Unsubscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent
        {
            var eventType = typeof(TEvent);
            if (_asyncHandlers.TryGetValue(eventType, out var handlers))
            {
                handlers.Remove(handler);
                _logger.LogDebug("Unregistered async handler for event {EventType}", eventType.Name);
            }
        }
    }
} 