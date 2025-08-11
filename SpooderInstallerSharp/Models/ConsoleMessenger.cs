using Avalonia.Logging;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using SpooderInstallerSharp.ViewModels;
using Logger = SpooderInstallerSharp.ViewModels.Logger;

namespace SpooderInstallerSharp.Models
{
    /// <summary>
    /// Static console messaging service that provides semantic console output functionality
    /// accessible from anywhere in the application.
    /// </summary>
    public static class ConsoleMessenger
    {
        private static Action<string?, string[]?>? _addConsoleItemWithClasses;

        /// <summary>
        /// Event fired when a message is sent (useful for logging or debugging)
        /// </summary>
        public static event Action<string, string[]?>? MessageSent;

        /// <summary>
        /// Initializes the console messenger with the required actions.
        /// This should be called once during application startup.
        /// </summary>
        /// <param name="addConsoleItemWithClasses">Action to add styled console items</param>
        /// <param name="fallbackConsoleOutput">Fallback action for basic console output</param>
        public static void Initialize(Action<string?, string[]?>? addConsoleItemWithClasses)
        {
            _addConsoleItemWithClasses = addConsoleItemWithClasses;
            
            Debug.WriteLine($"ConsoleMessenger initialized - Enhanced: {addConsoleItemWithClasses != null}");
        }

        /// <summary>
        /// Checks if the messenger is properly initialized
        /// </summary>
        public static bool IsInitialized => _addConsoleItemWithClasses != null;

        /// <summary>
        /// Sends a message with optional CSS classes to the console
        /// </summary>
        /// <param name="message">The message to display</param>
        /// <param name="cssClasses">Optional CSS classes for styling</param>
        private static void SendMessage(string? message, params string[] cssClasses)
        {
            if (string.IsNullOrEmpty(message))
                return;


            // Fire the event for any listeners
            MessageSent?.Invoke(message, cssClasses.Length > 0 ? cssClasses : null);

            // Ensure we're on the UI thread for UI operations
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.InvokeAsync(() => SendMessage(message, cssClasses));
                return;
            }

            // Try enhanced console output first
            if (_addConsoleItemWithClasses != null)
            {
                _addConsoleItemWithClasses(message, cssClasses.Length > 0 ? cssClasses : null);
            }
            else
            {
                Debug.WriteLine("ConsoleMessenger: No console actions available, using Debug output");
                Debug.WriteLine($"[CONSOLE] {message}");
            }
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
        /// Clear console actions (useful for testing or shutdown)
        /// </summary>
        public static void Cleanup()
        {
            _addConsoleItemWithClasses = null;
            MessageSent = null;
            Debug.WriteLine("ConsoleMessenger cleaned up");
        }
    }
}