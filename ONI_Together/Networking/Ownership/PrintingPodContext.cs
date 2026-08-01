using System;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// The printing pod currently delivering, for the duration of that delivery.
	/// </summary>
	/// <remarks>
	/// A duplicant has to inherit the owner of the pod that printed it, but the two facts are not
	/// available in the same place. Telepad.OnAcceptDelivery knows the pod and calls
	/// <c>delivery.Deliver(...)</c>, whose result is a local variable a postfix cannot reach;
	/// MinionStartingStats.Deliver produces the duplicant but has no idea which pod asked for it.
	/// <para>
	/// Deliver runs synchronously inside OnAcceptDelivery, so scoping the pod around that call lets
	/// the second patch read what the first one knew. The alternative is a transpiler to capture the
	/// local, which breaks the moment Klei edits the method.
	/// </para>
	/// </remarks>
	public static class PrintingPodContext
	{
		[ThreadStatic]
		private static Telepad _current;

		/// <summary>The pod mid-delivery, or null outside of one.</summary>
		public static Telepad Current => _current;

		public static PodScope Scope(Telepad pod) => new PodScope(pod);

		public readonly struct PodScope : IDisposable
		{
			private readonly Telepad _previous;

			internal PodScope(Telepad pod)
			{
				_previous = _current;
				_current = pod;
			}

			public void Dispose()
			{
				_current = _previous;
			}
		}
	}
}
