@echo off

..\Tools\nuget.exe pack ClearPeople.VarnishPurge.csproj -Prop Configuration=Release -Build -OutputDirectory "..\Package" -Version "1.0.0"