#!/bin/bash
#
# Test script to diagnose Selenium/ChromeDriver issues
#

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=========================================="
echo "Selenium/ChromeDriver Diagnostic Test"
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

# Check Chrome/Chromium
echo ""
echo "2. Checking Chrome/Chromium..."
if command -v google-chrome &> /dev/null; then
    CHROME_VERSION=$(google-chrome --version)
    echo "   ✓ $CHROME_VERSION"
elif command -v chromium &> /dev/null; then
    CHROME_VERSION=$(chromium --version)
    echo "   ✓ $CHROME_VERSION"
elif command -v chromium-browser &> /dev/null; then
    CHROME_VERSION=$(chromium-browser --version)
    echo "   ✓ $CHROME_VERSION"
else
    echo "   ✗ Chrome/Chromium not found"
    echo "   Please install Chrome or Chromium"
    exit 1
fi

# Check ChromeDriver
echo ""
echo "3. Checking ChromeDriver..."
if command -v chromedriver &> /dev/null; then
    DRIVER_VERSION=$(chromedriver --version 2>&1 | head -1)
    echo "   ✓ $DRIVER_VERSION"
else
    echo "   ℹ ChromeDriver not in PATH (Selenium will try to download it automatically)"
fi

# Check venv
echo ""
echo "4. Checking virtual environment..."
if [ -d "venv" ]; then
    echo "   ✓ Virtual environment exists"
else
    echo "   ✗ Virtual environment not found"
    echo "   Run: ./setup.sh"
    exit 1
fi

# Check Selenium
echo ""
echo "5. Checking Selenium installation..."
source venv/bin/activate
if python -c "import selenium; print(f'   ✓ Selenium {selenium.__version__}')" 2>&1; then
    :
else
    echo "   ✗ Selenium not installed"
    echo "   Run: ./setup.sh"
    exit 1
fi

# Test WebDriver initialization
echo ""
echo "6. Testing WebDriver initialization..."
echo "   (This may take a moment...)"

timeout 30 python << 'EOF'
import sys
from selenium import webdriver
from selenium.webdriver.chrome.options import Options

try:
    options = Options()
    options.add_argument('--headless=new')
    options.add_argument('--no-sandbox')
    options.add_argument('--disable-dev-shm-usage')
    options.add_argument('--disable-gpu')
    
    driver = webdriver.Chrome(options=options)
    print("   ✓ WebDriver initialized successfully")
    driver.quit()
    print("   ✓ WebDriver closed successfully")
    sys.exit(0)
except Exception as e:
    print(f"   ✗ WebDriver initialization failed: {e}")
    import traceback
    traceback.print_exc()
    sys.exit(1)
EOF

if [ $? -eq 0 ]; then
    echo ""
    echo "=========================================="
    echo "All checks passed!"
    echo "=========================================="
    echo ""
    echo "You can now run: ./download.sh"
else
    echo ""
    echo "=========================================="
    echo "Some checks failed!"
    echo "=========================================="
    echo ""
    echo "Please address the errors above before running ./download.sh"
    exit 1
fi
