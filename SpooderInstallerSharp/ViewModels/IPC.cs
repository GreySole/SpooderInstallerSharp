using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpooderInstallerSharp.ViewModels
{
    internal class IPC
    {
        private Process _process;
        private readonly Action<string> _appendToConsoleOutput;
        private NamedPipeServerStream _pipeServer;
        private string _pipeName;
        private bool _isDisposed = false;

        public IPC(Action<string> appendToConsoleOutput)
        {
            _appendToConsoleOutput = appendToConsoleOutput;
        }

        // Add event for receiving IPC messages
        public event EventHandler<string> MessageReceived;

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
            Task.Run(async () =>
            {
                try
                {
                    await _pipeServer.WaitForConnectionAsync();
                    _appendToConsoleOutput("IPC pipe connected successfully");

                    using (var reader = new StreamReader(_pipeServer))
                    {
                        string line;
                        while ((line = await reader.ReadLineAsync()) != null && !_isDisposed)
                        {
                            OnMessageReceived(line);
                        }
                    }
                }
                catch (Exception ex) when (!_isDisposed)
                {
                    _appendToConsoleOutput($"Error in IPC pipe: {ex.Message}");
                }
            });
        }

        private void SetupStdoutReading()
        {
            // Start a background task to read stdout for regular output
            Task.Run(async () =>
            {
                try
                {
                    using (var reader = new StreamReader(_process.StandardOutput.BaseStream))
                    {
                        string line;
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
                                // Regular output
                                _appendToConsoleOutput(line);
                            }
                        }
                    }
                }
                catch (Exception ex) when (!_isDisposed)
                {
                    _appendToConsoleOutput($"Error reading stdout: {ex.Message}");
                }
            });
        }

        private bool IsJsonMessage(string line)
        {
            try
            {
                var trimmed = line.Trim();
                return (trimmed.StartsWith("{") && trimmed.EndsWith("}")) ||
                       (trimmed.StartsWith("[") && trimmed.EndsWith("]"));
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
                    _appendToConsoleOutput($"Error sending message to Spooder: {ex.Message}");
                }
            }
        }

        public string GetPipeName()
        {
            return _pipeName;
        }

        public void Cleanup()
        {
            _isDisposed = true;

            try
            {
                _pipeServer?.Close();
                _pipeServer?.Dispose();
            }
            catch (Exception ex)
            {
                _appendToConsoleOutput($"Error cleaning up IPC pipe: {ex.Message}");
            }

            _pipeServer = null;
            _process = null;
        }
    }
}