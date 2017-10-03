param([string]$apikey) 


# Remove any old nuget packages
gci -r -include bin, obj | rm -rec -fo
# Build new nuget packages
dotnet restore
dotnet pack /p:Version=$(gitversion /output json /showvariable FullSemVer) --configuration Release
# Get all nuget packages under the specific folders
$packages = gci -r -filter *.nupkg ReactiveCompositeCollections
# Publish them all
foreach ($package in $packages) {
    & dotnet nuget push $package
}

