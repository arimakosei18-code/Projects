using System;
using System.Drawing;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Linq;
using System.Collections.Generic;

namespace NetworkMonitorGUI.Forms
{
    public partial class AddDeviceDialog : Form
    {
        public string DeviceName { get; private set; }
        public string IPAddress { get; private set; }
        public string MACAddress { get; private set; }
        public string Manufacturer { get; private set; }
        public bool IsEnabled { get; private set; }
        
        // MAC vendor database (matching the one in MainForm)
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
        
        public AddDeviceDialog()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.CenterParent;
        }
        
        public AddDeviceDialog(string defaultName = "", string defaultIp = "", bool defaultEnabled = true)
            : this()
        {
            txtDeviceName.Text = defaultName;
            txtIPAddress.Text = defaultIp;
            chkEnabled.Checked = defaultEnabled;
        }
        
        // New constructor with MAC and Manufacturer
        public AddDeviceDialog(string defaultName = "", string defaultIp = "", 
                              string defaultMAC = "", string defaultManufacturer = "", 
                              bool defaultEnabled = true) : this()
        {
            txtDeviceName.Text = defaultName;
            txtIPAddress.Text = defaultIp;
            txtMACAddress.Text = defaultMAC;
            
            if (!string.IsNullOrEmpty(defaultManufacturer) && defaultManufacturer != "Unknown")
            {
                txtManufacturer.Text = defaultManufacturer;
            }
            else if (!string.IsNullOrEmpty(defaultMAC))
            {
                // Try to auto-detect manufacturer from MAC
                txtManufacturer.Text = GetManufacturerFromMAC(defaultMAC);
            }
            
            chkEnabled.Checked = defaultEnabled;
        }
        
        private void InitializeComponent()
        {
            this.Text = "Add Device";
            this.Size = new Size(600, 350); // Increased height for new fields
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ShowIcon = false;
            this.StartPosition = FormStartPosition.CenterParent;
            
            // Main panel
            var mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(20),
                BackColor = Color.WhiteSmoke
            };
            
            // Title
            var lblTitle = new Label
            {
                Text = "➕ Configure Device Info:",
                Font = new Font("Arial", 14, FontStyle.Bold),
                Location = new Point(10, 10),
                AutoSize = true,
                ForeColor = Color.FromArgb(33, 33, 33)
            };
            
            var lblSubtitle = new Label
            {
                Text = "Enter device details below:",
                Font = new Font("Arial", 9),
                Location = new Point(10, 40),
                AutoSize = true,
                ForeColor = Color.Gray
            };
            
