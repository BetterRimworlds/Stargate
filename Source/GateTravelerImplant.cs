// ==== Source/GateTravelerImplant.cs ====
using System.Collections.Generic;
using Enhanced_Development.Stargate.Saving;
using RimWorld;
using UnityEngine;
using Verse;

namespace BetterRimworlds.Stargate;

[DefOf]
public static class StargateHediffDefOf
{
    public static HediffDef GateTravelerImplant;

    // Static constructor is required so RimWorld initializes this DefOf.
    static StargateHediffDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(StargateHediffDefOf));
    }
}

/// Additional Gate Traveler systems, such as pawn-carried research memory,
/// live in partial class files under their own feature directories.
public partial class GateTravelerImplant : Hediff_Implant
{
    public List<StargateRelation> relationships = new List<StargateRelation>();

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Collections.Look(
            ref this.relationships,
            "relationships",
            LookMode.Deep
        );

        this.ExposeResearchData();

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            this.relationships ??= new List<StargateRelation>();
            this.EnsureResearchMemoryInitialized();
        }
    }

    public override void PostMake()
    {
        base.PostMake();

        this.relationships ??= new List<StargateRelation>();
        this.EnsureResearchMemoryInitialized();

        this.RefreshRelationships();
    }

    public void RefreshRelationshipsForStargateEntry()
    {
        this.RefreshRelationships();
    }

    /// <summary>
    /// Legacy buffer-entry hook retained for callers still using the old name.
    /// Age tracking now belongs to CryoRegenesis; this only refreshes relationships.
    /// </summary>
    public void RecordStargateBufferEntry()
    {
        this.RefreshRelationshipsForStargateEntry();
    }

    private void RefreshRelationships()
    {
        var snapshot = new List<StargateRelation>();
        var directRelations = this.pawn?.relations?.DirectRelations;

        if (directRelations != null)
        {
            foreach (DirectPawnRelation rel in directRelations)
            {
                if (rel == null || rel.def == null || rel.otherPawn == null)
                {
                    continue;
                }

                snapshot.Add(new StargateRelation(
                    rel.otherPawn,
                    rel.def.defName,
                    rel
                ));
            }
        }

        // Publish only a fully built snapshot, so stale or ended relationships
        // are removed when the pawn re-enters the Stargate buffer.
        this.relationships = snapshot;
    }
}
