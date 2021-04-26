namespace HoLLy.ManagedInjector
{
	public enum ProcessStatus
	{
		Unknown,

		/// <summary>
		/// DLL injection is supported in this process
		/// </summary>
		Ok,

		/// <summary>
		/// The target process is 32-bit, while the host process is 64-bit (or vice versa).
		/// </summary>
		ArchitectureMismatch,

		/// <summary>
		/// No supported .NET runtime was found in the target process.
		/// </summary>
		NoRuntimeFound,

		// TODO: no permissions (admin)
	}
}
