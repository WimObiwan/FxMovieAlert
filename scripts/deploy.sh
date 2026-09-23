
# Usage:
#   ./scripts/deploy.sh "net8.0" "user@server.domain.tld" "/var/www/www.domain.tld" "kestrel-domain.tld"

export RELEASE=$1
export TARGET_SERVER=$2
export TARGET_PATH=$3
export TARGET_SERVICE=$4

dotnet publish --configuration Release
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* ./Site/bin/Release/$RELEASE/publish/* $TARGET_SERVER:$TARGET_PATH/
rsync -av --info=progress2 --exclude=appsettings.* --exclude=wwwroot/images/cache/* ./Grabber/bin/Release/$RELEASE/publish/* $TARGET_SERVER:$TARGET_PATH/grabber/

# The app runs as www-data and must be able to WRITE, not just read:
#  - the SQLite databases themselves, and
#  - the deploy directory, because SQLite creates -wal/-shm files alongside them.
# rsync above runs as root, so everything it touches is root-owned. Chowning only
# dp_keys (as this line did until 2026-09-23) leaves the databases root-owned and
# mode 644: reads still work, so the site looks completely healthy right up until
# the first write, which is usually ordinary usage tracking on a page view.
#
# The restart is also load-bearing: an already-open file descriptor keeps the
# access mode it had when it was opened, so chown alone does not fix a running
# process - it keeps the O_RDONLY handle until it is restarted.
ssh $TARGET_SERVER "
  chown www-data:www-data '$TARGET_PATH'
  find '$TARGET_PATH' -maxdepth 1 -name '*.db*' -exec chown www-data:www-data {} +
  chown -R www-data:www-data '$TARGET_PATH/dp_keys/'
  service $TARGET_SERVICE restart
"
