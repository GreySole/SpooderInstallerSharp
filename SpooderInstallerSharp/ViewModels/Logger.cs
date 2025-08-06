using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SpooderInstallerSharp.ViewModels
{
    public static class Logger
    {
        private static readonly string _logFilePath;
        private static readonly object _lockObject = new object();
        private static readonly string _logDirectory;

        public static string LogFilePath => _logFilePath;

        static Logger()
        {
            _logDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SpooderInstaller",
                "Logs"
            );

            // Ensure the log directory exists
            Directory.CreateDirectory(_logDirectory);

            // Generate log file name with timestamp
            string fileName = $"SpooderInstaller_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            _logFilePath = Path.Combine(_logDirectory, fileName);

            // Write initial log entry
            WriteToFile($"=== Log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public static void LogInfo(string message)
        {
            Log("INFO", message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public static void LogWarning(string message)
        {
            Log("WARN", message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
        public static void LogError(string message)
        {
            Log("ERROR", message);
        }

        /// <summary>
        /// Logs an error with exception details
        /// </summary>
        public static void LogError(string message, Exception exception)
        {
            Log("ERROR", $"{message} - Exception: {exception.Message}\nStack Trace: {exception.StackTrace}");
        }

        /// <summary>
        /// Logs a debug message
        /// </summary>
        public static void LogDebug(string message)
        {
            Log("DEBUG", message);
        }

        /// <summary>
        /// Logs a message with the specified level
        /// </summary>
        public static void Log(string level, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logEntry = $"[{timestamp}] [{level}] {message}";

            WriteToFile(logEntry);

            // Also output to debug console for development
            Debug.WriteLine(logEntry);
        }

        /// <summary>
        /// Writes a raw message to the log file without formatting
        /// </summary>
        public static void LogRaw(string message)
        {
            WriteToFile(message);
        }

        /// <summary>
        /// Opens the log file in the default text editor
        /// </summary>
        public static void OpenLogFileInEditor()
        {
            try
            {
                if (!File.Exists(_logFilePath))
                {
                    LogWarning("Log file does not exist, cannot open it");
                    return;
                }

                ProcessStartInfo? processStartInfo = GetPlatformSpecificTextEditorProcess(_logFilePath);

                if (processStartInfo != null)
                {
                    Process.Start(processStartInfo);
                    LogInfo($"Opened log file in default text editor: {_logFilePath}");
                }
                else
                {
                    LogError("Could not determine appropriate text editor for this platform");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to open log file in text editor", ex);
            }
        }

        /// <summary>
        /// Opens the log directory in the file explorer instead of opening the log file directly
        /// </summary>
        public static void OpenLogFile()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    LogWarning("Log directory does not exist, cannot open it");
                    return;
                }

                ProcessStartInfo? processStartInfo = GetPlatformSpecificFileManagerProcess(_logDirectory);

                if (processStartInfo != null)
                {
                    Process.Start(processStartInfo);
                    LogInfo($"Opened log directory in file explorer: {_logDirectory}");
                }
                else
                {
                    LogError("File manager not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to open log directory", ex);
            }
        }

        /// <summary>
        /// Opens the log directory in the file explorer
        /// </summary>
        public static void OpenLogDirectory()
        {
            try
            {
                if (!Directory.Exists(_logDirectory))
                {
                    LogWarning("Log directory does not exist, cannot open it");
                    return;
                }

                ProcessStartInfo? processStartInfo = GetPlatformSpecificFileManagerProcess(_logDirectory);

                if (processStartInfo != null)
                {
                    Process.Start(processStartInfo);
                    LogInfo($"Opened log directory in file explorer: {_logDirectory}");
                }
                else
                {
                    LogError("File manager not supported on this platform");
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to open log directory", ex);
            }
        }

        /// <summary>
        /// Clears the current log file
        /// </summary>
        public static void ClearLog()
        {
            try
            {
                lock (_lockObject)
                {
                    File.WriteAllText(_logFilePath, string.Empty);
                }
                WriteToFile($"=== Log cleared at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                LogInfo("Log file cleared");
            }
            catch (Exception ex)
            {
                LogError($"Failed to clear log file", ex);
            }
        }

        /// <summary>
        /// Gets all log files in the log directory
        /// </summary>
        public static string[] GetLogFiles()
        {
            try
            {
                return Directory.GetFiles(_logDirectory, "*.log");
            }
            catch (Exception ex)
            {
                LogError($"Failed to get log files", ex);
                return Array.Empty<string>();
            }
        }

        /// <summary>
        /// Deletes old log files, keeping only the specified number of recent files
        /// </summary>
        public static void CleanupOldLogs(int keepCount = 10)
        {
            try
            {
                var logFiles = Directory.GetFiles(_logDirectory, "*.log");
                if (logFiles.Length <= keepCount)
                    return;

                var sortedFiles = logFiles.OrderByDescending(f => File.GetCreationTime(f)).ToArray();
                var filesToDelete = sortedFiles.Skip(keepCount);

                foreach (var file in filesToDelete)
                {
                    try
                    {
                        File.Delete(file);
                        LogInfo($"Deleted old log file: {Path.GetFileName(file)}");
                    }
                    catch (Exception ex)
                    {
                        LogWarning($"Failed to delete log file {Path.GetFileName(file)}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogError($"Failed to cleanup old logs", ex);
            }
        }

        /// <summary>
        /// Creates a new log file with a custom filename
        /// </summary>
        public static void CreateNewLogFile(string? customFileName = null)
        {
            try
            {
                string fileName = customFileName ?? $"SpooderInstaller_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
                string newLogPath = Path.Combine(_logDirectory, fileName);
                
                // Use reflection to update the static readonly field
                var logFilePathField = typeof(Logger).GetField("_logFilePath", 
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                logFilePathField?.SetValue(null, newLogPath);
                
                WriteToFile($"=== New log started at {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===");
                LogInfo($"Created new log file: {newLogPath}");
            }
            catch (Exception ex)
            {
                LogError($"Failed to create new log file", ex);
            }
        }

        private static void WriteToFile(string message)
        {
            try
            {
                lock (_lockObject)
                {
                    File.AppendAllText(_logFilePath, message + Environment.NewLine);
                }
            }
            catch (Exception ex)
            {
                // If we can't write to the log file, at least output to debug console
                Debug.WriteLine($"Failed to write to log file: {ex.Message}");
                Debug.WriteLine($"Original message: {message}");
            }
        }

        private static ProcessStartInfo? GetPlatformSpecificTextEditorProcess(string filePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo
                {
                    FileName = "notepad.exe",
                    Arguments = $"\"{filePath}\"",
                    UseShellExecute = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try common Linux text editors in order of preference
                string[] textEditors = { "gedit", "kate", "mousepad", "leafpad", "nano", "vim", "xdg-open" };

                foreach (string editor in textEditors)
                {
                    if (IsCommandAvailable(editor))
                    {
                        return new ProcessStartInfo
                        {
                            FileName = editor,
                            Arguments = $"\"{filePath}\"",
                            UseShellExecute = true
                        };
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"-t \"{filePath}\"",
                    UseShellExecute = true
                };
            }

            // Fallback: try to open with default application
            return new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            };
        }

        private static ProcessStartInfo? GetPlatformSpecificFileManagerProcess(string path)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                };
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // Try common Linux file managers in order of preference
                string[] fileManagers = { "xdg-open", "nautilus", "dolphin", "thunar", "pcmanfm", "nemo" };

                foreach (string fileManager in fileManagers)
                {
                    if (IsCommandAvailable(fileManager))
                    {
                        return new ProcessStartInfo
                        {
                            FileName = fileManager,
                            Arguments = $"\"{path}\"",
                            UseShellExecute = true
                        };
                    }
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                return new ProcessStartInfo
                {
                    FileName = "open",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                };
            }

            return null;
        }

        private static bool IsCommandAvailable(string command)
        {
            try
            {
                var whichCommand = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "where" : "which";
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = whichCommand,
                    Arguments = command,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processStartInfo))
                {
                    if (process != null)
                    {
                        process.WaitForExit();
                        return process.ExitCode == 0;
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }
    }
}
