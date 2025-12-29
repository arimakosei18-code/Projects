using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net.NetworkInformation;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using System.ServiceProcess;
using System.Diagnostics;
using System.Threading;
using System.Text.RegularExpressions;
using System.Text;
using System.Management;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Net.Mail;
using System.Net.Http;
using NetworkMonitorGUI.Models;
using NetworkMonitor.Shared.Models;
using NetworkMonitor.Shared;

namespace NetworkMonitorGUI.Forms
{
    // FlashWindow helper class
    public static class FlashWindow
    {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_ALL = 0x00000003;
        private const uint FLASHW_TIMERNOFG = 0x0000000C;

        public static bool Flash(IntPtr hWnd)
        {
            FLASHWINFO fInfo = new FLASHWINFO();
            fInfo.cbSize = Convert.ToUInt32(Marshal.SizeOf(fInfo));
            fInfo.hwnd = hWnd;
            fInfo.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
            fInfo.uCount = uint.MaxValue;
            fInfo.dwTimeout = 0;

            return FlashWindowEx(ref fInfo);
        }
    }

    public partial class MainForm : Form
    {
        // Data classes - SIMPLIFIED
        public class Device
        {
            public Guid Id { get; set; } = Guid.NewGuid();
            public string Name { get; set; }
            public string IPAddress { get; set; }
            public string MACAddress { get; set; } = "";
            public string Manufacturer { get; set; } = "Unknown";
            public bool IsEnabled { get; set; } = true;
            public string Status { get; set; } = "Unknown";
            public DateTime? LastSeen { get; set; }
            public int PingTime { get; set; }
            public Color StatusColor { get; set; } = Color.Gray;
            
            // SIMPLIFIED: Only track consecutive offline pings
            public int ConsecutiveOfflinePings { get; set; } = 0;
            public bool OfflineEmailSent { get; set; } = false;
        }

        public class AppConfiguration
        {
            public List<Device> Devices { get; set; } = new List<Device>();
            public int PingIntervalSeconds { get; set; } = 60;
            public int PingTimeoutMs { get; set; } = 2000;
            
            // SIMPLIFIED: Only offline threshold setting
            public int OfflinePingThreshold { get; set; } = 30; // Default 30 pings

            // Server connection info (for GUI server sync)
            public ServerConnectionInfo ServerInfo { get; set; } = new ServerConnectionInfo();
            
            // Gmail SMTP settings (HARDCODED)
            public string SmtpServer { get; set; } = "smtp.gmail.com";
            public int SmtpPort { get; set; } = 587;
            public bool EnableSsl { get; set; } = true;
            public string Username { get; set; } = "tt639288@gmail.com";
            public string Password { get; set; } = "vfdh sfvq bpxk eimt";
            public string FromEmail { get; set; } = "tt639288@gmail.com";
            public List<string> Recipients { get; set; } = new List<string>() { "tt639288@gmail.com" };
            
            public string LogFilePath { get; set; } = @"C:\NetworkMonitor\Logs\service.log";
            public bool AutoDetectMAC { get; set; } = true;
            public bool MinimizeToTray { get; set; } = true;
            public bool StartMinimized { get; set; } = false;
            
            public void AddDevice(Device device) => Devices.Add(device);
            
            public bool RemoveDevice(Guid id)
            {
                var device = Devices.FirstOrDefault(d => d.Id == id);
                if (device != null)
                {
                    Devices.Remove(device);
                    return true;
                }
                return false;
            }
            
            public Device GetDeviceByIp(string ip) => 
                Devices.FirstOrDefault(d => d.IPAddress.Equals(ip, StringComparison.OrdinalIgnoreCase));
            
            public Device GetDeviceById(Guid id) => 
                Devices.FirstOrDefault(d => d.Id == id);
            
            public void SetRecipientsFromString(string recipients)
            {
                Recipients = recipients.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(r => r.Trim())
                    .Where(r => !string.IsNullOrEmpty(r))
                    .ToList();
            }
        }

        public static class ConfigManager
        {
            private static readonly string ConfigPath = @"C:\NetworkMonitor\config.xml";
            
            public static string GetConfigPath()
            {
                return ConfigPath;
            }

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
                    MessageBox.Show($"Error loading config: {ex.Message}", "Warning", 
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                return new AppConfiguration();
            }
            
            public static void SaveConfiguration(AppConfiguration config)
            {
                try
                {
                    var directory = Path.GetDirectoryName(ConfigPath);
                    if (!Directory.Exists(directory))
                        Directory.CreateDirectory(directory);
                    
                    var serializer = new XmlSerializer(typeof(AppConfiguration));
                    using (var writer = new StreamWriter(ConfigPath))
                    {
                        serializer.Serialize(writer, config);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error saving config: {ex.Message}", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    throw;
                }
            }
        }

        // UI Components
        private AppUser _currentUser;
        private TabControl _tabControl;
        private DataGridView _dgvDevices;
        private Button _btnAdd;
        private Button _btnUpdate;
        private Button _btnRemove;
        private Button _btnTestAll;
        private Panel _statusPanel;
        private Label _lblMonitoringStatus;
        private TextBox _txtLogs;
        private Label _lblServiceStatus;
        
        // Data and state
        private AppConfiguration _config;
        private Device _selectedDevice = null;
        private System.Windows.Forms.Timer _monitoringTimer;
        private System.Windows.Forms.Timer _serviceStatusTimer;
        private System.Windows.Forms.Timer _trayUpdateTimer;
        private bool _isMonitoring = false;
        private readonly string _serviceName = "NetworkMonitorService";
        
        // Thread-safe collections and synchronization
        private readonly List<Device> _devicesToAdd = new List<Device>();
        private readonly List<Guid> _devicesToRemove = new List<Guid>();
        private readonly Dictionary<Guid, Device> _devicesToUpdate = new Dictionary<Guid, Device>();
        private readonly object _deviceSyncLock = new object();
        private readonly CancellationTokenSource _monitoringCts = new CancellationTokenSource();
        
        // Status colors
        private readonly Color _onlineColor = Color.FromArgb(76, 175, 80);    // Green
        private readonly Color _offlineColor = Color.FromArgb(244, 67, 54);   // Red
        private readonly Color _checkingColor = Color.FromArgb(33, 150, 243); // Blue
        private readonly Color _disabledColor = Color.FromArgb(158, 158, 158); // Gray
        
        // Animation
        private System.Windows.Forms.Timer _animationTimer;
        private int _animationStep = 0;
        private Dictionary<Guid, int> _deviceAnimationStates = new Dictionary<Guid, int>();
        
        // Tooltips
        private ToolTip _toolTip;
        
        // System Tray
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private bool _isClosing = false;
        
        // Icon caching
        private readonly Dictionary<Color, Icon> _cachedIcons = new Dictionary<Color, Icon>();
        private Color _lastTrayColor = Color.Empty;
        
        // MAC address vendor database (simplified - in production, use a complete OUI database)
        private static readonly Dictionary<string, string> _macVendors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            {"00:50:C2", "Microsoft"},
            {"00:0C:29", "VMware"},
            {"00:1A:11", "Google"},
            {"00:1D:0F", "Apple"},
            {"00:24:36", "Cisco"},
            {"00:26:B0", "HP"},
            {"00:1E:68", "Dell"},
            {"00:15:5D", "Microsoft Hyper-V"},
            {"00:0D:3A", "Microsoft Azure"},
            {"00:50:56", "VMware ESX"},
            {"00:1B:63", "Intel"},
            {"00:1C:42", "Dell"},
            {"00:1E:67", "Acer"},
            {"00:1F:29", "ASUS"},
            {"00:21:5A", "Samsung"},
            {"00:22:48", "Lenovo"},
            {"00:23:8B", "TP-Link"},
            {"00:24:01", "Netgear"},
            {"00:25:9C", "Huawei"},
            {"00:26:5E", "Belkin"},
            {"08:00:27", "Oracle VirtualBox"},
            {"0C:9D:92", "Cisco"},
            {"1C:1B:0D", "Google"},
            {"28:16:2E", "HP"},
            {"34:E6:D7", "Dell"},
            {"3C:5A:B4", "Google"},
            {"44:8A:5B", "Huawei"},
            {"4C:32:75", "Apple"},
            {"50:46:5D", "Apple"},
            {"54:EE:75", "Apple"},
            {"60:6C:66", "TP-Link"},
            {"64:A6:51", "ASUS"},
            {"74:DA:38", "TP-Link"},
            {"80:71:1F", "ASUS"},
            {"84:38:35", "Apple"},
            {"90:9F:33", "D-Link"},
            {"A4:34:D9", "Intel"},
            {"AC:87:A3", "Intel"},
            {"B8:27:EB", "Raspberry Pi"},
            {"C8:69:CD", "Apple"},
            {"CC:20:E8", "Apple"},
            {"DC:4A:3E", "HP"},
            {"E4:CE:8F", "Samsung"},
            {"F0:18:98", "Apple"},
            {"FC:F1:36", "Samsung"}
        };
        
