#!/bin/bash
set +e

if [[ "$1" != "no-submodule-update" ]]; then
    git submodule update --init --recursive --single-branch .
fi

if [[ "$OSTYPE" == "msys" ]]; then
    powershell -executionpolicy bypass -File "extern/ImGui.NET/download-native-deps.ps1" -tag 1.90.1 -repository "https://github.com/Helco/ImGui.NET-nativebuild"
else
    bash extern/ImGui.NET/download-native-deps.sh 1.90.1
fi

mkdir -p nuget-feed

# Why does ImGui use OS-specific downloads again?
REMOTERY_REPO=https://github.com/Helco/Remotery.NET
MOJOAL_REPO=https://github.com/Helco/MojoAL.NET
REMOTERY_TAG=1.21.1
MOJOAL_TAG=1.1.2
CURL_ARGS="-Lo"
if [[ "$OSTYPE" == "msys" ]]; then
    CURL_ARGS="--ssl-no-revoke -Lo" # oh that's why
fi
curl $CURL_ARGS "nuget-feed/Remotery.NET.Native.$REMOTERY_TAG.nupkg" --ssl-no-revoke "$REMOTERY_REPO/releases/download/$REMOTERY_TAG/Remotery.NET.Native.$REMOTERY_TAG.nupkg"
curl $CURL_ARGS "nuget-feed/Remotery.NET.$REMOTERY_TAG.nupkg" --ssl-no-revoke "$REMOTERY_REPO/releases/download/$REMOTERY_TAG/Remotery.NET.$REMOTERY_TAG.nupkg"
curl $CURL_ARGS "nuget-feed/MojoAL.NET.Native.$MOJOAL_TAG.nupkg" --ssl-no-revoke "$MOJOAL_REPO/releases/download/$MOJOAL_TAG/MojoAL.NET.Native.$MOJOAL_TAG.nupkg"

DefaultEcsHash=`git -C extern/DefaultEcs rev-parse --short HEAD`
VeldridHash=`git -C extern/Veldrid rev-parse --short HEAD`
ImGuiNETHash=`git -C extern/ImGui.NET rev-parse --short HEAD`
MlangHash=`git -C extern/Mlang rev-parse --short HEAD`
NLayerHash=`git -C extern/NLayer rev-parse --short HEAD`
Configuration=Release
ConfigSuffix=
VeldridHash=4.9.0-717ab09d
CommonFlags="--include-symbols -p:EmbedAllSources=true -p:DebugType=portable -p:SymbolPackageFormat=snupkg -clp:ErrorsOnly -o nuget-feed"

dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c SafeDebug $CommonFlags "-p:TEST=true" --version-suffix safe-$DefaultEcsHash
dotnet pack extern/DefaultEcs/source/DefaultEcs/DefaultEcs.csproj -c $Configuration $CommonFlags "-p:TEST=true" --version-suffix $DefaultEcsHash$ConfigSuffix
dotnet pack extern/Veldrid/src/Veldrid/Veldrid.csproj -c $Configuration $CommonFlags "-p:ExcludeOpenGL=true" "-p:ExcludeD3D11=true" "-p:ExcludeMetal=true"
dotnet pack extern/Veldrid/src/Veldrid.MetalBindings/Veldrid.MetalBindings.csproj -c $Configuration $CommonFlags
dotnet pack extern/Veldrid/src/Veldrid.RenderDoc/Veldrid.RenderDoc.csproj -c $Configuration $CommonFlags
dotnet pack extern/ImGui.NET/src/ImGui.NET/ImGui.NET.csproj -c $Configuration $CommonFlags -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix
dotnet pack extern/ImGui.NET/src/ImGuizmo.NET/ImGuizmo.NET.csproj -c $Configuration $CommonFlags -p:PackagePrereleaseIdentifier=-$ImGuiNETHash$ConfigSuffix
dotnet pack extern/Mlang/Mlang/Mlang.csproj -c $Configuration $CommonFlags --version-suffix $MlangHash
dotnet pack extern/Mlang/Mlang.Compiler/Mlang.Compiler.csproj -c $Configuration $CommonFlags --version-suffix $MlangHash
dotnet pack extern/Mlang/Mlang.MSBuild/Mlang.MSBuild.csproj -c $Configuration $CommonFlags --version-suffix $MlangHash
dotnet pack extern/NLayer/NLayer/NLayer.csproj -c $Configuration $CommonFlags -p:Version=1.15.0-$NLayerHash

# Prevent dirty submodules
pushd extern/ImGui.NET
git reset --hard HEAD
popd
