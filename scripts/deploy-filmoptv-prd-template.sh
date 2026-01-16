#!/bin/sh
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
TARGET_SERVER="someserver"
"$SCRIPT_DIR/deploy.sh" "net10.0" "$TARGET_SERVER" "/var/www/www.site" "kestrel-site"
