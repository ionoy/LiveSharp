#Set-Location "build"

Write-Output "Setting API key"

$apiKey = Read-Host -Prompt "Please enter API key"

&"..\.nuget\nuget.exe" setApiKey $apiKey -source https://www.nuget.org/api/v2/package

Get-ChildItem '*.nupkg' -Recurse | ForEach-Object {
    Write-Output "Uploading $_"
    &"..\.nuget\nuget.exe" push $_ -Source https://www.nuget.org/api/v2/package
}