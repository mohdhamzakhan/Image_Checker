using System;
using System.IO.Ports;
using System.Management;

namespace Image_Checker.Services
{
    public class UsbLightController : IDisposable
    {
        private SerialPort _serialPort;
        private bool _isConnected;
        private string _portName;

        public bool IsConnected => _isConnected;
        public string PortName => _portName;

        /// <summary>
        /// Attempts to connect to USB light device (Arduino, relay module, etc.)
        /// </summary>
        public bool Connect(string portName = null)
        {
            try
            {
                // Auto-detect if no port specified
                if (string.IsNullOrEmpty(portName))
                {
                    portName = AutoDetectPort();
                    if (string.IsNullOrEmpty(portName))
                    {
                        Console.WriteLine("❌ No USB serial device found");
                        return false;
                    }
                }

                _serialPort = new SerialPort(portName)
                {
                    BaudRate = 9600,
                    DataBits = 8,
                    Parity = Parity.None,
                    StopBits = StopBits.One,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };

                _serialPort.Open();
                _isConnected = true;
                _portName = portName;

                Console.WriteLine($"✅ USB Light connected on {portName}");

                // Turn off initially
                TurnOff();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Failed to connect USB light: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Auto-detect available COM ports
        /// </summary>
        private string AutoDetectPort()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'"))
                {
                    foreach (ManagementObject queryObj in searcher.Get())
                    {
                        string caption = queryObj["Caption"]?.ToString();
                        string deviceId = queryObj["DeviceID"]?.ToString();

                        if (string.IsNullOrEmpty(caption))
                            continue;

                        Console.WriteLine($"🔍 Found: {caption}");

                        // Extract COM port number
                        var match = System.Text.RegularExpressions.Regex.Match(caption, @"COM(\d+)");
                        if (!match.Success)
                            continue;

                        string portName = $"COM{match.Groups[1].Value}";

                        // Filter by known USB-to-Serial chip manufacturers
                        if (caption.Contains("Arduino", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("CH340", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("CH341", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("CP210", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("FTDI", StringComparison.OrdinalIgnoreCase) ||
                            caption.Contains("USB Serial", StringComparison.OrdinalIgnoreCase) ||
                            deviceId.Contains("VID_2341")) // Arduino Vendor ID
                        {
                            Console.WriteLine($"✅ Detected USB device on {portName}");
                            return portName;
                        }
                    }
                }

                Console.WriteLine("⚠️ No USB light device found, checking available COM ports...");

                // Fallback to manual port listing
                var ports = SerialPort.GetPortNames();
                if (ports.Length > 0)
                {
                    Console.WriteLine("Available ports:");
                    foreach (var port in ports)
                        Console.WriteLine($"  - {port}");

                    Console.WriteLine("Please specify the correct port manually.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Port detection failed: {ex.Message}");
            }

            return null;
        }

        /// <summary>
        /// Turn the USB light ON
        /// </summary>
        public void TurnOn()
        {
            if (!_isConnected || _serialPort == null || !_serialPort.IsOpen)
                return;

            try
            {
                _serialPort.Write("ON\n");
                Console.WriteLine("💡 USB Light: ON");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to turn light ON: {ex.Message}");
            }
        }

        /// <summary>
        /// Turn the USB light OFF
        /// </summary>
        public void TurnOff()
        {
            if (!_isConnected || _serialPort == null || !_serialPort.IsOpen)
                return;

            try
            {
                _serialPort.Write("OFF\n");
                Console.WriteLine("💡 USB Light: OFF");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Failed to turn light OFF: {ex.Message}");
            }
        }

        /// <summary>
        /// Blink the light (useful for testing)
        /// </summary>
        public void Blink(int times = 3, int delayMs = 500)
        {
            for (int i = 0; i < times; i++)
            {
                TurnOn();
                System.Threading.Thread.Sleep(delayMs);
                TurnOff();
                System.Threading.Thread.Sleep(delayMs);
            }
        }

        public void Dispose()
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                TurnOff();
                _serialPort.Close();
                _serialPort.Dispose();
            }
            _isConnected = false;
        }
    }
}