using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using AdalightNet;

namespace ConsoleTest {
	internal static class Program {
		private static void Main() {
			var devInfo = Adalight.FindDevices();
			var devs = new List<Adalight>();
			if (devInfo.Count > 0) {
				foreach (var port in devInfo) {
					Console.WriteLine($"Found device at COM{port}.");
					var count = 0;
					var bri = 0;
					var set = false;

					while (!set) {
						if (port.Value.Value != 0) {
							Console.WriteLine("Enter LED count:");
							var input = Console.ReadLine();

							if (!string.IsNullOrEmpty(input)) {
								if (int.TryParse(input, out count)) {
									set = true;
								}
							}

							if (!set) Console.WriteLine("Invalid input.");
						} else {
							Console.WriteLine("Count auto-detected as " + count);
						}

						if (port.Value.Key != 0) {
							Console.WriteLine("We have a brightness: " + port.Value.Key);
							bri = port.Value.Key;
						}
					}
					devs.Add(new Adalight(port.Key, count,115200,bri));
				}

				foreach (var dev in devs) {
					// Connect to device
					var count = dev.LedCount;
					var color = Color.Red;
					if (!dev.Connected) {
						dev.UpdateBrightness(255);
						var state = dev.GetState();
						var aState = dev.GetState();
						Console.WriteLine("State: " + state[0] + " or " + aState[1]);
						Console.WriteLine($"Setting strip on port {dev.Port} to red.");
					}
					
					for (var i = 0; i < count; i++) {
						// Update each pixel, but don't set the value yet.
						dev.UpdatePixel(i, color, false);
					}
					// Update device now, since we didn't do it on the pixel update.
					dev.Update();
				}

				Task.Delay(3000);
				
				foreach (var dev in devs) {
					if (!dev.Connected) continue;
					// Connect to device
					var count = dev.LedCount;
					var color = Color.Blue;
					dev.UpdateBrightness(128);
					Console.WriteLine($"Setting strip on port {dev.Port} to red.");
					for (var i = 0; i < count; i++) {
						// Update each pixel, and immediately set the value (Color wipe)
						dev.UpdatePixel(i, color);
						// Wait to produce effect
						Task.Delay(1);
					}
				}

				Task.Delay(3000);
				
				foreach (var dev in devs) {
					if (!dev.Connected) continue;
					var count = dev.LedCount;
					var color = Color.Blue;
					Console.WriteLine($"Setting strip on port {dev.Port} to red.");
					var colors = new Color[count];
					for (var i = 0; i < count; i++) {
						colors[i] = color;
						// Update each pixel, and immediately set the value (Color wipe)
						dev.UpdateColors(colors.ToList());
						// Wait to produce effect
						Task.Delay(1);
					}
				}
				
				Task.Delay(3000);

				
				foreach (var dev in devs) {
					if (!dev.Connected) continue;
					var count = dev.LedCount;
					var color = Color.Green;
					Console.WriteLine($"Setting strip on port {dev.Port} to red.");
					var colors = new Color[count];
					for (var i = 0; i < count; i++) {
						colors[i] = color;
						// Update each pixel, and immediately set the value (Color wipe)
						dev.UpdateColorsAsync(colors.ToList()).ConfigureAwait(false);
						// Wait to produce effect
						Task.Delay(1);
					}
				}
				
				Task.Delay(3000);

				foreach (var dev in devs) {
					// Disconnect our device. If "false" is specified as a parameter, lights will remain in
					// the last state they were in.
					var disconnected = dev.Disconnect();
					if (disconnected) Console.WriteLine($"Device on port {dev.Port} is disconnected.");
					// Completely dispose of it and the underlying port
					dev.Dispose();
				}
			} else {
				Console.WriteLine("No adalight devices were found.");
			}
		}
	}
}