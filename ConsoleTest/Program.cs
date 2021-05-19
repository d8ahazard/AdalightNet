using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using AdalightNet;

namespace ConsoleTest {
	internal static class Program {
		private static void Main() {
			var ports = Adalight.FindDevices();
			var devs = new List<Adalight>();
			if (ports.Count > 0) {
				foreach (var port in ports) {
					Console.WriteLine($"Found device at COM{port}.");
					var count = 0;
					var set = false;

					while (!set) {
						Console.WriteLine("Enter LED count:");
						var input = Console.ReadLine();

						if (!string.IsNullOrEmpty(input)) {
							if (int.TryParse(input, out count)) {
								set = true;
							}	
						}
						if (!set)Console.WriteLine("Invalid input.");
					}
					devs.Add(new Adalight(port, count));
				}

				foreach (var dev in devs) {
					// Connect to device
					var connected = dev.Connect();
					var count = dev.LedCount;
					var color = Color.Red;
					if (connected) Console.WriteLine($"Setting strip on port {dev.PortNumber} to red.");
					for (var i = 0; i < count; i++) {
						// Update each pixel, but don't set the value yet.
						dev.UpdatePixel(i, color, false);
					}
					// Update device now, since we didn't do it on the pixel update.
					dev.Update();
				}

				Task.Delay(3000);
				
				foreach (var dev in devs) {
					// Connect to device
					dev.Connect();
					var count = dev.LedCount;
					var color = Color.Blue;
					Console.WriteLine($"Setting strip on port {dev.PortNumber} to red.");
					for (var i = 0; i < count; i++) {
						// Update each pixel, and immediately set the value (Color wipe)
						dev.UpdatePixel(i, color);
						// Wait to produce effect
						Task.Delay(1);
					}
				}

				Task.Delay(3000);
				
				foreach (var dev in devs) {
					// Connect to device
					dev.Connect();
					var count = dev.LedCount;
					var color = Color.Blue;
					Console.WriteLine($"Setting strip on port {dev.PortNumber} to red.");
					var colors = new List<Color>();
					for (var i = 0; i < count; i++) {
						colors.Add(color);
						// Update each pixel, and immediately set the value (Color wipe)
						dev.UpdateColors(colors);
						// Wait to produce effect
						Task.Delay(1);
					}
				}
				
				Task.Delay(3000);

				foreach (var dev in devs) {
					// Disconnect our device. If "false" is specified as a parameter, lights will remain in
					// the last state they were in.
					var disconnected = dev.Disconnect();
					if (disconnected) Console.WriteLine($"Device on port {dev.PortNumber} is disconnected.");
					// Completely dispose of it and the underlying port
					dev.Dispose();
				}
			} else {
				Console.WriteLine("No adalight devices were found.");
			}
		}
	}
}