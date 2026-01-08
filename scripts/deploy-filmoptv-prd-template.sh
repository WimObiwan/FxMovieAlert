#!/bin/sh
SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
"$SCRIPT_DIR/deploy.sh" "net10.0" "root@server3.foxinnovations.be" "/var/www/www.site" "kestrel-site"
