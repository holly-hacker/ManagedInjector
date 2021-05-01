using System;
using System.Runtime.InteropServices;

namespace HoLLy.ManagedInjector
{
	public static class Native
	{
		[DllImport("kernel32.dll")]
		public static extern int GetLastError();

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess, bool bInheritHandle,
			uint dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool CloseHandle(IntPtr hObject);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool IsWow64Process(IntPtr processHandle, out bool wow64Process);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateToolhelp32Snapshot(SnapshotFlags dwFlags, uint th32ProcessId);

		[DllImport("kernel32.dll")]
		public static extern bool Module32First(IntPtr hSnapshot, ref ModuleEntry32 lpme);

		[DllImport("kernel32.dll")]
		public static extern bool Module32Next(IntPtr hSnapshot, ref ModuleEntry32 lpme);

		/// <remarks> <paramref name="hProcess"/> must have <see cref="ProcessAccessFlags.VirtualMemoryRead"/> access </remarks>
		/// <seealso> https://docs.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-readprocessmemory </seealso>
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer,
			nuint dwSize, out nuint lpNumberOfBytesRead);

		/// <remarks> <paramref name="hProcess"/> must have <seealso cref="ProcessAccessFlags.VirtualMemoryWrite"/> and <seealso cref="ProcessAccessFlags.VirtualMemoryOperation"/> access </remarks>
		/// <seealso> https://docs.microsoft.com/en-us/windows/win32/api/memoryapi/nf-memoryapi-writeprocessmemory </seealso>
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [In] [Out] byte[] buffer,
			nuint size, out nuint lpNumberOfBytesWritten);

		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, int flAllocationType,
			int flProtect);

		/// <seealso> https://docs.microsoft.com/en-us/windows/win32/api/processthreadsapi/nf-processthreadsapi-createremotethread </seealso>
		// NOTE: could give lpThreadAttributes proper type
		[DllImport("kernel32.dll", SetLastError = true)]
		public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, nuint dwStackSize,
			IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

		[Flags]
		public enum ProcessAccessFlags : uint
		{
			Terminate = 0x0001,
			CreateThread = 0x0002,
			VirtualMemoryOperation = 0x0008,
			VirtualMemoryRead = 0x0010,
			VirtualMemoryWrite = 0x0020,
			DuplicateHandle = 0x0040,
			CreateProcess = 0x0080,
			SetQuota = 0x0100,
			SetInformation = 0x0200,
			QueryInformation = 0x0400,
			QueryLimitedInformation = 0x1000,

			Synchronize = 0x0010_0000,

			//All = 0x001F_0FFF,
			All = Terminate | CreateThread | 0x0004 | VirtualMemoryOperation
			      | VirtualMemoryRead | VirtualMemoryWrite | DuplicateHandle | CreateProcess
			      | SetQuota | SetInformation | QueryInformation | 0x0800
			      | 0x001F_0000,
		}

		[Flags]
		public enum SnapshotFlags : uint
		{
			HeapList = 0x00000001,
			Process = 0x00000002,
			Thread = 0x00000004,
			Module = 0x00000008,
			Module32 = 0x00000010,
			All = HeapList | Process | Thread | Module | Module32,
			NoHeaps = 0x40000000,
			Inherit = 0x80000000,
		}

		[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
		public struct ModuleEntry32
		{
			public uint DwSize;
			public uint Th32ModuleID;
			public uint Th32ProcessID;
			public uint GlobalCountUsage;
			public uint ProcessCountUsage;
			public IntPtr ModBaseAddr;
			public uint ModBaseSize;
			public IntPtr HModule;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 255 + 1)]
			public string SzModule;
			[MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
			public string SzExePath;
		}
	}
}
