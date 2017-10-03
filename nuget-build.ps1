param([string]$apikey) 

$version=gitversion /output json /showvariable FullSemVer
write-host "VERSION is $version"
$args="/p:Version=$version"
dotnet pack -o ./artifacts  --configuration Release /p:Version=$version ReactiveCompositeCollections\ReactiveCompositeCollections.csproj
# Get all nuget packages under the specific folders
$packages = gci -r -filter *.nupkg ReactiveCompositeCollections
dotnet nuget push ReactiveCompositeCollections\artifacts\ReactiveCompositeCollections.$version.nupkg

