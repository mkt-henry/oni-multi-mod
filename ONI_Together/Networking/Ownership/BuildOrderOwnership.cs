using System.Collections.Generic;
using ONI_Together.DebugTools;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Remembers who ordered a building, from the moment the order arrives until the building exists.
	/// </summary>
	/// <remarks>
	/// A building is owned by whoever ordered it, not by whoever built it - work is shared, so the
	/// duplicant that does the construction may belong to someone else entirely. But ordering and
	/// completing are separated by however long the construction takes, and by a change of object:
	/// the order produces a blueprint, and the blueprint is replaced by the finished building.
	/// <para>
	/// Keyed by cell because that is the one thing both ends agree on. The order names a cell and the
	/// finished building stands on it.
	/// </para>
	/// </remarks>
	public static class BuildOrderOwnership
	{
		private static readonly Dictionary<int, PlayerId> _pending = new Dictionary<int, PlayerId>();

		/// <summary>
		/// Notes that <paramref name="orderer"/> asked for something to be built here.
		/// </summary>
		/// <remarks>
		/// A re-order at the same cell replaces the previous note: the earlier order was either
		/// cancelled or already collected, and in both cases the newer one is the truth.
		/// </remarks>
		public static void Record(int cell, PlayerId orderer)
		{
			if (!orderer.IsValid || !Grid.IsValidCell(cell))
				return;

			_pending[cell] = orderer;
		}

		public static bool TryTake(int cell, out PlayerId orderer)
		{
			if (_pending.TryGetValue(cell, out orderer))
			{
				_pending.Remove(cell);
				return true;
			}

			orderer = PlayerId.None;
			return false;
		}

		public static void Clear()
		{
			// Orders outstanding when a world unloads describe buildings that will never appear here.
			int abandoned = _pending.Count;
			_pending.Clear();

			if (abandoned > 0)
				DebugConsole.Log($"[BuildOrder] Discarded {abandoned} pending build order(s) on world unload.");
		}
	}
}
