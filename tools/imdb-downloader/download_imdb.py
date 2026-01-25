#!/usr/bin/env python3
"""
IMDb Ratings & Watchlist Downloader

This script downloads your IMDb ratings and watchlist as CSV files.
It uses Selenium to automate the login and export process.

Requirements:
- Python 3.8+
- Selenium
- Chrome/Chromium browser

Usage:
    python download_imdb.py --email YOUR_EMAIL --password YOUR_PASSWORD --output ./output

The script will:
1. Login to IMDb using your credentials
2. Navigate to the exports page
3. Generate and download ratings.csv and watchlist.csv
4. Save them to the output directory
"""

import os
import sys
import time
import argparse
from pathlib import Path
from selenium import webdriver
from selenium.webdriver.common.by import By
from selenium.webdriver.support.ui import WebDriverWait
from selenium.webdriver.support import expected_conditions as EC
from selenium.common.exceptions import TimeoutException, NoSuchElementException


class ImdbDownloader:
    """Downloads IMDb ratings and watchlist CSV exports"""
    
    def __init__(self, email, password, output_dir, headless=True):
        self.email = email
        self.password = password
        self.output_dir = Path(output_dir)
        self.headless = headless
        self.driver = None
        self.wait = None
        
    def setup_driver(self):
        """Initialize Chrome WebDriver with download preferences"""
        print("Initializing Chrome WebDriver...")
        options = webdriver.ChromeOptions()
        
        if self.headless:
            # Use new headless mode for better compatibility with modern Chrome
            options.add_argument('--headless=new')
        
        options.add_argument('--no-sandbox')
        options.add_argument('--disable-dev-shm-usage')
        options.add_argument('--disable-gpu')
        options.add_argument('--window-size=1920,1080')
        
        # Set download directory
        self.output_dir.mkdir(parents=True, exist_ok=True)
        prefs = {
            "download.default_directory": str(self.output_dir.absolute()),
            "download.prompt_for_download": False,
            "download.directory_upgrade": True,
            "safebrowsing.enabled": False
        }
        options.add_experimental_option("prefs", prefs)
        
        try:
            print("  Creating Chrome driver (this may take a moment)...")
            self.driver = webdriver.Chrome(options=options)
            self.wait = WebDriverWait(self.driver, 30)
            print("✓ WebDriver initialized")
        except Exception as e:
            print(f"✗ Failed to initialize WebDriver: {e}")
            print("\nTroubleshooting:")
            print("  1. Ensure Chrome or Chromium is installed")
            print("  2. Check if ChromeDriver is compatible with your Chrome version")
            print("  3. Try running with --no-headless to see browser errors")
            raise
        
    def login(self):
        """Login to IMDb"""
        print(f"Logging in to IMDb as {self.email}...")
        
        try:
            # Navigate to IMDb sign-in page
            self.driver.get("https://www.imdb.com/ap/signin?openid.pape.max_auth_age=0")
            print("  Loaded login page")
            
            # Wait for and fill email field
            email_field = self.wait.until(
                EC.presence_of_element_located((By.ID, "ap_email"))
            )
            email_field.send_keys(self.email)
            print("  Entered email")
            
            # Fill password field
            password_field = self.driver.find_element(By.ID, "ap_password")
            password_field.send_keys(self.password)
            print("  Entered password")
            
            # Click sign-in button
            sign_in_button = self.driver.find_element(By.ID, "signInSubmit")
            sign_in_button.click()
            print("  Clicked sign-in button")
            
            # Wait for login to complete (check for user menu or redirect)
            time.sleep(5)
            
            # Check for error messages on the login page
            try:
                error_box = self.driver.find_element(By.ID, "auth-error-message-box")
                if error_box:
                    error_text = error_box.text
                    print(f"✗ Login error message: {error_text}")
                    return False
            except:
                pass  # No error box found, continue
            
            # Check for CAPTCHA
            try:
                captcha = self.driver.find_element(By.ID, "auth-captcha-image")
                if captcha:
                    print("✗ CAPTCHA detected - automated login not possible")
                    print("  IMDb has detected automated access and requires CAPTCHA verification")
                    print("  Please try one of these alternatives:")
                    print("    1. Export CSV manually from IMDb website")
                    print("    2. Wait a while and try again later")
                    print("    3. Try from a different IP address")
                    return False
            except:
                pass  # No CAPTCHA found, continue
            
            # Verify login success by checking for user menu or profile
            try:
                self.wait.until(
                    EC.presence_of_element_located((By.CSS_SELECTOR, "[data-testid='user-menu']"))
                )
                print("✓ Successfully logged in")
                return True
            except TimeoutException:
                # Try alternative selector
                try:
                    self.driver.find_element(By.CSS_SELECTOR, "[data-testid='user-profile-menu']")
                    print("✓ Successfully logged in")
                    return True
                except:
                    pass
                
                print("✗ Login verification failed - user menu not found")
                print("  Current URL:", self.driver.current_url)
                
                # Save screenshot for debugging
                screenshot_path = self.output_dir / "login_failure.png"
                self.output_dir.mkdir(parents=True, exist_ok=True)
                self.driver.save_screenshot(str(screenshot_path))
                print(f"  Screenshot saved to: {screenshot_path}")
                print("  Check the screenshot to see what went wrong")
                
                return False
                
        except Exception as e:
            print(f"✗ Login failed with error: {e}")
            print(f"  Error type: {type(e).__name__}")
            
            # Save screenshot for debugging
            try:
                screenshot_path = self.output_dir / "login_error.png"
                self.output_dir.mkdir(parents=True, exist_ok=True)
                self.driver.save_screenshot(str(screenshot_path))
                print(f"  Screenshot saved to: {screenshot_path}")
            except:
                pass
            
            import traceback
            print("\nFull error details:")
            traceback.print_exc()
            
            return False
    
    def generate_exports(self, download_ratings=True, download_watchlist=True):
        """Generate CSV exports on IMDb"""
        print("\nGenerating exports...")
        
        if download_ratings:
            self._generate_export("ratings", "https://www.imdb.com/list/ratings")
            
        if download_watchlist:
            self._generate_export("watchlist", "https://www.imdb.com/list/watchlist")
        
        print("✓ Export generation completed")
    
    def _generate_export(self, list_type, url):
        """Generate a single export"""
        print(f"  Generating {list_type} export...")
        
        self.driver.get(url)
        time.sleep(2)
        
        try:
            # Find and click the export button
            export_button = self.wait.until(
                EC.element_to_be_clickable((By.CSS_SELECTOR, "div[data-testid*='hero-list-subnav-export-button'] button"))
            )
            self.driver.execute_script("arguments[0].scrollIntoView(true);", export_button)
            time.sleep(1)
            self.driver.execute_script("arguments[0].click();", export_button)
            print(f"  ✓ {list_type} export requested")
            time.sleep(2)
        except TimeoutException:
            print(f"  ! Export button not found for {list_type} (list may be empty)")
    
    def wait_for_exports(self):
        """Wait for exports to be ready on IMDb's exports page"""
        print("\nWaiting for exports to be ready...")
        
        max_wait_time = 600  # 10 minutes
        start_time = time.time()
        
        while time.time() - start_time < max_wait_time:
            self.driver.get("https://www.imdb.com/exports/")
            time.sleep(3)
            
            try:
                # Check if any exports are still processing
                summary_items = self.driver.find_elements(By.CSS_SELECTOR, ".ipc-metadata-list-summary-item")
                
                if not summary_items:
                    print("  ! No export items found")
                    break
                
                in_progress = False
                for item in summary_items:
                    if "in progress" in item.text.lower():
                        in_progress = True
                        break
                
                if not in_progress:
                    print("✓ All exports are ready")
                    return True
                else:
                    print("  ⏳ Exports still processing, waiting 10 seconds...")
                    time.sleep(10)
                    
            except Exception as e:
                print(f"  ! Error checking export status: {e}")
                break
        
        print("✗ Timeout waiting for exports")
        return False
    
    def download_exports(self, download_ratings=True, download_watchlist=True):
        """Download the generated CSV exports"""
        print("\nDownloading exports...")
        
        self.driver.get("https://www.imdb.com/exports/")
        time.sleep(3)
        
        try:
            summary_items = self.driver.find_elements(By.CSS_SELECTOR, ".ipc-metadata-list-summary-item")
            
            if not summary_items:
                print("✗ No export items found on exports page")
                return False
            
            # Download ratings
            if download_ratings:
                self._download_export(summary_items, "ratings", "ratings.csv")
            
            # Download watchlist
            if download_watchlist:
                self._download_export(summary_items, "watchlist", "watchlist.csv")
            
            print("✓ Downloads completed")
            return True
            
        except Exception as e:
            print(f"✗ Error downloading exports: {e}")
            return False
    
    def _download_export(self, summary_items, list_type, filename):
        """Download a single export file"""
        print(f"  Downloading {list_type}...")
        
        # Clear any existing CSV files
        for file in self.output_dir.glob("*.csv"):
            if file.name.startswith("imdb-") or file.name in ["ratings.csv", "watchlist.csv", "checkins.csv"]:
                file.unlink()
        
        # Find the download button for this list type
        button = None
        for item in summary_items:
            if list_type.lower() in item.text.lower():
                try:
                    button = item.find_element(By.CSS_SELECTOR, "button[data-testid*='export-status-button']")
                    break
                except NoSuchElementException:
                    pass
        
        if not button:
            print(f"  ! Download button not found for {list_type}")
            return
        
        # Click download button
        self.driver.execute_script("arguments[0].scrollIntoView(true);", button)
        time.sleep(1)
        self.driver.execute_script("arguments[0].click();", button)
        
        # Wait for download to complete (check for new files)
        max_wait = 30
        start_time = time.time()
        initial_files = set(self.output_dir.glob("*.csv"))
        
        while time.time() - start_time < max_wait:
            current_files = set(self.output_dir.glob("*.csv"))
            new_files = current_files - initial_files
            if new_files:
                break
            time.sleep(2)  # Check every 2 seconds to reduce I/O
        
        # Give a bit more time for file to finish writing
        time.sleep(2)
        
        # Find the downloaded file and rename it
        downloaded_files = sorted(
            [f for f in self.output_dir.glob("*.csv")],
            key=lambda f: f.stat().st_mtime,
            reverse=True
        )
        
        if downloaded_files:
            target_path = self.output_dir / filename
            downloaded_files[0].rename(target_path)
            print(f"  ✓ Saved as {filename}")
        else:
            print(f"  ! Could not find downloaded file for {list_type}")
    
    def cleanup(self):
        """Close the browser"""
        if self.driver:
            self.driver.quit()
            print("\n✓ Browser closed")
    
    def run(self, download_ratings=True, download_watchlist=True):
        """Main execution flow"""
        try:
            self.setup_driver()
            
            if not self.login():
                return False
            
            self.generate_exports(download_ratings, download_watchlist)
            
            if not self.wait_for_exports():
                return False
            
            if not self.download_exports(download_ratings, download_watchlist):
                return False
            
            return True
            
        finally:
            self.cleanup()


