using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Locations;

namespace GusIslandVisitor
{
    /// <summary>Guarantees Gus is among the Ginger Island resort visitors whenever visitors are chosen.</summary>
    public class ModEntry : Mod
    {
        private const string GusName = "Gus";

        internal static IMonitor Log = null!;

        /// <summary>While re-running the vanilla selection, the only NPCs allowed to be picked.</summary>
        private static HashSet<string>? Allowed;
        private static bool Rerunning;

        public override void Entry(IModHelper helper)
        {
            Log = this.Monitor;
            var harmony = new Harmony(this.ModManifest.UniqueID);
            try
            {
                harmony.Patch(
                    original: AccessTools.Method(typeof(IslandSouth), nameof(IslandSouth.SetupIslandSchedules)),
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_SetupIslandSchedules), null) { priority = Priority.Last });
                harmony.Patch(
                    original: AccessTools.Method(typeof(IslandSouth), nameof(IslandSouth.CanVisitIslandToday)),
                    postfix: new HarmonyMethod(typeof(ModEntry), nameof(After_CanVisitIslandToday), null) { priority = Priority.Last });
            }
            catch (Exception ex)
            {
                Log.Log($"Failed to apply patches; the mod will do nothing.\n{ex}", LogLevel.Error);
            }
        }

        /// <summary>Restricts the vanilla candidate pool during our re-run.</summary>
        private static void After_CanVisitIslandToday(NPC npc, ref bool __result)
        {
            if (Allowed != null && __result && !Allowed.Contains(npc.Name))
                __result = false;
        }

        /// <summary>
        /// After vanilla picks visitors: if it's a visit day and Gus wasn't picked, swap one random visitor
        /// for Gus by re-running the vanilla selection restricted to (old visitors - one + Gus). This reuses
        /// all vanilla schedule/activity code, so Gus gets his normal resort-bar schedule.
        /// </summary>
        private static void After_SetupIslandSchedules()
        {
            if (Rerunning || !Game1.IsMasterGame)
                return;

            try
            {
                ISet<string> visitors = Game1.netWorldState.Value.IslandVisitors;
                if (visitors.Count == 0 || visitors.Contains(GusName))
                    return; // no island visit today, or Gus already chosen

                NPC? gus = Game1.getCharacterFromName(GusName);
                if (gus == null || !IslandSouth.CanVisitIslandToday(gus))
                    return;

                var original = new HashSet<string>(visitors);
                var pool = new HashSet<string>(original) { GusName };
                var rng = Utility.CreateRandom(Game1.uniqueIDForThisGame * 1.7, Game1.stats.DaysPlayed * 3.1);
                var removed = new List<string>();

                // Never bump a player's spouse. Names are sorted so the result is deterministic per day.
                bool CanBump(string name) => name != GusName && !Game1.getAllFarmers().Any(f => f.spouse == name);
                bool BumpOne()
                {
                    var candidates = pool.Where(CanBump).OrderBy(n => n, StringComparer.Ordinal).ToList();
                    if (candidates.Count == 0)
                        return false;
                    string name = candidates[rng.Next(candidates.Count)];
                    pool.Remove(name);
                    removed.Add(name);
                    return true;
                }

                // Vanilla targets 5 visitors; make room for Gus.
                if (pool.Count > 5)
                    BumpOne();

                // Vanilla's random fill can pick fewer NPCs than the pool holds, and the roll is seeded per day so
                // an identical re-run gives an identical result. If Gus is missed, shrink the pool and retry
                // (a pool of 3 or fewer is always picked in full).
                try
                {
                    Rerunning = true;
                    while (true)
                    {
                        Allowed = pool;
                        IslandSouth.SetupIslandSchedules();
                        if (visitors.Contains(GusName) || !BumpOne())
                            break;
                    }
                }
                finally
                {
                    Allowed = null;
                    Rerunning = false;
                }

                // Return anyone who was dropped (or picked only during an earlier attempt) to their normal schedule.
                foreach (string name in original.Where(n => n != GusName && !visitors.Contains(n)))
                {
                    NPC? bumped = Game1.getCharacterFromName(name);
                    if (bumped != null)
                    {
                        bumped.islandScheduleName.Value = null;
                        bumped.scheduleDelaySeconds = 0f;
                        bumped.TryLoadSchedule();
                    }
                }

                if (!visitors.Contains(GusName))
                    Log.Log("Re-ran island selection but Gus was not picked; vanilla result may have changed.", LogLevel.Warn);
                else
                    Log.Log($"Gus is visiting the island today (replacing: {(removed.Count > 0 ? string.Join(", ", removed) : "nobody")}).", LogLevel.Debug);
            }
            catch (Exception ex)
            {
                Allowed = null;
                Rerunning = false;
                Log.Log($"Failed forcing Gus onto the island visitor list.\n{ex}", LogLevel.Error);
            }
        }
    }
}
