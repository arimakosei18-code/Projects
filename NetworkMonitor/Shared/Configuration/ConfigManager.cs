using System;
using System.IO;
using System.Xml.Serialization;
using NetworkMonitor.Shared.Models;

namespace NetworkMonitor.Shared.Configuration
{
    public static class ConfigManager
    {
        private static readonly string ConfigPath = @"C:\NetworkMonitor\config.xml";
        
        public static AppConfiguration LoadConfiguration()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var serializer = new XmlSerializer(typeof(AppConfiguration));
                    using (var reader = new StreamReader(ConfigPath))
                    {
                        return (AppConfiguration)serializer.Deserialize(reader);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading config: {ex.Message}");
            }
            
            // Return default configuration if file doesn't exist or error occurs
            return new AppConfiguration();
        }
        
        public static void SaveConfiguration(AppConfiguration config)
        {
            try
            {
                var directory = Path.GetDirectoryName(ConfigPath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                
                var serializer = new XmlSerializer(typeof(AppConfiguration));
                using (var writer = new StreamWriter(ConfigPath))
                {
                    serializer.Serialize(writer, config);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving config: {ex.Message}");
                throw;
            }
        }
    }
}