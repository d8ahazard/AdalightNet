using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;

namespace AdalightNet {
    public sealed class Adalight : IDisposable
    {
        private readonly List<Color> _ledMatrix;

        private readonly SerialPort _comPort;

        private readonly byte[] _serialData;
        private const string MagicWord = "Ada";
        private bool _connected;
    
        /// <summary>
        /// The currently assigned port number
        /// </summary>
        public int PortNumber { get; }
        
        /// <summary>
        /// The currently assigned led count
        /// </summary>
        public int LedCount { get; }

        /// <summary>
        /// Initialize a new Adalight Device
        /// </summary>
        /// <param name="port">Port number to connect to</param>
        /// <param name="ledCount">Number of LEDs to control</param>
        /// <param name="speed">Optional baud rate, default is 115200</param>
        public Adalight(int port, int ledCount, int speed = 115200) {
            // Set Properties
            LedCount = ledCount;
            PortNumber = port;

            try {
                // Create connection object
                _comPort = new SerialPort {
                    PortName = "COM" + PortNumber,
                    BaudRate = speed,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One
                };
            } catch (Exception) {
                // Ignored
            }

            // Create Matrix Array
            _ledMatrix = new List<Color>();
            for (var i = 0; i < ledCount; i++) {
                _ledMatrix.Add(Color.FromArgb(0, 0, 0));
            }

            // Redefine ByteArray length on runtime of current LED count
            _serialData = new byte[6 + ledCount * 3 + 1];
        }
        

        /// <summary>
        /// Connect to our device
        /// </summary>
        /// <returns>"OK" on</returns>
        public bool Connect() {
            try {
                _comPort.Open();
                _connected = true;
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("Exception connecting to port: " + ex.Message);
                return false;
            }
        }
        

        /// <summary>
        /// Disconnect from device
        /// </summary>
        /// <param name="reset">If true, will turn off LEDs before disconnecting.</param>
        /// <returns></returns>
        public bool Disconnect(bool reset = true) {
            if (!_connected) return false;
            try {
                if (reset) {
                    for (var i = 0; i < LedCount; i++) {
                        UpdatePixel(i, Color.Black, false);
                    }

                    Update();
                }

                _comPort.Close();
                _connected = false;
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("Exception closing port: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Update the colors of the LED strip
        /// </summary>
        /// <param name="colors">A list of colors to send. If less than LED count, black will be sent.</param>
        /// <param name="update">Whether to send the colors immediately, or wait.</param>
        public void UpdateColors(List<Color> colors, bool update=true) {
            for (var i = 0; i < LedCount; i++) {
                var color = Color.FromArgb(0, 0, 0);
                if (i < colors.Count) {
                    color = colors[i];
                }

                _ledMatrix[i] = color;
            }
            if (update) Update();
        }

        /// <summary>
        /// Update an individual pixel in our LED strip
        /// </summary>
        /// <param name="color">The color to set</param>
        /// <param name="index">The index of the LED to set</param>
        /// <param name="update">Whether to update immediately</param>
        public void UpdatePixel(int index, Color color, bool update = true) {
            if (index < LedCount) {
                _ledMatrix[index] = color;
            }
            if (update) Update();
        }

        private void WriteHeader() {
            _serialData[0] = Convert.ToByte(MagicWord[0]); // MagicWord
            _serialData[1] = Convert.ToByte(MagicWord[1]);
            _serialData[2] = Convert.ToByte(MagicWord[2]);
            _serialData[3] = Convert.ToByte((LedCount - 1) >> 8); // LED count high byte
            _serialData[4] = Convert.ToByte((LedCount - 1) & 0xFF); // LED count low byte
            _serialData[5] = Convert.ToByte(_serialData[3] ^ _serialData[4] ^ 0x55); // Checksum
        }

        private void WriteMatrixToSerialData() {
            var serialOffset = 6;
            for (var i = 0; i <= _ledMatrix.Count - 1; i++) {
                _serialData[serialOffset] = _ledMatrix[i].R; // red
                serialOffset += 1;
                _serialData[serialOffset] = _ledMatrix[i].G; // green
                serialOffset += 1;
                _serialData[serialOffset] = _ledMatrix[i].B; // blue
                serialOffset += 1;
            }
        }

        /// <summary>
        ///     Discover Devices
        ///     Returns a list of devices responded with the correct Adalight magic word
        ///     </summary>
        ///     <returns> A list of ports responding to ada commands.</returns>
        public static List<int> FindDevices() {
            var output = new List<int>();

            foreach (var dev in SerialPort.GetPortNames()) {
                try {
                    var i = new SerialPort {
                        PortName = dev,
                        BaudRate = 115200,
                        Parity = Parity.None,
                        DataBits = 8,
                        StopBits = StopBits.One,
                        ReadTimeout = 1500
                    };
                    i.Open();
                    if (i.ReadLine() == "Ada") {
                        var port = int.Parse(dev.Replace("COM", ""));
                        output.Add(port);
                    }
                    
                    i.Close();
                }
                catch (Exception) {
                    // Ignore
                }
            }
            return output;
        }

        /// <summary>
        /// Send data to lights
        /// </summary>
        /// <returns>True if no errors occurred and connected, false if not</returns>
        public bool Update() {
            if (!_connected) return false;
            try {
                WriteHeader();
                WriteMatrixToSerialData();
                _comPort.Write(_serialData, 0, _serialData.Length);
                return true;
            } catch (Exception) {
                return false;
            }
        }

        private bool _disposedValue; // To detect redundant calls

        // IDisposable
        private void Dispose(bool disposing)
        {
            if (!_disposedValue) {
                if (disposing) {
                    try {
                        if (_connected) _comPort?.Close();
                    } catch (Exception) {
                        // Ignored
                    }
                    _comPort?.Dispose();
                    _ledMatrix.Clear();
                }
            }
            _disposedValue = true;
        }

    
        public void Dispose() {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
