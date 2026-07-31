using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using HarmonyLib;
using Verse;

namespace Enhanced_Development.Stargate.Saving;

internal static class StargateHediffXmlCompatibility
{
    private static readonly HashSet<string> PreparedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private const string MissingHediffDefName = "StargateMissingHediff";
    private const string MissingHediffClassName = "BetterRimworlds.Stargate.MissingStargateHediff";

    private const string OriginalLabelNodeName = "originalHediffLabel";
    private const string OriginalModNodeName = "originalHediffModName";
    private const string OriginalPackageNodeName = "originalHediffPackageId";
    private const string OriginalDefNodeName = "originalHediffDefName";
    private const string OriginalClassNodeName = "originalHediffClass";
    private const string OriginalXmlNodeName = "originalHediffXml";
    private const string LegacyWakeUpRibPackageId = "hopeseekr.betterrimworlds.wakeupimplant";

    public static void AnnotateHediffSources(XmlDocument doc)
    {
        XmlNodeList hediffNodes = doc.SelectNodes("//healthTracker/hediffSet/hediffs/li");
        if (hediffNodes == null)
        {
            return;
        }

        foreach (XmlNode hediffNode in hediffNodes)
        {
            XmlNode defNode = hediffNode.SelectSingleNode("def");
            string defName = defNode?.InnerText;
            if (defName.NullOrEmpty() || IsMissingPlaceholder(hediffNode, defName))
            {
                continue;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef == null)
            {
                continue;
            }

            UpsertChildText(doc, hediffNode, OriginalLabelNodeName, hediffDef.label);
            UpsertChildText(doc, hediffNode, OriginalDefNodeName, defName);

            string serializedClass = hediffNode.Attributes?["Class"]?.Value;
            if (!serializedClass.NullOrEmpty())
            {
                UpsertChildText(doc, hediffNode, OriginalClassNodeName, serializedClass);
            }

            if (hediffDef.modContentPack != null)
            {
                UpsertChildText(doc, hediffNode, OriginalModNodeName, hediffDef.modContentPack.Name);
                UpsertChildText(doc, hediffNode, OriginalPackageNodeName, hediffDef.modContentPack.PackageId);
            }
        }
    }

    public static string PrepareLoadFile(string fileLocation)
    {
        XmlDocument doc = new XmlDocument();
        doc.Load(fileLocation);

        bool changedAny = RestoreAvailableHediffs(doc);
        changedAny |= ReplaceMissingHediffs(doc);
        if (!changedAny)
        {
            return fileLocation;
        }

        string tempFile = Path.Combine(
            Path.GetTempPath(),
            "Stargate-" + Guid.NewGuid().ToString("N") + ".xml"
        );

        PreparedFiles.Add(tempFile);
        try
        {
            doc.Save(tempFile);
        }
        catch
        {
            try
            {
                File.Delete(tempFile);
            }
            catch
            {
                // Keep the path tracked so a later cleanup attempt can retry it.
            }

            throw;
        }
        return tempFile;
    }

    internal static bool IsPreparedFile(string path) => PreparedFiles.Contains(path);

    internal static void CleanupPreparedFiles()
    {
        foreach (string path in new List<string>(PreparedFiles))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e)
            {
                Log.Warning("Could not delete temporary Stargate load file: " + e.Message);
            }

