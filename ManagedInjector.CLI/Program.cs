using System;
using System.Diagnostics;
using System.Linq;
using CommandLine;
using HoLLy.ManagedInjector;

namespace ManagedInjector.CLI
{
	internal static class Program
	{
		private static void Main(string[] args)
		{
			var result = Parser.Default.ParseArguments<CliOptions>(args)
				.WithParsed(Run);
		}

		private static void Run(CliOptions cli)
		{
			int pid = GetProcessId(cli);
			var process = new InjectableProcess(pid);

			Console.WriteLine("PID: " + pid);
			Console.WriteLine("Status: " + process.GetStatus());
			Console.WriteLine("Arch: " + process.GetArchitecture());

			if (process.GetStatus() != ProcessStatus.Ok)
				throw new Exception("Expected OK status for process");
		}

		private static int GetProcessId(CliOptions cli)
		{
			if (cli.ProcessName is not null)
			{
				var processes = Process.GetProcessesByName(cli.ProcessName);

				if (processes.Length == 0)
				{
					var message = $"Could not find process by name '{cli.ProcessName}'.";

					bool hasExtension = cli.ProcessName.EndsWith(".exe", StringComparison.InvariantCultureIgnoreCase);
					if (hasExtension)
						message += " Make sure not to add a file extension.";

					throw new Exception(message);
				}

				if (processes.Length > 1)
					throw new Exception($"Multiple processes found by name '{cli.ProcessName}'.");

				return processes.Single().Id;
			}

			if (cli.ProcessId is not null)
			{
				return cli.ProcessId.Value;
			}

			throw new Exception("No target process found");
		}
	}
}
