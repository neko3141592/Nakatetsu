#!/bin/sh
set -eu

cd "$(dirname "$0")/.."

version=2.84.0
tools_dir=".tools/grpc.tools/$version"
output_dir="Assets/Nakatetsu/Application/Communication/Generated"

case "$(uname -s):$(uname -m)" in
  Darwin:*) platform=macosx_universal ;;
  Linux:aarch64|Linux:arm64) platform=linux_arm64 ;;
  Linux:x86_64) platform=linux_x64 ;;
  MINGW*:x86_64|MSYS*:x86_64) platform=windows_x64 ;;
  *) echo "Unsupported platform for Grpc.Tools: $(uname -s) $(uname -m)" >&2; exit 1 ;;
esac

plugin="$tools_dir/tools/$platform/grpc_csharp_plugin"
case "$platform" in windows_*) plugin="$plugin.exe" ;; esac

if [ ! -f "$plugin" ]; then
  mkdir -p "$tools_dir"
  archive="$tools_dir/grpc.tools.nupkg"
  curl --fail --location --retry 3 \
    "https://api.nuget.org/v3-flatcontainer/grpc.tools/$version/grpc.tools.$version.nupkg" \
    --output "$archive"
  unzip -oq "$archive" "tools/$platform/*" -d "$tools_dir"
  rm "$archive"
  chmod +x "$plugin"
fi

mkdir -p "$output_dir"
protoc -I Protocol \
  --csharp_out="$output_dir" \
  --grpc_out="$output_dir" \
  --grpc_opt=no_server \
  --plugin="protoc-gen-grpc=$plugin" \
  Protocol/ctc.proto
