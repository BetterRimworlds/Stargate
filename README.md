# Rimworld Stargate
WARNING: This mod uses the Rimworld Base Save Functionality in ways that it was never designed to support, 
mainly saving specific things into a separate file instead of saving everything on the map to one file at 
once. Because of this there may be adverse effects. The main one that I have found is that after traveling 
through the Stargate Social opinions relating to that colonist will be lost, they will effectively be 
meeting everyone for the first time again. At this time I do not know of a practical way to resolve this issue.

The Stargate system allows you to transport materials over the great distances between colonies.

## Change Log

**v1.0.0: 2016-04-21** [by Jaxxa]
* Initial Release

**v1.0.1: 2016-05-29** [by Jaxxa]
* Fixing crash when loading with Stargates that locally have things in the Buffer.

**v2.0.0: 2016-07-17** [by Jaxxa]
* Alpha 14 Update

**v2.0.1: 2016-07-26** [by Jaxxa]
* Fix for potentially not loading Graphical Resources on loading a saved game.

**v2.0.2: 2016-08-27** [by Jaxxa]
* Building against 1249

--------------------------------------------------------------------------------------
* Officially forked from jaxxa/ED-Stargate.
--------------------------------------------------------------------------------------

**v2.0.3: 2020-08-03**
* Alpha 15 Update (only had to change the supported version number)

**v2.5.0: 2020-08-03**
* Alpha 16 Update (lots of small compatibility changes, esp regarding multiple maps).

**v3.0.0: 2020-08-04**
* Alpha 16: Reworked the Offworld Gate so that when it's activated, all higher 
  lifeforms on the map are damaged by a huge psionic blast.

**v4.0.0: 2020-08-05**
* Officially ported the mod to the Better Rimworlds organization as a member project.

**v4.1.0: 2020-08-05**
* Alpha 17 Update (v0.17.1557)

**v4.2.0: 2020-08-05**
* Beta 18 Update (v0.18.1722)

**v4.3.0: 2020-08-05**
* Beta 19 Update (v0.19.2009)

**v4.4.0: 2020-08-06**
* v1.0 Update (v1.0.2559)
* Fixed the "inspect string for * contains empty lines" console error.
* Fixed all of the "can't assign items to a faction" error messages.
* Fixed the bug since B19 where colonists went *crazy* if they had ever been drafted.

**v5.5.0: 2020-08-06**
* Made the Stargate research much more difficult to achieve.
* Added the single-use Transdimensional Stargate.
  * The Off-world Gate will no longer allow outgoing travel.
  * The Off-world Gate now falls into ruin upon the first off-world recall.
* The cost for constructing the Stargate has been greatly, and appropriately, increased.
* Only one Transdimensional Gate is allowed per area.
* Lots of code cleanup.

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

**v6.1.0: 2021-10-30**
* Automatically recall items Transdimensional Gates on activation.
* Fixed: Prevent the Stargate from demolecularizing bionics.
* Fixed: Recalled animals won't "collapse due to extreme exhaustion" anymore.
* Offset pawns' chronological age in reference to the time differential of when they entered the stargate.


## Contributors

This mod is forked off of the incredible engineering work by Jaxxa in his [**ED-Stargate mod**](https://github.com/jaxxa/ED-Stargate).
I asked him for years to port this to A16, but then I learned C# and ported it myself ;-)

Then I made it even better!
