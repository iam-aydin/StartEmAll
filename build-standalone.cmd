@echo off
rem Big exe (~150 MB) that runs on any Windows PC with nothing installed. Output: dist\StartEmAll.exe
dotnet publish StartEmAll.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
