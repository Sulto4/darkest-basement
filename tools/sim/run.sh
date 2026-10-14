#!/bin/sh
set -eu
SIM_DIR=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
export DOTNET_CLI_HOME="$SIM_DIR/.dotnet-home"
export NUGET_PACKAGES="$SIM_DIR/.dotnet-home/packages"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE=true
export MSBuildEnableWorkloadResolver=false
export TMPDIR="$SIM_DIR/.dotnet-home/tmp"
export NUGET_HTTP_CACHE_PATH="$SIM_DIR/.dotnet-home/http-cache"
export NUGET_SCRATCH="$SIM_DIR/.dotnet-home/nuget-scratch"
mkdir -p "$TMPDIR" "$NUGET_SCRATCH"
DOTNET="/Volumes/Dock & Hub 2TB/DevStorage/dotnet/sdk8/dotnet"
"$DOTNET" build "$SIM_DIR/ModelCompatibility.csproj" -p:BaseIntermediateOutputPath="$SIM_DIR/.dotnet-home/compatibility-obj/" --configuration Release --nologo --verbosity quiet
"$DOTNET" build "$SIM_DIR/Sim.csproj" --configuration Release --nologo --verbosity quiet
"$DOTNET" "$SIM_DIR/bin/Release/net8.0/Sim.dll" tests
"$DOTNET" "$SIM_DIR/bin/Release/net8.0/Sim.dll" sim --runs 500 --seed 1
