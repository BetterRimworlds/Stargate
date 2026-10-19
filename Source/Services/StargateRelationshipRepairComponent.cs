// ==== Source/Services/StargateRelationshipRepairComponent.cs ====
using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate.Services
{
    /// Repairs relationship trackers that were damaged by older Stargate builds.
    /// The repair runs once per save after loading, using the GateTravelerImplant
    /// snapshots as the source of truth.
    public class StargateRelationshipRepairComponent : GameComponent
    {
        private const int CurrentRepairVersion = 1;

        private int repairVersion = 0;

        public StargateRelationshipRepairComponent(Game game)
        {
        }

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Values.Look(ref repairVersion, "stargateRelationshipRepairVersion", 0);
        }

        public override void LoadedGame()
        {
            base.LoadedGame();

            if (repairVersion >= CurrentRepairVersion)
            {
                return;
            }

            Log.Message("[Stargate] Repairing relationships damaged by an older build.");
            StargatePawnService.RebuildRelationships();
            repairVersion = CurrentRepairVersion;
        }
    }
}
