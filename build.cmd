@echo off
rem Small exe (needs the .NET 8 Desktop Runtime installed). Output: dist\StartEmAll.exe
dotnet publish StartEmAll.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
