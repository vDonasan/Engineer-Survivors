#!/usr/bin/env bash
# Roda os testes da simulação (fora da Unity).
# Usa $DOTNET se definido; senão Z:/Dev/tools/dotnet; senão o dotnet do PATH.
set -euo pipefail
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
if [ -n "${DOTNET:-}" ]; then dn="$DOTNET"
elif [ -x /z/Dev/tools/dotnet/dotnet.exe ]; then dn=/z/Dev/tools/dotnet/dotnet.exe
else dn=dotnet; fi
"$dn" test "$(dirname "$0")/ES.Tools.sln" "$@"
