using System;
using System.IO;
using System.Reflection;

namespace ONI_Together.Misc
{
	/// <summary>
	/// Human readable identifier for the build that is actually running.
	/// </summary>
	/// <remarks>
	/// Comparing dll hashes tells you whether two machines match, but not which one is behind. During
	/// two machine testing that distinction is the thing you actually want, and a mismatched build
	/// presents as a sync bug rather than as a version problem, so it needs to be obvious.
	/// <para>
	/// Read from a BUILD.txt written next to the assembly at deploy time rather than baked in at
	/// compile time, because embedding it would mean editing the upstream build files this fork tries
	/// to leave alone.
	/// </para>
	/// </remarks>
	public static class BuildStamp
	{
		private const string FileName = "BUILD.txt";

		private static string _value;

		/// <summary>Deploy stamp, or a clear marker when the file is absent.</summary>
		public static string Value
		{
			get
			{
				if (_value != null)
					return _value;

				_value = Read() ?? "unstamped (built or copied without the deploy script)";
				return _value;
			}
		}

		private static string Read()
		{
			try
			{
				string dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
				if (string.IsNullOrEmpty(dir))
					return null;

				string path = Path.Combine(dir, FileName);
				if (!File.Exists(path))
					return null;

				string text = File.ReadAllText(path).Trim();
				return string.IsNullOrEmpty(text) ? null : text;
			}
			catch (Exception)
			{
				// Never let a missing or unreadable stamp interfere with loading the mod.
				return null;
			}
		}
	}
}
