dotnet publish -c Release -r win-x64 -o publish
vpk pack --packId VesperApp --packVersion 1.0.27 --packDir publish --mainExe VesperApp.exe --channel win-x64-stable --icon Assets\bat.ico