        public MainForm(AppUser user)
        {
            try
            {
                _currentUser = user;
                InitializeForm();
                LoadConfiguration();
                InitializeToolTips();
                InitializeUI();
                LoadSampleDevices();
                InitializeTrayIcon();
                StartMonitoring();
                StartServiceStatusTimer();
                
                // Show form normally
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.Show();
                this.BringToFront();
                this.Activate();
                
                // Update window title with username
                this.Text = $"Network Monitor - Control Panel ({user.Username})";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error in MainForm constructor: {ex.Message}\n\nStack Trace:\n{ex.StackTrace}", 
                            "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }
        }
        
        private void InitializeForm()
        {
            this.Text = "Network Monitor - Control Panel";
            this.Size = new Size(1200, 750);
            this.StartPosition = FormStartPosition.CenterScreen;
            
            // Handle form closing for tray minimization
            this.FormClosing += (s, e) => 
            {
                if (!_isClosing)
                {
                    if (e.CloseReason == CloseReason.UserClosing && _config.MinimizeToTray)
                    {
                        e.Cancel = true;
                        HideToTray();
                        return;
                    }
                }
                
                // Really close
                StopMonitoring();
                _monitoringCts.Cancel();
                
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                
                DisposeCachedIcons();
                SaveConfig();
            };
            
            // Set form icon if available
            try
            {
                var iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { }
        }
        
        private void DisposeCachedIcons()
        {
            foreach (var icon in _cachedIcons.Values)
            {
                icon?.Dispose();
            }
            _cachedIcons.Clear();
        }
        
        private void LoadConfiguration()
        {
            _config = ConfigManager.LoadConfiguration();
        }
        
        private void InitializeToolTips()
        {
            _toolTip = new ToolTip();
            _toolTip.AutoPopDelay = 5000;
            _toolTip.InitialDelay = 1000;
            _toolTip.ReshowDelay = 500;
            _toolTip.ShowAlways = true;
        }
        
        private void InitializeUI()
        {
            // Create main tab control
            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Location = new Point(0, 0),
                Size = this.ClientSize
            };
            
            // Add tabs
            _tabControl.TabPages.Add(CreateDevicesTab());
            _tabControl.TabPages.Add(CreateMonitoringTab());
            _tabControl.TabPages.Add(CreateServiceTab());
            _tabControl.TabPages.Add(CreateLogsTab());
            _tabControl.TabPages.Add(CreateSettingsTab());
            _tabControl.TabPages.Add(CreateServerTab()); 

            this.Controls.Add(_tabControl);
        }
        
        private void InitializeTrayIcon()
        {
            // Create tray menu
            _trayMenu = new ContextMenuStrip();
            
            var showItem = new ToolStripMenuItem("Show Control Panel");
            showItem.Click += (s, e) => ShowFromTray();
            _trayMenu.Items.Add(showItem);
            
            _trayMenu.Items.Add(new ToolStripSeparator());
            
            var testAllItem = new ToolStripMenuItem("Test All Devices");
            testAllItem.Click += (s, e) => TestAllDevices();
            _trayMenu.Items.Add(testAllItem);
            
            _trayMenu.Items.Add(new ToolStripSeparator());

            var logoutItem = new ToolStripMenuItem("Logout");
            logoutItem.Click += (s, e) => Logout();
            _trayMenu.Items.Add(logoutItem);

            _trayMenu.Items.Add(new ToolStripSeparator());
            
            var exitItem = new ToolStripMenuItem("Exit");
            exitItem.Click += (s, e) => ExitApplication();
            _trayMenu.Items.Add(exitItem);
            
            // Create tray icon
            _trayIcon = new NotifyIcon
            {
                Icon = this.Icon != null ? new Icon(this.Icon, 16, 16) : SystemIcons.Application,
                Text = "Network Device Monitor",
                ContextMenuStrip = _trayMenu,
                Visible = true
            };
            
            _trayIcon.DoubleClick += (s, e) => ShowFromTray();
            _trayIcon.BalloonTipClicked += (s, e) => ShowFromTray();
            
            // Update tray icon periodically
            _trayUpdateTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _trayUpdateTimer.Tick += (s, e) => UpdateTrayIcon();
            _trayUpdateTimer.Start();
            
            UpdateTrayIcon();
        }
        
        private void UpdateTrayIcon()
        {
            if (_trayIcon == null) return;
            
            try
            {
                int onlineCount = 0;
                int totalCount = 0;
                
                lock (_deviceSyncLock)
                {
                    onlineCount = _config.Devices.Count(d => d.Status.Contains("Online"));
                    totalCount = _config.Devices.Count(d => d.IsEnabled);
                }
                
                string tooltip = $"Network Device Monitor\n";
                if (totalCount > 0)
                {
                    tooltip += $"Devices: {onlineCount}/{totalCount} online";
                }
                else
                {
                    tooltip += "No devices configured";
                }
                
                _trayIcon.Text = tooltip;
                
                Color iconColor;
                if (!_isMonitoring)
                {
                    iconColor = Color.Gray;
                }
                else if (onlineCount == totalCount && totalCount > 0)
                {
                    iconColor = Color.LimeGreen;
                }
                else if (onlineCount > 0)
                {
                    iconColor = Color.Orange;
                }
                else if (totalCount > 0)
                {
                    iconColor = Color.Red;
                }
                else
                {
                    iconColor = Color.Blue;
                }
                
                if (!_cachedIcons.TryGetValue(iconColor, out Icon trayIcon))
                {
                    trayIcon = CreateColoredIcon(iconColor);
                    _cachedIcons[iconColor] = trayIcon;
                }
                
                if (_lastTrayColor != iconColor)
                {
                    _trayIcon.Icon = trayIcon;
                    _lastTrayColor = iconColor;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating tray icon: {ex.Message}");
            }
        }
        
        private Icon CreateColoredIcon(Color color)
        {
            try
            {
                using (Bitmap bmp = new Bitmap(16, 16))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.Clear(Color.Transparent);
                    
                    using (Brush brush = new SolidBrush(color))
                    {
                        g.FillEllipse(brush, 1, 1, 14, 14);
                    }
                    
                    using (Pen pen = new Pen(Color.Black, 1))
                    {
                        g.DrawEllipse(pen, 1, 1, 14, 14);
                    }
                    
                    IntPtr hIcon = bmp.GetHicon();
                    using (Icon tempIcon = Icon.FromHandle(hIcon))
                    {
                        return new Icon(tempIcon, 16, 16);
                    }
                }
            }
            catch
            {
                return this.Icon != null ? new Icon(this.Icon, 16, 16) : SystemIcons.Application;
            }
        }
        
        private void ShowFromTray()
        {
            try
            {
                this.Show();
                this.WindowState = FormWindowState.Normal;
                this.ShowInTaskbar = true;
                this.BringToFront();
                this.Activate();
                
                FlashWindow.Flash(this.Handle);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error showing from tray: {ex.Message}");
            }
        }
        
        private void HideToTray()
        {
            try
            {
                this.Hide();
                this.ShowInTaskbar = false;
                
                _trayIcon.ShowBalloonTip(1000, "Network Monitor",
                    "Application minimized to system tray.\nDouble-click to restore.",
                    ToolTipIcon.Info);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error hiding to tray: {ex.Message}");
            }
        }
        
        private void ExitApplication()
        {
            try
            {
                _isClosing = true;
                
                StopMonitoring();
                _monitoringCts.Cancel();
                
                _trayUpdateTimer?.Stop();
                
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                    _trayIcon.Dispose();
                }
                
                DisposeCachedIcons();
                SaveConfig();
                
                Application.Exit();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error exiting application: {ex.Message}");
                Application.Exit();
            }
        }
        
        private TabPage CreateDevicesTab()
        {
            var tab = new TabPage("Devices");
            tab.Padding = new Padding(20);
            
            // Main panel for devices tab
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.WhiteSmoke
            };
            
            // Title with icon
            var titlePanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(1140, 60),
                BackColor = Color.FromArgb(33, 33, 33)
            };
            
            var lblTitle = new Label
            {
                Text = "🌐 Network Monitoring",
                Font = new Font("Arial", 16, FontStyle.Bold),
                Location = new Point(20, 15),
                AutoSize = true,
                ForeColor = Color.White
            };
            
            _lblMonitoringStatus = new Label
            {
                Text = "🟢 Monitoring Active",
                Font = new Font("Arial", 10, FontStyle.Bold),
                Location = new Point(950, 20),
                AutoSize = true,
                ForeColor = Color.LimeGreen
            };
            
            titlePanel.Controls.AddRange(new Control[] { lblTitle, _lblMonitoringStatus });
            
            // Status indicator panel
            _statusPanel = new Panel
            {
                Location = new Point(0, 70),
                Size = new Size(1140, 40),
                BackColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var lblStatusHelp = new Label
            {
                Text = "Status Legend: ",
                Location = new Point(10, 10),
                AutoSize = true,
                Font = new Font("Arial", 9)
            };
            
            // Create status indicators
            var onlineIndicator = CreateStatusIndicator("Online", _onlineColor, 100);
            var offlineIndicator = CreateStatusIndicator("Offline", _offlineColor, 180);
            var checkingIndicator = CreateStatusIndicator("Checking", _checkingColor, 260);
            var disabledIndicator = CreateStatusIndicator("Disabled", _disabledColor, 340);
            
            _statusPanel.Controls.AddRange(new Control[] 
            { 
                lblStatusHelp, onlineIndicator, offlineIndicator, 
                checkingIndicator, disabledIndicator 
            });
            
            // Device grid
            var lblDevices = new Label
            {
                Text = "📱 Devices Being Monitored:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(10, 130),
                AutoSize = true
            };
            
            _dgvDevices = new DataGridView
            {
                Location = new Point(10, 160),
                Size = new Size(1120, 300),
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.Fixed3D,
                GridColor = Color.LightGray
            };
            
            // Add columns
            var enabledColumn = new DataGridViewTextBoxColumn { Name = "Enabled", HeaderText = "✓", Width = 40 };
            var statusColumn = new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", Width = 100 };
            var nameColumn = new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Device Name", Width = 150 };
            var ipColumn = new DataGridViewTextBoxColumn { Name = "IP", HeaderText = "IP Address", Width = 120 };
            var macColumn = new DataGridViewTextBoxColumn { Name = "MAC", HeaderText = "MAC Address", Width = 120 };
            var manufacturerColumn = new DataGridViewTextBoxColumn { Name = "Manufacturer", HeaderText = "Manufacturer", Width = 150 };
            var pingColumn = new DataGridViewTextBoxColumn { Name = "Ping", HeaderText = "Ping (ms)", Width = 80 };
            var lastSeenColumn = new DataGridViewTextBoxColumn { Name = "LastSeen", HeaderText = "Last Seen", Width = 150 };
            
            _dgvDevices.Columns.AddRange(new DataGridViewColumn[] 
            { 
                enabledColumn, statusColumn, nameColumn, ipColumn, 
                macColumn, manufacturerColumn, pingColumn, lastSeenColumn 
            });
            
            _dgvDevices.SelectionChanged += (s, e) => OnDeviceSelected();
            
            // Device editor panel
            var pnlEditor = new Panel
            {
                Location = new Point(10, 480),
                Size = new Size(1120, 120),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(15),
                BackColor = Color.White,
                Name = "pnlEditor"
            };
            
            var lblEditor = new Label
            {
                Text = "✏️ Device Management:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(10, 15),
                AutoSize = true,
                Name = "lblEditor"
            };
            
            // Action buttons
            var pnlButtons = new Panel
            {
                Location = new Point(10, 45),
                Size = new Size(800, 40),
                Name = "pnlButtons"
            };
            
            _btnAdd = CreateStyledButton("➕ Add New Device", Color.FromArgb(76, 175, 80), 0);
            _btnAdd.Click += (s, e) => AddDevice();
            _btnAdd.TabIndex = 1;
            _btnAdd.Name = "btnAdd";
            
            _btnUpdate = CreateStyledButton("✏️ Edit Selected", Color.FromArgb(33, 150, 243), 140);
            _btnUpdate.Click += (s, e) => UpdateDevice();
            _btnUpdate.Enabled = false;
            _btnUpdate.TabIndex = 2;
            _btnUpdate.Name = "btnUpdate";
            
            _btnRemove = CreateStyledButton("🗑️ Remove", Color.FromArgb(244, 67, 54), 280);
            _btnRemove.Click += (s, e) => RemoveDevice();
            _btnRemove.Enabled = false;
            _btnRemove.TabIndex = 3;
            _btnRemove.Name = "btnRemove";
            
            _btnTestAll = CreateStyledButton("🔍 Test All Now", Color.FromArgb(255, 152, 0), 420);
            _btnTestAll.Click += (s, e) => TestAllDevices();
            _btnTestAll.TabIndex = 4;
            _btnTestAll.Name = "btnTestAll";
            
            pnlButtons.Controls.AddRange(new Control[] 
            { 
                _btnAdd, _btnUpdate, _btnRemove, _btnTestAll
            });
            
            // Quick info label
            var lblInfo = new Label
            {
                Text = "💡 Tip: MAC addresses and manufacturers are automatically detected for local network devices.",
                Location = new Point(10, 85),
                AutoSize = true,
                Name = "lblInfo",
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray
            };
            
            pnlEditor.Controls.AddRange(new Control[]
            {
                lblEditor, pnlButtons, lblInfo
            });
            
            // Add all to main panel
            mainPanel.Controls.AddRange(new Control[]
            {
                titlePanel, _statusPanel,
                lblDevices, _dgvDevices, pnlEditor
            });
            
            tab.Controls.Add(mainPanel);
            
            // Add tooltips after controls are created
            AddToolTips();
            
            return tab;
        }
        
