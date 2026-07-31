using System.Text;
using Verse;

namespace BetterRimworlds.Stargate;

public class MissingStargateHediff : HediffWithComps
{
    private const string UnknownMod = "unknown mod";

    public string OriginalHediffLabel;
    public string OriginalHediffModName;
    public string OriginalHediffPackageId;
    public string OriginalHediffDefName;
    public string OriginalHediffClass;
    public string OriginalHediffXml;

    public override string LabelBase
    {
        get
        {
            return this.OriginalHediffLabel.NullOrEmpty()
                ? "missing hediff"
                : "missing hediff: " + this.OriginalHediffLabel;
        }
    }

    public override string LabelInBrackets
    {
        get
        {
            string modName = this.OriginalHediffModName;
            if (modName.NullOrEmpty())
            {
                modName = UnknownMod;
            }

            return modName;
        }
    }

    private string MissingDescription
    {
        get
        {
            var description = new StringBuilder();
            description.Append("This pawn arrived through the Stargate with a health condition from a mod that is not installed in this RimWorld instance.");

            if (!this.OriginalHediffModName.NullOrEmpty())
            {
                description.AppendLine();
                description.AppendLine();
                description.Append("Source mod: ");
                description.Append(this.OriginalHediffModName);
            }

            if (!this.OriginalHediffPackageId.NullOrEmpty())
            {
                description.AppendLine();
                description.Append("Package id: ");
                description.Append(this.OriginalHediffPackageId);
            }

            return description.ToString();
        }
    }

    public override string TipStringExtra
    {
        get
        {
            string baseTip = base.TipStringExtra;
            string description = this.MissingDescription;

            if (baseTip.NullOrEmpty())
            {
                return description;
            }

            return baseTip + "\n" + description;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();

        Scribe_Values.Look(ref this.OriginalHediffLabel, "originalHediffLabel");
        Scribe_Values.Look(ref this.OriginalHediffModName, "originalHediffModName");
        Scribe_Values.Look(ref this.OriginalHediffPackageId, "originalHediffPackageId");
        Scribe_Values.Look(ref this.OriginalHediffDefName, "originalHediffDefName");
        Scribe_Values.Look(ref this.OriginalHediffClass, "originalHediffClass");
        Scribe_Values.Look(ref this.OriginalHediffXml, "originalHediffXml");
    }
}
