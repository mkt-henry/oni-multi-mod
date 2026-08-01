using ONI_Together.Networking.Ownership;

namespace ONI_Together.DebugTools.UnitTests
{
	/// <summary>
	/// Covers the rules that decide who may command what. Built on a throwaway registry so nothing
	/// here touches the live session.
	/// </summary>
	public static class PermissionTests
	{
		private static readonly PlayerId Alice = new PlayerId(2001uL);
		private static readonly PlayerId Bob = new PlayerId(2002uL);

		private const int AlicesDupe = 100;
		private const int BobsDupe = 101;
		private const int UnownedDupe = 102;
		private const int AlicesBuilding = 200;
		private const int UnownedBuilding = 201;

		private static IPermissionService NewService()
		{
			var registry = new OwnershipRegistry();
			registry.Register(AlicesDupe, Alice, OwnershipType.Duplicant, 0);
			registry.Register(BobsDupe, Bob, OwnershipType.Duplicant, 0);
			registry.Register(AlicesBuilding, Alice, OwnershipType.Building, 0);
			return new PermissionService(registry);
		}

		[UnitTest(name: "Owner may command their own duplicant", category: "Permissions")]
		public static UnitTestResult OwnerMayCommand()
		{
			if (!NewService().CanDirectControl(Alice, AlicesDupe))
				return UnitTestResult.Fail("Owner was refused their own duplicant");

			return UnitTestResult.Pass("Owner allowed");
		}

		[UnitTest(name: "Others may not command someone else's duplicant", category: "Permissions")]
		public static UnitTestResult StrangerRefused()
		{
			var service = NewService();

			if (service.CanDirectControl(Bob, AlicesDupe))
				return UnitTestResult.Fail("Bob was allowed to command Alice's duplicant");

			if (service.CanDirectControl(Alice, BobsDupe))
				return UnitTestResult.Fail("Alice was allowed to command Bob's duplicant");

			// An unattributed packet must not slip through as a valid actor.
			if (service.CanDirectControl(PlayerId.None, AlicesDupe))
				return UnitTestResult.Fail("An unattributed actor was allowed");

			return UnitTestResult.Pass("Foreign and unattributed commands refused");
		}

		[UnitTest(name: "Unowned duplicants are open to everyone", category: "Permissions")]
		public static UnitTestResult UnownedIsOpen()
		{
			var service = NewService();

			// The starting crew is placed by worldgen with no pod to inherit from. Protecting what
			// nobody owns would leave them frozen, so this is deliberate rather than an oversight.
			if (!service.CanDirectControl(Alice, UnownedDupe) || !service.CanDirectControl(Bob, UnownedDupe))
				return UnitTestResult.Fail("An unowned duplicant was protected from someone");

			return UnitTestResult.Pass("Unowned duplicants controllable by anyone");
		}

		[UnitTest(name: "Buildings run only for their owner's duplicants", category: "Permissions")]
		public static UnitTestResult BuildingOperationIsOwnerScoped()
		{
			var service = NewService();

			if (!service.CanOperateBuilding(AlicesDupe, AlicesBuilding))
				return UnitTestResult.Fail("Alice's duplicant was refused Alice's building");

			if (service.CanOperateBuilding(BobsDupe, AlicesBuilding))
				return UnitTestResult.Fail("Bob's duplicant was allowed to run Alice's building");

			// Public infrastructure, and duplicants nobody owns, stay usable.
			if (!service.CanOperateBuilding(BobsDupe, UnownedBuilding))
				return UnitTestResult.Fail("An unowned building was restricted");

			if (!service.CanOperateBuilding(UnownedDupe, AlicesBuilding))
				return UnitTestResult.Fail("An unowned duplicant was refused");

			return UnitTestResult.Pass("Operation scoped to the owner, public things stay public");
		}

		[UnitTest(name: "Configure and print follow ownership", category: "Permissions")]
		public static UnitTestResult ConfigureAndPrintFollowOwnership()
		{
			var service = NewService();

			if (!service.CanConfigureBuilding(Alice, AlicesBuilding))
				return UnitTestResult.Fail("Owner was refused configuring their own building");

			if (service.CanConfigureBuilding(Bob, AlicesBuilding))
				return UnitTestResult.Fail("Bob was allowed to configure Alice's building");

			if (service.CanPrint(Bob, AlicesBuilding))
				return UnitTestResult.Fail("Bob was allowed to print from Alice's pod");

			return UnitTestResult.Pass("Configure and print restricted to the owner");
		}
	}
}
