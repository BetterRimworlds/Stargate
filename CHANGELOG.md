**v10.0.0: 2025-02-13**
* **[2025-02-13 17:26:19 CST]** Rebuild relationships when a pawn comes through the Stargate.
* **[2025-02-12 10:30:18 CST]** Remove any ghost WorldPawns for all transmitted pawns. origin/relationships-redux.2, origin/relationships-redux, relationships-redux.2, relationships-redux
* **[2025-02-12 10:25:43 CST]** Completely reimplemented relationships to being stored inside each Pawn's Gate Traveler brain implant.
* **[2025-02-12 10:02:48 CST]** Added the Gate Traveler brain implant.
* **[2025-02-12 05:17:46 CST]** Introduce GenerateMissingRelationshipRecord for missing pawn relationships
* **[2025-02-09 08:39:40 CST]** Animals training is no longer reset by gate travel.
* **[2025-01-24 22:52:39 CST]** Fixed a reversion: Stargate's wormhole shows immediately on resources being sent through.
* **[2024-05-30 00:41:53 CDT]** More tweaks to hopefully address lack of training transfer in animals.
* **[2024-05-30 00:41:27 CDT]** Try to skip but continue when a particular Thing cannot be transmitted.
* **[2024-05-29 23:47:23 CDT]** Don't require extra power for incoming wormholes' Stargate Buffer.
* **[2024-05-27 23:35:21 CDT]** Fixed a reversion where animals couldn't be transmitted.
* **[2024-05-27 23:14:08 CDT]** Refactored power draw logic.
* **[2024-05-20 05:12:37 CDT]** Added enhanced logging to try to solve powering up bugs.
* **[2024-05-20 00:30:02 CDT]** Added more compat for transferring Humans between Rimworld v1.4 and v1.5 to v1.2 and v1.3.
* **[2024-05-09 00:10:46 CDT]** Fixed Rimworld v1.2 compatibility.

**v9.1.0: 2024-05-07**
* **[2024-05-07 01:14:20 CDT]** [major] Effectively cured Stargate Psychosis (!!!).
* **[2024-05-07 01:13:08 CDT]** [m] Reduced the size of the wormhole icon (from 1.56 MB to 31 KB (-98%)).
* **[2024-05-07 00:18:59 CDT]** [major] Found the cure for Animal Stargate Psychosis!
* **[2024-05-06 23:56:35 CDT]** Fixed a major edgecase when the Stargate tried to charge with no batteries pn the power grid.
* **[2024-05-06 23:55:49 CDT]** [m] Cosmetic edits to the definition files and README.
* **[2024-05-06 23:55:17 CDT]** Fixed some bugs with the stored mass not resetting when appropriate.
* **[2024-05-06 23:54:33 CDT]** Shuffled the offworld gate psionic curses to their own class.

**v9.0.0: 2024-03-21**
* **[2024-03-21 18:44:28 CDT]** **[Major]** Upgraded to Rimworld v1.5.
* **[2024-03-21 18:41:38 CDT]** Fixed a bug where local teleports didn't work correctly with an offworld team.
* **[2024-03-21 18:36:47 CDT]** Brought in the new Power-needs code from DeMaterializer.
* **[2024-03-21 16:39:41 CDT]** **[Major]** Made the Stargate UI gizmos dynamic based on the state of the stargate network.
* **[2024-03-21 04:46:32 CDT]** Shifted to the BetterRimworld CI/CD system.
* **[2024-03-20 21:08:57 CDT]** Added sounds to the Stargate.
* **[2024-03-20 03:42:52 CDT]** Disable power toggle on the Stargate, as it doesn't make sense.
* **[2024-03-20 03:37:47 CDT]** Fixed a bug where colonists couldn't be recalled after a local teleport.
* **[2024-03-12 05:57:00 CDT]** Make the Stargate require more and more power, 2 kW for every 1 kg in the Stargate buffer over 1,000 kg.
* **[2024-02-17 11:08:29 CST]** Added to Steam Workshop.

**v8.1.0: 2023-09-02**
* **[2023-09-02 23:00:35 CDT]** Fixed a terrible bug where Colonists without weapons get stuck in the Stargate Buffer.
* **[2023-09-02 22:57:36 CDT]** Do not destroy the Transdimensional Stargate until the entire buffer is emptied.
  
**v8.0.2: 2023-07-28**
* **[2023-07-28 17:30:51 CDT]** Fixed relationships reassignment on gate travel. 
* **[2023-07-28 17:30:28 CDT]** Increased the stargate buffer to 5000 items.

**v8.0.1: 2023-06-16**
* **[2023-06-16 14:48:57 CDT]** Fixed a bug where the Stargate would not power up without a battery on the grid.

**v8.0.0: 2023-06-07**
* **[2023-05-22 18:50:19 CDT]** **[Major]** Added compilation support for Rimworld v1.4.
* **[2023-05-22 18:54:13 CDT]** Fixed a bug where Stargates from other instances of RimWorld interfered with one another.
* **[2023-05-22 18:57:44 CDT]** **[Major]** Completely reworked Power: Now increases based on Power Availability.
* **[2023-05-22 19:04:32 CDT]** **[Major]** Automatically add resources to the Stargate that surround it.
* **[2023-05-22 19:05:30 CDT]** [m] Removed some Stargate Psychosis fixes that are fixed elsewhere now.
* **[2023-05-22 19:06:55 CDT]** Fixed bugs with the local teleportation mechanism.
* **[2023-05-22 19:11:08 CDT]** **[Major]** Properly fixed Stargate Psychosis and fully remove Pawns after going through the stargate.
* **[2023-05-22 19:11:45 CDT]** Add Pawns to the top of the Stargate Buffer to ensure they're always exited first.
* **[2023-05-22 19:12:20 CDT]** Allow an unlimited number of Pawns through the stargate, because there are no stack issues.
* **[2023-05-22 20:54:13 CDT]** [m] Updated the deploy script.
* **[2023-05-22 20:55:35 CDT]** Added more prereq tech: Quantum Teleportation and Quantum Storage.
* **[2023-06-03 03:32:18 CDT]** Now, the colonists are removed from the top Colony Bar when they're transmitted.

