#!/bin/bash

# Check if inotifywait is installed
if [ -z "$(which inotifywait)" ]; then
    echo "inotifywait not installed."
    echo "Install the inotify-tools package and try again."
    exit 1
fi

# Directory to monitor for changes
dir="./Source"
MOD=$(basename $PWD)

# Define the path to your solution file
solutionPath="Source/${MOD}.sln"

# Define an array of configurations
configurations=("v1.2" "v1.3" "v1.4" "v1.5" "v1.6")

dotnet restore "$solutionPath"

function sync_mod()
{
    # Copy over the mod directory.
    rsync -a ${MOD} /rimworld/1.2/Mods/

    # Copy over and reformat the README.
    cp README.md /rimworld/1.2/Mods/${MOD}
    unix2dos /rimworld/1.2/Mods/${MOD}/README.md

    rm -rf /rimworld/1.3/Mods/${MOD}
    rm -rf /rimworld/1.4/Mods/${MOD}
    rm -rf /rimworld/1.5/Mods/${MOD}
    rm -rf /rimworld/1.6/Mods/${MOD}
    rm -rf /rimworld/1.6-steam/Mods/${MOD}

    cp -af /rimworld/1.2/Mods/${MOD} /rimworld/1.3/Mods
    cp -af /rimworld/1.2/Mods/${MOD} /rimworld/1.4/Mods
    cp -af /rimworld/1.2/Mods/${MOD} /rimworld/1.5/Mods
    cp -af /rimworld/1.2/Mods/${MOD} /rimworld/1.6/Mods
    cp -af /rimworld/1.2/Mods/${MOD} /rimworld/1.6-steam/Mods
}

function build() {
    rm -rf /rimworld/1.2/Mods/${MOD}

    # Loop through each configuration and build it
    for config in "${configurations[@]}"; do
        echo "Building for configuration: $config"
        dotnet build --no-restore "$solutionPath" --configuration "Release $config" &
    done

    wait  # Blocks until all background jobs finish

    sync_mod

    echo "All builds completed!"
}

build

if [ "$1" == "1" ]; then
    echo "Done"
    exit
fi

# Watch for changes to .cs and XML files in the directory and subdirectories
inotifywait --recursive --monitor --format "%e %w%f" \
    --exclude '/\.idea($|/)' \
    --event modify,move,create,delete "$dir" "$MOD" |
    while read event fullpath; do
        if [[ "$fullpath" == "$dir"* && "$fullpath" == *.cs ]]; then
            echo "Running build for $fullpath"
            build
        elif [[ "$fullpath" == "$MOD"* && "$fullpath" == *.xml ]]; then
            echo "Running sync_mod for $fullpath"
            sync_mod
        fi
    done