            if (!File.Exists(path))
            {
                PreparedFiles.Remove(path);
            }
        }
    }

    public static void DeletePreparedLoadFile(string originalFileLocation, string preparedFileLocation)
    {
        if (string.Equals(originalFileLocation, preparedFileLocation, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            File.Delete(preparedFileLocation);
        }
        catch (Exception e)
        {
            Log.Warning("Could not delete temporary Stargate load file: " + e.Message);
        }
        finally
        {
            if (!File.Exists(preparedFileLocation))
            {
                PreparedFiles.Remove(preparedFileLocation);
            }
        }
    }

    private static bool ReplaceMissingHediffs(XmlDocument doc)
    {
        XmlNodeList hediffNodes = doc.SelectNodes("//healthTracker/hediffSet/hediffs/li");
        if (hediffNodes == null)
        {
            return false;
        }

        bool replacedAny = false;
        foreach (XmlNode hediffNode in hediffNodes)
        {
            XmlNode defNode = hediffNode.SelectSingleNode("def");
            string defName = defNode?.InnerText;
            if (defName.NullOrEmpty() || IsMissingPlaceholder(hediffNode, defName))
            {
                continue;
            }

            if (DefDatabase<HediffDef>.GetNamedSilentFail(defName) != null)
            {
                continue;
            }

            ReplaceMissingHediff(doc, hediffNode, defNode, defName);
            replacedAny = true;
        }

        return replacedAny;
    }

    private static void ReplaceMissingHediff(
        XmlDocument doc,
        XmlNode hediffNode,
        XmlNode defNode,
        string missingDefName
    )
    {
        // Archive the untouched node once so subclass fields and comp XML survive a round trip.
        if (ReadChildText(hediffNode, OriginalXmlNodeName).NullOrEmpty())
        {
            UpsertChildText(doc, hediffNode, OriginalXmlNodeName, hediffNode.OuterXml);
        }
        string originalLabel = ReadChildText(hediffNode, OriginalLabelNodeName);
        string originalModName = ReadChildText(hediffNode, OriginalModNodeName);
        string originalPackageId = ReadChildText(hediffNode, OriginalPackageNodeName);
        string serializedClass = hediffNode.Attributes?["Class"]?.Value;

        ApplyHardcodedMissingHediffMetadata(
            missingDefName,
            ref originalLabel,
            ref originalModName,
            ref originalPackageId
        );

        UpsertChildText(doc, hediffNode, OriginalDefNodeName, missingDefName);
        if (!serializedClass.NullOrEmpty() && serializedClass != MissingHediffClassName)
        {
            UpsertChildText(doc, hediffNode, OriginalClassNodeName, serializedClass);
        }

        SetClassAttribute(doc, hediffNode, MissingHediffClassName);

        if (!originalLabel.NullOrEmpty())
        {
            UpsertChildText(doc, hediffNode, OriginalLabelNodeName, originalLabel);
        }
        if (!originalModName.NullOrEmpty())
        {
            UpsertChildText(doc, hediffNode, OriginalModNodeName, originalModName);
        }
        if (!originalPackageId.NullOrEmpty())
        {
            UpsertChildText(doc, hediffNode, OriginalPackageNodeName, originalPackageId);
        }

        defNode.InnerText = MissingHediffDefName;

        Log.Warning(
            "Replaced missing Stargate hediff def " + missingDefName +
            " with " + MissingHediffDefName +
            (originalModName.NullOrEmpty() ? "." : " from mod " + originalModName + ".")
        );
    }

    private static bool RestoreAvailableHediffs(XmlDocument doc)
    {
        XmlNodeList hediffNodes = doc.SelectNodes("//healthTracker/hediffSet/hediffs/li");
        if (hediffNodes == null)
        {
            return false;
        }

        bool restoredAny = false;
        foreach (XmlNode candidateNode in hediffNodes)
        {
            XmlNode hediffNode = candidateNode;
            XmlNode defNode = hediffNode.SelectSingleNode("def");
            string currentDefName = defNode?.InnerText;
            if (!IsMissingPlaceholder(hediffNode, currentDefName))
            {
                continue;
            }

            if (defNode == null)
            {
                continue;
            }

            string originalDefName = ReadChildText(hediffNode, OriginalDefNodeName);
            string originalLabel = ReadChildText(hediffNode, OriginalLabelNodeName);
            string originalModName = ReadChildText(hediffNode, OriginalModNodeName);
            string packageId = ReadChildText(hediffNode, OriginalPackageNodeName);
            string originalClass = ReadChildText(hediffNode, OriginalClassNodeName);
            if (originalDefName.NullOrEmpty() && packageId == LegacyWakeUpRibPackageId)
            {
                originalDefName = "WakeUpRib";
                UpsertChildText(doc, hediffNode, OriginalDefNodeName, originalDefName);
            }

            HediffDef resolvedDef = originalDefName.NullOrEmpty() ? null : DefDatabase<HediffDef>.GetNamedSilentFail(originalDefName);
            if (resolvedDef == null ||
                (!packageId.NullOrEmpty() && (resolvedDef.modContentPack == null ||
                    !string.Equals(resolvedDef.modContentPack.PackageId, packageId, StringComparison.OrdinalIgnoreCase))))
            {
                continue;
            }

            defNode.InnerText = originalDefName;
            string archivedXml = ReadChildText(hediffNode, OriginalXmlNodeName);
            XmlNode restoredNode = TryImportArchivedNode(doc, archivedXml);
            if (restoredNode != null)
            {
                OverlayMutableChildren(doc, hediffNode, restoredNode);
                hediffNode.ParentNode?.ReplaceChild(restoredNode, hediffNode);
                hediffNode = restoredNode;
                defNode = hediffNode.SelectSingleNode("def");
                defNode.InnerText = originalDefName;
                UpsertChildText(doc, hediffNode, OriginalDefNodeName, originalDefName);
                UpsertChildText(doc, hediffNode, OriginalLabelNodeName, originalLabel);
                UpsertChildText(doc, hediffNode, OriginalModNodeName, originalModName);
                UpsertChildText(doc, hediffNode, OriginalPackageNodeName, packageId);
                UpsertChildText(doc, hediffNode, OriginalClassNodeName, originalClass);
                UpsertChildText(doc, hediffNode, OriginalXmlNodeName, archivedXml);
            }
            if (resolvedDef.hediffClass != null && !resolvedDef.hediffClass.FullName.NullOrEmpty())
            {
                SetClassAttribute(doc, hediffNode, resolvedDef.hediffClass.FullName);
            }
            else if (IsSafeRecordedClass(originalClass))
            {
                SetClassAttribute(doc, hediffNode, originalClass);
            }
            else
            {
                hediffNode.Attributes?.RemoveNamedItem("Class");
            }

            restoredAny = true;
            Log.Message("Prepared restoration of Stargate hediff " + originalDefName + " from missing placeholder.");
        }

        return restoredAny;
    }

    private static XmlNode TryImportArchivedNode(XmlDocument target, string archivedXml)
    {
        if (archivedXml.NullOrEmpty()) return null;
        try
        {
            XmlDocument archive = new XmlDocument { XmlResolver = null };
            archive.LoadXml(archivedXml);
            if (archive.DocumentElement == null || archive.DocumentElement.Name != "li" ||
                archive.DocumentElement.SelectSingleNode("def") == null)
            {
                Log.Warning("Ignoring invalid archived Stargate hediff XML (expected an li element with a def child).");
                return null;
            }
            return target.ImportNode(archive.DocumentElement, true);
        }
        catch (Exception e)
        {
            Log.Warning("Ignoring malformed archived Stargate hediff XML: " + e.Message);
            return null;
        }
    }

    private static void OverlayMutableChildren(XmlDocument doc, XmlNode current, XmlNode archived)
    {
        foreach (XmlNode child in current.ChildNodes)
        {
            if (child.NodeType != XmlNodeType.Element || child.Name == "def" || child.Name == "comps" ||
                child.Name.StartsWith("originalHediff", StringComparison.Ordinal))
            {
                continue;
            }

            XmlNode replacement = doc.ImportNode(child, true);
            XmlNode existing = archived.SelectSingleNode(child.Name);
            if (existing == null)
            {
                archived.AppendChild(replacement);
            }
            else
            {
                archived.ReplaceChild(replacement, existing);
            }
        }
    }

    private static bool IsSafeRecordedClass(string className)
    {
        if (className.NullOrEmpty()) return false;
        Type type = AccessTools.TypeByName(className);
        return type != null && typeof(Hediff).IsAssignableFrom(type);
    }

    private static bool IsMissingPlaceholder(XmlNode node, string defName)
    {
        return defName == MissingHediffDefName ||
            string.Equals(node.Attributes?["Class"]?.Value, MissingHediffClassName, StringComparison.Ordinal);
    }

    private static void SetClassAttribute(XmlDocument doc, XmlNode node, string className)
    {
        XmlAttribute classAttribute = node.Attributes?["Class"];
        if (classAttribute == null)
        {
            classAttribute = doc.CreateAttribute("Class");
            node.Attributes?.Append(classAttribute);
        }

        classAttribute.Value = className;
    }

    private static string ReadChildText(XmlNode node, string childName)
    {
        return node.SelectSingleNode(childName)?.InnerText;
    }

    private static void ApplyHardcodedMissingHediffMetadata(
        string missingDefName,
        ref string originalLabel,
        ref string originalModName,
        ref string originalPackageId
    )
    {
        if (missingDefName != "WakeUpRib")
        {
            return;
        }

        if (originalLabel.NullOrEmpty())
        {
            originalLabel = "wake-up rib";
        }

        if (originalModName.NullOrEmpty())
        {
            originalModName = "WakeUp Implant";
        }

        if (originalPackageId.NullOrEmpty())
        {
            originalPackageId = LegacyWakeUpRibPackageId;
        }
    }

    private static void UpsertChildText(XmlDocument doc, XmlNode parent, string childName, string value)
    {
        if (value.NullOrEmpty())
        {
            return;
        }

        XmlNode child = parent.SelectSingleNode(childName);
        if (child == null)
        {
            child = doc.CreateElement(childName);
            parent.AppendChild(child);
        }

        child.InnerText = value;
    }
}