            // Device name
            var lblName = new Label
            {
                Text = "Device Name:",
                Location = new Point(10, 70),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            
            txtDeviceName = new TextBox
            {
                Location = new Point(120, 67),
                Size = new Size(340, 25),
                Font = new Font("Arial", 9)
            };
            
            // IP Address
            var lblIP = new Label
            {
                Text = "IP Address:",
                Location = new Point(10, 110),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            
            txtIPAddress = new TextBox
            {
                Location = new Point(120, 107),
                Size = new Size(200, 25),
                Font = new Font("Arial", 9)
            };
            
            // IP Example label
            var lblIPExample = new Label
            {
                Text = "Example: 192.168.1.1",
                Location = new Point(330, 110),
                AutoSize = true,
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray
            };
            
            // MAC Address
            var lblMAC = new Label
            {
                Text = "MAC Address:",
                Location = new Point(10, 150),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            
            txtMACAddress = new TextBox
            {
                Location = new Point(120, 147),
                Size = new Size(200, 25),
                Font = new Font("Arial", 9),
                CharacterCasing = CharacterCasing.Upper
            };
            
            // MAC Example label and format button
            var lblMACExample = new Label
            {
                Text = "Format: XX:XX:XX:XX:XX:XX",
                Location = new Point(330, 150),
                AutoSize = true,
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray
            };
            
            var btnFormatMAC = new Button
            {
                Text = "Format",
                Location = new Point(478, 147),
                Size = new Size(60, 25),
                BackColor = Color.LightBlue,
                Font = new Font("Arial", 8)
            };
            btnFormatMAC.Click += (s, e) => FormatMACAddress();
            
            // Manufacturer
            var lblManufacturer = new Label
            {
                Text = "Manufacturer:",
                Location = new Point(10, 190),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            
            txtManufacturer = new TextBox
            {
                Location = new Point(120, 187),
                Size = new Size(200, 25),
                Font = new Font("Arial", 9)
            };
            
            // Auto-detect manufacturer button
            var btnDetectManufacturer = new Button
            {
                Text = "Auto-Detect",
                Location = new Point(478, 187),
                Size = new Size(80, 25),
                BackColor = Color.LightGreen,
                Font = new Font("Arial", 8)
            };
            btnDetectManufacturer.Click += (s, e) => AutoDetectManufacturer();
            
            // Manufacturer suggestions
            var lblManufacturerHelp = new Label
            {
                Text = "auto-detection from MAC",
                Location = new Point(330, 190),
                AutoSize = true,
                Font = new Font("Arial", 8),
                ForeColor = Color.Gray
            };
            
            // Enabled checkbox
            chkEnabled = new CheckBox
            {
                Text = "Enable monitoring for this device",
                Location = new Point(10, 230),
                AutoSize = true,
                Checked = true,
                Font = new Font("Arial", 9)
            };
            
            // Quick add buttons
            var lblQuickAdd = new Label
            {
                Text = "Quick Add:",
                Location = new Point(10, 260),
                AutoSize = true,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            
            var btnRouter = CreateQuickAddButton("Router", "192.168.1.1", "00:1A:2B:3C:4D:5E", "Netgear", 80, 258);
            btnRouter.Click += (s, e) => SetQuickAdd("Router", "192.168.1.1", "00:1A:2B:3C:4D:5E", "Netgear");
            
            var btnGoogle = CreateQuickAddButton("Google DNS", "8.8.8.8", "", "Google", 150, 258);
            btnGoogle.Click += (s, e) => SetQuickAdd("Google DNS", "8.8.8.8", "", "Google");
            
            var btnCloudflare = CreateQuickAddButton("Cloudflare", "1.1.1.1", "", "Cloudflare", 220, 258);
            btnCloudflare.Click += (s, e) => SetQuickAdd("Cloudflare DNS", "1.1.1.1", "", "Cloudflare");
            
            // Button panel
            var buttonPanel = new Panel
            {
                Location = new Point(0, 258),
                Size = new Size(560, 40)
            };
            
            btnOK = new Button
            {
                Text = "Continue",
                Location = new Point(380, 0),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                DialogResult = DialogResult.OK,
                Font = new Font("Arial", 9, FontStyle.Bold)
            };
            btnOK.Click += (s, e) => ValidateAndClose();
            
            btnCancel = new Button
            {
                Text = "Cancel",
                Location = new Point(475, 0),
                Size = new Size(85, 30),
                BackColor = Color.FromArgb(158, 158, 158),
                ForeColor = Color.White,
                DialogResult = DialogResult.Cancel,
                Font = new Font("Arial", 9)
            };
            
            buttonPanel.Controls.AddRange(new Control[] { btnOK, btnCancel });
            
            // Add controls to main panel
            mainPanel.Controls.AddRange(new Control[]
            {
                lblTitle, lblSubtitle,
                lblName, txtDeviceName,
                lblIP, txtIPAddress, lblIPExample,
                lblMAC, txtMACAddress, lblMACExample, btnFormatMAC,
                lblManufacturer, txtManufacturer, btnDetectManufacturer, lblManufacturerHelp,
                chkEnabled,
                lblQuickAdd, btnRouter, btnGoogle, btnCloudflare,
                buttonPanel
            });
            
            this.Controls.Add(mainPanel);
            
            // Set accept and cancel buttons
            this.AcceptButton = btnOK;
            this.CancelButton = btnCancel;
            
            // Set focus to device name
            this.Shown += (s, e) => txtDeviceName.Focus();
            
            // Add text change events for auto-completion
            txtMACAddress.TextChanged += (s, e) => AutoFormatMACAddress();
            txtMACAddress.Leave += (s, e) => AutoDetectManufacturer();
        }
        
        private Button CreateQuickAddButton(string text, string ip, string mac, string manufacturer, int x, int y)
        {
            return new Button
            {
                Text = text,
                Location = new Point(x, y),
                Size = new Size(65, 23),
                BackColor = Color.LightSkyBlue,
                FlatStyle = FlatStyle.Flat,
                Font = new Font("Arial", 8),
                Tag = new string[] { ip, mac, manufacturer }
            };
        }
        
        private void SetQuickAdd(string name, string ip, string mac, string manufacturer)
        {
            txtDeviceName.Text = name;
            txtIPAddress.Text = ip;
            txtMACAddress.Text = mac;
            txtManufacturer.Text = manufacturer;
            chkEnabled.Checked = true;
            txtDeviceName.Focus();
            txtDeviceName.SelectAll();
        }
        
        private void AutoFormatMACAddress()
        {
            string mac = txtMACAddress.Text;
            if (string.IsNullOrEmpty(mac)) return;
            
            // Remove any non-alphanumeric characters
            string cleanMAC = new string(mac.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            // Auto-insert colons as user types
            if (cleanMAC.Length >= 2 && cleanMAC.Length < 17)
            {
                string formatted = "";
                for (int i = 0; i < cleanMAC.Length; i++)
                {
                    formatted += cleanMAC[i];
                    if ((i + 1) % 2 == 0 && i < cleanMAC.Length - 1 && i < 11)
                    {
                        formatted += ":";
                    }
                }
                
                if (formatted != mac)
                {
                    int cursorPos = txtMACAddress.SelectionStart;
                    txtMACAddress.Text = formatted.ToUpper();
                    txtMACAddress.SelectionStart = Math.Min(cursorPos + 1, txtMACAddress.Text.Length);
                }
            }
        }
        
        private void FormatMACAddress()
        {
            string mac = txtMACAddress.Text.Trim();
            if (string.IsNullOrEmpty(mac)) return;
            
            // Remove any non-alphanumeric characters
            string cleanMAC = new string(mac.Where(c => char.IsLetterOrDigit(c)).ToArray());
            
            if (cleanMAC.Length == 12) // Valid length
            {
                string formatted = string.Format("{0}:{1}:{2}:{3}:{4}:{5}",
                    cleanMAC.Substring(0, 2),
                    cleanMAC.Substring(2, 2),
                    cleanMAC.Substring(4, 2),
                    cleanMAC.Substring(6, 2),
                    cleanMAC.Substring(8, 2),
                    cleanMAC.Substring(10, 2));
                
                txtMACAddress.Text = formatted.ToUpper();
            }
            else
            {
                MessageBox.Show("MAC address must be 12 hexadecimal characters (e.g., 001A2B3C4D5E)", 
                    "Invalid MAC Format", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
        
        private void AutoDetectManufacturer()
        {
            string mac = txtMACAddress.Text.Trim();
            if (string.IsNullOrEmpty(mac) || !string.IsNullOrEmpty(txtManufacturer.Text)) return;
            
            string manufacturer = GetManufacturerFromMAC(mac);
            if (manufacturer != "Unknown" && manufacturer != "Unknown Manufacturer")
            {
                txtManufacturer.Text = manufacturer;
            }
        }
        
        private string GetManufacturerFromMAC(string macAddress)
        {
            if (string.IsNullOrEmpty(macAddress))
                return "Unknown";
                
            // Clean and format MAC address
            string cleanMAC = new string(macAddress.Where(c => char.IsLetterOrDigit(c)).ToArray());
            if (cleanMAC.Length < 6) return "Unknown";
            
            // Extract OUI (first 3 octets)
            string oui = $"{cleanMAC.Substring(0, 2)}:{cleanMAC.Substring(2, 2)}:{cleanMAC.Substring(4, 2)}";
            
            if (_macVendors.TryGetValue(oui, out string vendor))
            {
                return vendor;
            }
            
            return "Unknown Manufacturer";
        }
        
        private void ValidateAndClose()
        {
            string name = txtDeviceName.Text.Trim();
            string ip = txtIPAddress.Text.Trim();
            string mac = txtMACAddress.Text.Trim();
            string manufacturer = txtManufacturer.Text.Trim();
            
            // Validate inputs
            if (string.IsNullOrWhiteSpace(name))
            {
                MessageBox.Show("Please enter a device name.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtDeviceName.Focus();
                return;
            }
            
            if (string.IsNullOrWhiteSpace(ip))
            {
                MessageBox.Show("Please enter an IP address.", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtIPAddress.Focus();
                return;
            }
            
            if (!IsValidIPAddress(ip))
            {
                MessageBox.Show("Please enter a valid IP address (e.g., 192.168.1.1).", "Validation Error", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtIPAddress.Focus();
                txtIPAddress.SelectAll();
                return;
            }
            
            if (!string.IsNullOrEmpty(mac) && !IsValidMACAddress(mac))
            {
                MessageBox.Show("Please enter a valid MAC address (format: XX:XX:XX:XX:XX:XX).\n\n" +
                              "You can use the 'Format' button to fix the format.", 
                              "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtMACAddress.Focus();
                txtMACAddress.SelectAll();
                return;
            }
            
            // Auto-detect manufacturer if MAC is provided but manufacturer is empty
            if (!string.IsNullOrEmpty(mac) && string.IsNullOrEmpty(manufacturer))
            {
                manufacturer = GetManufacturerFromMAC(mac);
                txtManufacturer.Text = manufacturer;
            }
            
            // Set properties and close
            DeviceName = name;
            IPAddress = ip;
            MACAddress = mac;
            Manufacturer = string.IsNullOrEmpty(manufacturer) ? "Unknown" : manufacturer;
            IsEnabled = chkEnabled.Checked;
            
            this.DialogResult = DialogResult.OK;
        }
        
        private bool IsValidIPAddress(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return false;
            
            string ipPattern = @"^(?:(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)\.){3}(?:25[0-5]|2[0-4][0-9]|[01]?[0-9][0-9]?)$";
            return Regex.IsMatch(ip, ipPattern);
        }
        
        private bool IsValidMACAddress(string mac)
        {
            if (string.IsNullOrEmpty(mac))
                return true; // Empty MAC is valid (optional)
                
            // Regular expression for MAC address validation (with or without separators)
            string macPattern = @"^([0-9A-Fa-f]{2}[:-]){5}([0-9A-Fa-f]{2})$|^([0-9A-Fa-f]{12})$";
            
            return Regex.IsMatch(mac, macPattern);
        }
        
        // Form controls
        private TextBox txtDeviceName;
        private TextBox txtIPAddress;
        private TextBox txtMACAddress;
        private TextBox txtManufacturer;
        private CheckBox chkEnabled;
        private Button btnOK;
        private Button btnCancel;
    }
}