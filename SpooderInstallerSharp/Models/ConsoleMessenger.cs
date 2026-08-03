using Avalonia.Logging;
using Avalonia.Threading;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Logger = SpooderInstallerSharp.Models.Logger;

namespace SpooderInstallerSharp.Models
{
    /// <summary>
    /// Static console messaging service that provides semantic console output functionality
    /// accessible from anywhere in the application.
    /// </summary>
    public static class ConsoleMessenger
    {
        private static Action<string?, string[]?>? _addConsoleItemWithClasses;

        // Queue for storing messages before initialization
        private static readonly Queue<QueuedMessage> _messageQueue = new Queue<QueuedMessage>();
        private static readonly object _queueLock = new object();

        /// <summary>
        /// Event fired when a message is sent (useful for logging or debugging)
        /// </summary>
        public static event Action<string, string[]?>? MessageSent;

        /// <summary>
        /// Helper class to store queued messages
        /// </summary>
        private class QueuedMessage
        {
            public string Message { get; set; } = string.Empty;
            public string[] CssClasses { get; set; } = Array.Empty<string>();
            public DateTime Timestamp { get; set; } = DateTime.Now;
        }

        /// <summary>
        /// Initializes the console messenger with the required actions.
        /// This should be called once during application startup.
        /// </summary>
        /// <param name="addConsoleItemWithClasses">Action to add styled console items</param>
        public static void Initialize(Action<string?, string[]?>? addConsoleItemWithClasses)
        {
            _addConsoleItemWithClasses = addConsoleItemWithClasses;
            
            Debug.WriteLine($"ConsoleMessenger initialized - Enhanced: {addConsoleItemWithClasses != null}");
            
            // Process any queued messages
            ProcessQueuedMessages();
        }

        /// <summary>
        /// Processes all queued messages that were sent before initialization
        /// </summary>
        private static void ProcessQueuedMessages()
        {
            lock (_queueLock)
            {
                if (_messageQueue.Count == 0)
                {
                    Debug.WriteLine("ConsoleMessenger: No queued messages to process");
                    return;
                }

                Debug.WriteLine($"ConsoleMessenger: Processing {_messageQueue.Count} queued messages");
                
                // Process all queued messages in order
                while (_messageQueue.Count > 0)
                {
                    var queuedMessage = _messageQueue.Dequeue();
                    
                    // Send the message directly (bypassing the queue check since we're now initialized)
                    SendMessageDirect(queuedMessage.Message, queuedMessage.CssClasses);
                }
                
                Debug.WriteLine("ConsoleMessenger: Finished processing queued messages");
            }
        }

        /// <summary>
        /// Checks if the messenger is properly initialized
        /// </summary>
        public static bool IsInitialized => _addConsoleItemWithClasses != null;

        /// <summary>
        /// Gets the number of messages currently in the queue
        /// </summary>
        public static int QueuedMessageCount
        {
            get
            {
                lock (_queueLock)
                {
                    return _messageQueue.Count;
                }
            }
        }

        /// <summary>
        /// Sends a message with optional CSS classes to the console
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="cssClasses">Optional CSS classes for styling</param>
        private static void SendMessage(string? message, params string[] cssClasses)
        {
            if (string.IsNullOrEmpty(message))
                return;

            // If not initialized, queue the message
            if (!IsInitialized)
            {
                QueueMessage(message, cssClasses);
                return;
            }

            SendMessageDirect(message, cssClasses);
        }

        /// <summary>
        /// Queues a message for later processing when Initialize is called
        /// </summary>
        /// <param name="message">The message to queue</param>
        /// <param name="cssClasses">CSS classes for the message</param>
        private static void QueueMessage(string message, string[] cssClasses)
        {
            lock (_queueLock)
            {
                _messageQueue.Enqueue(new QueuedMessage
                {
                    Message = message,
                    CssClasses = cssClasses,
                    Timestamp = DateTime.Now
                });
                
                Debug.WriteLine($"ConsoleMessenger: Queued message (Queue size: {_messageQueue.Count}): {message}");
            }
            
            // Still log queued messages immediately
            Logger.Log("Console", $"[QUEUED] {message}");
        }

