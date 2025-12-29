using System;
using System.ServiceProcess;
using System.Threading;

namespace NetworkMonitor.Service
{
    static class Program
    {
        static void Main()
        {
            // Check if running as console app for debugging
            if (Environment.UserInteractive)
            {
                Console.WriteLine("Running in console mode (for debugging)...");
                
                // Create and run service manually for debugging
                RunServiceInConsoleMode();
            }
            else
            {
                // Run as Windows Service
                ServiceBase[] ServicesToRun;
                ServicesToRun = new ServiceBase[]
                {
                    new NetworkMonitorService()
                };
                ServiceBase.Run(ServicesToRun);
            }
        }

        private static void RunServiceInConsoleMode()
        {
            var service = new NetworkMonitorService();
            
            // Use reflection to call the protected methods
            var onStartMethod = typeof(ServiceBase).GetMethod("OnStart", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var onStopMethod = typeof(ServiceBase).GetMethod("OnStop", 
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            
            try
            {
                Console.WriteLine("Starting service...");
                onStartMethod?.Invoke(service, new object[] { new string[] { } });
                
                Console.WriteLine("Service started. Press Enter to stop...");
                Console.ReadLine();
                
                Console.WriteLine("Stopping service...");
                onStopMethod?.Invoke(service, null);
                
                Console.WriteLine("Service stopped.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}