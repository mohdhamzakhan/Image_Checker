using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Image_Checker.Services
{
    public class UsbPortPowerController : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private const uint GENERIC_WRITE = 0x40000000;
        private const uint GENERIC_READ = 0x80000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_SHARE_READ = 0x00000001;
        private const uint FILE_SHARE_WRITE = 0x00000002;

        // USB IO Control Codes
        private const uint IOCTL_USB_HUB_CYCLE_PORT = 0x00220044;
        private const uint IOCTL_USB_GET_NODE_INFORMATION = 0x00220408;

        private SafeFileHandle _hubHandle;
        private string _devicePath;
        private bool _isConnected;

        public bool IsConnected => _isConnected;
        public string DevicePath => _devicePath;

        /// <summary>
        /// Lists all available USB hubs on the system
        /// </summary>
        public static List<string> GetAvailableUsbHubs()
        {
            var hubs = new List<string>();

            Console.WriteLine("🔍 Scanning for USB hubs...\n");

            // Try common HCD (Host Controller Device) paths
            for (int i = 0; i < 20; i++)
            {
                string path = $"\\\\.\\HCD{i}";

                var handle = CreateFile(
                    path,
                    GENERIC_WRITE | GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (!handle.IsInvalid)
                {
                    hubs.Add(path);
                    handle.Dispose();
                    Console.WriteLine($"✅ Found USB hub: {path}");
                }
            }

            // Try alternative USB root hub paths
            for (int i = 0; i < 10; i++)
            {
                string path = $"\\\\.\\USB{i}";

                var handle = CreateFile(
                    path,
                    GENERIC_WRITE | GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (!handle.IsInvalid)
                {
                    hubs.Add(path);
                    handle.Dispose();
                    Console.WriteLine($"✅ Found USB hub: {path}");
                }
            }

            Console.WriteLine($"\n📊 Total hubs found: {hubs.Count}\n");
            return hubs;
        }

        /// <summary>
        /// Auto-detect and connect to first available USB hub
        /// </summary>
        public bool AutoConnect()
        {
            var hubs = GetAvailableUsbHubs();

            if (hubs.Count == 0)
            {
                Console.WriteLine("❌ No USB hubs found on this system");
                Console.WriteLine("   This feature may not be supported on your hardware.");
                return false;
            }

            // Try connecting to each hub
            foreach (var hub in hubs)
            {
                if (Connect(hub))
                {
                    Console.WriteLine($"✅ Auto-connected to: {hub}");
                    return true;
                }
            }

            Console.WriteLine("❌ Could not connect to any USB hub");
            return false;
        }

        /// <summary>
        /// Connect to a specific USB hub
        /// </summary>
        public bool Connect(string usbHubPath)
        {
            try
            {
                // Close existing connection if any
                if (_hubHandle != null && !_hubHandle.IsInvalid)
                {
                    _hubHandle.Dispose();
                }

                _devicePath = usbHubPath;
                _hubHandle = CreateFile(
                    usbHubPath,
                    GENERIC_WRITE | GENERIC_READ,
                    FILE_SHARE_READ | FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    OPEN_EXISTING,
                    0,
                    IntPtr.Zero);

                if (_hubHandle.IsInvalid)
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ Failed to open USB hub: {usbHubPath}");
                    Console.WriteLine($"   Error code: {error}");
                    Console.WriteLine($"   Make sure you're running as Administrator!");
                    _isConnected = false;
                    return false;
                }

                _isConnected = true;
                Console.WriteLine($"✅ Connected to USB hub: {usbHubPath}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error connecting to USB hub: {ex.Message}");
                _isConnected = false;
                return false;
            }
        }

        /// <summary>
        /// Test if the connection is valid
        /// </summary>
        public bool TestConnection()
        {
            if (_hubHandle == null || _hubHandle.IsInvalid)
            {
                Console.WriteLine("❌ Not connected to any USB hub");
                return false;
            }

            Console.WriteLine($"✅ Connection Status:");
            Console.WriteLine($"   Device Path: {_devicePath}");
            Console.WriteLine($"   Handle Valid: {!_hubHandle.IsInvalid}");
            Console.WriteLine($"   Handle Closed: {_hubHandle.IsClosed}");
            Console.WriteLine($"   Is Connected: {_isConnected}");

            return true;
        }

        /// <summary>
        /// Cycle (power off and on) a specific USB port
        /// WARNING: This will disconnect any device on that port!
        /// </summary>
        public bool CyclePort(int portNumber)
        {
            if (_hubHandle == null || _hubHandle.IsInvalid)
            {
                Console.WriteLine("❌ Not connected to USB hub");
                return false;
            }

            try
            {
                Console.WriteLine($"⚡ Cycling USB port {portNumber}...");

                IntPtr portPtr = Marshal.AllocHGlobal(4);
                Marshal.WriteInt32(portPtr, portNumber);

                bool success = DeviceIoControl(
                    _hubHandle,
                    IOCTL_USB_HUB_CYCLE_PORT,
                    portPtr,
                    4,
                    IntPtr.Zero,
                    0,
                    out uint bytesReturned,
                    IntPtr.Zero);

                Marshal.FreeHGlobal(portPtr);

                if (success)
                {
                    Console.WriteLine($"✅ Successfully cycled USB port {portNumber}");
                    Console.WriteLine($"   The device should reconnect automatically in a few seconds.");
                }
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    Console.WriteLine($"❌ Failed to cycle port {portNumber}");
                    Console.WriteLine($"   Error code: {error}");
                    Console.WriteLine($"   Possible reasons:");
                    Console.WriteLine($"   • Port number doesn't exist");
                    Console.WriteLine($"   • Hub doesn't support per-port control");
                    Console.WriteLine($"   • Insufficient permissions (need Administrator)");
                }

                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error cycling port: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Power off a USB port (if supported by hub)
        /// </summary>
        public bool PowerOffPort(int portNumber)
        {
            Console.WriteLine($"⚠️ Note: Most USB hubs don't support selective port power off.");
            Console.WriteLine($"   Using CyclePort instead (power off + on).");
            return CyclePort(portNumber);
        }

        /// <summary>
        /// Power on a USB port (if supported by hub)
        /// </summary>
        public bool PowerOnPort(int portNumber)
        {
            Console.WriteLine($"⚠️ Note: Most USB hubs don't support selective port power on.");
            Console.WriteLine($"   Port should power on automatically after cycling.");
            return true;
        }

        /// <summary>
        /// Check if USB port power control is supported on this system
        /// </summary>
        public static bool IsSupported()
        {
            var hubs = GetAvailableUsbHubs();
            bool supported = hubs.Count > 0;

            if (!supported)
            {
                Console.WriteLine("\n⚠️ USB Port Power Control is NOT supported on this system.");
                Console.WriteLine("   Reasons this might fail:");
                Console.WriteLine("   • Running on a laptop (integrated hubs don't support it)");
                Console.WriteLine("   • USB hubs don't support per-port power management");
                Console.WriteLine("   • Need Administrator privileges");
                Console.WriteLine("   • Motherboard/chipset limitations");
                Console.WriteLine("\n💡 Recommended alternative: Use Arduino + Relay for reliable control");
            }
            else
            {
                Console.WriteLine($"\n✅ USB Port Power Control may be supported ({hubs.Count} hub(s) found)");
                Console.WriteLine("   Note: Not all hubs support per-port power control.");
                Console.WriteLine("   Testing required to confirm functionality.");
            }

            return supported;
        }

        /// <summary>
        /// Run a complete diagnostic test
        /// </summary>
        public static void RunDiagnostics()
        {
            Console.WriteLine("╔════════════════════════════════════════════════╗");
            Console.WriteLine("║   USB PORT POWER CONTROL DIAGNOSTICS           ║");
            Console.WriteLine("╚════════════════════════════════════════════════╝\n");

            // Step 1: Check for hubs
            Console.WriteLine("STEP 1: Detecting USB Hubs");
            Console.WriteLine("─────────────────────────────");
            var hubs = GetAvailableUsbHubs();

            if (hubs.Count == 0)
            {
                Console.WriteLine("\n❌ DIAGNOSTIC FAILED: No USB hubs detected");
                Console.WriteLine("\nConclusion: USB port power control is NOT available on this system.\n");
                return;
            }

            // Step 2: Test connection
            Console.WriteLine("\nSTEP 2: Testing Connection");
            Console.WriteLine("─────────────────────────────");

            using (var controller = new UsbPortPowerController())
            {
                if (!controller.Connect(hubs[0]))
                {
                    Console.WriteLine("\n❌ DIAGNOSTIC FAILED: Could not connect to USB hub");
                    Console.WriteLine("\nMake sure you're running as Administrator!\n");
                    return;
                }

                controller.TestConnection();

                Console.WriteLine("\n✅ DIAGNOSTIC PASSED: Connection successful!");
                Console.WriteLine("\n⚠️ WARNING: Actual port cycling may still fail if:");
                Console.WriteLine("   • The hub doesn't support per-port power management");
                Console.WriteLine("   • Port numbers are incorrect");
                Console.WriteLine("   • System security policies prevent it");

                Console.WriteLine("\n💡 To test actual port control:");
                Console.WriteLine("   1. Plug a USB device (like a USB light) into a specific port");
                Console.WriteLine("   2. Note the port number");
                Console.WriteLine("   3. Call CyclePort(portNumber)");
                Console.WriteLine("   4. Observe if the device disconnects and reconnects\n");
            }
        }

        public void Dispose()
        {
            if (_hubHandle != null && !_hubHandle.IsInvalid)
            {
                _hubHandle.Dispose();
            }
            _isConnected = false;
        }
    }
}