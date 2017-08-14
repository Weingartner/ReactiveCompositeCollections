param([string]$apikey, [string]$configuration="Release") 
param([string]$apikey) 

# Remove any old nuget packages
gci -r -include bin, obj | rm -rec -fo
# Build new nuget packages
msbuild /t:ReactiveCompositeCollections:Pack /p:Configuration=Release
# Get all nuget packages under the specific folders
$packages = gci -r -include *.nupkg -path ReactiveCompositeCollections
# Publish them all
foreach ($package in $packages) {
    & "C:\Program Files\dotnet\dotnet.exe" nuget push $package
}
