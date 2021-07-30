namespace StormworksLuaDocsGen
{
	public static class StringExtensions
	{
		public static string RemoveNewlines(this string original)
			=> original.Replace("\r\n", " ").Replace("\r", " ").Replace("\n", " ");
	}
}