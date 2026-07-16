# Set Working Directory
Split-Path $MyInvocation.MyCommand.Path | Push-Location
[Environment]::CurrentDirectory = $PWD

./Publish.ps1 -ProjectPath "p4rpc.trip2.debugui/p4rpc.trip2.debugui.csproj" -PackageName "p4rpc.trip2.debugui" -PublishOutputDir "Publish/debugui/ToUpload"
./Publish.ps1 -ProjectPath "p4rpc.trip2.debug.reloadedconsole/p4rpc.trip2.debug.reloadedconsole.csproj" -PackageName "p4rpc.trip2.debug.reloadedconsole" -PublishOutputDir "Publish/reloadedconsole/ToUpload"
./Publish.ps1 -ProjectPath "p4rpc.trip2.debug.uobjectviewer/p4rpc.trip2.debug.uobjectviewer.csproj" -PackageName "p4rpc.trip2.debug.uobjectviewer" -PublishOutputDir "Publish/uobjectviewer/ToUpload"