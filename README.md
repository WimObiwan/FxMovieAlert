## Coding

### Code cleanup
JetBrains Resharper command line tools  
https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html

``` powershell
# Setup 
# (Last stable version 2021.2.2 gives compiler errors after cleanup)
dotnet tool install --global JetBrains.ReSharper.GlobalTools --version 2021.3.0-eap10

# Cleanup
jb cleanupcode ./FxMovieAlert.sln
# Inspect
jb inspectcode -o=/tmp/test.xml ./FxMovieAlert.sln
# Duplicate finder
jb dupfinder ./FxMovieAlert.slnjb dupfinder ./FxMovieAlert.sln
```