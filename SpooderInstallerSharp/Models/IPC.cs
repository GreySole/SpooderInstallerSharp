using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.Models
{
    public class IPC : IDisposable
    {
        private Process? _process;
        private NamedPipeServerStream? _pipeServer;
        private string? _pipeName;
        private bool _isDisposed = false;

        public IPC()
        {
            // No longer need appendToConsoleOutput - using static ConsoleMessenger
        }

        // Add event for receiving IPC messages
        public event EventHandler<string>? MessageReceived;

        protected virtual void OnMessageReceived(string message)
        {
            MessageReceived?.Invoke(this, message);
        }

        public void SetupIpcCommunication(Process process)
        {
            _process = process;

            // Set up named pipe for true IPC
            SetupNamedPipe();

            // Set up stdout reading for regular output
            SetupStdoutReading();
        }

        private void SetupNamedPipe()
        {
            _pipeName = $"spooder_ipc_{Guid.NewGuid():N}";
            _pipeServer = new NamedPipeServerStream(_pipeName, PipeDirection.InOut, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            _isDisposed = false;

            // Start listening for pipe connections
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_pipeServer != null)
                    {
                        await _pipeServer.WaitForConnectionAsync();
                        ConsoleMessenger.AddSuccessMessage("IPC pipe connected successfully");

                        using (var reader = new StreamReader(_pipeServer))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null && !_isDisposed)
                            {
                                OnMessageReceived(line);
                            }
                        }
                    }
                }
                catch (Exception ex) when (!_isDisposed)
                {
                    ConsoleMessenger.AddErrorMessageF("Error in IPC pipe: {0}", ex.Message);
                }
            });
        }

        private void SetupStdoutReading()
        {
            // Start a background task to read stdout for regular output
            _ = Task.Run(async () =>
            {
                try
                {
                    if (_process?.StandardOutput != null)
                    {
                        using (var reader = new StreamReader(_process.StandardOutput.BaseStream))
                        {
                            string? line;
                            while ((line = await reader.ReadLineAsync()) != null && !_isDisposed)
                            {
                                // Check if this is a structured IPC message via stdout
                                if (line.StartsWith("IPC:"))
                                {
                                    OnMessageReceived(line.Substring(4)); // Remove "IPC:" prefix
                                }
                                else if (IsJsonMessage(line))
                                {
                                    OnMessageReceived(line);
                                }
                                else
                                {
                                    // Regular output - use ConsoleMessenger's plain message for real-time output
                                    ConsoleMessenger.AddPlainMessage(line);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) when (!_isDisposed)
                {
                    ConsoleMessenger.AddErrorMessageF("Error reading stdout: {0}", ex.Message);
                }
            });
        }

        private static bool IsJsonMessage(string line)
        {
            try
            {
                var trimmed = line.Trim();
                return trimmed.StartsWith("{") && trimmed.EndsWith("}") ||
                       trimmed.StartsWith("[") && trimmed.EndsWith("]");
            }
            catch
            {
                return false;
            }
        }

        public void SendMessageToSpooder(string message)
        {
            if (_process != null && !_process.HasExited)
            {
                try
                {
                    // Try to send via pipe first (true IPC)
                    if (_pipeServer != null && _pipeServer.IsConnected)
                    {
                        using (var writer = new StreamWriter(_pipeServer, leaveOpen: true))
                        {
                            writer.WriteLine(message);
                            writer.Flush();
                        }
                    }
                    else
                    {
                        // Fallback to stdin
                        _process.StandardInput.WriteLine(message);
                        _process.StandardInput.Flush();
                    }
                }
                catch (Exception ex)
                {
                    ConsoleMessenger.AddErrorMessageF("Error sending message to Spooder: {0}", ex.Message);
                }
            }
        }

        public string? GetPipeName()
        {
            return _pipeName;
        }

        public void Cleanup()
        {
            Dispose();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed && disposing)
            {
                _isDisposed = true;

                try
                {
                    _pipeServer?.Close();
                    _pipeServer?.Dispose();
                }
                catch (Exception ex)
                {
                    ConsoleMessenger.AddErrorMessageF("Error cleaning up IPC pipe: {0}", ex.Message);
                }

                _pipeServer = null;
                _process = null;
            }
        }
    }
}