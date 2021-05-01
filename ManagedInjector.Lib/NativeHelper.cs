using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Text;

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

		public static IntPtr OpenProcess(Native.ProcessAccessFlags dwDesiredAccess, uint dwProcessId)
		{
			var ret = Native.OpenProcess(dwDesiredAccess, false, dwProcessId);

			if (ret == IntPtr.Zero)
				throw new Win32Exception(Native.GetLastError());

			return ret;
		}

		public static byte[] ReadProcessMemory(IntPtr handle, IntPtr address, nuint size)
		{
			var buffer = new byte[size];
			var success = Native.ReadProcessMemory(handle, address, buffer, size, out var read);

			if (!success)
				throw new Win32Exception(Native.GetLastError());

			if (read != size)
			{
				Debug.Assert(read < size);
				Debug.Assert(read < int.MaxValue);
				Array.Resize(ref buffer, (int) read);
			}

			return buffer;
		}

		/// <remarks>
		/// <paramref name="handle"/> requires <c>PROCESS_QUERY_INFORMATION</c> and <c>PROCESS_VM_READ</c> permissions.
		/// </remarks>
		public static string[] GetModules(IntPtr handle)
		{
			// TODO: EnumProcessModulesEx could allow cross-architecture support?
			// NOTE: could speed this up by stopping the loop when we match an architecture

			if (!Native.EnumProcessModules(handle, null, 0, out uint arraySize))
				throw new Win32Exception(Native.GetLastError());

			uint size = (uint) (arraySize / IntPtr.Size);

			var modulesHandles = new IntPtr[size];

			if (!Native.EnumProcessModules(handle, modulesHandles, arraySize, out _))
				throw new Win32Exception(Native.GetLastError());

			var moduleNames = new string[size];
			for (var i = 0; i < modulesHandles.Length; i++)
			{
				var hMod = modulesHandles[i];

				var sb = new StringBuilder(0x100);
				var ret = Native.GetModuleBaseName(handle, hMod, sb, (uint) sb.Capacity);

				if (ret == 0)
					throw new Win32Exception(Native.GetLastError());

				moduleNames[i] = sb.ToString();

				Debug.Assert(ret == sb.ToString().Length);
			}

			return moduleNames;
		}
	}
}
