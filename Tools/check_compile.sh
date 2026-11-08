#!/usr/bin/env bash
# Offline compile check of the game scripts (Assembly-CSharp + Assembly-CSharp-Editor), WITHOUT Unity.
# Uses the Roslyn compiler shipped with the Unity editor. Prints only errors (and "OK" when clean).
# Usage: bash Tools/check_compile.sh            (from the project root)
# Lets several people/agents check their code while Unity is busy in Play mode.
set -u
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
U="/c/Program Files/Unity/Hub/Editor/6000.6.8f1/Editor/Data"
DOTNET="$U/DotNetSdk/dotnet.exe"
CSC="$U/DotNetSdk/sdk/8.0.318/Roslyn/bincore/csc.dll"
OUT="${TMPDIR:-/tmp}/survivor_check_$$"
mkdir -p "$OUT"

RSP="$OUT/refs.rsp"
: > "$RSP"
echo "-r:\"$(cygpath -w "$U/NetStandard/ref/2.1.0/netstandard.dll")\"" >> "$RSP"
for f in "$U"/Managed/UnityEngine/*.dll; do echo "-r:\"$(cygpath -w "$f")\"" >> "$RSP"; done
for f in "$ROOT"/Library/ScriptAssemblies/*.dll; do
  case "$(basename "$f")" in Assembly-CSharp*.dll) continue;; esac
  echo "-r:\"$(cygpath -w "$f")\"" >> "$RSP"
done
for f in "$U"/NetStandard/compat/2.1.0/shims/netfx/*.dll; do [ -f "$f" ] && echo "-r:\"$(cygpath -w "$f")\"" >> "$RSP"; done

RUNTIME_SRC="$OUT/runtime.rsp"
EDITOR_SRC="$OUT/editor.rsp"
( cd "$ROOT" && find Assets -name "*.cs" -not -path "*/Editor/*" ) | while read -r f; do echo "\"$(cygpath -w "$ROOT/$f")\""; done > "$RUNTIME_SRC"
( cd "$ROOT" && find Assets -name "*.cs" -path "*/Editor/*" ) | while read -r f; do echo "\"$(cygpath -w "$ROOT/$f")\""; done > "$EDITOR_SRC"

COMMON="-nologo -noconfig -nostdlib -langversion:9 -nowarn:CS0618,CS0649,CS0414,CS0169,CS0067 -define:UNITY_EDITOR;UNITY_6000_0_OR_NEWER;UNITY_STANDALONE_WIN;UNITY_IOS_OFF -t:library"
status=0
"$DOTNET" "$CSC" $COMMON -out:"$(cygpath -w "$OUT/Assembly-CSharp.dll")" @"$(cygpath -w "$RSP")" @"$(cygpath -w "$RUNTIME_SRC")" 2>&1 | grep -E "error CS" | grep -oE 'Assets.*' | sort -u
[ "${PIPESTATUS[0]}" -ne 0 ] && status=1
if [ $status -eq 0 ] && [ -s "$EDITOR_SRC" ]; then
  "$DOTNET" "$CSC" $COMMON -out:"$(cygpath -w "$OUT/Assembly-CSharp-Editor.dll")" @"$(cygpath -w "$RSP")" -r:"$(cygpath -w "$OUT/Assembly-CSharp.dll")" @"$(cygpath -w "$EDITOR_SRC")" 2>&1 | grep -E "error CS" | grep -oE 'Assets.*' | sort -u
  [ "${PIPESTATUS[0]}" -ne 0 ] && status=1
fi
rm -rf "$OUT"
[ $status -eq 0 ] && echo "OK: game scripts compile"
exit $status
