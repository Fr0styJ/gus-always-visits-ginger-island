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

                var keep = new HashSet<string>(visitors) { GusName };
                string? victim = null;

                if (visitors.Count >= 5)
                {
                    // never bump a player's spouse; deterministic per day
                    var candidates = visitors
                        .Where(name => !Game1.getAllFarmers().Any(f => f.spouse == name))
                        .OrderBy(name => name, StringComparer.Ordinal)
                        .ToList();
                    if (candidates.Count == 0)
                        return;
                    var rng = Utility.CreateRandom(Game1.uniqueIDForThisGame * 1.7, Game1.stats.DaysPlayed * 3.1);
                    victim = candidates[rng.Next(candidates.Count)];
                    keep.Remove(victim);
                }

                Allowed = keep;
                Rerunning = true;
                try
                {
                    IslandSouth.SetupIslandSchedules();
                }
                finally
                {
                    Allowed = null;
                    Rerunning = false;
                }

                if (victim != null && !visitors.Contains(victim))
                {
                    NPC? bumped = Game1.getCharacterFromName(victim);
                    if (bumped != null)
                    {
                        bumped.islandScheduleName.Value = null;
                        bumped.scheduleDelaySeconds = 0f;
                        bumped.TryLoadSchedule(); // back to their normal schedule
                    }
                }

                if (!visitors.Contains(GusName))
                    Log.Log("Re-ran island selection but Gus was not picked; vanilla result may have changed.", LogLevel.Warn);
                else
                    Log.Log($"Gus is visiting the island today{(victim != null ? $" (replacing {victim})" : "")}.", LogLevel.Debug);
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
