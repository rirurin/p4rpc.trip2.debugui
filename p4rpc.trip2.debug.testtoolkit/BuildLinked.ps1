# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4rpc.trip2.debug.testtoolkit/*" -Force -Recurse
dotnet publish "./p4rpc.trip2.debug.testtoolkit.csproj" -c Release -o "$env:RELOADEDIIMODS/p4rpc.trip2.debug.testtoolkit" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location