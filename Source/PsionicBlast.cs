using RimWorld;
using Verse;

namespace BetterRimworlds.Stargate
{
    public class PsionicBlast
    {
        public void CauseHeartAttack(Pawn pawn)
        {
            HediffDef heartAttack = HediffDef.Named("HeartAttack");

            BodyPartRecord heart = pawn.RaceProps.body.AllParts.Find(bpr => bpr.def.defName == "Heart");

            pawn.health.AddHediff(heartAttack, heart, null);
        }

        private static void CauseSedation(Pawn pawn)
        {
            pawn.health.AddHediff(HediffDefOf.Anesthetic, null, null);
        }

        private void CauseDisorientation(Pawn pawn)
        {
            pawn.health.AddHediff(HediffDefOf.PsychicHangover, null, null);
            pawn.health.AddHediff(HediffDefOf.CryptosleepSickness, null, null);
        }

        private static void CauseCatatonia(Pawn pawn)
        {
            pawn.health.AddHediff(HediffDefOf.CatatonicBreakdown, null, null);
        }

        public void AddPsionicShock(Pawn pawn)
        {
            System.Random rand = new System.Random();

            int psychicSensitivity = 0;
            bool? shouldGiveHeartAttack = null;
            bool? shouldSedate = null;

            if (pawn.story?.traits != null && pawn.story.traits.HasTrait(TraitDef.Named("PsychicSensitivity")))
            {
                Trait psychicSensitivityTrait = pawn.story.traits.GetTrait(TraitDef.Named("PsychicSensitivity"));
                psychicSensitivity = psychicSensitivityTrait.Degree;
            }

            // If they're Psychically Deaf, do nothing:
            if (psychicSensitivity == -2)
            {
                return;
            }
            // If they're Psychically Dull, don't give them a heart attack.
            else if (psychicSensitivity == -1)
            {
                shouldGiveHeartAttack = false;
                CauseDisorientation(pawn);
            }
            // If they're Psychically Sensitive, make sure they're passed out for a few hours.
            else if (psychicSensitivity == 1)
            {
                shouldSedate = true;
            }
            // If they're Psychically Hypersensitive, unfortunately, it will mean instant death or catatonia :-(
            else if (psychicSensitivity >= 2)
            {
                bool shouldKill = rand.Next(1, 13) >= 6;
                if (shouldKill == true)
                {
                    if (pawn.IsColonist)
                    {
                        Messages.Message(pawn.Name.ToStringShort + " was psychically supersensitive and died because of the psionic blast.", MessageTypeDefOf.ThreatSmall);
                    }

                    HealthUtility.DamageUntilDead(pawn);
                }
                else
                {
                    CauseCatatonia(pawn);
                }
            }

            // If they have a Bionic Heart, then skip but give them bad food poisoning instead.
            // bool hasBionicHeart = pawn.RaceProps.body.AllParts.Exists(bpr =>
            // {
            //     return bpr.def.defName == "Heart" && (
            //         bpr.def.label.Contains("bionic") ||
            //         bpr.def.label.Contains("arcotech")
            //     );
            // });
            bool hasBionicHeart =
                pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamedSilentFail("BionicHeart"));
            if (pawn.IsColonist)
            {
                Log.Error("Does " + pawn.Name + " have a bionic heart? " + hasBionicHeart);
            }

            if (shouldGiveHeartAttack != false && hasBionicHeart)
            {
                shouldGiveHeartAttack = false;
            }

            Hediff shock = HediffMaker.MakeHediff(HediffDefOf.PsychicShock, pawn, null);
            pawn.health.AddHediff(shock, null, null);

            if (shouldGiveHeartAttack == null)
            {
                shouldGiveHeartAttack = rand.Next(1, 11) >= 3;
            }

            if (shouldGiveHeartAttack == true)
            {
                this.CauseHeartAttack(pawn);
            }

            if (shouldSedate == null)
            {
                int likelihood = rand.Next(1, 11);
                shouldSedate = likelihood >= 6;
            }

            if (shouldSedate == true)
            {
                CauseSedation(pawn);
            }

            DamageInfo psionicIntensity = new DamageInfo(DamageDefOf.Stun, 50);
            pawn.TakeDamage(psionicIntensity);
        }

    }
}
