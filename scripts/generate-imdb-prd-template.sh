#!/bin/sh
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET_SERVER="someserver"
"$SCRIPT_DIR/generate-imdb.sh" "net10.0" "root@$TARGET_SERVER" "/var/www/www.site"