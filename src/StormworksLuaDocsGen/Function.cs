using System.Collections.Generic;

namespace StormworksLuaDocsGen
{
	public class Function
	{
		public string Name { get; set; }

		public string Description { get; set; }

		public List<Parameter> Parameters { get; set; } = new();

		public List<Parameter> ReturnParameters { get; set; } = new();
	}
}