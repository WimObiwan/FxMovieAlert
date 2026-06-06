#!/bin/bash
#
# Test script to verify the setup
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=========================================="
echo "IMDb Downloader - Setup Test"
echo "=========================================="
echo ""

# Check Python
echo "1. Checking Python..."
if command -v python3 &> /dev/null; then
    PYTHON_VERSION=$(python3 --version)
    echo "   ✓ $PYTHON_VERSION"
else
    echo "   ✗ Python 3 not found"
    exit 1
fi

# Check venv
echo ""
echo "2. Checking virtual environment..."
if [ -d "venv" ]; then
    echo "   ✓ Virtual environment exists"
else
    echo "   ✗ Virtual environment not found"
    echo "   Run: ./setup.sh"
    exit 1
fi

# Test Python script syntax
echo ""
echo "3. Testing Python script..."
source venv/bin/activate
if python -m py_compile download_imdb.py 2>&1; then
    echo "   ✓ Python script is valid"
else
    echo "   ✗ Python script has syntax errors"
    exit 1
fi

# Test imports
echo ""
echo "4. Testing imports..."
if python -c "import webbrowser, pathlib, shutil, datetime; print('   ✓ All imports successful')" 2>&1; then
    :
else
    echo "   ✗ Import errors"
    exit 1
fi

echo ""
echo "=========================================="
echo "All checks passed!"
echo "=========================================="
echo ""
echo "You can now run: ./download.sh"
