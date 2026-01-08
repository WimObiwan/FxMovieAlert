#!/bin/sh
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"

TARGET_SERVER="someserver"
"$SCRIPT_DIR/deploy.sh" "net10.0" "$TARGET_SERVER" "/var/www/dev.site" "kestrel-dev.site"

ssh "$TARGET_SERVER" <<'EOF'
echo Executing sqlite3 VACUUM to copy imdb.db from prd to dev
sqlite3 /var/www/www.site/imdb.db \
  "VACUUM INTO '/var/www/dev.site/imdb.db'"

echo Updating EPG on dev.site
cd /var/www/dev.site/grabber/ || exit 1
nice -n 15 ./UpdateEPG.sh
EOF
