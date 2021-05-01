using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;

namespace HoLLy.ManagedInjector
{
	public static class NativeHelper
	{
		public static bool In64BitProcess { get; } = Is64BitProcess();
		public static bool In64BitMachine { get; } = Is64BitMachine();

		public static bool Is64BitProcess(IntPtr handle) => In64BitMachine && !IsWow64Process(handle);
		private static bool Is64BitMachine() => In64BitProcess || IsWow64Process(Process.GetCurrentProcess().Handle);
		private static bool Is64BitProcess() => IntPtr.Size == 8;

		private static bool IsWow64Process(IntPtr handle)
		{
			if (!Native.IsWow64Process(handle, out bool wow64))
				throw new Win32Exception(Native.GetLastError());

			return wow64;
		}

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

		public static IReadOnlyList<(IntPtr baseAddress, string moduleName)> GetModules(uint pid)
		{
			var hSnapshot = Native.CreateToolhelp32Snapshot(Native.SnapshotFlags.Module | Native.SnapshotFlags.Module32, pid);

			if (hSnapshot == new IntPtr(-1))
				throw new Win32Exception(Native.GetLastError());

			var module = new Native.ModuleEntry32 {DwSize = (uint) Marshal.SizeOf<Native.ModuleEntry32>()};
			var list = new List<(IntPtr baseAddress, string moduleName)>();

			bool next;
			do
			{

				// TODO: handle errors. seems to return 0 or 1 on success, and ERROR_NO_MORE_FILES (18?) on fail
				next = !list.Any()
					? Native.Module32First(hSnapshot, ref module)
					: Native.Module32Next(hSnapshot, ref module);

				if (next)
					list.Add((module.ModBaseAddr, module.SzModule));

			} while (next);

			return list;
		}
	}
}
