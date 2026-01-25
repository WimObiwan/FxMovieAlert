#!/bin/bash
#
# Download script for IMDb Ratings & Watchlist
# This script activates the venv and runs the Python downloader
#
# Usage: ./download.sh [output_directory]

set -e  # Exit on error

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

# Default output directory
OUTPUT_DIR="${1:-./output}"

# Check if virtual environment exists
if [ ! -d "venv" ]; then
    echo "Error: Virtual environment not found"
    echo "Please run ./setup.sh first"
    exit 1
fi

# Check if config file exists
if [ ! -f "config.ini" ]; then
    echo "Error: config.ini not found"
    echo "Please create config.ini from config.ini.template"
    exit 1
fi

# Activate virtual environment
source venv/bin/activate

# Read credentials from config file
EMAIL=$(grep -E "^email\s*=" config.ini | sed 's/^email\s*=\s*//' | sed 's/\s*$//')
PASSWORD=$(grep -E "^password\s*=" config.ini | sed 's/^password\s*=\s*//' | sed 's/\s*$//')

if [ -z "$EMAIL" ] || [ -z "$PASSWORD" ]; then
    echo "Error: email or password not set in config.ini"
    exit 1
fi

# Run the downloader
echo "Starting IMDb download..."
python download_imdb.py \
    --email "$EMAIL" \
    --password "$PASSWORD" \
    --output "$OUTPUT_DIR"

echo ""
echo "Download completed! Files saved to: $OUTPUT_DIR"
