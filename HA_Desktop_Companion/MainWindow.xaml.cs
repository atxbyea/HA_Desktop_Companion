﻿using System.Net.Http;
using System.Windows;
using System.Collections.Generic;
using System.Net.Http.Json;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using System.Windows.Threading;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security;
using System.Text;


namespace HA_Desktop_Companion
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        static byte[] entropy = Encoding.Unicode.GetBytes("SaLtY bOy 6970 ePiC");
        public static HAApi ApiConnectiom;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            string decodedApiToken = ToInsecureString(DecryptString(Properties.Settings.Default.apiToken));
            string webhook_id = Properties.Settings.Default.apiWebhookId;
            string base_url = Properties.Settings.Default.apiBaseUrl;

            apiToken.Password = decodedApiToken;
            apiBaseUrl.Text = base_url;

            if (webhook_id != "")
            {
                ApiConnectiom = new HAApi(base_url, decodedApiToken, webhook_id);
                StartWatchdog();
                //registration.IsEnabled = false;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
            e.Cancel = true;
        }

        private void registration_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.apiBaseUrl = apiBaseUrl.Text;
            Properties.Settings.Default.apiToken = EncryptString(ToSecureString(apiToken.Password));
            Properties.Settings.Default.Save();

            string hostname = System.Net.Dns.GetHostName() + "_debug";
            string maufactorer = queryWMIC("Win32_ComputerSystem", "Manufacturer", @"\\root\CIMV2");
            string model = queryWMIC("Win32_ComputerSystem", "Model", @"\\root\CIMV2");
            string os = queryWMIC("Win32_OperatingSystem", "Caption", @"\\root\CIMV2");

            Title = hostname;

            ApiConnectiom = new HAApi(apiBaseUrl.Text, apiToken.Password, hostname.ToLower(), hostname, model, maufactorer, os, Environment.OSVersion.ToString());

            ApiConnectiom.HASenzorRegistration("battery_level", "Battery Level","Unknown");
            ApiConnectiom.HASenzorRegistration("battery_state", "Battery State", "Unknown");
            ApiConnectiom.HASenzorRegistration("is_charging", "Is Charging", "Unknown");
            ApiConnectiom.HASenzorRegistration("wifi_ssid", "Wifi SSID", "Unknown");

            StartWatchdog();
            //registration.IsEnabled = false;
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            ApiConnectiom.HASendSenzorData("battery_level", GetBatteryPercent().ToString());
            ApiConnectiom.HASendSenzorData("battery_state", GetBatteryStatus().ToString());
            ApiConnectiom.HASendSenzorData("is_charging", GetPowerLineStatus().ToString());
            ApiConnectiom.HASendSenzorData("wifi_ssid", getWifiSSID().ToString());
        }

        public static double GetBatteryPercent()
        {
            return Int32.Parse(queryWMIC("Win32_Battery", "EstimatedChargeRemaining", @"\\root\CIMV2"));
        }

        public static string GetBatteryStatus()
        {
            Dictionary<int, string> StatusCodes = new Dictionary<int, string>();
            StatusCodes.Add(1, "Discharging");
            StatusCodes.Add(2, "On AC");
            StatusCodes.Add(3, "Fully Charged");
            StatusCodes.Add(4, "Low");
            StatusCodes.Add(5, "Critical");
            StatusCodes.Add(6, "Charging");
            StatusCodes.Add(7, "Charging and High");
            StatusCodes.Add(8, "Charging and Low");
            StatusCodes.Add(9, "Undefined");
            StatusCodes.Add(10, "Partially Charged");

            int state = Int32.Parse(queryWMIC("Win32_Battery", "BatteryStatus", @"\\root\CIMV2"));
            if (state <= StatusCodes.Count)
            {
                return StatusCodes[state];
            }
            return "Unknown";
        }

        public static string GetPowerLineStatus()
        {
            if (Boolean.Parse(queryWMIC("BatteryStatus", "PowerOnline", @"\\root\wmi")))
            {
                return "plugged in";
            }

            return "plugged in";
        }

        private string getWifiSSID()
        {
            var process = new Process
            {
                StartInfo = {
                FileName = "netsh.exe",
                Arguments = "wlan show interfaces",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
                }
            };
            process.Start();

            foreach (var item in process.StandardOutput.ReadToEnd().ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None))
            {
                if (item.Contains("SSID") && !item.Contains("BSSID"))
                {
                    return item;
                }
            }
            return "";
        }

        public static string EncryptString(SecureString input)
        {
            byte[] encryptedData = ProtectedData.Protect(Encoding.Unicode.GetBytes(ToInsecureString(input)), entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedData);
        }

        public static SecureString DecryptString(string encryptedData)
        {
            try
            {
                byte[] decryptedData = ProtectedData.Unprotect(Convert.FromBase64String(encryptedData), entropy, DataProtectionScope.CurrentUser);
                return ToSecureString(Encoding.Unicode.GetString(decryptedData));
            }
            catch
            {
                return new SecureString();
            }
        }

        public static SecureString ToSecureString(string input)
        {
            SecureString secure = new SecureString();
            foreach (char c in input)
            {
                secure.AppendChar(c);
            }
            secure.MakeReadOnly();
            return secure;
        }

        public static string ToInsecureString(SecureString input)
        {
            string returnValue = string.Empty;
            IntPtr ptr = System.Runtime.InteropServices.Marshal.SecureStringToBSTR(input);
            try
            {
                returnValue = System.Runtime.InteropServices.Marshal.PtrToStringBSTR(ptr);
            }
            finally
            {
                System.Runtime.InteropServices.Marshal.ZeroFreeBSTR(ptr);
            }
            return returnValue;
        }

        public void StartWatchdog()
        {
            DispatcherTimer watchdogTimer = new DispatcherTimer();
            watchdogTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            watchdogTimer.Interval = new TimeSpan(0, 0, 10);
            watchdogTimer.Start();

            registration.Content = "Registered";
        }

        public static string queryWMIC(string path, string selector, string wmiNmaespace = @"\\root\wmi")
        {
            var process = new Process
            {
                StartInfo = {
                    FileName = "wmic.exe",
                    Arguments = ("/namespace:\"" + wmiNmaespace + "\" path " + path + " get " + selector),
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };

            process.Start();
            string[] output = process.StandardOutput.ReadToEnd().ToString().Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            process.Dispose();

            for (int line = 0; line < output.Length; line++)
            {
                if (output[line].Contains(selector))
                {
                    return output[line + 1];
                }

            }

            return "";
        }

        /*string[] drives = Environment.GetLogicalDrives();
        Console.WriteLine("GetLogicalDrives: {0}", String.Join(", ", drives));*/
    }
}
