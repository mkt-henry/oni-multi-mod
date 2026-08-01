using System.Collections.Generic;
using ONI_Together.DebugTools;
using ONI_Together.Networking.Packets.Architecture;
using ONI_Together.Patches.GamePatches;
using ProcGen;
using Shared.Profiling;
using UnityEngine;

namespace ONI_Together.Networking.Ownership
{
	/// <summary>
	/// Stamps an extra starting area so a player who has no printing pod gets one.
	/// </summary>
	/// <remarks>
	/// Placing a bare Headquarters is not an option. A freshly generated world is solid rock outside
	/// the original starting area, so nothing passes IsValidPlaceLocation. The starting base template
	/// carries its own cells, which means stamping it digs out the space it needs - that is why this
	/// works and the simpler approach does not.
	/// <para>
	/// The template comes from the world definition rather than a hardcoded path, so a world that is
	/// not sandstone gets the starting area that belongs to it.
	/// </para>
	/// <para>
	/// Stamping is asynchronous and runs in four phases, so the pod does not exist when Stamp returns.
	/// Only one is allowed in flight at a time: two overlapping stamps would race over both the
	/// terrain and the pending-owner handoff.
	/// </para>
	/// </remarks>
	public static class StartingAreaPlacer
	{
		/// <summary>Gap left around the template when testing a candidate spot.</summary>
		private const int Padding = 4;

		/// <summary>Horizontal step between candidate positions, in cells.</summary>
		private const int SearchStep = 12;

		private const int MaxSearchSteps = 40;

		/// <summary>Keeps a stamped area clear of the world edge.</summary>
		private const int EdgeMargin = 8;

		/// <summary>Matches the crew a vanilla world opens with.</summary>
		private const int CrewSize = 3;

		private static bool _stampInFlight;

		public static bool IsBusy => _stampInFlight;

		/// <summary>
		/// Places a starting area for <paramref name="owner"/> if a spot can be found.
		/// </summary>
		/// <returns>False when nothing was started, for any reason.</returns>
		public static bool TryPlaceFor(PlayerId owner)
		{
			using var _ = Profiler.Scope();

			if (_stampInFlight)
				return false;

			if (!owner.IsValid)
				return false;

			try
			{
				var existingPods = ExistingPodPositions();
				if (existingPods.Count == 0)
				{
					// Nothing to measure against, and no way to tell which world to build on.
					DebugConsole.LogWarning("[StartingArea] No existing pod to place relative to; skipping.");
					return false;
				}

				var template = LoadStartingTemplate(out string templatePath);
				if (template == null)
					return false;

				if (!TryFindSpot(template, existingPods, out var spot))
				{
					DebugConsole.LogWarning("[StartingArea] No usable location found for an extra starting area.");
					return false;
				}

				_stampInFlight = true;

				// Hand the owner to the Telepad patch rather than reassigning afterwards, so the pod is
				// never briefly owned by the wrong player.
				TelepadOwnershipPatch.NextPodOwner = owner;

				DebugConsole.Log($"[StartingArea] Stamping '{template.name}' at {spot} for {owner}.");

				TemplateLoader.Stamp(template, new Vector2(spot.x, spot.y), () =>
				{
					_stampInFlight = false;
					TelepadOwnershipPatch.NextPodOwner = PlayerId.None;
					RevealArea(template, spot);
					SpawnStartingCrew(spot, owner);
					DebugConsole.Log($"[StartingArea] Stamp complete at {spot} for {owner}.");
				});

				// Clients hold the world as it was when they were sent the save, so a stamp made now
				// exists only here. Rather than resend the whole world, they are told to stamp the same
				// template at the same place: the template is game data both sides already have, and
				// NetIds are derived from position, so the two results agree.
				PacketSender.SendToAllClients(new Packets.World.StartingAreaStampPacket
				{
					TemplatePath = templatePath,
					X = spot.x,
					Y = spot.y,
				});

				return true;
			}
			catch (System.Exception ex)
			{
				_stampInFlight = false;
				TelepadOwnershipPatch.NextPodOwner = PlayerId.None;
				DebugConsole.LogWarning($"[StartingArea] Failed to place a starting area: {ex}");
				return false;
			}
		}

