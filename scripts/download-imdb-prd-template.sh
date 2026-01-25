#!/bin/sh
# Template for downloading IMDb ratings & watchlist
# Copy this file to download-imdb-prd.sh and update the OUTPUT_PATH

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
OUTPUT_PATH="/path/to/output/directory"

cd "$SCRIPT_DIR/../tools/imdb-downloader"
./download.sh "$OUTPUT_PATH"
