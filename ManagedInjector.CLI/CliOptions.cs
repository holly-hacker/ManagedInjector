using CommandLine;

namespace ManagedInjector.CLI
{
	public class CliOptions
	{
		[Option('n', "name", Group = "process", Required = true, HelpText = "Specifies the target process name")]
		public string? ProcessName { get; set; }

		[Option('p', "pid", Group = "process", Required = true, HelpText = "Specifies the target process id")]
		public int? ProcessId { get; set; }
	}
}
