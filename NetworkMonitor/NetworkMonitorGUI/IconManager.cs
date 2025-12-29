using System;
using System.Drawing;
using System.IO;

namespace NetworkMonitorGUI
{
    public static class IconManager
    {
        private static Icon _appIcon;
        private static Icon _trayIcon;
        private static bool _iconsLoaded = false;
        
        public static Icon AppIcon
        {
            get
            {
                EnsureIconsLoaded();
                return _appIcon;
            }
        }
        
        public static Icon TrayIcon
        {
            get
            {
                EnsureIconsLoaded();
                return _trayIcon;
            }
        }
        
        private static void EnsureIconsLoaded()
        {
            if (!_iconsLoaded)
            {
                LoadIcons();
                _iconsLoaded = true;
            }
        }
        
        private static void LoadIcons()
        {
            try
            {
                // Try to load from application directory
                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string iconPath = Path.Combine(appDir, "icon.ico");
                
                if (File.Exists(iconPath))
                {
                    // Load the main application icon
                    _appIcon = new Icon(iconPath);
                    
                    // For tray, use a 16x16 version if available, or resize
                    using (var tempIcon = new Icon(iconPath, new Size(16, 16)))
                    {
                        _trayIcon = (Icon)tempIcon.Clone();
                    }
                }
                else
                {
                    // Fall back to system icons
                    _appIcon = SystemIcons.Application;
                    _trayIcon = SystemIcons.Application;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load application icons: {ex.Message}");
                _appIcon = SystemIcons.Application;
                _trayIcon = SystemIcons.Application;
            }
        }
        
        // Method to get a cloned icon for safe usage
        public static Icon GetAppIconClone()
        {
            return (Icon)AppIcon.Clone();
        }
        
        public static Icon GetTrayIconClone()
        {
            return (Icon)TrayIcon.Clone();
        }
        
        // Cleanup method (call on application exit)
        public static void DisposeIcons()
        {
            _appIcon?.Dispose();
            _trayIcon?.Dispose();
            _appIcon = null;
            _trayIcon = null;
            _iconsLoaded = false;
        }
    }
}