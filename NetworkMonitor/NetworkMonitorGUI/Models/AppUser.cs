using System;

namespace NetworkMonitorGUI.Models
{
    public class AppUser
    {
        public string Username { get; set; }
        public string EmailSubject { get; set; }
        public DateTime LoginTime { get; set; }
    }
}