**v7.0.0: 2022-10-21**
* Developed THE ACTUAL CURE for Stargate Psychosis!!!
* Arranged it so that the number of pawns doesn't count towards the total Stargate buffer.
* Added functionality to rebuild pawn relationships after gating.
* Reworked the re-adding of relationships across stargate jumps.
* Majorly refactored how relationships are loaded and rebuilt.
* Reduce the Stargate's power draw to 0 when fully charged.
* Move each existing Stargate buffer backup to its own file.
* Majorly refactored the transmission of Gate contents from the Stargate itself into the Stargate Buffer.
* Refactored the Stargate Recall() method.
* Removed the extremely deprecated ability to define the Stargate.xml path from in the ThingDef XML.
* Removed debug code.
* YESS!! I got everything working!

**v6.5.0: 2022-02-10**
* Migrated the build system to Linux.
* Refactored the namespaces from Enhanced_Development to BetterRimworlds.
* Refactored the Stargate.xml file location to be OS independent.
* Completely reimplemented the StargateBuffer mechanism so that buffer items are saved with the saved games.
* Reset the Guilty tag of Humans who come through the gate.
* Fixed viewing of the contents of the Stargate's outgoing buffer.
* Upgrade to .NET Framework v4.8.
* Fixed the Transdimensional Stargate.
* Upgraded to Rimworld v1.3.
* Refactored "colonists" to "pawns".
* Persist hediffs through gate rematerialization.
* An attempt to cure Animal Stargate Psychosis.
* Adjust the pawn's chronological age by time diffs of the origin + destination stargates.
* Misc. changes.

**v6.1.0: 2021-10-30**
* Automatically recall items Transdimensional Gates on activation.
* Fixed: Prevent the Stargate from demolecularizing bionics.
* Fixed: Recalled animals won't "collapse due to extreme exhaustion" anymore.
* Offset pawns' chronological age in reference to the time differential of when they entered the stargate.

**v6.0.0: 2021-08-10**
* Upgraded to C# v8.0.
* Disorient, sicken or kill nearly every pawn on the map after activating an Transdimensional Stargate.
* [Fixed] Fixed Humans being afflicted with Wormhole Insanity Disorder.
* Created a new StargateBuffer to hold Things in Rimworld v1.0 fashion.
* Stop drawing power if the circuits are full.
* Added a Stargate Network system.
* Added Teleportation between on-world gates.
* Changes necessary for Rimworld v1.2.
* Made Stargates substantially more expensive to build.
* [Fixed] Colonists' max-XP-per-day now resets after jumping through a gate.
* You can now view the contents of the Stargate's outgoing buffer!
* Limit the number of item sets held in the stargate buffer to 500.

**v5.0.0: 2020-08-06**
* Made the Stargate research much more difficult to achieve.
* Added the single-use Transdimensional Stargate.
* The Off-world Gate will no longer allow outgoing travel.
* The Off-world Gate now falls into ruin upon the first off-world recall.
* The cost for constructing the Stargate has been greatly, and appropriately, increased.
* Only one Transdimensional Gate is allowed per area.
* Lots of code cleanup.

**v4.4.0: 2020-08-06**
* v1.0 Update (v1.0.2559)
* Fixed the "inspect string for * contains empty lines" console error.
* Fixed all of the "can't assign items to a faction" error messages.
* Fixed the bug since B19 where colonists went *crazy* if they had ever been drafted.

**v4.3.0: 2020-08-05**
* Beta 19 Update (v0.19.2009)

**v4.2.0: 2020-08-05**
* Beta 18 Update (v0.18.1722)

**v4.1.0: 2020-08-05**
* Alpha 17 Update (v0.17.1557)

**v4.0.0: 2020-08-05**
* Officially ported the mod to the Better Rimworlds organization as a member project.

**v3.0.0: 2020-08-04**
* Alpha 16: Reworked the Offworld Gate so that when it's activated, all higher
  lifeforms on the map are damaged by a huge psionic blast.

**v2.5.0: 2020-08-03**
* Alpha 16 Update (lots of small compatibility changes, esp regarding multiple maps).

**v2.0.3: 2020-08-03**
* Alpha 15 Update (only had to change the supported version number)

--------------------------------------------------------------------------------------
* Officially forked from jaxxa/ED-Stargate.
--------------------------------------------------------------------------------------

**v2.0.2: 2016-08-27** [by Jaxxa]
* Building against 1249

**v2.0.1: 2016-07-26** [by Jaxxa]
* Fix for potentially not loading Graphical Resources on loading a saved game.

**v2.0.0: 2016-07-17** [by Jaxxa]
* Alpha 14 Update

**v1.0.1: 2016-05-29** [by Jaxxa]
* Fixing crash when loading with Stargates that locally have things in the Buffer.

**v1.0.0: 2016-04-21** [by Jaxxa]
* Initial Release
