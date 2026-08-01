namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// What kind of thing an ownership record covers.
	/// </summary>
	/// <remarks>
	/// Backed by byte and explicitly numbered because these values are written into save files.
	/// Never renumber an existing member; only append.
	/// </remarks>
	public enum OwnershipType : byte
	{
		/// <summary>Unset. A record should never carry this.</summary>
		None = 0,

		PrintingPod = 1,
		Duplicant = 2,
		Building = 3,
		Storage = 4,
		Rocket = 5,
		SharedProject = 6,
	}
}