		/// <summary>
		/// The starting base template this world was generated with.
		/// </summary>
		/// <summary>
		/// Reproduces a stamp the host made, for a client whose world predates it.
		/// </summary>
		public static void StampFromNetwork(string templatePath, int x, int y)
		{
			using var _ = Profiler.Scope();

			try
			{
				var template = TemplateCache.GetTemplate(templatePath);
				if (template == null)
				{
					DebugConsole.LogWarning($"[StartingArea] Host stamped '{templatePath}' but it could not be loaded here.");
					return;
				}

				var spot = new Vector2I(x, y);
				DebugConsole.Log($"[StartingArea] Reproducing host stamp of '{templatePath}' at {spot}.");

				TemplateLoader.Stamp(template, new Vector2(x, y), () =>
				{
					RevealArea(template, spot);
					DebugConsole.Log($"[StartingArea] Reproduced stamp complete at {spot}.");
				});
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning($"[StartingArea] Failed to reproduce the host's stamp: {ex}");
			}
		}

		/// <summary>
		/// Gives a newly stamped pod a crew, since the template does not carry one.
		/// </summary>
		/// <remarks>
		/// A world's opening duplicants are spawned by game start logic, not by the starting base
		/// template, so a stamped area arrives empty. Delivered through the same path as printing -
		/// with the pod in scope - so they inherit its owner and reach clients by the entity spawn
		/// packet that already exists, rather than needing anything new.
		/// </remarks>
		private static void SpawnStartingCrew(Vector2I spot, PlayerId owner)
		{
			try
			{
				var pod = FindPodOwnedBy(owner);
				if (pod == null)
				{
					DebugConsole.LogWarning($"[StartingArea] No pod found for {owner}; it will have no crew.");
					return;
				}

				var position = pod.transform.GetPosition();

				for (int i = 0; i < CrewSize; i++)
				{
					var stats = new MinionStartingStats(is_starter_minion: true);

					using (PrintingPodContext.Scope(pod))
					{
						stats.Deliver(position);
					}
				}

				DebugConsole.Log($"[StartingArea] Delivered {CrewSize} duplicants to {owner}'s new pod.");
			}
			catch (System.Exception ex)
			{
				DebugConsole.LogWarning($"[StartingArea] Placed the area but could not deliver a crew: {ex}");
			}
		}

		private static Telepad FindPodOwnedBy(PlayerId owner)
		{
			foreach (var telepad in global::Components.Telepads.Items)
			{
				if (telepad == null) continue;

				if (telepad.TryGetComponent<OwnershipComponent>(out var ownership)
					&& ownership.HasOwner
					&& ownership.Owner == owner)
				{
					return telepad;
				}
			}

			return null;
		}

		private static TemplateContainer LoadStartingTemplate(out string templatePath)
		{
			templatePath = null;
			var cluster = ClusterManager.Instance;
			var world = cluster != null ? cluster.activeWorld : null;
			if (world == null)
			{
				DebugConsole.LogWarning("[StartingArea] No active world.");
				return null;
			}

			var definition = ResolveWorldDefinition(world);
			if (definition == null)
				return null;

			string path = definition.startingBaseTemplate;
			if (string.IsNullOrEmpty(path))
			{
				DebugConsole.LogWarning($"[StartingArea] World '{definition.filePath}' declares no starting base template.");
				return null;
			}

			var template = TemplateCache.GetTemplate(path);
			if (template == null)
			{
				DebugConsole.LogWarning($"[StartingArea] Template '{path}' could not be loaded.");
				return null;
			}

			templatePath = path;
			return template;
		}

		/// <summary>
		/// Lifts the fog over a freshly stamped area so it can actually be seen.
		/// </summary>
		/// <remarks>
		/// Stamping places terrain and buildings but reveals nothing. In a normal game the starting
		/// area is visible because duplicants spawn standing in it and their GridVisibility reveals
		/// what surrounds them; a stamped area has nobody in it, so it stays under fog and looks like
		/// it was never created.
		/// <para>
		/// GridVisibility.Reveal only fades in the visibility values. Grid.Revealed and the fog mask
		/// are maintained by its caller, so both are done here too - otherwise the area brightens but
		/// the fog sheet stays drawn over it.
		/// </para>
		/// </remarks>
		private static void RevealArea(TemplateContainer template, Vector2I spot)
		{
			try
			{
				var bounds = template.GetTemplateBounds(new Vector2(spot.x, spot.y), Padding);

				int centreX = bounds.xMin + bounds.width / 2;
				int centreY = bounds.yMin + bounds.height / 2;
				int radius = Mathf.Max(bounds.width, bounds.height) / 2 + Padding;

				// innerRadius must stay below radius; the falloff divides by the difference.
				GridVisibility.Reveal(centreX, centreY, radius, Mathf.Max(1f, radius - 2f));

				for (int y = bounds.yMin; y <= bounds.yMax; y++)
				{
					for (int x = bounds.xMin; x <= bounds.xMax; x++)
					{
						int cell = Grid.XYToCell(x, y);
						if (!Grid.IsValidCell(cell)) continue;

						Grid.Revealed[cell] = true;
						FogOfWarMask.ClearMask(cell);
					}
				}
			}
			catch (System.Exception ex)
			{
				// The area exists either way; not seeing it is better than losing it.
				DebugConsole.LogWarning($"[StartingArea] Placed the area but could not reveal it: {ex}");
			}
		}

