// ==== Source/Stargate.cs ====
using System.IO;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class Stargate : Mod
    {
        private static ModContentPack contentPack;

        /// Savegame-scoped: once true, packaged SG-1 / scenario-seed teams are never offered again
        /// on any map or stargate. Scribed by <see cref="GameComponent_Stargate"/>.
        internal static bool scenarioSeedTeamDisabled = false;

        public Stargate(ModContentPack content) : base(content)
        {
            contentPack = content;
        }

        public static string ModRootPath()
        {
            return contentPack.RootDir;
        }

        /// Default player off-world buffer path under the RimWorld save-data folder.
        public static string DefaultPlayerBufferFilePath()
        {
            return Path.Combine(GenFilePaths.SaveDataFolderPath, "Stargate", "Stargate.xml");
        }

        /// Whether any stargate in this save may still recall the packaged scenario-seed team.
        /// Always false when a player <c>Stargate.xml</c> exists (and that permanently disables the seed).
        public static bool IsScenarioSeedTeamAllowed()
        {
            if (scenarioSeedTeamDisabled)
            {
                return false;
            }

            // Existing player buffer always wins and forever retires the packaged SG-1 seed for this save.
            if (File.Exists(DefaultPlayerBufferFilePath()))
            {
                DisableScenarioSeedTeam("player Stargate.xml is present");
                return false;
            }

            return StargateScenarioUtility.IsStargateBaseScenario();
        }

        /// Permanently disable packaged scenario-seed teams for the current savegame
        /// (all maps / all stargates). Safe to call repeatedly.
        public static void DisableScenarioSeedTeam(string reason = null)
        {
            if (scenarioSeedTeamDisabled)
            {
                return;
            }

            scenarioSeedTeamDisabled = true;

            if (string.IsNullOrEmpty(reason))
            {
                Log.Message("[Stargate] Packaged scenario-seed Stargate team disabled for this savegame.");
            }
            else
            {
                Log.Message("[Stargate] Packaged scenario-seed Stargate team disabled for this savegame (" + reason + ").");
            }
        }
    }

    /// Persists <see cref="Stargate.scenarioSeedTeamDisabled"/> with the save and resets it on new games.
    public class GameComponent_Stargate : GameComponent
    {
        public override void StartedNewGame()
        {
            // Static flag can linger from a previous game in the same process.
            Stargate.scenarioSeedTeamDisabled = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref Stargate.scenarioSeedTeamDisabled, "scenarioSeedTeamDisabled", false);
        }
    }
}
