# if css/js has changed, re-minify
cd ./FxMovieAlert/
dotnet bundle
cd ..

dotnet build --configuration Release
dotnet publish --configuration Release
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* /mnt/data/archive/Projects/dotnetcore/FxMovieAlert/FxMovieAlert/bin/Release/netcoreapp2.2/publish/* root@server3.foxinnovations.be:/var/www/www.filmoptv.be/
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* /mnt/data/archive/Projects/dotnetcore/FxMovieAlert/Grabber/bin/Release/netcoreapp2.2/publish/* root@server3.foxinnovations.be:/var/www/www.filmoptv.be/grabber/

ssh root@server3.foxinnovations.be "service kestrel-filmoptv.be restart"