		/// <summary>
		/// Finds the generation-time definition behind a loaded world.
		/// </summary>
		/// <remarks>
		/// Harder than it looks. WorldContainer.worldType is not the key the world was loaded under -
		/// it holds a localisation key such as STRINGS.WORLDS.SANDSTONE_DEFAULT.NAME, which is what
		/// ProcGen.World exposes as its name. The key is the file path, held separately in filePath.
		/// <para>
		/// So the display name is matched back to a definition, with direct lookups tried first in
		/// case some worlds do store a path there. On failure the candidates are logged, because
		/// guessing a second time from the same absence of information is not worth the round trip.
		/// </para>
		/// </remarks>
		private static ProcGen.World ResolveWorldDefinition(WorldContainer world)
		{
			foreach (string key in new[] { world.worldType, world.worldName })
			{
				if (!string.IsNullOrEmpty(key) && SettingsCache.worlds.HasWorld(key))
					return SettingsCache.worlds.GetWorldData(key);
			}

			var names = SettingsCache.worlds.GetNames();

			foreach (string key in names)
			{
				var candidate = SettingsCache.worlds.GetWorldData(key);
				if (candidate == null) continue;

				if (candidate.name == world.worldType || candidate.name == world.worldName)
					return candidate;
			}

			DebugConsole.LogWarning(
				$"[StartingArea] Could not match world (worldType='{world.worldType}', worldName='{world.worldName}') " +
				$"to any of {names.Count} known definitions. First few: {string.Join(", ", names.GetRange(0, System.Math.Min(5, names.Count)))}");

			return null;
		}

		private static List<Vector2I> ExistingPodPositions()
		{
			var positions = new List<Vector2I>();

			foreach (var telepad in global::Components.Telepads.Items)
			{
				if (telepad == null) continue;

				int cell = Grid.PosToCell(telepad);
				if (!Grid.IsValidCell(cell)) continue;

				Grid.CellToXY(cell, out int x, out int y);
				positions.Add(new Vector2I(x, y));
			}

			return positions;
		}

		/// <summary>
		/// Walks outward from the first pod along the same horizontal band.
		/// </summary>
		/// <remarks>
		/// Staying at the same depth keeps the new area in comparable terrain; moving vertically would
		/// drop it into a different biome, or into space. Alternating left and right keeps it as close
		/// to the middle of the map as the spacing allows.
		/// <para>
		/// Crude on purpose. Vanilla picks this spot from a worldgen graph that a loaded world no
		/// longer has, so there is nothing to reuse - and a first version that is easy to reason about
		/// is worth more than a clever one whose failures are hard to read.
		/// </para>
		/// </remarks>
		private static bool TryFindSpot(TemplateContainer template, List<Vector2I> existingPods, out Vector2I spot)
		{
			spot = default;

			var origin = existingPods[0];
			var occupied = new List<RectInt>();
			foreach (var pod in existingPods)
				occupied.Add(template.GetTemplateBounds(new Vector2(pod.x, pod.y), Padding));

			for (int step = 1; step <= MaxSearchSteps; step++)
			{
				foreach (int direction in new[] { -1, 1 })
				{
					var candidate = new Vector2I(origin.x + direction * step * SearchStep, origin.y);
					if (!IsUsable(template, candidate, occupied))
						continue;

					spot = candidate;
					return true;
				}
			}

			return false;
		}

		private static bool IsUsable(TemplateContainer template, Vector2I candidate, List<RectInt> occupied)
		{
			var bounds = template.GetTemplateBounds(new Vector2(candidate.x, candidate.y), Padding);

			if (bounds.xMin < EdgeMargin || bounds.xMax >= Grid.WidthInCells - EdgeMargin)
				return false;

			if (bounds.yMin < EdgeMargin || bounds.yMax >= Grid.HeightInCells - EdgeMargin)
				return false;

			foreach (var taken in occupied)
			{
				if (bounds.Overlaps(taken))
					return false;
			}

			return true;
		}
	}
}
