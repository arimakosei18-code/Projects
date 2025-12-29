using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace NetworkMonitorGUI.Forms
{
    public partial class LoginForm : Form
    {
        // Properties to pass data back to Program.cs
        public AppUser LoggedInUser { get; private set; }
        
        // UI Components
        private Panel _pnlLeft;
        private Panel _pnlRight;
        private PictureBox _pbLogo;
        private TextBox _txtUsername;
        private TextBox _txtPassword;
        private Button _btnLogin;
        private Button _btnExit;
        private Label _lblStatus;
        
        // Demo users
        private readonly AppUser[] _demoUsers = new[]
        {
            new AppUser { 
                Username = "admin", 
                Password = "Cpd8255ny!", 
                DisplayName = "Administrator",
                Role = "Admin"
            },
            new AppUser { 
                Username = "user", 
                Password = "user123!", 
                DisplayName = "Standard User",
                Role = "User"
            },
            new AppUser { 
                Username = "technician", 
                Password = "tech123", 
                DisplayName = "Network Technician",
                Role = "Technician"
            }
        };
        
        // Color scheme
        private readonly Color _primaryColor = Color.FromArgb(26, 95, 180);
        private readonly Color _secondaryColor = Color.FromArgb(33, 150, 243);
        private readonly Color _successColor = Color.FromArgb(76, 175, 80);
        private readonly Color _errorColor = Color.FromArgb(244, 67, 54);
        private readonly Color _lightGray = Color.FromArgb(245, 245, 245);
        private readonly Color _mediumGray = Color.FromArgb(189, 189, 189);
        private readonly Color _darkGray = Color.FromArgb(97, 97, 97);
        
        public LoginForm()
        {
            InitializeForm();
            LoadSavedCredentials();
        }
        
        private void InitializeForm()
        {
            // Form settings
            this.Text = "Network Monitor - Login";
            this.ClientSize = new Size(900, 550);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.BackColor = Color.White;
            
            // Set icon
            SetFormIcon();
            
            // Create panels
            CreatePanels();
            
            // Initialize content
            InitializeLeftPanel();
            InitializeRightPanel();
            
            this.Shown += (s, e) => _txtUsername.Focus();
        }
        
        private void CreatePanels()
        {
            // Left panel - 40% width
            _pnlLeft = new Panel
            {
                Size = new Size(360, this.ClientSize.Height),
                Location = new Point(0, 0),
                BackColor = _primaryColor
            };
            
            // Right panel - 60% width
            _pnlRight = new Panel
            {
                Size = new Size(this.ClientSize.Width - 360, this.ClientSize.Height),
                Location = new Point(360, 0),
                BackColor = Color.White
            };
            
            this.Controls.Add(_pnlLeft);
            this.Controls.Add(_pnlRight);
        }
        
        private void SetFormIcon()
        {
            try
            {
                string iconPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "icon.ico");
                if (System.IO.File.Exists(iconPath))
                    this.Icon = new Icon(iconPath);
            }
            catch { }
        }
        
        private void InitializeLeftPanel()
        {
            // Logo
            _pbLogo = new PictureBox
            {
                Image = LoadLogoImage(),
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(120, 120),
                Location = new Point((_pnlLeft.Width - 120) / 2, 60)
            };
            
            // Application title
            var lblAppTitle = new Label
            {
                Text = "NETWORK MONITOR",
                Font = new Font("Segoe UI", 24, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(0, 200),
                Size = new Size(_pnlLeft.Width, 40),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            // Tagline
            var lblTagline = new Label
            {
                Text = "Enterprise Monitoring Solution",
                Font = new Font("Segoe UI", 11),
                ForeColor = Color.FromArgb(200, 230, 255),
                Location = new Point(0, 250),
                Size = new Size(_pnlLeft.Width, 25),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            // Divider
            var divider = new Panel
            {
                Size = new Size(200, 1),
                Location = new Point((_pnlLeft.Width - 200) / 2, 290),
                BackColor = Color.FromArgb(100, 255, 255, 255)
            };
            
            // Features list - properly centered
            var features = new[]
            {
                "✓ Real-time Device Monitoring",
                "✓ Performance Analytics",
                "✓ Alert Notifications",
                "✓ Network Diagnostics"
            };
            
            int featureStartY = 320;
            int featureSpacing = 30;
            
            for (int i = 0; i < features.Length; i++)
            {
                var lblFeature = new Label
                {
                    Text = features[i],
                    Font = new Font("Segoe UI", 10),
                    ForeColor = Color.White,
                    Location = new Point(0, featureStartY + (i * featureSpacing)),
                    Size = new Size(_pnlLeft.Width, 25),
                    TextAlign = ContentAlignment.MiddleCenter
                };
                _pnlLeft.Controls.Add(lblFeature);
            }
            
            // Version info
            var lblVersion = new Label
            {
                Text = "Version 2.1.0",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(180, 220, 255),
                Location = new Point(0, _pnlLeft.Height - 40),
                Size = new Size(_pnlLeft.Width, 20),
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            _pnlLeft.Controls.AddRange(new Control[]
            {
                _pbLogo, lblAppTitle, lblTagline, divider, lblVersion
            });
        }
        
        private void InitializeRightPanel()
        {
            // Add padding to right panel
            int padding = 60;
            int fieldWidth = _pnlRight.Width - (padding * 2);
            
            // Welcome title
            var lblTitle = new Label
            {
                Text = "Welcome Back",
                Font = new Font("Segoe UI", 26, FontStyle.Bold),
                ForeColor = _darkGray,
                Location = new Point(padding, 80),
                Size = new Size(fieldWidth, 50),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            // Subtitle
            var lblSubtitle = new Label
            {
                Text = "Sign in to access your network dashboard",
                Font = new Font("Segoe UI", 11),
                ForeColor = _mediumGray,
                Location = new Point(padding, 130),
                Size = new Size(fieldWidth, 25),
                TextAlign = ContentAlignment.MiddleLeft
            };
            
            // Username label
            var lblUsername = new Label
            {
                Text = "USERNAME",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(padding, 190),
                Size = new Size(fieldWidth, 20),
                ForeColor = _darkGray
            };
            
            // Username textbox
            _txtUsername = new TextBox
            {
                Location = new Point(padding, 215),
                Size = new Size(fieldWidth, 40),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _lightGray
            };
            _txtUsername.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    _txtPassword.Focus();
                    e.Handled = true;
                }
            };
            
            // Password label
            var lblPassword = new Label
            {
                Text = "PASSWORD",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                Location = new Point(padding, 270),
                Size = new Size(fieldWidth, 20),
                ForeColor = _darkGray
            };
            
            // Password textbox
            _txtPassword = new TextBox
            {
                Location = new Point(padding, 295),
                Size = new Size(fieldWidth, 40),
                Font = new Font("Segoe UI", 11),
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = _lightGray,
                UseSystemPasswordChar = true
            };
            _txtPassword.KeyPress += (s, e) =>
            {
                if (e.KeyChar == (char)Keys.Enter)
                {
                    AttemptLogin();
                    e.Handled = true;
                }
            };
            
            // Remember me checkbox
            var chkRemember = new CheckBox
            {
                Text = "Remember my credentials",
                Location = new Point(padding, 350),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                ForeColor = _darkGray
            };
            
            // Forgot password link
            var lnkForgot = new LinkLabel
            {
                Text = "Forgot password?",
                Location = new Point(padding + fieldWidth - 100, 350),
                AutoSize = true,
                Font = new Font("Segoe UI", 9),
                LinkColor = _secondaryColor
            };
            lnkForgot.LinkClicked += (s, e) =>
            {
                MessageBox.Show("Please contact your system administrator\nto reset your password.",
                    "Password Assistance", MessageBoxButtons.OK, MessageBoxIcon.Information);
            };
            
            // Status label
            _lblStatus = new Label
            {
                Text = "",
                Location = new Point(padding, 385),
                Size = new Size(fieldWidth, 25),
                Font = new Font("Segoe UI", 9),
                ForeColor = _errorColor,
                TextAlign = ContentAlignment.MiddleLeft,
                Visible = false
            };
            
            // Login button
            _btnLogin = new Button
            {
                Text = "SIGN IN",
                Location = new Point(padding, 420),
                Size = new Size(fieldWidth, 45),
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                BackColor = _secondaryColor,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnLogin.FlatAppearance.BorderSize = 0;
            _btnLogin.Click += (s, e) => AttemptLogin();
            
            // Exit button
            _btnExit = new Button
            {
                Text = "EXIT APPLICATION",
                Location = new Point(padding, 475),
                Size = new Size(fieldWidth, 35),
                Font = new Font("Segoe UI", 10),
                BackColor = Color.Transparent,
                ForeColor = _mediumGray,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            _btnExit.FlatAppearance.BorderSize = 1;
            _btnExit.FlatAppearance.BorderColor = _lightGray;
            _btnExit.Click += (s, e) =>
            {
                if (MessageBox.Show("Exit the application?", "Confirm Exit",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    this.DialogResult = DialogResult.Cancel;
                    this.Close();
                }
            };
            
            // Demo hint
            var lblDemo = new Label
            {
                Text = "ElectroGuard Monitoring",
                Location = new Point(padding, _pnlRight.Height - 40),
                Size = new Size(fieldWidth, 20),
                Font = new Font("Segoe UI", 8),
                ForeColor = _mediumGray,
                TextAlign = ContentAlignment.MiddleCenter
            };
            
            _pnlRight.Controls.AddRange(new Control[]
            {
                lblTitle, lblSubtitle,
                lblUsername, _txtUsername,
                lblPassword, _txtPassword,
                chkRemember, lnkForgot,
                _lblStatus, _btnLogin, _btnExit,
                lblDemo
            });
        }
        
        private Image LoadLogoImage()
        {
            try
            {
                string logoPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.png");
                if (System.IO.File.Exists(logoPath))
                    return Image.FromFile(logoPath);
            }
            catch { }
            
            // Fallback logo
            return CreateFallbackLogo();
        }
        
        private Image CreateFallbackLogo()
        {
            var bmp = new Bitmap(120, 120);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);
                
                // Background circle
                using (var brush = new SolidBrush(_primaryColor))
                {
                    g.FillEllipse(brush, 10, 10, 100, 100);
                }
                
                // Network icon
                using (var pen = new Pen(Color.White, 4))
                {
                    // Center circle
                    g.DrawEllipse(pen, 40, 40, 40, 40);
                    
                    // Connecting lines
                    g.DrawLine(pen, 60, 60, 30, 30);  // Top-left
                    g.DrawLine(pen, 60, 60, 90, 30);  // Top-right
                    g.DrawLine(pen, 60, 60, 30, 90);  // Bottom-left
                    g.DrawLine(pen, 60, 60, 90, 90);  // Bottom-right
                }
            }
            return bmp;
        }
        
        private void LoadSavedCredentials()
        {
            try
            {
                string configPath = GetConfigPath();
                if (System.IO.File.Exists(configPath))
                {
                    var lines = System.IO.File.ReadAllLines(configPath);
                    if (lines.Length >= 2)
                    {
                        _txtUsername.Text = lines[0];
                        _txtPassword.Text = DecryptPassword(lines[1]);
                    }
                }
            }
            catch { }
        }
        
        private void SaveCredentials()
        {
            try
            {
                string configPath = GetConfigPath();
                var directory = System.IO.Path.GetDirectoryName(configPath);
                
                if (!System.IO.Directory.Exists(directory))
                    System.IO.Directory.CreateDirectory(directory);
                
                System.IO.File.WriteAllLines(configPath, new[]
                {
                    _txtUsername.Text,
                    EncryptPassword(_txtPassword.Text)
                });
            }
            catch { }
        }
        
        private string GetConfigPath()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return System.IO.Path.Combine(appData, "NetworkMonitor", "login.config");
        }
        
        private string EncryptPassword(string password)
        {
            return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(password));
        }
        
        private string DecryptPassword(string encrypted)
        {
            try
            {
                return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(encrypted));
            }
            catch
            {
                return string.Empty;
            }
        }
        
        private void AttemptLogin()
        {
            string username = _txtUsername.Text.Trim();
            string password = _txtPassword.Text;
            
            // Clear any previous errors
            _lblStatus.Visible = false;
            
            // Validation
            if (string.IsNullOrEmpty(username))
            {
                ShowError("Please enter your username");
                _txtUsername.Focus();
                return;
            }
            
            if (string.IsNullOrEmpty(password))
            {
                ShowError("Please enter your password");
                _txtPassword.Focus();
                return;
            }
            
            // Show loading state
            _btnLogin.Enabled = false;
            _btnLogin.Text = "AUTHENTICATING...";
            _btnLogin.BackColor = _mediumGray;
            
            // Simulate network delay
            var timer = new Timer { Interval = 800 };
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                
                try
                {
                    // Check credentials
                    var user = _demoUsers.FirstOrDefault(u => 
                        u.Username.Equals(username, StringComparison.OrdinalIgnoreCase) && 
                        u.Password == password);
                    
                    if (user != null)
                    {
                        // Successful login
                        SaveCredentials();
                        
                        LoggedInUser = user;
                        LoggedInUser.LoginTime = DateTime.Now;
                        
                        // Show success
                        _btnLogin.Text = "✓ SUCCESS";
                        _btnLogin.BackColor = _successColor;
                        
                        // Close after delay
                        var closeTimer = new Timer { Interval = 500 };
                        closeTimer.Tick += (s2, e2) =>
                        {
                            closeTimer.Stop();
                            closeTimer.Dispose();
                            this.DialogResult = DialogResult.OK;
                            this.Close();
                        };
                        closeTimer.Start();
                    }
                    else
                    {
                        // Failed login
                        ShowError("Invalid username or password");
                        ResetLoginButton();
                        _txtPassword.SelectAll();
                        _txtPassword.Focus();
                    }
                }
                catch (Exception ex)
                {
                    ShowError($"Login error: {ex.Message}");
                    ResetLoginButton();
                }
            };
            timer.Start();
        }
        
        private void ResetLoginButton()
        {
            _btnLogin.Enabled = true;
            _btnLogin.Text = "SIGN IN";
            _btnLogin.BackColor = _secondaryColor;
        }
        
        private void ShowError(string message)
        {
            _lblStatus.Text = message;
            _lblStatus.Visible = true;
            
            // Auto-hide after 5 seconds
            var timer = new Timer { Interval = 5000 };
            timer.Tick += (s, e) =>
            {
                _lblStatus.Visible = false;
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }
    }
    
    // Extended User model class
    public class AppUser
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string DisplayName { get; set; }
        public string Role { get; set; }
        public string EmailSubject { get; set; }
        public DateTime LoginTime { get; set; }
    }
}