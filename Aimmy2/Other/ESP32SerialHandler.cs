using System.IO.Ports;

namespace InputLogic
{
    /// <summary>
    /// Thin wrapper around a serial connection to an ESP32 running either the
    /// mouse (ESP32_SerialToHID.ino) or gamepad (ESP32_SerialToHID_Gamepad.ino)
    /// sketch. The command protocol itself (M,/MD,/MU,/S,/RS,/RT,/B,/STATUS) is
    /// defined by whichever sketch is flashed - this class only knows how to open
    /// the port and push raw command strings down it.
    ///
    /// Reconstructed to match the exact public API that MouseManager.cs already
    /// depends on (Connect, Disconnect, IsConnected, SendCommand, AutoDetect,
    /// GetAvailablePorts) after the original file was lost.
    /// </summary>
    internal static class ESP32SerialHandler
    {
        private static SerialPort? _serialPort;
        private static readonly object _lock = new();

        public static bool IsConnected => _serialPort is { IsOpen: true };

        /// <summary>
        /// Opens a connection to the given COM port at the given baud rate.
        /// Returns true on success, false if the port couldn't be opened.
        /// </summary>
        public static bool Connect(string comPort, int baudRate)
        {
            lock (_lock)
            {
                try
                {
                    Disconnect();

                    _serialPort = new SerialPort(comPort, baudRate)
                    {
                        ReadTimeout = 500,
                        WriteTimeout = 500,
                        NewLine = "\n"
                    };

                    _serialPort.Open();
                    return _serialPort.IsOpen;
                }
                catch
                {
                    _serialPort?.Dispose();
                    _serialPort = null;
                    return false;
                }
            }
        }

        public static void Disconnect()
        {
            lock (_lock)
            {
                try
                {
                    if (_serialPort is { IsOpen: true })
                    {
                        _serialPort.Close();
                    }
                    _serialPort?.Dispose();
                }
                catch
                {
                    // ignore - we're tearing down anyway
                }
                finally
                {
                    _serialPort = null;
                }
            }
        }

        /// <summary>
        /// Sends a raw command string (e.g. "M,5,-3", "RT,127") followed by a
        /// newline, matching the line-based protocol both .ino sketches parse.
        /// Silently no-ops if not connected - callers already check IsConnected
        /// or have a SendInput fallback, consistent with existing call sites.
        /// </summary>
        public static void SendCommand(string command)
        {
            lock (_lock)
            {
                if (_serialPort is not { IsOpen: true }) return;

                try
                {
                    _serialPort.WriteLine(command);
                }
                catch
                {
                    // A write failure usually means the device was unplugged -
                    // drop the connection so IsConnected reflects reality and
                    // callers fall back to their SendInput path.
                    Disconnect();
                }
            }
        }

        /// <summary>
        /// Tries each available COM port in turn, sends "STATUS", and considers
        /// the first port that opens successfully a match. Simple presence-based
        /// detection - does not strictly validate the "STATUS,OK" reply, since
        /// read timing varies across boards/drivers.
        /// </summary>
        public static bool AutoDetect()
        {
            foreach (string port in GetAvailablePorts())
            {
                if (Connect(port, 115200))
                {
                    try
                    {
                        SendCommand("STATUS");
                        return true;
                    }
                    catch
                    {
                        Disconnect();
                    }
                }
            }

            return false;
        }

        public static string[] GetAvailablePorts()
        {
            try
            {
                return SerialPort.GetPortNames();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
