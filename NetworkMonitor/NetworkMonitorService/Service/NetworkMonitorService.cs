using System;
using System.Linq;
using System.ServiceProcess;
using System.Threading;
using NetworkMonitor.Shared.Models;
using NetworkMonitor.Shared.Configuration;

namespace NetworkMonitor.Service
{
    public partial class NetworkMonitorService : ServiceBase
    {
        private MonitorEngine _monitorEngine;
        private Thread _loggingThread;
        private bool _isRunning = false;
        private readonly string _logFilePath = @"C:\NetworkMonitor\Logs\service.log";

        public NetworkMonitorService()
        {
            InitializeComponent();
            ServiceName = "NetworkMonitorService";
        }

        protected override void OnStart(string[] args)
        {
            try
            {
                // Create log directory if it doesn't exist
                var logDir = System.IO.Path.GetDirectoryName(_logFilePath);
                if (!System.IO.Directory.Exists(logDir))
                    System.IO.Directory.CreateDirectory(logDir);

                // Load configuration
                var config = ConfigManager.LoadConfiguration();
                
                // Initialize and start monitor engine
                _monitorEngine = new MonitorEngine(config);
                _monitorEngine.LogMessage += OnLogMessage;
                _monitorEngine.Start();

                // Start logging thread
                _isRunning = true;
                _loggingThread = new Thread(LogStatusLoop);
                _loggingThread.IsBackground = true;
                _loggingThread.Start();

                Log("Network Monitor Service started successfully.");
            }
            catch (Exception ex)
            {
                Log($"Error starting service: {ex.Message}");
                throw;
            }
        }

        protected override void OnStop()
        {
            try
            {
                _isRunning = false;
                
                // Stop monitor engine
                _monitorEngine?.Stop();
                
                // Wait for logging thread to exit
                _loggingThread?.Join(5000);
                
                Log("Network Monitor Service stopped.");
            }
            catch (Exception ex)
            {
                Log($"Error stopping service: {ex.Message}");
            }
        }

        private void LogStatusLoop()
        {
            while (_isRunning)
            {
                try
                {
                    // Log current status every minute
                    if (_monitorEngine != null)
                    {
                        var statuses = _monitorEngine.GetCurrentStatuses();
                        
                        // FIXED: Count is a property, not a method
                        if (statuses.Count > 0) // Changed from statuses.Count() to statuses.Count
                        {
                            var onlineCount = statuses.Count(s => s.IsOnline);
                            Log($"Status: {onlineCount}/{statuses.Count} devices online"); // Fixed here too
                        }
                    }
                    
                    Thread.Sleep(60000); // Sleep for 1 minute
                }
                catch (ThreadInterruptedException)
                {
                    // Expected when stopping
                    break;
                }
                catch (Exception ex)
                {
                    Log($"Error in logging thread: {ex.Message}");
                    Thread.Sleep(10000); // Wait 10 seconds on error
                }
            }
        }

        private void OnLogMessage(object sender, string message)
        {
            Log(message);
        }

        private void Log(string message)
        {
            try
            {
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var logMessage = $"[{timestamp}] {message}";
                
                // Write to console (for debugging)
                Console.WriteLine(logMessage);
                
                // Write to log file
                System.IO.File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
            catch
            {
                // Ignore logging errors
            }
        }

        private void InitializeComponent()
        {
            this.CanHandlePowerEvent = true;
            this.CanHandleSessionChangeEvent = true;
            this.CanPauseAndContinue = true;
            this.CanShutdown = true;
            this.CanStop = true;
            this.AutoLog = true;
        }

        // Public methods for console debugging
        public void StartService()
        {
            OnStart(null);
        }

        public void StopService()
        {
            OnStop();
        }
    }
}