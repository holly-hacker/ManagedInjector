using System;
using System.Diagnostics;
using System.Linq;

namespace HoLLy.ManagedInjector
{
	public class InjectableProcess : IDisposable
	{
		private readonly int _pid;
		private IntPtr _handle;
		private ProcessStatus _status = ProcessStatus.Unknown;
		private ProcessArchitecture _architecture = ProcessArchitecture.Unknown;

		public InjectableProcess(int pid)
		{
			_pid = pid;
		}

		public ProcessStatus GetStatus()
		{
			if (_status != ProcessStatus.Unknown)
				return _status;

			var handle = GetHandle();

			if (NativeHelper.In64BitProcess != NativeHelper.Is64BitProcess(handle))
				return _status = ProcessStatus.ArchitectureMismatch;

			if (GetArchitecture() == ProcessArchitecture.Unknown)
				return _status = ProcessStatus.NoRuntimeFound;

			return _status = ProcessStatus.Ok;
		}

		public ProcessArchitecture GetArchitecture()
		{
			if (_architecture != ProcessArchitecture.Unknown)
				return _architecture;

			// TODO: no architectures detected yet
			using var process = Process.GetProcessById(_pid);

			bool HasModule(string s) => process.Modules.OfType<ProcessModule>().Any(x => x.ModuleName.Equals(s, StringComparison.InvariantCultureIgnoreCase));

			// .NET 2 has mscoree and mscorwks
			// .NET 4 has mscoree and clr
			// .NET Core 3.1 has coreclr
			// Some unity games have mono-2.0-bdwgc.dll

			if (HasModule("mscoree.dll"))
			{
				if (HasModule("clr.dll"))
					return _architecture = ProcessArchitecture.NetFrameworkV4;

				if (HasModule("mscorwks.dll"))
					return _architecture = ProcessArchitecture.NetFrameworkV2;
			}

			if (HasModule("coreclr.dll"))
				return _architecture = ProcessArchitecture.NetCore;

			// TODO: also check non-bleeding mono dll
			if (HasModule("mono-2.0-bdwgc.dll"))
				return _architecture = ProcessArchitecture.Mono;

			return ProcessArchitecture.Unknown;
		}

		private IntPtr GetHandle()
		{
			if (_handle == IntPtr.Zero)
				_handle = Native.OpenProcess(Native.ProcessAccessFlags.QueryInformation, false, _pid);

			return _handle;
		}

		private void ReleaseUnmanagedResources()
		{
			if (_handle != IntPtr.Zero)
				Native.CloseHandle(_handle);
		}

		public void Dispose()
		{
			ReleaseUnmanagedResources();
			GC.SuppressFinalize(this);
		}

		~InjectableProcess()
		{
			ReleaseUnmanagedResources();
		}
	}
}
