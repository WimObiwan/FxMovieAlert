## Coding

### Deploy

``` bash
./scripts/deploy.sh "net6.0" "user@linuxserver" "/var/www/sitepath" "<service-to-restart>"
```

### Migrations

``` bash
cd ./Grabber/
dotnet ef migrations add AddMovieEventIgnore --project ./Grabber/ --context MoviesDbContext
```

### Code cleanup
JetBrains Resharper command line tools  
https://www.jetbrains.com/help/resharper/ReSharper_Command_Line_Tools.html

``` bash
# Setup 
# (Last stable version 2021.2.2 gives compiler errors after cleanup)
dotnet tool install --global JetBrains.ReSharper.GlobalTools --version 2021.3.0-eap10

# Cleanup
jb cleanupcode ./FxMovieAlert.sln
# Inspect
jb inspectcode -f=html -o=/tmp/inspectcode.html ./FxMovieAlert.sln
# Duplicate finder
jb dupfinder ./FxMovieAlert.sln
```