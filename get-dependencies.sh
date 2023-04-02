#!/bin/bash
set +e

if [[ "$1" != "no-submodule-update" ]]; then
    git submodule update --init --recursive --single-branch .
fi

if [[ "$OSTYPE" == "msys" ]]; then
    powershell -executionpolicy bypass -File "extern/ImGui.NET/download-native-deps.ps1" -tag 1.88 -repository "https://github.com/Helco/ImGui.NET-nativebuild"
else
    bash extern/ImGui.NET/download-native-deps.sh 1.88
fi

DefaultEcsHash=`git -C extern/DefaultEcs rev-parse --short HEAD`
VeldridHash=`git -C extern/Veldrid rev-parse --short HEAD`
ImGuiNETHash=`git -C extern/ImGui.NET rev-parse --short HEAD`
Configuration=Release
ConfigSuffix=
VeldridHash=notactuallyused
CommonFlags="--include-symbols -p:EmbedAllSources=true -p:DebugType=embedded -p:SymbolPackageFormat=snupkg -clp:ErrorsOnly -o nuget-feed"

dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c SafeDebug $CommonFlags "-p:TEST=true" --version-suffix safe-$DefaultEcsHash
dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c $Configuration $CommonFlags "-p:TEST=true" --version-suffix $DefaultEcsHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid/Veldrid.csproj -c $Configuration $CommonFlags "-p:ExcludeOpenGL=true" --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.MetalBindings/Veldrid.MetalBindings.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.ImageSharp/Veldrid.ImageSharp.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.SDL2/Veldrid.SDL2.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.StartupUtilities/Veldrid.StartupUtilities.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.ImGui/Veldrid.ImGui.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.RenderDoc/Veldrid.RenderDoc.csproj -c $Configuration $CommonFlags --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/ImGui.NET/src/ImGui.NET/ImGui.NET.csproj -c $Configuration $CommonFlags -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix
dotnet pack extern/ImGui.NET/src/ImGuizmo.NET/ImGuizmo.NET.csproj -c $Configuration $CommonFlags -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix

# Prevent dirty submodules
pushd extern/ImGui.NET
git reset --hard HEAD
popd
