using System;
using System.Drawing;
using System.ServiceProcess;
using System.Windows.Forms;

namespace NetworkMonitorGUI.Forms
{
    public class TrayForm : Form
    {
        private NotifyIcon _trayIcon;
        private ContextMenuStrip _trayMenu;
        private Timer _statusTimer;
        private readonly string _serviceName = "NetworkMonitorService";
        
        public TrayForm()
        {
            InitializeTray();
            this.WindowState = FormWindowState.Minimized;
            this.ShowInTaskbar = false;
        }
        
        private void InitializeTray()
        {
            _trayIcon = new NotifyIcon
            {
                Text = "Network Monitor",
                Icon = new Icon(SystemIcons.Application, 40, 40),
                Visible = true
            };
            
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Items.Add("Show Control Panel", null, OnShowControlPanel);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Start Service", null, OnStartService);
            _trayMenu.Items.Add("Stop Service", null, OnStopService);
            _trayMenu.Items.Add("-");
            _trayMenu.Items.Add("Exit", null, OnExit);
            
            _trayIcon.ContextMenuStrip = _trayMenu;
            _trayIcon.DoubleClick += OnShowControlPanel;
            
            _statusTimer = new Timer { Interval = 10000 };
            _statusTimer.Tick += UpdateTrayStatus;
            _statusTimer.Start();
        }
        
        private void UpdateTrayStatus(object sender, EventArgs e)
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    if (sc.Status == ServiceControllerStatus.Running)
                    {
                        _trayIcon.Icon = new Icon(SystemIcons.Shield, 40, 40);
                        _trayIcon.Text = "Network Monitor: Running";
                    }
                    else
                    {
                        _trayIcon.Icon = new Icon(SystemIcons.Warning, 40, 40);
                        _trayIcon.Text = "Network Monitor: Stopped";
                    }
                }
            }
            catch
            {
                _trayIcon.Icon = new Icon(SystemIcons.Error, 40, 40);
                _trayIcon.Text = "Network Monitor: Service Not Found";
            }
        }
        
        private void OnShowControlPanel(object sender, EventArgs e)
        {
            // Launch main GUI
            System.Diagnostics.Process.Start("NetworkMonitorGUI.exe");
        }
        
        private void OnStartService(object sender, EventArgs e)
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    sc.Start();
                    _trayIcon.ShowBalloonTip(3000, "Service Started", 
                        "Network Monitor Service is now running.", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _trayIcon.ShowBalloonTip(5000, "Error", 
                    $"Failed to start service: {ex.Message}", ToolTipIcon.Error);
            }
        }
        
        private void OnStopService(object sender, EventArgs e)
        {
            try
            {
                using (var sc = new ServiceController(_serviceName))
                {
                    sc.Stop();
                    _trayIcon.ShowBalloonTip(3000, "Service Stopped", 
                        "Network Monitor Service has been stopped.", ToolTipIcon.Info);
                }
            }
            catch (Exception ex)
            {
                _trayIcon.ShowBalloonTip(5000, "Error", 
                    $"Failed to stop service: {ex.Message}", ToolTipIcon.Error);
            }
        }
        
        private void OnExit(object sender, EventArgs e)
        {
            _trayIcon.Visible = false;
            Application.Exit();
        }
        
        protected override void OnLoad(EventArgs e)
        {
            Visible = false;
            ShowInTaskbar = false;
            base.OnLoad(e);
        }
        
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _trayIcon?.Dispose();
                _trayMenu?.Dispose();
                _statusTimer?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}