def main():
    parser = argparse.ArgumentParser(
        description="Download IMDb ratings and watchlist as CSV files",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Download both ratings and watchlist
  python download_imdb.py --email user@example.com --password mypass --output ./output
  
  # Download only ratings
  python download_imdb.py --email user@example.com --password mypass --output ./output --no-watchlist
  
  # Download only watchlist
  python download_imdb.py --email user@example.com --password mypass --output ./output --no-ratings
  
  # Run with visible browser (not headless)
  python download_imdb.py --email user@example.com --password mypass --output ./output --no-headless
        """
    )
    
    parser.add_argument("--email", required=True, help="IMDb account email")
    parser.add_argument("--password", required=True, help="IMDb account password")
    parser.add_argument("--output", required=True, help="Output directory for CSV files")
    parser.add_argument("--no-ratings", action="store_true", help="Skip downloading ratings")
    parser.add_argument("--no-watchlist", action="store_true", help="Skip downloading watchlist")
    parser.add_argument("--no-headless", action="store_true", help="Show browser window (not headless)")
    
    args = parser.parse_args()
    
    download_ratings = not args.no_ratings
    download_watchlist = not args.no_watchlist
    
    if not download_ratings and not download_watchlist:
        print("Error: At least one of ratings or watchlist must be enabled")
        sys.exit(1)
    
    print("=" * 60)
    print("IMDb Ratings & Watchlist Downloader")
    print("=" * 60)
    print(f"Email: {args.email}")
    print(f"Output: {args.output}")
    print(f"Download ratings: {download_ratings}")
    print(f"Download watchlist: {download_watchlist}")
    print("=" * 60)
    print()
    
    downloader = ImdbDownloader(
        email=args.email,
        password=args.password,
        output_dir=args.output,
        headless=not args.no_headless
    )
    
    success = downloader.run(download_ratings, download_watchlist)
    
    if success:
        print("\n" + "=" * 60)
        print("SUCCESS! Files downloaded to:", args.output)
        print("=" * 60)
        sys.exit(0)
    else:
        print("\n" + "=" * 60)
        print("FAILED! Check the errors above")
        print("=" * 60)
        sys.exit(1)


if __name__ == "__main__":
    main()
