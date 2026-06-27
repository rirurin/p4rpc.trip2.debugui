# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

Remove-Item "$env:RELOADEDIIMODS/p4rpc.trip2.debug.reloadedconsole/*" -Force -Recurse
dotnet publish "./p4rpc.trip2.debug.reloadedconsole.csproj" -c Release -o "$env:RELOADEDIIMODS/p4rpc.trip2.debug.reloadedconsole" /p:OutputPath="./bin/Release" /p:ReloadedILLink="true"

# Restore Working Directory
Pop-Location