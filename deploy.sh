#!/bin/bash

rsync -rcv --delete-after README.md Stargate/About Stargate/Defs /c/RimWorld1-2-2900Win64/Mods/Stargate/
unix2dos /c/RimWorld1-2-2900Win64/Mods/Stargate/README.md
