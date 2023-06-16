**v8.0.1: 2023-06.16**

* **[2023-06-16 14:48:57 CDT]** Fixed a bug where the Stargate would not power up without a battery on the grid.

**v8.0.0: 2023-06-07**
* **[2023-05-22 18:50:19 CDT]** [Major] Added compilation support for Rimworld v1.4.
* **[2023-05-22 18:54:13 CDT]** Fixed a bug where Stargates from other instances of RimWorld interfered with one another.
* **[2023-05-22 18:57:44 CDT]** [Major] Completely reworked Power: Now increases based on Power Availability.
* **[2023-05-22 19:04:32 CDT]** [Major] Automatically add resources to the Stargate that surround it.
* **[2023-05-22 19:05:30 CDT]** [m] Removed some Stargate Psychosis fixes that are fixed elsewhere now.
* **[2023-05-22 19:06:55 CDT]** Fixed bugs with the local teleportation mechanism.
* **[2023-05-22 19:11:08 CDT]** [Major] Properly fixed Stargate Psychosis and fully remove Pawns after going through the stargate.
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
