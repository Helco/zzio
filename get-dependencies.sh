#!/bin/bash
set +e

git submodule update --init --recursive --single-branch .

if [[ "$OSTYPE" == "msys" ]]; then
    powershell -executionpolicy bypass -File "extern/ImGui.NET/download-native-deps.ps1" -tag 1.88 -repository "https://github.com/Helco/ImGui.NET-nativebuild"
else
    bash extern/ImGui.NET/download-native-deps.sh 1.88
fi

dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c SafeDebug --include-symbols --version-suffix safe "-p:TEST=true" -o nuget-feed
dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c Release --include-symbols "-p:TEST=true" -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid/Veldrid.csproj -c Release --include-symbols "-p:ExcludeOpenGL=true" -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.MetalBindings/Veldrid.MetalBindings.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.ImageSharp/Veldrid.ImageSharp.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.SDL2/Veldrid.SDL2.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.StartupUtilities/Veldrid.StartupUtilities.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.RenderDoc/Veldrid.RenderDoc.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/ImGui.NET/src/ImGui.NET/ImGui.NET.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/ImGui.NET/src/ImGuizmo.NET/ImGuizmo.NET.csproj -c Release --include-symbols -o nuget-feed

# Prevent dirty submodules
pushd extern/ImGui.NET
git reset --hard HEAD
popd
