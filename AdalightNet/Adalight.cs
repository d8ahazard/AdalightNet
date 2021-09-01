using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;

namespace AdalightNet {
    public sealed class Adalight : IDisposable
    {
        private Color[] _ledMatrix;

        private readonly SerialPort _comPort;

        private readonly byte[] _serialData;
        private const string MagicWord = "Ada";
        private const string CmdWord = "Adb";
        
        private const string StWord = "ST";
        private const string BrWord = "BR";

        private string _line;
        
        /// <summary>
        /// Is our device connected?
        /// </summary>
        public bool Connected { get; private set; }
    
        /// <summary>
        /// The currently assigned port number
        /// </summary>
        public string Port { get; }
        
        /// <summary>
        /// The currently assigned led count
        /// </summary>
        public int LedCount { get; }
        public int Brightness { get; private set; }

        private bool _sending;
        private readonly AutoResetEvent _dataReceived;

        /// <summary>
        /// Initialize a new Adalight Device
        /// </summary>
        /// <param name="port">Port number to connect to</param>
        /// <param name="ledCount">Number of LEDs to control</param>
        /// <param name="speed">Optional baud rate, default is 115200</param>
        /// <param name="brightness">Because sometimes, we can set the brightness.</param>
        public Adalight(string port, int ledCount, int speed = 115200, int brightness = 255) {
            // Set Properties
            LedCount = ledCount;
            Brightness = brightness;
            Port = port;
            // Create Matrix Array
            _ledMatrix = new Color[ledCount];
            for (var i = 0; i < ledCount; i++) {
                _ledMatrix[i] = Color.FromArgb(0, 0, 0);
            }

            // Redefine ByteArray length on runtime of current LED count
            _serialData = new byte[6 + ledCount * 3 + 1];
            
            _dataReceived = new AutoResetEvent(false);
            try {
                // Create connection object
                _comPort = new SerialPort {
                    PortName = port,
                    BaudRate = speed,
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One
                };
                _comPort.DataReceived += ParseMessage;

            } catch (Exception) {
                // Ignored
            }

           
        }
        

        /// <summary>
        /// Connect to our device
        /// </summary>
        /// <returns>"OK" on</returns>
        public bool Connect() {
            try {
                _comPort.Open();
                Connected = true;
                //UpdateBrightnessAsync(Brightness).ConfigureAwait(false);
                return true;
            } catch (Exception ex) {
                Debug.WriteLine("Exception connecting to port: " + ex.Message);
                return false;
            }
        }

        private void ParseMessage(object sender, SerialDataReceivedEventArgs e) {
            _line = ReadLineAsync().Result;
            if (_line.Contains("Ada") || _line.Contains("Adb")) {
                _line = _line.Replace("Adb", "");
                _line = _line.Replace("Ada", "");
                _dataReceived.Set();
            } else {
                _line = "";
            }
        }

