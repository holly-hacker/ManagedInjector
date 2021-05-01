using System;
using System.Diagnostics;
using HoLLy.ManagedInjector;

namespace ManagedInjector.GUI.Models
{
	public class UserProcess
	{
		public Process DotNetProcess { get; private set; }

		public InjectableProcess InjectableProcess { get; private set; }

		public uint Pid => InjectableProcess.Pid;

		public string Name => DotNetProcess.ProcessName;

		public string FileName => DotNetProcess.MainModule?.FileName;

		public ProcessStatus Status => InjectableProcess.GetStatus();

		public ProcessArchitecture Architecture => InjectableProcess.GetArchitecture();

		public string BitnessText => InjectableProcess.Is64Bit ? "64-bit" : "32-bit";

		public string ArchitectureText
		{
			get
			{
				try
				{
					return InjectableProcess.GetStatus() switch
					{
						ProcessStatus.Ok => InjectableProcess.GetArchitecture().ToString(),

						ProcessStatus.Unknown => "Error: Unknown",
						ProcessStatus.ArchitectureMismatch => "Error: Architecture mismatch",
						ProcessStatus.NoRuntimeFound => "Error: No runtime found",
						_ => throw new ArgumentOutOfRangeException(),
					};
				}
				catch (Exception e)
				{
					return e.Message.ToString();
				}
			}
		}

		public UserProcess(Process proc)
		{
			DotNetProcess = proc;
			InjectableProcess = new InjectableProcess((uint)proc.Id);
		}
	}
}
