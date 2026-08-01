using System.Linq;
using ONI_Together.Networking.Ownership;

namespace ONI_Together.DebugTools.UnitTests
{
	/// <summary>
	/// Exercises the ownership index against a throwaway registry so nothing here touches live session state.
	/// </summary>
	public static class OwnershipTests
	{
		private static readonly PlayerId Alice = new PlayerId(1001uL);
		private static readonly PlayerId Bob = new PlayerId(1002uL);

		private static OwnershipRegistry NewRegistry() => new OwnershipRegistry();

		[UnitTest(name: "Register and read back an owner", category: "Ownership")]
		public static UnitTestResult RegisterThenRead()
		{
			var registry = NewRegistry();

			if (!registry.Register(netId: 10, Alice, OwnershipType.Duplicant, worldId: 0))
				return UnitTestResult.Fail("Register rejected a valid record");

			if (!registry.TryGetOwner(10, out var owner))
				return UnitTestResult.Fail("TryGetOwner found nothing after Register");

			if (owner != Alice)
				return UnitTestResult.Fail($"Expected owner {Alice}, got {owner}");

			if (registry.Count != 1)
				return UnitTestResult.Fail($"Expected 1 record, got {registry.Count}");

			return UnitTestResult.Pass("Record stored and retrieved");
		}

		[UnitTest(name: "Reject unusable registrations", category: "Ownership")]
		public static UnitTestResult RejectsBadInput()
		{
			var registry = NewRegistry();

			// NetId 0 means NetworkIdentity has not assigned one; such a record could never be matched back.
			if (registry.Register(netId: 0, Alice, OwnershipType.Duplicant, 0))
				return UnitTestResult.Fail("Accepted netId 0");

			if (registry.Register(netId: 11, PlayerId.None, OwnershipType.Duplicant, 0))
				return UnitTestResult.Fail("Accepted an invalid owner");

			if (registry.Register(netId: 12, Alice, OwnershipType.None, 0))
				return UnitTestResult.Fail("Accepted OwnershipType.None");

			if (registry.Count != 0)
				return UnitTestResult.Fail($"Rejected registrations still stored {registry.Count} record(s)");

			return UnitTestResult.Pass("All three rejected without storing anything");
		}

		[UnitTest(name: "Re-registering the same record is idempotent", category: "Ownership")]
		public static UnitTestResult DuplicateRegistrationIsIdempotent()
		{
			var registry = NewRegistry();

			registry.Register(20, Alice, OwnershipType.Building, 0);
			registry.Register(20, Alice, OwnershipType.Building, 0);

			if (registry.Count != 1)
				return UnitTestResult.Fail($"Expected 1 record after duplicate register, got {registry.Count}");

			if (registry.ByOwner(Alice).Count() != 1)
				return UnitTestResult.Fail("Owner index gained a duplicate entry");

			return UnitTestResult.Pass("Duplicate register left a single record");
		}

		[UnitTest(name: "Conflicting registration replaces and reindexes", category: "Ownership")]
		public static UnitTestResult ConflictingRegistrationReplaces()
		{
			var registry = NewRegistry();

			registry.Register(30, Alice, OwnershipType.Building, 0);
			registry.Register(30, Bob, OwnershipType.Building, 0);

			if (!registry.TryGetOwner(30, out var owner) || owner != Bob)
				return UnitTestResult.Fail("Second registration did not take effect");

			// The stale owner must not keep a dangling reverse index entry.
			if (registry.ByOwner(Alice).Any())
				return UnitTestResult.Fail("Previous owner still indexed after replacement");

			if (registry.ByOwner(Bob).Count() != 1)
				return UnitTestResult.Fail("New owner not indexed exactly once");

			return UnitTestResult.Pass("Ownership replaced and both indexes corrected");
		}

