using System;
using System.Windows.Forms;
using NetworkMonitorGUI.Forms;

namespace NetworkMonitorGUI
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                // Show login form
                using (var loginForm = new LoginForm())
                {
                    if (loginForm.ShowDialog() == DialogResult.OK)
                    {
                        // Pass the logged in user to main form
                        Application.Run(new MainForm(loginForm.LoggedInUser));
                    }
                    else
                    {
                        MessageBox.Show("Login was cancelled or failed.", "Login", 
                                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application startup error: {ex.Message}\n\n{ex.StackTrace}", 
                            "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}