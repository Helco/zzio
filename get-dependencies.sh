#!/bin/bash
set +e

git submodule update --init --recursive --single-branch .

dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c SafeDebug --include-symbols --version-suffix safe -o nuget-feed
dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid/Veldrid.csproj -c Release --include-symbols //p:ExcludeOpenGL=true -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.MetalBindings/Veldrid.MetalBindings.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.ImageSharp/Veldrid.ImageSharp.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.SDL2/Veldrid.SDL2.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.StartupUtilities/Veldrid.StartupUtilities.csproj -c Release --include-symbols -o nuget-feed
dotnet pack extern/Veldrid/src/Veldrid.RenderDoc/Veldrid.RenderDoc.csproj -c Release --include-symbols -o nuget-feed

