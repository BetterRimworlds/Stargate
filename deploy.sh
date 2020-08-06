#!/bin/bash

rsync -rcv --delete-after README.md Stargate/About Stargate/Defs /c/Program\ Files\ \(x86\)/Steam/steamapps/common/RimWorld/Mods/Stargate/
unix2dos /c/Program\ Files\ \(x86\)/Steam/steamapps/common/RimWorld/Mods/Stargate/README.md