		[UnitTest(name: "Transfer moves ownership and index", category: "Ownership")]
		public static UnitTestResult TransferMovesOwnership()
		{
			var registry = NewRegistry();

			registry.Register(40, Alice, OwnershipType.PrintingPod, worldId: 3);

			if (!registry.Transfer(40, Bob))
				return UnitTestResult.Fail("Transfer of a registered object failed");

			if (!registry.TryGetRecord(40, out var record))
				return UnitTestResult.Fail("Record vanished after transfer");

			if (record.Owner != Bob)
				return UnitTestResult.Fail($"Expected owner {Bob}, got {record.Owner}");

			// Type and world must survive a change of owner.
			if (record.Type != OwnershipType.PrintingPod || record.WorldId != 3)
				return UnitTestResult.Fail($"Transfer altered the record: {record}");

			if (registry.ByOwner(Alice).Any())
				return UnitTestResult.Fail("Old owner still indexed after transfer");

			if (registry.ByOwner(Bob).Count() != 1)
				return UnitTestResult.Fail("New owner not indexed after transfer");

			return UnitTestResult.Pass("Transfer moved the record and kept type and world");
		}

		[UnitTest(name: "Transfer of an unknown object fails", category: "Ownership")]
		public static UnitTestResult TransferUnknownFails()
		{
			var registry = NewRegistry();

			if (registry.Transfer(999, Bob))
				return UnitTestResult.Fail("Transfer succeeded for an unregistered netId");

			if (registry.Count != 0)
				return UnitTestResult.Fail("Failed transfer created a record");

			return UnitTestResult.Pass("Unknown transfer refused");
		}

		[UnitTest(name: "Remove clears record and index", category: "Ownership")]
		public static UnitTestResult RemoveClearsEverything()
		{
			var registry = NewRegistry();

			registry.Register(50, Alice, OwnershipType.Storage, 0);

			if (!registry.Remove(50))
				return UnitTestResult.Fail("Remove reported nothing to remove");

			if (registry.IsOwned(50))
				return UnitTestResult.Fail("Record still present after Remove");

			if (registry.ByOwner(Alice).Any())
				return UnitTestResult.Fail("Owner index still holds the removed object");

			if (registry.Remove(50))
				return UnitTestResult.Fail("Second Remove reported success");

			return UnitTestResult.Pass("Remove cleared both structures and is safe to repeat");
		}

		[UnitTest(name: "Ownership checks fail closed", category: "Ownership")]
		public static UnitTestResult OwnershipChecksFailClosed()
		{
			var registry = NewRegistry();

			registry.Register(60, Alice, OwnershipType.Duplicant, 0);

			if (registry.IsOwnedBy(60, Bob))
				return UnitTestResult.Fail("Reported Bob owns Alice's object");

			// An unowned object must not read as owned by anyone, or permission gates would open up.
			if (registry.IsOwnedBy(61, Alice))
				return UnitTestResult.Fail("Reported ownership of an unregistered object");

			if (registry.IsOwnedBy(60, PlayerId.None))
				return UnitTestResult.Fail("PlayerId.None matched an owned object");

			if (!registry.IsOwnedBy(60, Alice))
				return UnitTestResult.Fail("Real owner was not recognised");

			return UnitTestResult.Pass("Denies unknown, foreign and unowned cases");
		}

		[UnitTest(name: "ByOwner returns only that player's objects", category: "Ownership")]
		public static UnitTestResult ByOwnerIsScoped()
		{
			var registry = NewRegistry();

			registry.Register(70, Alice, OwnershipType.Duplicant, 0);
			registry.Register(71, Alice, OwnershipType.Duplicant, 0);
			registry.Register(72, Bob, OwnershipType.Duplicant, 0);

			var alices = registry.ByOwner(Alice).Select(r => r.NetId).OrderBy(id => id).ToList();

			if (alices.Count != 2 || alices[0] != 70 || alices[1] != 71)
				return UnitTestResult.Fail($"Expected [70,71] for Alice, got [{string.Join(",", alices)}]");

			if (registry.ByOwner(PlayerId.None).Any())
				return UnitTestResult.Fail("PlayerId.None returned objects");

			return UnitTestResult.Pass("Owner scoping correct");
		}

		[UnitTest(name: "Clear empties the registry", category: "Ownership")]
		public static UnitTestResult ClearEmptiesRegistry()
		{
			var registry = NewRegistry();

			registry.Register(80, Alice, OwnershipType.Building, 0);
			registry.Register(81, Bob, OwnershipType.Building, 0);
			registry.Clear();

			if (registry.Count != 0)
				return UnitTestResult.Fail($"Expected empty registry, got {registry.Count}");

			if (registry.ByOwner(Alice).Any() || registry.ByOwner(Bob).Any())
				return UnitTestResult.Fail("Owner index survived Clear");

			return UnitTestResult.Pass("Registry emptied");
		}
	}
}
