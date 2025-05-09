using System;
using System.Threading.Tasks;
using TDFAPI.Messaging.Interfaces;

namespace TDFAPI.Messaging.Interfaces
{
    /// <summary>
    /// Interface for event mediator that allows services to publish and subscribe to events
    /// </summary>
    public interface IEventMediator
    {
        /// <summary>
        /// Publish an event to all subscribers
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="eventData">Event data</param>
        void Publish<TEvent>(TEvent eventData) where TEvent : IEvent;
        
        /// <summary>
        /// Publish an event to all subscribers asynchronously and wait for completion
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="eventData">Event data</param>
        /// <returns>Task representing the asynchronous operation</returns>
        Task PublishAsync<TEvent>(TEvent eventData) where TEvent : IEvent;
        
        /// <summary>
        /// Subscribe to an event
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="handler">Handler function</param>
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
        
        /// <summary>
        /// Subscribe to an event with async handler
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="handler">Async handler function</param>
        void Subscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent;
        
        /// <summary>
        /// Unsubscribe from an event
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="handler">Handler function to unsubscribe</param>
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IEvent;
        
        /// <summary>
        /// Unsubscribe from an event with async handler
        /// </summary>
        /// <typeparam name="TEvent">Type of event</typeparam>
        /// <param name="handler">Async handler function to unsubscribe</param>
        void Unsubscribe<TEvent>(Func<TEvent, Task> handler) where TEvent : IEvent;
    }
} 