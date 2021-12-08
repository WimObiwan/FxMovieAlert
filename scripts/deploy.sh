
# Usage:
#   ./scripts/deploy.sh "net6.0" "user@server.domain.tld" "/var/www/www.domain.tld" "kestrel-domain.tld"

export RELEASE=$1
export TARGET_SERVER=$2
export TARGET_PATH=$3
export TARGET_SERVICE=$4

dotnet publish --configuration Release
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* ./Site/bin/Release/$RELEASE/publish/* $TARGET_SERVER:$TARGET_PATH/
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* ./Grabber/bin/Release/$RELEASE/publish/* $TARGET_SERVER:$TARGET_PATH/grabber/
ssh $TARGET_SERVER "service $TARGET_SERVICE restart"
