# Rimworld Stargate
WARNING: This mod uses the Rimworld Base Save Functionality in ways that it was never designed to support, 
mainly saving specific things into a separate file instead of saving everything on the map to one file at 
once. Because of this there may be adverse effects. The main one that I have found is that after traveling 
through the Stargate Social opinions relating to that colonist will be lost, they will effectively be 
meeting everyone for the first time again. At this time I do not know of a practical way to resolve this issue.

The Stargate system allows you to transport materials over the great distances between colonies.
![Stargate](https://github.com/BetterRimworlds/Rimworld-Stargate/assets/1125541/375be511-ea8c-4964-bb56-93689ceb6535)

## Latest Changes

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
* **[2023-05-22 20:55:35 CDT]** Added more prereq tech: Quantum Teleportation and Quantum Storage.
* **[2023-06-03 03:32:18 CDT]** Now, the colonists are removed from the top Colony Bar when they're transmitted.

You can also read the full [CHANGELOG.md](CHANGELOG.md).

## Contributors

This mod is forked off of the incredible engineering work by Jaxxa in his [**ED-Stargate mod**](https://github.com/jaxxa/ED-Stargate).
I asked him for years to port this to A16, but then I learned C# and ported it myself ;-)

Then I made it even better!
