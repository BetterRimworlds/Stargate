# Rimworld Stargate
WARNING: This mod uses the Rimworld Base Save Functionality in ways that it was never designed to support, 
mainly saving specific things into a separate file instead of saving everything on the map to one file at 
once. Because of this there may be adverse effects. The main one that I have found is that after traveling 
through the Stargate Social opinions relating to that colonist will be lost, they will effectively be 
meeting everyone for the first time again. At this time I do not know of a practical way to resolve this issue.

The Stargate system allows you to transport materials over the great distances between colonies.
![Stargate](https://github.com/BetterRimworlds/Rimworld-Stargate/assets/1125541/375be511-ea8c-4964-bb56-93689ceb6535)

Demo Video: https://youtu.be/PR_j4uGLeGw

## Latest Changes
**v11.0.0: 2025-07-08**
* **[2025-07-08 23:50:33 CDT]** Massively refactored the relationship restoration to count for existing gate traveler implants.
* **[2025-07-08 23:49:22 CDT]** Added support for Rimworld v1.6.
* **[2025-07-08 23:45:38 CDT]** Fixes for many problems with Rimworld v1.4-v1.5.
* **[2025-07-08 23:43:42 CDT]** [m] Added debug log.warnings to help the v1.6 port.
* **[2025-07-08 18:55:23 CDT]** Removed some XML interferring with other mods.
* **[2025-07-08 18:11:12 CDT]** Majorly improved ./build.sh to handle XML changes as well.
* **[2025-07-08 18:05:17 CDT]** Reorganized the source code per the Better Rimworlds standard layout.

**v10.5.0: 2025-06-21**
* **[2025-06-21 04:15:34 CDT]** [m] Fixed a regression where the game would freeze if a buffer contained no pawns and only things.
* **[2025-06-19 05:53:52 CDT]** Radically refactored stargate relationships.
* **[2025-06-17 11:46:26 CDT]** Moved a lot of the receiving matter logic to the Stargate Buffer.
* **[2025-06-17 11:43:17 CDT]** Don't require power at all if stored mass is less than 1,000 kg.
* **[2025-06-17 11:31:02 CDT]** Improvements to the build system. Targetting .NET 4.72.
* **[2025-06-13 05:54:10 CDT]** The Stargate no longer casts a shadow.
* **[2025-05-26 06:44:59 CDT]** Now the Gate Traveler brain implant is ignored by Body Purists.
* **[2025-03-15 21:26:59 CST]** Improved the stability and performance of the Stargate system.
* **[2025-03-15 21:08:04 CST]** Converted the legacy project file to the SDK-style format.
* **[2025-03-15 18:53:25 CST]** Completely rewrote FindingThings to modern C# v10 standards and conciseness.
* **[2025-03-15 13:28:56 CST]** Migrated to .NET v9.0 and the Better Rimworlds build system.
* **[2025-03-05 22:03:43 CST]** Migrated to .NET v8.0 and C# v10.
* **[2025-02-15 14:33:11 CST]** [m] Added a link to the demo video.

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

You can also read the full [CHANGELOG.md](CHANGELOG.md).

## Contributors

This mod is forked off of the incredible engineering work by Jaxxa in his [**ED-Stargate mod**](https://github.com/jaxxa/ED-Stargate).
I asked him for years to port this to A16, but then I learned C# and ported it myself ;-)

Then I made it even better!
