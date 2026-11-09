using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace TerrariaPerformanceOverhaul.Common.Diagnostics
{
	/// <summary>
	/// Read-only hardware names from the Windows registry, for benchmark metadata only.
	/// Returns null on other platforms or when a key is missing; never throws.
	/// </summary>
	internal static class WindowsHardware
	{
		private const string CpuKey = @"HARDWARE\DESCRIPTION\System\CentralProcessor\0";
		// Display adapter device class GUID (GUID_DEVCLASS_DISPLAY).
		private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

		public static object CpuName() {
			if (!OperatingSystem.IsWindows())
				return null;

			try {
				using RegistryKey key = Registry.LocalMachine.OpenSubKey(CpuKey);
				return (key?.GetValue("ProcessorNameString") as string)?.Trim();
			}
			catch (Exception) {
				return null;
			}
		}

		/// <summary>DriverDesc of every display adapter, distinct, separated by ';' (may include virtual adapters).</summary>
		public static object GpuNames() {
			if (!OperatingSystem.IsWindows())
				return null;

			try {
				using RegistryKey cls = Registry.LocalMachine.OpenSubKey(DisplayClassKey);
				if (cls == null)
					return null;

				var names = new List<string>();
				foreach (string sub in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit))) {
					try {
						using RegistryKey dev = cls.OpenSubKey(sub);
						if (dev?.GetValue("DriverDesc") is string desc && !string.IsNullOrWhiteSpace(desc))
							names.Add(desc.Trim());
					}
					catch (Exception) {
						// Some subkeys can be access-restricted; skip them.
					}
				}
				return names.Count == 0 ? null : string.Join(";", names.Distinct());
			}
			catch (Exception) {
				return null;
			}
		}
	}
}
