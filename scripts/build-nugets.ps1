# Copy everything to `packages` folder
# Remove old nuget packages
Remove-Item "*.nupkg"

# Find version file
$versionFile = Get-ChildItem '*.version'

if (!$versionFile) { 
    Write-Output "No version file found"
    exit 
}

Write-Output "Found version file $versionFile"
$m = ([regex]"(\d+)\.(\d+)\.(\d+)").match($versionFile)

$major = $m.Groups[1].Value
$minor = $m.Groups[2].Value
$patch = $m.Groups[3].Value

$currentVersion = "$major.$minor.$patch"

# Rewrite all nuspec files with new version
Get-ChildItem '*.nuspec' -Recurse | ForEach-Object {
    Write-Output "Replacing version in $_ to $currentVersion"
    (Get-Content $_ | 
       ForEach-Object  { $_ -replace [regex]'<version>\d+\.\d+\.\d+.*</version>', "<version>$currentVersion</version>" }) | 
       ForEach-Object  { $_ -replace [regex]'version="\d+\.\d+\.\d+.*"', "version=`"$currentVersion`"" } |
     Set-Content $_
}

Get-ChildItem '*.nuspec' -Recurse | ForEach-Object {
    Write-Output "Building NuGet package for $_"
    &"..\.nuget\nuget.exe" pack "$_" -Verbosity detailed
}

Copy-Item -Path "..\build\LiveSharp.Build.dll" -Destination "LiveSharp.Build.dll"

$incrementedPatch = ($patch -as [int]) + 1

Write-Output "Removing $versionFile"
Remove-Item $versionFile

$newVersion = "$major.$minor.$incrementedPatch"
$newVersionFile = "$newVersion.version"

Write-Output "Creating incremented file $newVersionFile"
New-Item $newVersionFile
