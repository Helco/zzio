#!/bin/bash
set +e

git submodule update --init --recursive --single-branch .

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
SymbolFlags="--include-symbols -p:EmbedAllSources=true -p:DebugType=embedded -p:SymbolPackageFormat=snupkg"

dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c SafeDebug $SymbolFlags "-p:TEST=true" -o nuget-feed --version-suffix safe-$DefaultEcsHash
dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c $Configuration $SymbolFlags "-p:TEST=true" -o nuget-feed --version-suffix $DefaultEcsHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid/Veldrid.csproj -c $Configuration $SymbolFlags "-p:ExcludeOpenGL=true" -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.MetalBindings/Veldrid.MetalBindings.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.ImageSharp/Veldrid.ImageSharp.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.SDL2/Veldrid.SDL2.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.StartupUtilities/Veldrid.StartupUtilities.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.ImGui/Veldrid.ImGui.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid.RenderDoc/Veldrid.RenderDoc.csproj -c $Configuration $SymbolFlags -o nuget-feed --version-suffix $VeldridHash$ConfigSuffix
dotnet pack extern/ImGui.NET/src/ImGui.NET/ImGui.NET.csproj -c $Configuration $SymbolFlags -o nuget-feed -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix
dotnet pack extern/ImGui.NET/src/ImGuizmo.NET/ImGuizmo.NET.csproj -c $Configuration $SymbolFlags -o nuget-feed -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix

# Prevent dirty submodules
pushd extern/ImGui.NET
git reset --hard HEAD
popd
