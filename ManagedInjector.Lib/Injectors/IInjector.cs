namespace HoLLy.ManagedInjector.Injectors
{
	public interface IInjector
	{
		EntryPointType EntryPoint { get; }
		void Inject(InjectableProcess process, string dllPath, string typeName, string methodName);
	}
}