        private TabPage CreateMonitoringTab()
        {
            var tab = new TabPage("Monitoring");
            tab.Padding = new Padding(20);
            
            var lblTitle = new Label
            {
                Text = "Monitoring Settings",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            // Ping interval
            var lblPingInterval = new Label
            {
                Text = "Ping Interval (seconds):",
                Location = new Point(10, 50),
                AutoSize = true
            };
            
            var numPingInterval = new NumericUpDown
            {
                Location = new Point(150, 47),
                Size = new Size(80, 25),
                Minimum = 10,
                Maximum = 3600,
                Value = _config.PingIntervalSeconds
            };
            numPingInterval.ValueChanged += (s, e) => 
            {
                _config.PingIntervalSeconds = (int)numPingInterval.Value;
                SaveConfig();
                if (_monitoringTimer != null && _isMonitoring)
                {
                    _monitoringTimer.Stop();
                    _monitoringTimer.Interval = _config.PingIntervalSeconds * 1000;
                    _monitoringTimer.Start();
                }
            };
            
            // Ping timeout
            var lblPingTimeout = new Label
            {
                Text = "Ping Timeout (milliseconds):",
                Location = new Point(10, 90),
                AutoSize = true
            };
            
            var numPingTimeout = new NumericUpDown
            {
                Location = new Point(180, 87),
                Size = new Size(80, 25),
                Minimum = 100,
                Maximum = 10000,
                Value = _config.PingTimeoutMs
            };
            numPingTimeout.ValueChanged += (s, e) => 
            {
                _config.PingTimeoutMs = (int)numPingTimeout.Value;
                SaveConfig();
            };
            
            // Offline ping threshold setting
            var lblOfflineThreshold = new Label
            {
                Text = "Send offline email after (consecutive pings):",
                Location = new Point(10, 130),
                AutoSize = true
            };
            
            var numOfflineThreshold = new NumericUpDown
            {
                Location = new Point(250, 127),
                Size = new Size(80, 25),
                Minimum = 1,
                Maximum = 1000,
                Value = _config.OfflinePingThreshold
            };
            numOfflineThreshold.ValueChanged += (s, e) => 
            {
                _config.OfflinePingThreshold = (int)numOfflineThreshold.Value;
                SaveConfig();
            };
            
            // Auto-detect MAC checkbox
            var chkAutoDetectMAC = new CheckBox
            {
                Text = "Auto-detect MAC addresses for local devices",
                Location = new Point(10, 170),
                AutoSize = true,
                Checked = _config.AutoDetectMAC
            };
            chkAutoDetectMAC.CheckedChanged += (s, e) => 
            {
                _config.AutoDetectMAC = chkAutoDetectMAC.Checked;
                SaveConfig();
            };
            
            // Reset counters button
            var btnResetCounters = new Button
            {
                Text = "Reset All Offline Counters",
                Location = new Point(10, 210),
                Size = new Size(180, 40),
                BackColor = Color.LightGoldenrodYellow
            };
            btnResetCounters.Click += (s, e) => ResetAllOfflineCounters();
            
            // Monitoring actions
            var btnMonitorAll = new Button
            {
                Text = "Start Monitoring All",
                Location = new Point(200, 210),
                Size = new Size(150, 40),
                BackColor = Color.LightGreen
            };
            btnMonitorAll.Click += (s, e) => StartMonitoringAll();
            
            var btnStopAll = new Button
            {
                Text = "Stop Monitoring All",
                Location = new Point(360, 210),
                Size = new Size(150, 40),
                BackColor = Color.LightCoral
            };
            btnStopAll.Click += (s, e) => StopMonitoringAll();
            
            var btnRefreshNow = new Button
            {
                Text = "Refresh Now",
                Location = new Point(520, 210),
                Size = new Size(120, 40),
                BackColor = Color.LightBlue
            };
            btnRefreshNow.Click += async (s, e) => 
            {
                await CheckAllDevicesAsync();
                MessageBox.Show("Devices refreshed.", "Success", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            // Add help text
            var lblHelp = new Label
            {
                Text = "💡 Email Notification Logic:\n" +
                       "• Offline email: Sent ONCE after device is offline for X consecutive pings\n" +
                       "• Counters reset when device goes online\n" +
                       "• No flood of emails - only one per offline period",
                Location = new Point(10, 270),
                AutoSize = false,
                Size = new Size(600, 80),
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray
            };
            
            tab.Controls.AddRange(new Control[] 
            {
                lblTitle, lblPingInterval, numPingInterval,
                lblPingTimeout, numPingTimeout, lblOfflineThreshold, numOfflineThreshold,
                chkAutoDetectMAC, btnResetCounters, btnMonitorAll, 
                btnStopAll, btnRefreshNow, lblHelp
            });
            
            return tab;
        }
        
        private TabPage CreateServiceTab()
        {
            var tab = new TabPage("Service");
            tab.Padding = new Padding(20);
            
            var lblTitle = new Label
            {
                Text = "Service Management",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            _lblServiceStatus = new Label
            {
                Text = "Service Status: Checking...",
                Location = new Point(10, 50),
                AutoSize = true,
                Font = new Font("Arial", 10, FontStyle.Bold)
            };
            
            var btnStart = new Button
            {
                Text = "Start Service",
                Location = new Point(10, 90),
                Size = new Size(120, 40),
                BackColor = Color.LightGreen
            };
            btnStart.Click += (s, e) => StartService();
            
            var btnStop = new Button
            {
                Text = "Stop Service",
                Location = new Point(140, 90),
                Size = new Size(120, 40),
                BackColor = Color.LightCoral
            };
            btnStop.Click += (s, e) => StopService();
            
            var btnRestart = new Button
            {
                Text = "Restart Service",
                Location = new Point(270, 90),
                Size = new Size(120, 40),
                BackColor = Color.LightBlue
            };
            btnRestart.Click += (s, e) => RestartService();
            
            var btnInstall = new Button
            {
                Text = "Install Service",
                Location = new Point(10, 150),
                Size = new Size(120, 40),
                BackColor = Color.LightGoldenrodYellow
            };
            btnInstall.Click += (s, e) => InstallService();
            
            var btnUninstall = new Button
            {
                Text = "Uninstall Service",
                Location = new Point(140, 150),
                Size = new Size(120, 40),
                BackColor = Color.LightSalmon
            };
            btnUninstall.Click += (s, e) => UninstallService();
            
            var btnOpenConfig = new Button
            {
                Text = "Open Config Folder",
                Location = new Point(270, 150),
                Size = new Size(120, 40),
                BackColor = Color.LightGray
            };
            btnOpenConfig.Click += (s, e) => OpenConfigFolder();
            
            tab.Controls.AddRange(new Control[] 
            {
                lblTitle, _lblServiceStatus, 
                btnStart, btnStop, btnRestart,
                btnInstall, btnUninstall, btnOpenConfig
            });
            
            return tab;
        }
        
        private TabPage CreateLogsTab()
        {
            var tab = new TabPage("Logs");
            tab.Padding = new Padding(10);
            
            var lblTitle = new Label
            {
                Text = "View Logs",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            _txtLogs = new TextBox
            {
                Location = new Point(10, 50),
                Size = new Size(1150, 500),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9),
                BackColor = Color.Black,
                ForeColor = Color.Lime
            };
            
            var btnRefresh = new Button
            {
                Text = "Refresh Logs",
                Location = new Point(10, 560),
                Size = new Size(100, 30),
                BackColor = Color.LightBlue
            };
            btnRefresh.Click += (s, e) => RefreshLogs();
            
            var btnClear = new Button
            {
                Text = "Clear Logs",
                Location = new Point(120, 560),
                Size = new Size(100, 30),
                BackColor = Color.LightCoral
            };
            btnClear.Click += (s, e) => ClearLogs();
            
            var btnOpenFolder = new Button
            {
                Text = "Open Log Folder",
                Location = new Point(230, 560),
                Size = new Size(120, 30),
                BackColor = Color.LightGray
            };
            btnOpenFolder.Click += (s, e) => OpenLogFolder();
            
            tab.Controls.AddRange(new Control[] 
            {
                lblTitle, _txtLogs, btnRefresh, btnClear, btnOpenFolder
            });
            
            return tab;
        }
        
        private TabPage CreateSettingsTab()
        {
            var tab = new TabPage("Settings");
            tab.Padding = new Padding(20);
            
            var lblTitle = new Label
            {
                Text = "Application Settings",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            // Tray settings
            var lblTraySettings = new Label
            {
                Text = "System Tray Settings:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(10, 50),
                AutoSize = true
            };
            
            var chkMinimizeToTray = new CheckBox
            {
                Text = "Minimize to system tray when closed",
                Location = new Point(30, 80),
                AutoSize = true,
                Checked = _config.MinimizeToTray
            };
            chkMinimizeToTray.CheckedChanged += (s, e) => 
            {
                _config.MinimizeToTray = chkMinimizeToTray.Checked;
                SaveConfig();
            };
            
            var chkStartMinimized = new CheckBox
            {
                Text = "Start application minimized to tray",
                Location = new Point(30, 110),
                AutoSize = true,
                Checked = _config.StartMinimized
            };
            chkStartMinimized.CheckedChanged += (s, e) => 
            {
                _config.StartMinimized = chkStartMinimized.Checked;
                SaveConfig();
            };
            
            var lblTrayInfo = new Label
            {
                Text = "💡 When minimized to tray, the application continues monitoring in the background.",
                Location = new Point(30, 140),
                AutoSize = true,
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray
            };
            
            // Email settings info
            var lblEmailInfo = new Label
            {
                Text = "📧 Email Settings:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(10, 190),
                AutoSize = true
            };
            
            var lblEmailDetails = new Label
            {
                Text = "Server: smtp.gmail.com:587\n" +
                       "From: tt639288@gmail.com\n" +
                       "To: tt639288@gmail.com\n" +
                       "App Password configured",
                Location = new Point(30, 220),
                AutoSize = false,
                Size = new Size(400, 80),
                Font = new Font("Consolas", 9),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(5)
            };
            
            // Configuration management
            var lblConfig = new Label
            {
                Text = "Configuration Management:",
                Font = new Font("Arial", 11, FontStyle.Bold),
                Location = new Point(10, 320),
                AutoSize = true
            };
            
            var btnBackupConfig = new Button
            {
                Text = "Backup Configuration",
                Location = new Point(30, 350),
                Size = new Size(150, 40),
                BackColor = Color.LightGoldenrodYellow
            };
            btnBackupConfig.Click += (s, e) => BackupConfiguration();
            
            var btnRestoreConfig = new Button
            {
                Text = "Restore Configuration",
                Location = new Point(190, 350),
                Size = new Size(150, 40),
                BackColor = Color.LightCoral
            };
            btnRestoreConfig.Click += (s, e) => RestoreConfiguration();
            
            var btnResetConfig = new Button
            {
                Text = "Reset to Defaults",
                Location = new Point(350, 350),
                Size = new Size(120, 40),
                BackColor = Color.LightSalmon
            };
            btnResetConfig.Click += (s, e) => ResetConfiguration();
            
            tab.Controls.AddRange(new Control[] 
            {
                lblTitle, lblTraySettings, chkMinimizeToTray, chkStartMinimized, lblTrayInfo,
                lblEmailInfo, lblEmailDetails,
                lblConfig, btnBackupConfig, btnRestoreConfig, btnResetConfig
            });
            
            return tab;
        }

        private TabPage CreateServerTab()
        {
            var tab = new TabPage("Server");
            tab.Padding = new Padding(20);
            
            var lblTitle = new Label
            {
                Text = "Network Monitor Server Integration",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true
            };
            
            // Server URL
            var lblServerUrl = new Label
            {
                Text = "Server URL:",
                Location = new Point(10, 50),
                AutoSize = true
            };
            
            var txtServerUrl = new TextBox
            {
                Location = new Point(120, 47),
                Size = new Size(300, 25),
                Text = _config.ServerInfo.ServerUrl
            };
            
            // Enable server sync checkbox
            var chkEnableServerSync = new CheckBox
            {
                Text = "Enable server synchronization",
                Location = new Point(10, 90),
                AutoSize = true,
                Checked = _config.ServerInfo.EnableServerSync
            };
            chkEnableServerSync.CheckedChanged += (s, e) => 
            {
                _config.ServerInfo.EnableServerSync = chkEnableServerSync.Checked;
                SaveConfig();
            };
            
            // Client info
            var lblClientInfo = new Label
            {
                Text = $"Client ID: {_config.ServerInfo.ClientId}",
                Location = new Point(10, 120),
                AutoSize = true,
                Font = new Font("Consolas", 9)
            };
            
            var lblClientName = new Label
            {
                Text = $"Client Name: {_config.ServerInfo.ClientName}",
                Location = new Point(10, 140),
                AutoSize = true,
                Font = new Font("Consolas", 9)
            };
            
            // IP Address display
            var lblLocalIP = new Label
            {
                Text = "Local IP: Detecting...",
                Location = new Point(10, 170),
                AutoSize = true,
                Font = new Font("Arial", 9)
            };
            
            var lblPublicIP = new Label
            {
                Text = "Public IP: Detecting...",
                Location = new Point(10, 190),
                AutoSize = true,
                Font = new Font("Arial", 9)
            };
            
            // Refresh IPs button
            var btnRefreshIPs = new Button
            {
                Text = "Refresh IP Addresses",
                Location = new Point(10, 220),
                Size = new Size(150, 30),
                BackColor = Color.LightBlue
            };
            btnRefreshIPs.Click += async (s, e) => 
            {
                var localIP = GetLocalIPAddress();
                var publicIP = await GetPublicIPAddressAsync();
                
                lblLocalIP.Text = $"Local IP: {localIP}";
                lblPublicIP.Text = $"Public IP: {publicIP}";
                
                _config.ServerInfo.LocalIP = localIP;
                _config.ServerInfo.PublicIP = publicIP;
                SaveConfig();
            };
            
            // Test server connection button
            var btnTestConnection = new Button
            {
                Text = "Test Server Connection",
                Location = new Point(170, 220),
                Size = new Size(150, 30),
                BackColor = Color.LightGreen
            };
            btnTestConnection.Click += async (s, e) => 
            {
                _config.ServerInfo.ServerUrl = txtServerUrl.Text;
                SaveConfig();
                
                if (string.IsNullOrEmpty(_config.ServerInfo.ServerUrl))
                {
                    MessageBox.Show("Please enter a server URL first.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                var result = await SendHeartbeatToServerAsync();
                
                if (result)
                {
                    MessageBox.Show("✅ Server connection successful!", "Success", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show("❌ Server connection failed. Check the URL and try again.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            
            // Sync now button
            var btnSyncNow = new Button
            {
                Text = "Sync Devices Now",
                Location = new Point(330, 220),
                Size = new Size(120, 30),
                BackColor = Color.LightGoldenrodYellow
            };
            btnSyncNow.Click += async (s, e) => 
            {
                await SyncDevicesWithServerAsync();
                MessageBox.Show("Device sync completed.", "Sync", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            };

            // Sync helper - keep outside of lambda

            
            // Status label
            var lblStatus = new Label
            {
                Text = _config.ServerInfo.IsConnected ? 
                    "🟢 Connected to server" : "🔴 Not connected to server",
                Location = new Point(10, 260),
                AutoSize = true,
                Font = new Font("Arial", 10, FontStyle.Bold),
                ForeColor = _config.ServerInfo.IsConnected ? Color.Green : Color.Red
            };
            
            var lblLastHeartbeat = new Label
            {
                Text = $"Last heartbeat: {_config.ServerInfo.LastHeartbeat:yyyy-MM-dd HH:mm:ss}",
                Location = new Point(10, 285),
                AutoSize = true,
                Font = new Font("Arial", 9)
            };
            
            // Help text
            var lblHelp = new Label
            {
                Text = "💡 Server Integration Features:\n" +
                    "• Centralized monitoring dashboard\n" +
                    "• Remote device configuration\n" +
                    "• Cross-location monitoring\n" +
                    "• Historical data analysis",
                Location = new Point(10, 320),
                AutoSize = false,
                Size = new Size(500, 80),
                Font = new Font("Arial", 9),
                ForeColor = Color.Gray
            };
            
            // Initial IP detection
            Task.Run(async () => 
            {
                var localIP = GetLocalIPAddress();
                var publicIP = await GetPublicIPAddressAsync();
                
                this.Invoke((MethodInvoker)delegate
                {
                    lblLocalIP.Text = $"Local IP: {localIP}";
                    lblPublicIP.Text = $"Public IP: {publicIP}";
                    
                    _config.ServerInfo.LocalIP = localIP;
                    _config.ServerInfo.PublicIP = publicIP;
                    SaveConfig();
                });
            });
            
            tab.Controls.AddRange(new Control[] 
            {
                lblTitle, lblServerUrl, txtServerUrl,
                chkEnableServerSync, lblClientInfo, lblClientName,
                lblLocalIP, lblPublicIP, btnRefreshIPs, btnTestConnection, btnSyncNow,
                lblStatus, lblLastHeartbeat, lblHelp
            });
            
            return tab;
        }
        
        private void AddToolTips()
        {
            _toolTip.SetToolTip(_btnAdd, "Add a new device to the monitoring list");
            _toolTip.SetToolTip(_btnUpdate, "Edit the selected device");
            _toolTip.SetToolTip(_btnRemove, "Remove the selected device from monitoring");
            _toolTip.SetToolTip(_btnTestAll, "Test all devices immediately");
        }
        
        private Panel CreateStatusIndicator(string text, Color color, int x)
        {
            var panel = new Panel
            {
                Location = new Point(x, 10),
                Size = new Size(80, 20)
            };
            
            var indicator = new Panel
            {
                Location = new Point(0, 2),
                Size = new Size(16, 16),
                BackColor = color,
                BorderStyle = BorderStyle.FixedSingle
            };
            
            var label = new Label
            {
                Text = text,
                Location = new Point(20, 2),
                AutoSize = true,
                Font = new Font("Arial", 9)
            };
            
            panel.Controls.AddRange(new Control[] { indicator, label });
            return panel;
        }
        
        private Button CreateStyledButton(string text, Color backColor, int x)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, 0),
                Size = new Size(110, 30),
                BackColor = backColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
        }
        
        private void LoadSampleDevices()
        {
            // Only add samples if no devices exist
            if (_config.Devices.Count == 0)
            {
                lock (_deviceSyncLock)
                {
                    _config.Devices.Add(new Device { 
                        Name = "Office Router", 
                        IPAddress = "192.168.1.1", 
                        MACAddress = "00:1A:2B:3C:4D:5E",
                        Manufacturer = "Netgear",
                        Status = "Checking...",
                        StatusColor = _checkingColor
                    });
                    
                    _config.Devices.Add(new Device { 
                        Name = "Google DNS", 
                        IPAddress = "8.8.8.8", 
                        Status = "Checking...",
                        StatusColor = _checkingColor
                    });
                    
                    _config.Devices.Add(new Device { 
                        Name = "Cloudflare DNS", 
                        IPAddress = "1.1.1.1", 
                        Status = "Checking...",
                        StatusColor = _checkingColor
                    });
                }
                
                SaveConfig();
            }
            
            RefreshDeviceGrid();
        }
        
        private void StartMonitoring()
        {
            _isMonitoring = true;
            
            // Setup animation timer
            _animationTimer = new System.Windows.Forms.Timer { Interval = 100 };
            _animationTimer.Tick += (s, e) => UpdateAnimation();
            _animationTimer.Start();
            
            // Start monitoring timer
            _monitoringTimer = new System.Windows.Forms.Timer { Interval = _config.PingIntervalSeconds * 1000 };
            _monitoringTimer.Tick += async (s, e) => await CheckAllDevicesAsync();
            _monitoringTimer.Start();
            
            // Start background monitoring task
            Task.Run(() => BackgroundMonitoringTask(_monitoringCts.Token));
            
            // Initial check
            Task.Run(async () => await CheckAllDevicesAsync());
            
            // Update status label
            _lblMonitoringStatus.Text = "🟢 Monitoring Active";
            _lblMonitoringStatus.ForeColor = Color.LimeGreen;
        }
        
        private void StopMonitoring()
        {
            _isMonitoring = false;
            _monitoringTimer?.Stop();
            _animationTimer?.Stop();
            _serviceStatusTimer?.Stop();
            _trayUpdateTimer?.Stop();
            
            // Update status label
            _lblMonitoringStatus.Text = "⚫ Monitoring Stopped";
            _lblMonitoringStatus.ForeColor = Color.Gray;
            
            SaveConfig();
        }
        
        private async Task BackgroundMonitoringTask(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && _isMonitoring)
            {
                try
                {
                    // Apply pending device changes
                    ApplyPendingDeviceChanges();
                    
                    // Check devices
                    await CheckAllDevicesAsync();
                    
                    // Update tray icon
                    UpdateTrayIcon();
                    
                    // Wait for next interval
                    await Task.Delay(_config.PingIntervalSeconds * 1000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Log error but continue
                    Console.WriteLine($"Monitoring error: {ex.Message}");
                    await Task.Delay(5000, cancellationToken);
                }
            }
        }
        
        private void ApplyPendingDeviceChanges()
        {
            lock (_deviceSyncLock)
            {
                // Add new devices
                foreach (var device in _devicesToAdd)
                {
                    if (!_config.Devices.Any(d => d.Id == device.Id || 
                        d.IPAddress.Equals(device.IPAddress, StringComparison.OrdinalIgnoreCase)))
                    {
                        _config.Devices.Add(device);
                    }
                }
                _devicesToAdd.Clear();
                
                // Remove devices
                foreach (var deviceId in _devicesToRemove)
                {
                    var device = _config.Devices.FirstOrDefault(d => d.Id == deviceId);
                    if (device != null)
                    {
                        _config.Devices.Remove(device);
                    }
                }
                _devicesToRemove.Clear();
                
                // Update devices
                foreach (var kvp in _devicesToUpdate)
                {
                    var device = _config.Devices.FirstOrDefault(d => d.Id == kvp.Key);
                    if (device != null)
                    {
                        device.Name = kvp.Value.Name;
                        device.IPAddress = kvp.Value.IPAddress;
                        device.MACAddress = kvp.Value.MACAddress;
                        device.Manufacturer = kvp.Value.Manufacturer;
                        device.IsEnabled = kvp.Value.IsEnabled;
                        if (!device.IsEnabled)
                        {
                            device.Status = "Disabled";
                            device.StatusColor = _disabledColor;
                            // Reset counters for disabled devices
                            device.ConsecutiveOfflinePings = 0;
                            device.OfflineEmailSent = false;
                        }
                        else
                        {
                            device.Status = "Checking...";
                            device.StatusColor = _checkingColor;
                        }
                    }
                }
                _devicesToUpdate.Clear();
                
                // Save config if there were changes
                if (_devicesToAdd.Count > 0 || _devicesToRemove.Count > 0 || _devicesToUpdate.Count > 0)
                {
                    SaveConfig();
                }
            }
            
            // Refresh UI on main thread
            this.Invoke((MethodInvoker)delegate {
                RefreshDeviceGrid();
            });
        }
        
        private void StartServiceStatusTimer()
        {
            _serviceStatusTimer = new System.Windows.Forms.Timer { Interval = 5000 };
            _serviceStatusTimer.Tick += (s, e) => UpdateServiceStatus();
            _serviceStatusTimer.Start();
            UpdateServiceStatus(); // Initial update
        }
        
        // ========== SIMPLIFIED DEVICE CHECK LOGIC ==========
        
        private async Task CheckAllDevicesAsync()
        {
            if (!_isMonitoring) return;
            
            List<Device> devicesToCheck;
            lock (_deviceSyncLock)
            {
                devicesToCheck = new List<Device>(_config.Devices);
            }
            
            foreach (var device in devicesToCheck)
            {
                if (_monitoringCts.Token.IsCancellationRequested) break;
                
                if (!device.IsEnabled)
                {
                    device.Status = "Disabled";
                    device.StatusColor = _disabledColor;
                    device.ConsecutiveOfflinePings = 0;
                    device.OfflineEmailSent = false;
                    UpdateDeviceRow(device);
                    continue;
                }
                
                string previousStatus = device.Status.ToLower();
                
                device.Status = "Checking...";
                device.StatusColor = _checkingColor;
                UpdateDeviceRow(device);
                
                var result = await PingDeviceAsync(device.IPAddress);
                
                if (result.IsSuccess)
                {
                    device.Status = "Online";
                    device.StatusColor = _onlineColor;
                    device.PingTime = result.PingTime;
                    device.LastSeen = DateTime.Now;
                    
                    // Device came back online - reset counters
                    if (previousStatus.Contains("offline"))
                    {
                        device.ConsecutiveOfflinePings = 0;
                        device.OfflineEmailSent = false; // Reset for next offline period
                    }
                    
                    // Auto-detect MAC if enabled
                    if (_config.AutoDetectMAC && IsLocalIP(device.IPAddress))
                    {
                        await Task.Run(() => DetectMACAddress(device));
                    }
                }
                else
                {
                    device.Status = "Offline";
                    device.StatusColor = _offlineColor;
                    device.PingTime = 0;
                    
                    // Increment offline ping counter
                    device.ConsecutiveOfflinePings++;
                    
                    // Check if we need to send ONE offline email
                    if (!device.OfflineEmailSent && device.ConsecutiveOfflinePings >= _config.OfflinePingThreshold)
                    {
                        // Send ONE email only
                        SendOfflineEmail(device);
                        device.OfflineEmailSent = true;
                        
                        // STOP counting - we've sent the email
                        // Counter will reset when device comes back online
                    }
                }
                
                UpdateDeviceRow(device);
                await Task.Delay(500);
            }
            
            UpdateTrayIcon();
        }
        
        private void SendOfflineEmail(Device device)
        {
            try
            {
                // Use hardcoded Gmail SMTP settings
                using (var smtpClient = new SmtpClient("smtp.gmail.com", 587))
                {
                    smtpClient.EnableSsl = true;
                    smtpClient.Credentials = new NetworkCredential("tt639288@gmail.com", "vfdh sfvq bpxk eimt");
                    
                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress("tt639288@gmail.com"),
                        Subject = $"🚨 ALERT: Device {device.Name} is OFFLINE after {device.ConsecutiveOfflinePings} checks",
                        Body = $@"
                        Network Monitor Alert - Device Offline

                        Device Name: {device.Name}
                        IP Address: {device.IPAddress}
                        Status: OFFLINE
                        Consecutive Offline Pings: {device.ConsecutiveOfflinePings}
                        Threshold: {_config.OfflinePingThreshold}
                        Last Seen: {device.LastSeen?.ToString("yyyy-MM-dd HH:mm:ss") ?? "Never"}
                        Alert Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                        This device has been offline for {device.ConsecutiveOfflinePings} consecutive checks.
                        No further emails will be sent for this device until it comes back online.

                        ---
                        Network Monitor System
                        Auto-generated Alert
                        ",
                        IsBodyHtml = false
                    };
                    
                    mailMessage.To.Add("tt639288@gmail.com");
                    
                    smtpClient.Send(mailMessage);
                    
                    Console.WriteLine($"📧 Offline email sent for device: {device.Name} after {device.ConsecutiveOfflinePings} pings");
                    
                    // Update logs
                    this.Invoke((MethodInvoker)delegate
                    {
                        _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] EMAIL: Offline alert sent for {device.Name}\n");
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to send offline email: {ex.Message}");
                
                this.Invoke((MethodInvoker)delegate
                {
                    _txtLogs.AppendText($"[{DateTime.Now:HH:mm:ss}] ERROR: Failed to send email - {ex.Message}\n");
                });
            }
        }
        
        private async Task<PingResult> PingDeviceAsync(string ip)
        {
            try
            {
                using (var ping = new Ping())
                {
                    var reply = await ping.SendPingAsync(ip, _config.PingTimeoutMs);
                    return new PingResult
                    {
                        IsSuccess = reply.Status == IPStatus.Success,
                        PingTime = (int)reply.RoundtripTime
                    };
                }
            }
            catch
            {
                return new PingResult { IsSuccess = false, PingTime = 0 };
            }
        }
        
        private class PingResult
        {
            public bool IsSuccess { get; set; }
            public int PingTime { get; set; }
        }
        
        // ========== MAC ADDRESS DETECTION METHODS ==========
        
        private bool IsLocalIP(string ipAddress)
        {
            try
            {
                var ip = IPAddress.Parse(ipAddress);
                
                // Check if it's a private IP address
                if (ip.AddressFamily == AddressFamily.InterNetwork) // IPv4
                {
                    byte[] bytes = ip.GetAddressBytes();
                    
                    // Private IP ranges:
                    // 10.0.0.0 - 10.255.255.255 (10/8 prefix)
                    // 172.16.0.0 - 172.31.255.255 (172.16/12 prefix)
                    // 192.168.0.0 - 192.168.255.255 (192.168/16 prefix)
                    // 169.254.0.0 - 169.254.255.255 (APIPA)
                    
                    if (bytes[0] == 10) return true;
                    if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
                    if (bytes[0] == 192 && bytes[1] == 168) return true;
                    if (bytes[0] == 169 && bytes[1] == 254) return true;
                }
                else if (ip.AddressFamily == AddressFamily.InterNetworkV6) // IPv6
                {
                    // Check for IPv6 private addresses (ULA - fc00::/7)
                    string ipString = ip.ToString();
                    if (ipString.StartsWith("fc") || ipString.StartsWith("fd"))
                        return true;
                }
                
                return false;
            }
            catch
            {
                return false;
            }
        }
        
        private void DetectMACAddress(Device device)
        {
            try
            {
                // If MAC address is already known, just detect manufacturer
                if (!string.IsNullOrEmpty(device.MACAddress))
                {
                    if (string.IsNullOrEmpty(device.Manufacturer) || device.Manufacturer == "Unknown")
                    {
                        device.Manufacturer = GetManufacturerFromMAC(device.MACAddress);
                    }
                    return;
                }
                
                // Try ARP cache method
                string macAddress = GetMACFromARP(device.IPAddress);
                
                if (string.IsNullOrEmpty(macAddress))
                {
                    // Try WMI method (for Windows)
                    macAddress = GetMACFromWMI(device.IPAddress);
                }
                
                if (!string.IsNullOrEmpty(macAddress))
                {
                    device.MACAddress = FormatMACAddress(macAddress);
                    device.Manufacturer = GetManufacturerFromMAC(device.MACAddress);
                }
            }
            catch (Exception ex)
            {
                // Silently fail - MAC detection is not critical
                Console.WriteLine($"MAC detection failed for {device.IPAddress}: {ex.Message}");
            }
        }
        
        private string GetMACFromARP(string ipAddress)
        {
            try
            {
                // Use arp command to get MAC address
                var psi = new ProcessStartInfo
                {
                    FileName = "arp",
                    Arguments = $"-a {ipAddress}",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using (var process = Process.Start(psi))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    
                    // Parse ARP output to find MAC address
                    string[] lines = output.Split('\n');
                    foreach (string line in lines)
                    {
                        if (line.Contains(ipAddress))
                        {
                            // Extract MAC address (format varies by OS)
                            string[] parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length >= 2)
                            {
                                // MAC address is usually after IP
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i] == ipAddress && i + 1 < parts.Length)
                                    {
                                        return parts[i + 1];
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // ARP command failed
            }
            
            return null;
        }
        
        private string GetMACFromWMI(string ipAddress)
        {
            try
            {
                // Only works for Windows and requires appropriate permissions
                var scope = new ManagementScope(@"\\.\root\cimv2");
                var query = new ObjectQuery($"SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPAddress LIKE '%{ipAddress}%'");
                
                using (var searcher = new ManagementObjectSearcher(scope, query))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        if (mo["MACAddress"] != null)
                        {
                            return mo["MACAddress"].ToString();
                        }
                    }
                }
            }
            catch
            {
                // WMI access failed
            }
            
            return null;
        }
        
        private string FormatMACAddress(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return "";
                
            // Remove any non-alphanumeric characters
            string cleanMAC = new string(macAddress.Where(c => char.IsLetterOrDigit(c) || c == ':').ToArray());
            
            // Ensure proper format (XX:XX:XX:XX:XX:XX)
            if (cleanMAC.Length == 12) // No separators
            {
                return string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                    cleanMAC.Substring(0, 2),
                    cleanMAC.Substring(2, 2),
                    cleanMAC.Substring(4, 2),
                    cleanMAC.Substring(6, 2),
                    cleanMAC.Substring(8, 2),
                    cleanMAC.Substring(10, 2));
            }
            
            return cleanMAC.ToUpper();
        }
        
        private string GetManufacturerFromMAC(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return "Unknown";
                
            // Extract OUI (first 3 octets)
            string[] parts = macAddress.Split(':');
            if (parts.Length >= 3)
            {
                string oui = $"{parts[0]}:{parts[1]}:{parts[2]}";
                
                if (_macVendors.TryGetValue(oui, out string vendor))
                {
                    return vendor;
                }
            }
            
            return "Unknown Manufacturer";
        } 
        
        // ========== UI UPDATE METHODS ==========
        
        private void UpdateDeviceRow(Device device)
        {
            if (_dgvDevices.InvokeRequired)
            {
                _dgvDevices.Invoke(new Action<Device>(UpdateDeviceRow), device);
                return;
            }
            
            // Find the row for this specific device
            for (int i = 0; i < _dgvDevices.Rows.Count; i++)
            {
                var row = _dgvDevices.Rows[i];
                var ipCellValue = row.Cells["IP"]?.Value?.ToString();
                
                if (ipCellValue == device.IPAddress)
                {
                    // Update only this specific row
                    row.Cells["Enabled"].Value = device.IsEnabled ? "✓" : "✗";
                    row.Cells["Status"].Value = GetStatusWithIcon(device);
                    row.Cells["MAC"].Value = string.IsNullOrEmpty(device.MACAddress) ? "N/A" : device.MACAddress;
                    row.Cells["Manufacturer"].Value = device.Manufacturer;
                    row.Cells["Ping"].Value = device.PingTime > 0 ? $"{device.PingTime} ms" : "-";
                    row.Cells["LastSeen"].Value = device.LastSeen?.ToString("HH:mm:ss") ?? "Never";
                    
                    // Update cell colors
                    row.Cells["Status"].Style.ForeColor = device.StatusColor;
                    row.Cells["Status"].Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                    
                    row.Cells["Enabled"].Style.ForeColor = device.IsEnabled ? Color.Green : Color.Red;
                    row.Cells["Enabled"].Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                    
                    // Color manufacturer cell for known manufacturers
                    if (device.Manufacturer != "Unknown" && device.Manufacturer != "Unknown Manufacturer")
                    {
                        row.Cells["Manufacturer"].Style.ForeColor = Color.DarkBlue;
                        row.Cells["Manufacturer"].Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                    }
                    
                    if (device.PingTime > 0)
                    {
                        if (device.PingTime < 50)
                            row.Cells["Ping"].Style.ForeColor = Color.Green;
                        else if (device.PingTime < 150)
                            row.Cells["Ping"].Style.ForeColor = Color.Orange;
                        else
                            row.Cells["Ping"].Style.ForeColor = Color.Red;
                    }
                    
                    break;
                }
            }
        }
        
        private void UpdateAnimation()
        {
            _animationStep = (_animationStep + 1) % 10;
            
            lock (_deviceSyncLock)
            {
                foreach (var device in _config.Devices)
                {
                    if (device.Status.Contains("Checking"))
                    {
                        // Animate the checking dots
                        if (!_deviceAnimationStates.ContainsKey(device.Id))
                            _deviceAnimationStates[device.Id] = 0;
                        
                        _deviceAnimationStates[device.Id] = (_deviceAnimationStates[device.Id] + 1) % 4;
                        
                        var dots = new string('.', _deviceAnimationStates[device.Id] + 1);
                        device.Status = $"Checking{dots}";
                        
                        // Pulse the color
                        var alpha = 150 + (int)(105 * Math.Sin(_animationStep * 0.2));
                        device.StatusColor = Color.FromArgb(alpha, _checkingColor);
                        
                        // Update only this device's row
                        UpdateDeviceRow(device);
                    }
                }
            }
        }
        
        private void RefreshDeviceGrid()
        {
            if (_dgvDevices.InvokeRequired)
            {
                _dgvDevices.Invoke(new Action(RefreshDeviceGrid));
                return;
            }
            
            _dgvDevices.Rows.Clear();
            
            List<Device> devices;
            lock (_deviceSyncLock)
            {
                devices = new List<Device>(_config.Devices);
            }
            
            foreach (var device in devices)
            {
                int rowIndex = _dgvDevices.Rows.Add(
                    device.IsEnabled ? "✓" : "✗",
                    GetStatusWithIcon(device),
                    device.Name,
                    device.IPAddress,
                    string.IsNullOrEmpty(device.MACAddress) ? "N/A" : device.MACAddress,
                    device.Manufacturer,
                    device.PingTime > 0 ? $"{device.PingTime} ms" : "-",
                    device.LastSeen?.ToString("HH:mm:ss") ?? "Never"
                );
                
                // Color the status cell
                if (_dgvDevices.Rows[rowIndex].Cells["Status"] is DataGridViewCell statusCell)
                {
                    statusCell.Style.ForeColor = device.StatusColor;
                    statusCell.Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                }
                
                // Color the enabled cell
                if (_dgvDevices.Rows[rowIndex].Cells["Enabled"] is DataGridViewCell enabledCell)
                {
                    enabledCell.Style.ForeColor = device.IsEnabled ? Color.Green : Color.Red;
                    enabledCell.Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                }
                
                // Color manufacturer cell for known manufacturers
                if (_dgvDevices.Rows[rowIndex].Cells["Manufacturer"] is DataGridViewCell manufacturerCell)
                {
                    if (device.Manufacturer != "Unknown" && device.Manufacturer != "Unknown Manufacturer")
                    {
                        manufacturerCell.Style.ForeColor = Color.DarkBlue;
                        manufacturerCell.Style.Font = new Font(_dgvDevices.Font, FontStyle.Bold);
                    }
                }
                
                // Color the ping cell
                if (_dgvDevices.Rows[rowIndex].Cells["Ping"] is DataGridViewCell pingCell && device.PingTime > 0)
                {
                    if (device.PingTime < 50)
                        pingCell.Style.ForeColor = Color.Green;
                    else if (device.PingTime < 150)
                        pingCell.Style.ForeColor = Color.Orange;
                    else
                        pingCell.Style.ForeColor = Color.Red;
                }
            }
        }
        
        private string GetStatusWithIcon(Device device)
        {
            switch (device.Status)
            {
                case string s when s.Contains("Online"): return "🟢 Online";
                case string s when s.Contains("Offline"): 
                    string text = $"🔴 Offline";
                    if (device.OfflineEmailSent)
                    {
                        text += $" ({_config.OfflinePingThreshold}+ 📧)"; // Show threshold+ when email sent
                    }
                    else
                    {
                        text += $" ({device.ConsecutiveOfflinePings})"; // Show actual count when no email yet
                    }
                    return text;
                case string s when s.Contains("Disabled"): return "⚫ Disabled";
                case string s when s.Contains("Checking"): return "🔵 " + device.Status;
                default: return "⚪ " + device.Status;
            }
        }

        private string GetLocalIPAddress()
            {
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return ip.ToString();
                        }
                    }
                    return "127.0.0.1";
                }
                catch
                {
                    return "127.0.0.1";
                }
            }

            private async Task<string> GetPublicIPAddressAsync()
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        client.Timeout = TimeSpan.FromSeconds(10);
                        
                        // Try multiple IP detection services
                        var services = new[]
                        {
                            "https://api.ipify.org",
                            "https://icanhazip.com",
                            "https://checkip.amazonaws.com",
                            "https://ifconfig.me/ip"
                        };
                        
                        foreach (var service in services)
                        {
                            try
                            {
                                var response = await client.GetStringAsync(service);
                                var ip = response.Trim();
                                
                                // Validate it's an IP address
                                if (IsValidIPAddress(ip))
                                {
                                    return ip;
                                }
                            }
                            catch
                            {
                                continue; // Try next service
                            }
                        }
                        
                        return "Unable to determine";
                    }
                }
                catch
                {
                    return "Error fetching IP";
                }
            }

            private List<string> GetAllLocalIPAddresses()
            {
                var ips = new List<string>();
                try
                {
                    var host = Dns.GetHostEntry(Dns.GetHostName());
                    foreach (var ip in host.AddressList)
                    {
                        if (ip.AddressFamily == AddressFamily.InterNetwork)
                        {
                            ips.Add(ip.ToString());
                        }
                    }
                }
                catch
                {
                    // Ignore errors
                }
                return ips;
            }

            // Add to AppConfiguration class for server settings:
            public class ServerConnectionInfo
            {
                public string ServerUrl { get; set; } = "http://your-server-url.com";
                public string ClientId { get; set; } = Guid.NewGuid().ToString();
                public string ClientName { get; set; } = Environment.MachineName;
                public DateTime LastHeartbeat { get; set; }
                public bool IsConnected { get; set; } = false;
                public string LocalIP { get; set; } = "";
                public string PublicIP { get; set; } = "";
                public bool EnableServerSync { get; set; } = false;
            }

            // Add this property to AppConfiguration class:
            public ServerConnectionInfo ServerInfo { get; set; } = new ServerConnectionInfo();

        
        private async Task<bool> SendHeartbeatToServerAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_config.ServerInfo.ServerUrl) || 
                    _config.ServerInfo.ServerUrl == "http://your-server-url.com")
                {
                    return false; // Server not configured
                }
                
                var heartbeatData = new
                {
                    ClientId = _config.ServerInfo.ClientId,
                    ClientName = _config.ServerInfo.ClientName,
                    LocalIP = _config.ServerInfo.LocalIP,
                    PublicIP = _config.ServerInfo.PublicIP,
                    OnlineDevices = _config.Devices.Count(d => d.Status.Contains("Online")),
                    TotalDevices = _config.Devices.Count,
                    Timestamp = DateTime.UtcNow
                };
                
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(heartbeatData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var response = await client.PostAsync(
                        $"{_config.ServerInfo.ServerUrl}/api/heartbeat", 
                        content);
                    
                    _config.ServerInfo.IsConnected = response.IsSuccessStatusCode;
                    _config.ServerInfo.LastHeartbeat = DateTime.Now;
                    
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
            }
            catch
            {
                _config.ServerInfo.IsConnected = false;
            }
            return false;
        }

        private async Task<int> SyncDevicesWithServerAsync()
        {
            // Returns number of devices successfully synced
            try
            {
                if (_config?.ServerInfo == null || string.IsNullOrEmpty(_config.ServerInfo.ServerUrl))
                    return 0;

                using (var http = new HttpClient())
                {
                    // Send heartbeat first
                    await SendHeartbeatToServerAsync();

                    // Get devices from server
                    var response = await http.GetAsync($"{_config.ServerInfo.ServerUrl}/api/devices/{_config.ServerInfo.ClientId}");
                    if (!response.IsSuccessStatusCode)
                        return 0;

                    var respJson = await response.Content.ReadAsStringAsync();
                    var serverDevices = Newtonsoft.Json.JsonConvert.DeserializeObject<List<NetworkMonitor.Shared.Models.Device>>(respJson);

                    int added = 0;
                    foreach (var dev in serverDevices ?? new List<NetworkMonitor.Shared.Models.Device>())
                    {
                        if (!_config.Devices.Any(d => d.IPAddress == dev.IPAddress))
                        {
                            _config.Devices.Add(new Device
                            {
                                Id = dev.Id,
                                Name = dev.Name,
                                IPAddress = dev.IPAddress,
                                Manufacturer = dev.Manufacturer,
                                IsEnabled = dev.IsEnabled,
                                LastSeen = dev.LastSeen,
                                Status = dev.Status
                            });
                            added++;
                        }
                    }

                    // Optionally POST local device statuses back to server (not implemented here)

                    SaveConfig();
                    return added;
                }
            }
            catch
            {
                return 0;
            }
        }

        private async Task<List<Device>> GetDevicesFromServerAsync()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.Timeout = TimeSpan.FromSeconds(30);
                    
                    var response = await client.GetAsync(
                        $"{_config.ServerInfo.ServerUrl}/api/devices/{_config.ServerInfo.ClientId}");
                    
                    if (response.IsSuccessStatusCode)
                    {
                        var json = await response.Content.ReadAsStringAsync();
                        var serverDevices = Newtonsoft.Json.JsonConvert.DeserializeObject<List<Device>>(json);
                        return serverDevices ?? new List<Device>();
                    }
                }
            }
            catch
            {
                // Ignore errors
            }
            return new List<Device>();
        }

        private async Task<bool> SendDeviceStatusToServerAsync(Device device)
        {
            try
            {
                var statusData = new
                {
                    ClientId = _config.ServerInfo.ClientId,
                    DeviceId = device.Id,
                    DeviceName = device.Name,
                    IPAddress = device.IPAddress,
                    Status = device.Status,
                    PingTime = device.PingTime,
                    LastSeen = device.LastSeen,
                    Timestamp = DateTime.UtcNow
                };
                
                using (var client = new HttpClient())
                {
                    var json = Newtonsoft.Json.JsonConvert.SerializeObject(statusData);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");
                    
                    var response = await client.PostAsync(
                        $"{_config.ServerInfo.ServerUrl}/api/devicestatus", 
                        content);
                    
                    return response.IsSuccessStatusCode;
                }
            }
            catch
            {
                return false;
            }
        }


        private void OnDeviceSelected()
        {
            if (_dgvDevices.SelectedRows.Count > 0)
            {
                var ip = _dgvDevices.SelectedRows[0].Cells["IP"].Value?.ToString();
                if (!string.IsNullOrEmpty(ip))
                {
                    lock (_deviceSyncLock)
                    {
                        _selectedDevice = _config.Devices.FirstOrDefault(d => d.IPAddress == ip);
                    }
                    if (_selectedDevice != null)
                    {
                        UpdateButtonStates(true);
                    }
                }
            }
            else
            {
                UpdateButtonStates(false);
            }
        }
        
        // ========== ENHANCED DEVICE ADDITION UI METHODS ==========
        
        private void AddDevice()
        {
            try
            {
                // Show the Add Device dialog
                using (var addDialog = new AddDeviceDialog())
                {
                    if (addDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string name = addDialog.DeviceName;
                        string ip = addDialog.IPAddress;
                        string mac = addDialog.MACAddress;
                        string manufacturer = addDialog.Manufacturer;
                        bool isEnabled = addDialog.IsEnabled;
                        
                        lock (_deviceSyncLock)
                        {
                            // Check for duplicate IP
                            if (_config.Devices.Any(d => d.IPAddress.Equals(ip, StringComparison.OrdinalIgnoreCase)))
                            {
                                MessageBox.Show($"A device with IP '{ip}' already exists.", "Error", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            
                            // Check for duplicate name
                            if (_config.Devices.Any(d => d.Name.Equals(name, StringComparison.OrdinalIgnoreCase)))
                            {
                                MessageBox.Show($"A device named '{name}' already exists.", "Error", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            
                            var device = new Device
                            {
                                Name = name,
                                IPAddress = ip,
                                MACAddress = FormatMACAddress(mac),
                                Manufacturer = !string.IsNullOrEmpty(manufacturer) ? manufacturer : 
                                             (!string.IsNullOrEmpty(mac) ? GetManufacturerFromMAC(mac) : "Unknown"),
                                IsEnabled = isEnabled,
                                Status = "Checking...",
                                StatusColor = _checkingColor,
                                ConsecutiveOfflinePings = 0,
                                OfflineEmailSent = false
                            };
                            
                            // Add to pending list (thread-safe)
                            _devicesToAdd.Add(device);
                            _config.Devices.Add(device); // Also add immediately for UI responsiveness
                            
                            SaveConfig();
                            RefreshDeviceGrid();
                            
                            // Show success message with device info
                            MessageBox.Show($"✅ Device Added Successfully!\n\n" +
                                          $"Name: {name}\n" +
                                          $"IP Address: {ip}\n" +
                                          $"MAC Address: {device.MACAddress}\n" +
                                          $"Manufacturer: {device.Manufacturer}\n" +
                                          $"Status: Checking...\n\n" +
                                          $"The device will now be monitored.",
                                          "Device Added", 
                                          MessageBoxButtons.OK, MessageBoxIcon.Information);
                            
                            ClearForm();
                        }
                        
                        // Start checking the new device immediately
                        Task.Run(async () => await CheckDeviceAsync(ip));
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error adding device: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private async Task CheckDeviceAsync(string ip)
        {
            var result = await PingDeviceAsync(ip);
            
            this.Invoke((MethodInvoker)delegate
            {
                lock (_deviceSyncLock)
                {
                    var device = _config.Devices.FirstOrDefault(d => d.IPAddress == ip);
                    if (device != null)
                    {
                        if (result.IsSuccess)
                        {
                            device.Status = "Online";
                            device.StatusColor = _onlineColor;
                            device.PingTime = result.PingTime;
                            device.LastSeen = DateTime.Now;
                            device.ConsecutiveOfflinePings = 0;
                            device.OfflineEmailSent = false;
                            
                            // Auto-detect MAC address if empty and local IP
                            if (_config.AutoDetectMAC && IsLocalIP(ip) && string.IsNullOrEmpty(device.MACAddress))
                            {
                                Task.Run(() => DetectMACAddress(device));
                            }
                        }
                        else
                        {
                            device.Status = "Offline";
                            device.StatusColor = _offlineColor;
                            device.PingTime = 0;
                            
                            // Check if we already sent the email
                            if (!device.OfflineEmailSent)
                            {
                                device.ConsecutiveOfflinePings = 1;
                            }
                            else
                            {
                                // Keep it at the threshold value if email was already sent
                                device.ConsecutiveOfflinePings = _config.OfflinePingThreshold;
                            }
                            // If email was already sent, counter stays at threshold value
                        }
                        
                        UpdateDeviceRow(device); // Update only this device's row
                        SaveConfig();
                    }
                }
            });
        }
        
        private void UpdateDevice()
        {
            if (_selectedDevice == null) return;
            
            try
            {
                // Show the Add Device dialog with current values for editing
                using (var editDialog = new AddDeviceDialog(
                    _selectedDevice.Name, 
                    _selectedDevice.IPAddress,
                    _selectedDevice.MACAddress,
                    _selectedDevice.Manufacturer,
                    _selectedDevice.IsEnabled))
                {
                    editDialog.Text = "Edit Device";
                    if (editDialog.ShowDialog(this) == DialogResult.OK)
                    {
                        string name = editDialog.DeviceName;
                        string ip = editDialog.IPAddress;
                        string mac = editDialog.MACAddress;
                        string manufacturer = editDialog.Manufacturer;
                        bool isEnabled = editDialog.IsEnabled;
                        
                        // Validate IP address format
                        if (!IsValidIPAddress(ip))
                        {
                            MessageBox.Show("Please enter a valid IP address (e.g., 192.168.1.1)", "Error", 
                                          MessageBoxButtons.OK, MessageBoxIcon.Error);
                            return;
                        }
                        
                        lock (_deviceSyncLock)
                        {
                            var existingDevice = _config.Devices.FirstOrDefault(d => 
                                d.IPAddress.Equals(ip, StringComparison.OrdinalIgnoreCase) && 
                                d != _selectedDevice);
                            
                            if (existingDevice != null)
                            {
                                MessageBox.Show($"A device with IP '{ip}' already exists.", "Error", 
                                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            
                            // Check for duplicate name (excluding current device)
                            var existingNameDevice = _config.Devices.FirstOrDefault(d => 
                                d.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && 
                                d != _selectedDevice);
                            
                            if (existingNameDevice != null)
                            {
                                MessageBox.Show($"A device named '{name}' already exists.", "Error", 
                                              MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }
                            
                            // Update in pending list
                            var updatedDevice = new Device
                            {
                                Id = _selectedDevice.Id,
                                Name = name,
                                IPAddress = ip,
                                MACAddress = FormatMACAddress(mac),
                                Manufacturer = !string.IsNullOrEmpty(manufacturer) ? manufacturer : 
                                             (!string.IsNullOrEmpty(mac) ? GetManufacturerFromMAC(mac) : "Unknown"),
                                IsEnabled = isEnabled,
                                ConsecutiveOfflinePings = _selectedDevice.ConsecutiveOfflinePings,
                                OfflineEmailSent = _selectedDevice.OfflineEmailSent
                            };
                            
                            _devicesToUpdate[_selectedDevice.Id] = updatedDevice;
                            
                            // Also update immediately for UI responsiveness
                            _selectedDevice.Name = name;
                            _selectedDevice.IPAddress = ip;
                            _selectedDevice.MACAddress = FormatMACAddress(mac);
                            _selectedDevice.Manufacturer = !string.IsNullOrEmpty(manufacturer) ? manufacturer : 
                                                         (!string.IsNullOrEmpty(mac) ? GetManufacturerFromMAC(mac) : "Unknown");
                            _selectedDevice.IsEnabled = isEnabled;
                            
                            if (!_selectedDevice.IsEnabled)
                            {
                                _selectedDevice.Status = "Disabled";
                                _selectedDevice.StatusColor = _disabledColor;
                                // Reset counters for disabled devices
                                _selectedDevice.ConsecutiveOfflinePings = 0;
                                _selectedDevice.OfflineEmailSent = false;
                            }
                            else
                            {
                                _selectedDevice.Status = "Checking...";
                                _selectedDevice.StatusColor = _checkingColor;
                            }
                            
                            SaveConfig();
                            UpdateDeviceRow(_selectedDevice); // Update only this device's row
                            
                            MessageBox.Show($"✅ Device Updated Successfully!\n\n" +
                                          $"Name: {name}\n" +
                                          $"IP Address: {ip}\n" +
                                          $"MAC Address: {_selectedDevice.MACAddress}\n" +
                                          $"Manufacturer: {_selectedDevice.Manufacturer}\n" +
                                          $"Status: {_selectedDevice.Status}",
                                          "Device Updated", 
                                          MessageBoxButtons.OK, MessageBoxIcon.Information);
                            
                            ClearForm();
                        }
                        
                        // Re-check the device if enabled
                        if (isEnabled)
                        {
                            Task.Run(async () => await CheckDeviceAsync(ip));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Error updating device: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RemoveDevice()
        {
            if (_selectedDevice != null)
            {
                if (MessageBox.Show($"⚠️ Are you sure you want to remove this device?\n\n" +
                                  $"Name: {_selectedDevice.Name}\n" +
                                  $"IP Address: {_selectedDevice.IPAddress}\n" +
                                  $"MAC: {_selectedDevice.MACAddress}\n\n" +
                                  $"This action cannot be undone.",
                                  "Confirm Device Removal", 
                                  MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    lock (_deviceSyncLock)
                    {
                        // Add to removal list
                        _devicesToRemove.Add(_selectedDevice.Id);
                        
                        // Also remove immediately for UI responsiveness
                        _config.Devices.Remove(_selectedDevice);
                        
                        SaveConfig();
                        RefreshDeviceGrid();
                        
                        MessageBox.Show($"✅ Device '{_selectedDevice.Name}' has been removed from monitoring.",
                                      "Device Removed", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                        
                        ClearForm();
                    }
                }
            }
        }
        
        private void ClearForm()
        {
            _selectedDevice = null;
            
            // Clear selection in grid
            if (_dgvDevices.SelectedRows.Count > 0)
                _dgvDevices.ClearSelection();
            
            // Reset button states
            UpdateButtonStates(false);
        }
        
        private void UpdateButtonStates(bool editing)
        {
            _btnAdd.Enabled = true; // Always enabled
            _btnUpdate.Enabled = editing;
            _btnRemove.Enabled = editing;
        }
        
        private async void TestAllDevices()
        {
            _lblMonitoringStatus.Text = "🔄 Testing All Devices...";
            _lblMonitoringStatus.ForeColor = Color.Orange;
            
            var progressDialog = new Form
            {
                Text = "Testing Devices",
                Size = new Size(300, 100),
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false
            };
            
            var progressBar = new ProgressBar
            {
                Location = new Point(20, 20),
                Size = new Size(260, 30),
                Style = ProgressBarStyle.Marquee
            };
            
            var lblProgress = new Label
            {
                Text = "Testing all devices, please wait...",
                Location = new Point(20, 60),
                AutoSize = true
            };
            
            progressDialog.Controls.AddRange(new Control[] { progressBar, lblProgress });
            
            // Show progress dialog in a separate thread
            var progressTask = Task.Run(() => progressDialog.ShowDialog());
            
            await CheckAllDevicesAsync();
            
            // Close progress dialog
            progressDialog.Invoke((MethodInvoker)delegate { progressDialog.Close(); });
            
            _lblMonitoringStatus.Text = "🟢 Monitoring Active";
            _lblMonitoringStatus.ForeColor = Color.LimeGreen;
            
            // Show summary
            int onlineCount = _config.Devices.Count(d => d.Status.Contains("Online"));
            int offlineCount = _config.Devices.Count(d => d.Status.Contains("Offline"));
            int disabledCount = _config.Devices.Count(d => d.Status.Contains("Disabled"));
            int macKnownCount = _config.Devices.Count(d => !string.IsNullOrEmpty(d.MACAddress));
            int manufacturerKnownCount = _config.Devices.Count(d => d.Manufacturer != "Unknown");
            int offlineEmailSentCount = _config.Devices.Count(d => d.OfflineEmailSent);
            int offlineCountingCount = _config.Devices.Count(d => 
                d.Status.Contains("Offline") && !d.OfflineEmailSent);

            MessageBox.Show($"✅ All devices have been tested.\n\n" +
                        $"📊 Summary:\n" +
                        $"• Online: {onlineCount}\n" +
                        $"• Offline (counting): {offlineCountingCount}\n" +
                        $"• Offline (email sent): {offlineEmailSentCount}\n" +
                        $"• Disabled: {disabledCount}\n" +
                        $"• Total Devices: {_config.Devices.Count}\n\n" +
                        $"💡 Note: Offline counters STOP at threshold ({_config.OfflinePingThreshold}) after email is sent.",
                        "Test Complete", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        // ========== VALIDATION METHODS ==========
        
        private bool IsValidIPAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;
            
            // Regular expression for IP address validation
            string ipPattern = @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            
            return Regex.IsMatch(ip, ipPattern);
        }
        
        private bool IsValidMACAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac))
                return true; // Empty MAC is valid (optional)
                
            // Regular expression for MAC address validation
            string macPattern = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$";
            
            return Regex.IsMatch(mac, macPattern);
        }
        
        // ========== MONITORING TAB METHODS ==========
        
        private void StartMonitoringAll()
        {
            lock (_deviceSyncLock)
            {
                foreach (var device in _config.Devices)
                {
                    device.IsEnabled = true;
                    device.Status = "Checking...";
                    device.StatusColor = _checkingColor;
                    // Reset counters when starting monitoring
                    device.ConsecutiveOfflinePings = 0;
                    device.OfflineEmailSent = false;
                    UpdateDeviceRow(device);
                }
            }
            SaveConfig();
            MessageBox.Show("✅ All devices have been enabled for monitoring.", "Success", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void StopMonitoringAll()
        {
            lock (_deviceSyncLock)
            {
                foreach (var device in _config.Devices)
                {
                    device.IsEnabled = false;
                    device.Status = "Disabled";
                    device.StatusColor = _disabledColor;
                    // Reset counters when stopping monitoring
                    device.ConsecutiveOfflinePings = 0;
                    device.OfflineEmailSent = false;
                    UpdateDeviceRow(device);
                }
            }
            SaveConfig();
            MessageBox.Show("✅ All devices have been disabled for monitoring.", "Success", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        // ========== SERVICE TAB METHODS ==========
        
        private void UpdateServiceStatus()
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    var status = sc.Status;
                    _lblServiceStatus.Text = $"Service Status: {status}";
                    _lblServiceStatus.ForeColor = status == ServiceControllerStatus.Running ? 
                        Color.Green : Color.Red;
                }
            }
            catch
            {
                _lblServiceStatus.Text = "Service Status: Not Installed";
                _lblServiceStatus.ForeColor = Color.Orange;
            }
        }
        
        private void StartService()
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status != ServiceControllerStatus.Running)
                    {
                        sc.Start();
                        sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                        MessageBox.Show("✅ Service started successfully.", "Success", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("ℹ️ Service is already running.", "Info", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to start service: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void StopService()
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                        MessageBox.Show("✅ Service stopped successfully.", "Success", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    else
                    {
                        MessageBox.Show("ℹ️ Service is not running.", "Info", 
                                      MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to stop service: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RestartService()
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        sc.Stop();
                        sc.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(30));
                    }
                    sc.Start();
                    sc.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(30));
                    MessageBox.Show("✅ Service restarted successfully.", "Success", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to restart service: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void InstallService()
        {
            MessageBox.Show("🔧 Service Installation Instructions\n\n" +
                          "To install the service:\n\n" +
                          "1. Build the NetworkMonitorService project\n" +
                          "2. Run install-service.bat as Administrator\n" +
                          "3. Or use command: sc create NetworkMonitorService binPath= [path to .exe]\n\n" +
                          "Note: You need Administrator privileges to install services.", 
                          "Install Service", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void UninstallService()
        {
            MessageBox.Show("🗑️ Service Uninstallation Instructions\n\n" +
                          "To uninstall the service:\n\n" +
                          "1. Run: sc delete NetworkMonitorService\n" +
                          "2. Or use the uninstall-service.bat script\n\n" +
                          "Note: You need Administrator privileges to uninstall services.", 
                          "Uninstall Service", 
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        
        private void OpenConfigFolder()
        {
            try
            {
                var configDir = Path.GetDirectoryName(ConfigManager.GetConfigPath());
                if (!Directory.Exists(configDir))
                    Directory.CreateDirectory(configDir);
                    
                Process.Start("explorer.exe", configDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to open config folder: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // ========== SETTINGS TAB METHODS ==========
        
        private void BackupConfiguration()
        {
            try
            {
                string backupDir = @"C:\NetworkMonitor\Backups";
                if (!Directory.Exists(backupDir))
                    Directory.CreateDirectory(backupDir);
                
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string backupFile = Path.Combine(backupDir, $"config_backup_{timestamp}.xml");
                
                File.Copy(ConfigManager.GetConfigPath(), backupFile, true);
                
                MessageBox.Show($"✅ Configuration backed up successfully!\n\n" +
                              $"Backup saved to:\n{backupFile}", 
                              "Backup Complete", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to backup configuration: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void RestoreConfiguration()
        {
            try
            {
                string backupDir = @"C:\NetworkMonitor\Backups";
                if (!Directory.Exists(backupDir))
                {
                    MessageBox.Show("No backup directory found.", "Info", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                
                using (OpenFileDialog dialog = new OpenFileDialog())
                {
                    dialog.InitialDirectory = backupDir;
                    dialog.Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*";
                    dialog.Title = "Select backup file to restore";
                    
                    if (dialog.ShowDialog() == DialogResult.OK)
                    {
                        if (MessageBox.Show("Are you sure you want to restore this configuration?\n" +
                                          "Current settings will be overwritten.", 
                                          "Confirm Restore", 
                                          MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                        {
                            File.Copy(dialog.FileName, ConfigManager.GetConfigPath(), true);
                            
                            // Reload configuration
                            LoadConfiguration();
                            RefreshDeviceGrid();
                            
                            MessageBox.Show("✅ Configuration restored successfully!\n" +
                                          "Please restart the application for changes to take effect.", 
                                          "Restore Complete", 
                                          MessageBoxButtons.OK, MessageBoxIcon.Information);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to restore configuration: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        private void ResetConfiguration()
        {
            if (MessageBox.Show("⚠️ Are you sure you want to reset to default configuration?\n\n" +
                              "This will:\n" +
                              "• Remove all devices\n" +
                              "• Reset all settings to defaults\n" +
                              "• Keep SMTP settings \n\n" +
                              "This action cannot be undone!", 
                              "Confirm Reset", 
                              MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    // Backup current config first
                    BackupConfiguration();
                    
                    // Create new config with default values
                    var newConfig = new AppConfiguration();
                    
                    // SMTP settings are already hardcoded in the constructor
                    
                    ConfigManager.SaveConfiguration(newConfig);
                    _config = newConfig;
                    RefreshDeviceGrid();
                    
                    MessageBox.Show("✅ Configuration reset to defaults!", "Reset Complete", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Failed to reset configuration: {ex.Message}", "Error", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        // ========== LOGS TAB METHODS ==========
        
        private void RefreshLogs()
        {
            try
            {
                if (File.Exists(_config.LogFilePath))
                {
                    var lines = File.ReadAllLines(_config.LogFilePath);
                    var recent = lines.Length > 100 ? lines.Skip(lines.Length - 100) : lines;
                    _txtLogs.Text = string.Join(Environment.NewLine, recent);
                    _txtLogs.SelectionStart = _txtLogs.Text.Length;
                    _txtLogs.ScrollToCaret();
                }
                else
                {
                    _txtLogs.Text = "Log file not found. Service may not have started yet.";
                }
            }
            catch (Exception ex)
            {
                _txtLogs.Text = $"❌ Error reading log: {ex.Message}";
            }
        }
        
        private void ClearLogs()
        {
            if (MessageBox.Show("⚠️ Are you sure you want to clear all log files?\n\n" +
                              "This action cannot be undone.", 
                              "Confirm Log Clear", 
                              MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
            {
                try
                {
                    if (File.Exists(_config.LogFilePath))
                    {
                        File.Delete(_config.LogFilePath);
                        _txtLogs.Text = "✅ Logs cleared.";
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ Failed to clear logs: {ex.Message}", "Error", 
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        
        private void OpenLogFolder()
        {
            try
            {
                var logDir = Path.GetDirectoryName(_config.LogFilePath);
                if (!Directory.Exists(logDir))
                    Directory.CreateDirectory(logDir);
                    
                Process.Start("explorer.exe", logDir);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"❌ Failed to open log folder: {ex.Message}", "Error", 
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        
        // ========== CONFIGURATION METHODS ==========
        
        private void SaveConfig()
        {
            ConfigManager.SaveConfiguration(_config);
        }
        
        // Method to reset all offline counters
        private void ResetAllOfflineCounters()
        {
            if (MessageBox.Show("Reset all offline ping counters and email flags?\n\n" +
                              "This will:\n" +
                              "• Reset consecutive offline ping counts to 0\n" +
                              "• Clear offline email sent flags\n" +
                              "• Allow new emails to be sent when devices go offline again\n\n" +
                              "Continue?", 
                              "Reset Counters", 
                              MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                lock (_deviceSyncLock)
                {
                    foreach (var device in _config.Devices)
                    {
                        device.ConsecutiveOfflinePings = 0;
                        device.OfflineEmailSent = false;
                    }
                }
                
                RefreshDeviceGrid();
                
                MessageBox.Show("✅ All offline counters and email flags have been reset.", 
                              "Counters Reset", 
                              MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void Logout()
        {
            if (MessageBox.Show("Are you sure you want to logout?", "Confirm Logout",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                StopMonitoring();
                
                if (_trayIcon != null)
                {
                    _trayIcon.Visible = false;
                }
                
                DisposeCachedIcons();
                SaveConfig();
                
                Application.Restart();
                Environment.Exit(0);
            }
        }
    }
}