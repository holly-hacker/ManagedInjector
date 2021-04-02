using System;
using System.Diagnostics;

namespace HoLLy.ManagedInjector
{
	internal static class NativeHelper
	{
		public static bool In64BitProcess { get; } = Is64BitProcess();
		public static bool In64BitMachine { get; } = Is64BitMachine();

		public static bool Is64BitProcess(IntPtr handle) => Is64BitMachine() && !IsWow64Process(handle);

		private static bool IsWow64Process(IntPtr handle) => Native.IsWow64Process(handle, out bool wow64) && wow64;
		private static bool Is64BitMachine() => In64BitProcess || IsWow64Process(Process.GetCurrentProcess().Handle);
		private static bool Is64BitProcess() => IntPtr.Size == 8;
	}
}
