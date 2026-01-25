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

# Activate virtual environment
source venv/bin/activate

# Run the downloader
echo "Starting IMDb download..."
if ! python download_imdb.py --output "$OUTPUT_DIR"; then
    echo ""
    echo "=========================================="
    echo "Download failed!"
    echo "=========================================="
    echo ""
    exit 1
fi

echo ""
echo "Download completed! Files saved to: $OUTPUT_DIR"
