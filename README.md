# Rimworld Stargate
WARNING: This mod uses the Rimworld Base Save Functionality in ways that it was never designed to support, 
mainly saving specific things into a separate file instead of saving everything on the map to one file at 
once. Because of this there may be adverse effects. The main one that I have found is that after traveling 
through the Stargate Social opinions relating to that colonist will be lost, they will effectively be 
meeting everyone for the first time again. At this time I do not know of a practical way to resolve this issue.

The Stargate system allows you to transport materials over the great distances between colonies.
![Stargate](https://github.com/BetterRimworlds/Rimworld-Stargate/assets/1125541/375be511-ea8c-4964-bb56-93689ceb6535)

## Latest Changes

**v10.0.0: 2025-02-13**
* **[2025-02-13 17:26:19 CST]** Rebuild relationships when a pawn comes through the Stargate.
* **[2025-02-12 10:30:18 CST]** Remove any ghost WorldPawns for all transmitted pawns.
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
* **[2024-05-07 00:18:59 CDT]** [major] Found the cure for Animal Stargate Psychosis!
* **[2024-05-06 23:56:35 CDT]** Fixed a major edgecase when the Stargate tried to charge with no batteries pn the power grid.
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

You can also read the full [CHANGELOG.md](CHANGELOG.md).

## Contributors

This mod is forked off of the incredible engineering work by Jaxxa in his [**ED-Stargate mod**](https://github.com/jaxxa/ED-Stargate).
I asked him for years to port this to A16, but then I learned C# and ported it myself ;-)

Then I made it even better!
