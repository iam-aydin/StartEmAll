@echo off
rem Small exe (needs the .NET 8 Desktop Runtime installed). Output: dist\Launcher.exe
dotnet publish Launcher.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o dist