        /// <summary>
        /// Sends a message directly without queuing (used internally)
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="cssClasses">Optional CSS classes for styling</param>
        private static void SendMessageDirect(string message, string[] cssClasses)
        {
            // Fire the event for any listeners
            MessageSent?.Invoke(message, cssClasses.Length > 0 ? cssClasses : null);

            // Ensure we're on the UI thread for UI operations
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => SendMessageDirect(message, cssClasses));
                return;
            }

            // Send to console
            if (_addConsoleItemWithClasses != null)
            {
                _addConsoleItemWithClasses(message, cssClasses.Length > 0 ? cssClasses : null);
            }
            else
            {
                // This shouldn't happen if we're checking IsInitialized properly, but just in case
                Debug.WriteLine($"ConsoleMessenger: Fallback - no console action available for: {message}");
            }
            
            // Log the message
            Logger.Log("Console", message);
        }

        /// <summary>
        /// Semantic message methods - these can be called from anywhere in the app
        /// </summary>
        public static void AddErrorMessage(string message) => SendMessage(message, "error");
        public static void AddWarningMessage(string message) => SendMessage(message, "warning");
        public static void AddSuccessMessage(string message) => SendMessage(message, "success");
        public static void AddInfoMessage(string message) => SendMessage(message, "info");
        public static void AddDebugMessage(string message) => SendMessage(message, "debug");

        /// <summary>
        /// Send a message with custom CSS classes
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="cssClasses">CSS classes for custom styling</param>
        public static void AddStyledMessage(string message, params string[] cssClasses) => SendMessage(message, cssClasses);

        /// <summary>
        /// Send a plain message without any styling
        /// </summary>
        /// <param name="message">The message to display</param>
        public static void AddPlainMessage(string message) => SendMessage(message);

        /// <summary>
        /// Conditional message methods - only send if condition is true
        /// </summary>
        public static void AddErrorMessageIf(bool condition, string message) { if (condition) AddErrorMessage(message); }
        public static void AddWarningMessageIf(bool condition, string message) { if (condition) AddWarningMessage(message); }
        public static void AddSuccessMessageIf(bool condition, string message) { if (condition) AddSuccessMessage(message); }
        public static void AddInfoMessageIf(bool condition, string message) { if (condition) AddInfoMessage(message); }
        public static void AddDebugMessageIf(bool condition, string message) { if (condition) AddDebugMessage(message); }

        /// <summary>
        /// Format and send messages with interpolation
        /// </summary>
        public static void AddErrorMessageF(string format, params object[] args) => AddErrorMessage(string.Format(format, args));
        public static void AddWarningMessageF(string format, params object[] args) => AddWarningMessage(string.Format(format, args));
        public static void AddSuccessMessageF(string format, params object[] args) => AddSuccessMessage(string.Format(format, args));
        public static void AddInfoMessageF(string format, params object[] args) => AddInfoMessage(string.Format(format, args));
        public static void AddDebugMessageF(string format, params object[] args) => AddDebugMessage(string.Format(format, args));

        /// <summary>
        /// Clear console actions and message queue (useful for testing or shutdown)
        /// </summary>
        public static void Cleanup()
        {
            _addConsoleItemWithClasses = null;
            MessageSent = null;
            
            lock (_queueLock)
            {
                var queuedCount = _messageQueue.Count;
                _messageQueue.Clear();
                Debug.WriteLine($"ConsoleMessenger cleaned up - cleared {queuedCount} queued messages");
            }
        }

        /// <summary>
        /// Clear only the message queue without affecting initialization state (useful for testing)
        /// </summary>
        public static void ClearQueue()
        {
            lock (_queueLock)
            {
                var queuedCount = _messageQueue.Count;
                _messageQueue.Clear();
                Debug.WriteLine($"ConsoleMessenger: Cleared {queuedCount} queued messages");
            }
        }
    }
}