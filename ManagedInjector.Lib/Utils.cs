namespace HoLLy.ManagedInjector
{
	internal static class Utils
	{
		public static (string ns, string type) SplitType(string fullType)
		{
			int lastIndex = fullType.LastIndexOf('.');
			return (fullType.Substring(0, lastIndex), fullType.Substring(lastIndex + 1));
		}
	}
}
