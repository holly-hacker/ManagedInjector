using System;
using System.Diagnostics;
using System.Linq;
using HoLLy.ManagedInjector.Injectors;

namespace HoLLy.ManagedInjector
{
	public class InjectableProcess : IDisposable
	{
		private const Native.ProcessAccessFlags FlagsForInject = Native.ProcessAccessFlags.CreateThread |
		                                                         Native.ProcessAccessFlags.QueryInformation |
		                                                         Native.ProcessAccessFlags.VirtualMemoryOperation |
		                                                         Native.ProcessAccessFlags.VirtualMemoryRead |
		                                                         Native.ProcessAccessFlags.VirtualMemoryWrite;

		private const Native.ProcessAccessFlags BasicFlags = Native.ProcessAccessFlags.QueryInformation;

		private IntPtr _handle;
		private bool _isHandleFull;
		private bool? _is64Bit;
		private ProcessStatus _status = ProcessStatus.Unknown;
		private ProcessArchitecture _architecture = ProcessArchitecture.Unknown;

		public InjectableProcess(uint pid)
		{
			Pid = pid;
		}

		public uint Pid { get; }

		public bool Is64Bit => _is64Bit ??= NativeHelper.Is64BitProcess(Handle);

		/// <summary>
		/// Get a handle to the process. This is only guaranteed to have basic flags set.
		/// </summary>
		public IntPtr Handle
		{
			get
			{
				if (_handle == IntPtr.Zero)
					_handle = NativeHelper.OpenProcess(BasicFlags, Pid);

				return _handle;
			}
		}

		/// <summary>
		/// Gets a handle to the process that is guaranteed to have more flags set.
		/// </summary>
		public IntPtr FullHandle
		{
			get
			{
				if (!_isHandleFull)
				{
					Native.CloseHandle(_handle);
					_handle = IntPtr.Zero;
				}

				if (_handle == IntPtr.Zero)
				{
					_handle = NativeHelper.OpenProcess(FlagsForInject, Pid);
					_isHandleFull = true;
				}

				return _handle;
			}
		}

		public ProcessStatus GetStatus()
		{
			if (_status != ProcessStatus.Unknown)
				return _status;

			try
			{
				// 64-bit process can target 32-bit, but not the other way around
				if (!NativeHelper.In64BitProcess && NativeHelper.Is64BitProcess(Handle))
					return _status = ProcessStatus.ArchitectureMismatch;

				if (GetArchitecture() == ProcessArchitecture.Unknown)
					return _status = ProcessStatus.NoRuntimeFound;

				return _status = ProcessStatus.Ok;
			}
			catch (Exception)
			{
				return ProcessStatus.Unknown;
			}
		}

		public ProcessArchitecture GetArchitecture()
		{
			if (_architecture != ProcessArchitecture.Unknown)
				return _architecture;

			try
			{
				using var process = Process.GetProcessById((int) Pid);

				var modules = NativeHelper.GetModules(Pid);

				bool HasModule(string s) =>
					modules.Any(x => x.moduleName.Equals(s, StringComparison.InvariantCultureIgnoreCase));

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
			catch (Exception)
			{
				return ProcessArchitecture.Unknown;
			}
		}

		public IInjector GetInjector()
		{
			var arch = GetArchitecture();
			return arch switch
			{
				ProcessArchitecture.NetFrameworkV2 => new FrameworkV2Injector(),
				ProcessArchitecture.NetFrameworkV4 => new FrameworkV4Injector(),
				ProcessArchitecture.Mono => throw new NotImplementedException("Mono injector is not yet implemented"),
				ProcessArchitecture.NetCore => throw new NotImplementedException(
					".NET Core injector is not yet implemented"),
				ProcessArchitecture.Unknown => throw new Exception(
					"Tried to inject into process with unknown architecture"),
				_ => throw new NotSupportedException($"No injector found for architecture {arch}"),
			};
		}

		public void Inject(string dllPath, string typeName, string methodName)
		{
			IInjector injector = GetInjector();

			Inject(injector, dllPath, typeName, methodName);
		}

		public void Inject(IInjector injector, string dllPath, string typeName, string methodName) =>
			injector.Inject(this, dllPath, typeName, methodName);

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
