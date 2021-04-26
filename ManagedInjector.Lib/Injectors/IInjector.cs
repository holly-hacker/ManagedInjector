using System;

namespace HoLLy.ManagedInjector.Injectors
{
	public interface IInjector
	{
		void Inject(InjectableProcess process, string dllPath, string typeName, string methodName);
	}
}