        private async Task<string> ReadLineAsync() {
            _dataReceived.Reset();
            var buffer = new byte[1];
            var ret = string.Empty;

            // Read the input one byte at a time, convert the
            // byte into a char, add that char to the overall
            // response string, once the response string ends
            // with the line ending then stop reading
            while(true)
            {
                await _comPort.BaseStream.ReadAsync(buffer, 0, 1);
                ret += _comPort.Encoding.GetString(buffer);

                if (ret.EndsWith(_comPort.NewLine))
                    return ret.Substring(0, ret.Length - _comPort.NewLine.Length);
            }
        }

       
        /// <summary>
        /// Disconnect from device
        /// </summary>
        /// <param name="reset">If true, will turn off LEDs before disconnecting.</param>
        /// <returns></returns>
        public bool Disconnect(bool reset = true) {
            if (!Connected) return false;
            try {
                if (reset) {
                    for (var i = 0; i < LedCount; i++) {
                        UpdatePixel(i, Color.Black, false);
                    }
                    Update().ConfigureAwait(false);
                }

                _comPort.Close();
                Connected = false;
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
        public void UpdateColors(Color[] colors, bool update=true) {
            if (colors.Length == _ledMatrix.Length) {
                _ledMatrix = colors;
            }

            if (update) Update().ConfigureAwait(true);
        }
        
        /// <summary>
        /// Update the colors of the LED strip
        /// </summary>
        /// <param name="colors">A list of colors to send. If less than LED count, black will be sent.</param>
        /// <param name="update">Whether to send the colors immediately, or wait.</param>
        public async Task UpdateColorsAsync(Color[] colors, bool update=true) {
            for (var i = 0; i < LedCount; i++) {
                var color = Color.FromArgb(0, 0, 0);
                if (i < colors.Length) {
                    color = colors[i];
                }
                _ledMatrix[i] = color;
            }

            if (update) await Update();
        }

        /// <summary>
        /// Update strip brightness using the power of LOVE...er...programming.
        /// </summary>
        /// <param name="brightness"></param>
        public void UpdateBrightness(int brightness) {
            while (_sending) {
                Task.Delay(1);
            }

            if (brightness < 0 || brightness > 255) {
                return;
            }

            _sending = true;
            var output = new byte[6];
            output[0] = Convert.ToByte(CmdWord[0]); // MagicWord
            output[1] = Convert.ToByte(CmdWord[1]);
            output[2] = Convert.ToByte(CmdWord[2]);
            output[3] = Convert.ToByte(BrWord[0]);
            output[4] = Convert.ToByte(BrWord[1]);
            output[5] = (byte) brightness;
            _comPort.Write(output, 0, output.Length);
            _sending = false;
            var state = GetState();
            if (state[1] == brightness) Brightness = brightness;
        }
        
        /// <summary>
        /// Update strip brightness using the power of LOVE...er...programming.
        /// </summary>
        /// <param name="brightness"></param>
        public async Task UpdateBrightnessAsync(int brightness) {
            while (_sending) {
                await Task.Delay(1);
            }
            
            if (brightness >= 0 && brightness <= 255) {
                _sending = true;
                var output = new byte[6];
                output[0] = Convert.ToByte(CmdWord[0]); // MagicWord
                output[1] = Convert.ToByte(CmdWord[1]);
                output[2] = Convert.ToByte(CmdWord[2]);
                output[3] = Convert.ToByte(BrWord[0]);
                output[4] = Convert.ToByte(BrWord[1]);
                output[5] = (byte) brightness;
                await _comPort.BaseStream.WriteAsync(output, 0, output.Length);
                await _comPort.BaseStream.FlushAsync();
                _sending = false;
                var state = await GetStateAsync();
                if (state[0] == brightness) {
                    Brightness = brightness;
                }
            }
        }

        /// <summary>
        /// Get device brightness and led count
        /// </summary>
        /// <returns></returns>
        public int[] GetState() {
            while (_sending) {
                Task.Delay(1);
            }
            var ledCount = 0;
            var brightness = 0;

            try {
                _sending = true;
                var output = new byte[6];
                output[0] = Convert.ToByte(CmdWord[0]); // MagicWord
                output[1] = Convert.ToByte(CmdWord[1]);
                output[2] = Convert.ToByte(CmdWord[2]);
                output[3] = Convert.ToByte(StWord[0]);
                output[4] = Convert.ToByte(StWord[1]);
                output[5] = 0;
                _comPort.Write(output, 0, output.Length);
                _sending = false;
                _dataReceived.WaitOne(1000);
                if (!string.IsNullOrEmpty(_line)) {
                    if (_line.Contains("N=")) {
                        var splits = _line.Split(";");
                        foreach (var split in splits) {
                            var values = split.Split("=");
                            if (values[0] == "N") ledCount = int.Parse(values[1]);
                            if (values[0] == "B") brightness = int.Parse(values[1]);
                        }
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine("Exception: " + e.Message + " at " + e.StackTrace);
            }

            return new[] {ledCount, brightness};
            
        }
        
        /// <summary>
        /// Retrieve device brightness and led count async
        /// </summary>
        /// <returns></returns>
        public async Task<int[]> GetStateAsync() {
            while (_sending) {
                await Task.Delay(1);
            }
            var ledCount = 0;
            var brightness = 0;

            try {
                _sending = true;
                var output = new byte[6];
                output[0] = Convert.ToByte(CmdWord[0]); // MagicWord
                output[1] = Convert.ToByte(CmdWord[1]);
                output[2] = Convert.ToByte(CmdWord[2]);
                output[3] = Convert.ToByte(StWord[0]);
                output[4] = Convert.ToByte(StWord[1]);
                output[5] = 0;
                await _comPort.BaseStream.WriteAsync(output, 0, output.Length);
                await _comPort.BaseStream.FlushAsync();
                _sending = false;
                _dataReceived.WaitOne(1000);
                if (!string.IsNullOrEmpty(_line)) {
                    if (_line.Contains("N=")) {
                        var splits = _line.Split(";");
                        foreach (var split in splits) {
                            var values = split.Split("=");
                            if (values[0] == "N") ledCount = int.Parse(values[1]);
                            if (values[0] == "B") brightness = int.Parse(values[1]);
                        }
                    }
                }
            } catch (Exception e) {
                Debug.WriteLine("Exception: " + e.Message + " at " + e.StackTrace);
            }

            return new[] {ledCount, brightness};
            
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
            if (update) Update().ConfigureAwait(false);
        }

        private void WriteHeader() {
            _serialData[0] = Convert.ToByte(MagicWord[0]); // MagicWord
            _serialData[1] = Convert.ToByte(MagicWord[1]);
            _serialData[2] = Convert.ToByte(MagicWord[2]);
            _serialData[3] = Convert.ToByte(LedCount - 1 >> 8); // Brightness high byte
            _serialData[4] = Convert.ToByte(LedCount - 1 & 0xFF); // Brightness low byte
            _serialData[5] = Convert.ToByte(_serialData[3] ^ _serialData[4] ^ 0x55); // Checksum
        }

        private void WriteMatrixToSerialData() {
            var serialOffset = 6;
            for (var i = 0; i <= _ledMatrix.Length - 1; i++) {
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
        ///     <returns> A Dictionary of ports with possible brightness/ledCounts (if supported) responding to ada commands.
        ///     If no LED count/brightness is returned, it will be returned as 0;
        ///     </returns>
        public static Dictionary<string, KeyValuePair<int,int>> FindDevices() {
            var output = new Dictionary<string, KeyValuePair<int,int>>();

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
                    var line = i.ReadLine();
                    if (line.Substring(0, 3) == "Ada") {
                        output[dev] =  new KeyValuePair<int, int>(0,0);
                    }
                    
                    i.Close();
                }
                catch (Exception) {
                    //Console.WriteLine("Another error: " + e.Message);
                }
            }
            return output;
        }

        /// <summary>
        /// Send data to lights
        /// </summary>
        /// <returns>True if no errors occurred and connected, false if not</returns>
        public async Task Update() {
            if (!Connected) return;
            if (_sending) return;
            try {
                _sending = true;
                WriteHeader();
                WriteMatrixToSerialData();
                _comPort.Write(_serialData,0,_serialData.Length);
                //await _comPort.BaseStream.WriteAsync(data, 0, data.Length);
                //await _comPort.BaseStream.FlushAsync();
                await Task.FromResult(true);
                _sending = false;
            } catch (Exception) {
                // Ignored
            }
        }

        private bool _disposedValue; // To detect redundant calls

        // IDisposable
        private void Dispose(bool disposing)
        {
            if (!_disposedValue) {
                if (disposing) {
                    try {
                        if (Connected) _comPort?.Close();
                    } catch (Exception) {
                        // Ignored
                    }
                    _comPort?.Dispose();
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
