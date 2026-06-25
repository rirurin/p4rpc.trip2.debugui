# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4rpc.trip2.debugui/*" -Force -Recurse
dotnet publish "./p4rpc.trip2.debugui.csproj" -c Release -o "$env:RELOADEDIIMODS/p4rpc.trip2.debugui" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location