namespace ManagedInjector.GUI.Models
{
	public class UserAssembly
	{
		public UserAssembly(string path, string type, string method)
		{
			Path = path;
			Type = type;
			Method = method;
		}

		public string Path { get; private set; }
		public string Type { get; private set; }
		public string Method { get; private set; }
	}
}
