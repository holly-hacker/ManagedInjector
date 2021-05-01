using CommandLine;

namespace ManagedInjector.CLI
{
	public class CliOptions
	{
		[Option('n', "name", Group = "process", Required = true, HelpText = "Specifies the target process name")]
		public string? ProcessName { get; set; }

		[Option('p', "pid", Group = "process", Required = true, HelpText = "Specifies the target process id")]
		public uint? ProcessId { get; set; }

		[Option('i', "input", Required = true, HelpText = "The DLL to inject")]
		public string DllPath { get; set; } = null!;

		[Option('t', "type", Required = true, HelpText = "The full type of the entry point")]
		public string EntryType { get; set; } = null!;

		[Option('m', "method", Required = true, HelpText = "The method name of the entry point")]
		public string EntryMethod { get; set; } = null!;
	}
}
