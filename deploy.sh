#!/bin/bash

unix2dos README.md
rsync -rcv --delete-after README.md Stargate/About Stargate/Defs Stargate/Textures  /rimworld/1.2/Mods/Stargate/
rsync -rcv --delete-after README.md Stargate/About Stargate/Defs Stargate/Textures  /rimworld/1.3/Mods/Stargate/
rsync -rcv --delete-after README.md Stargate/About Stargate/Defs Stargate/Textures  /rimworld/1.4/Mods/Stargate/
rsync -rcv --delete-after README.md Stargate/About Stargate/Defs Stargate/Textures  $HOME/.steam/steam/steamapps/common/RimWorld/Mods/Stargate/
rsync -rcv /rimworld/1.2/Mods/Stargate/1.2 $HOME/.steam/steam/steamapps/common/RimWorld/Mods/Stargate/
rsync -rcv /rimworld/1.3/Mods/Stargate/1.3 $HOME/.steam/steam/steamapps/common/RimWorld/Mods/Stargate/
rsync -rcv /rimworld/1.4/Mods/Stargate/1.4 $HOME/.steam/steam/steamapps/common/RimWorld/Mods/Stargate/

rsync -rcv --delete-after $HOME/.steam/steam/steamapps/common/RimWorld/Mods/Stargate /rimworld/1.2/Mods
dos2unix README.md
