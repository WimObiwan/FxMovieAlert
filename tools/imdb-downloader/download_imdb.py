#!/usr/bin/env python3
"""
IMDb Ratings & Watchlist Downloader

This script helps you download your IMDb ratings and watchlist as CSV files.
It opens your default web browser where you can log in manually and download the exports.

Requirements:
- Python 3.8+

Usage:
    python download_imdb.py --output ./output

The script will:
1. Open your default browser to IMDb export pages
2. Wait for you to log in manually (avoiding CAPTCHA issues)
3. Guide you through downloading ratings.csv and watchlist.csv
4. Monitor your downloads folder and copy files to the output directory
"""

import os
import sys
import time
import argparse
import webbrowser
from pathlib import Path
import shutil
from datetime import datetime, timedelta


def get_downloads_folder():
    """Get the user's default downloads folder"""
    home = Path.home()
    
    # Try common download folder locations
    possible_paths = [
        home / "Downloads",
        home / "downloads", 
        home / "Download",
        home / "download"
    ]
    
    for path in possible_paths:
        if path.exists():
            return path
    
    # Default to Downloads
    return home / "Downloads"


def find_recent_csv(downloads_dir, filename_pattern, since_time):
    """Find recently downloaded CSV file matching pattern"""
    try:
        files = list(downloads_dir.glob(f"*{filename_pattern}*.csv"))
        
        # Filter by modification time
        recent_files = [
            f for f in files 
            if f.stat().st_mtime > since_time.timestamp()
        ]
        
        if recent_files:
            # Return most recent
            return max(recent_files, key=lambda f: f.stat().st_mtime)
    except Exception as e:
        print(f"Error searching for files: {e}")
    
    return None


def wait_for_download(downloads_dir, filename_pattern, timeout=300):
    """Wait for a file to be downloaded"""
    start_time = datetime.now()
    since_time = start_time - timedelta(seconds=5)  # Look for files from just before we started
    
    print(f"  Waiting for {filename_pattern} file (timeout: {timeout}s)...")
    print(f"  Checking: {downloads_dir}")
    
    while (datetime.now() - start_time).total_seconds() < timeout:
        file_path = find_recent_csv(downloads_dir, filename_pattern, since_time)
        if file_path:
            # Wait a bit to ensure download is complete
            time.sleep(2)
            
            # Check file size hasn't changed (download complete)
            size1 = file_path.stat().st_size
            time.sleep(1)
            size2 = file_path.stat().st_size
            
            if size1 == size2 and size1 > 0:
                return file_path
        
        time.sleep(2)
    
    return None


class ImdbDownloader:
    """Downloads IMDb ratings and watchlist CSV exports via web browser"""
    
    def __init__(self, output_dir):
        self.output_dir = Path(output_dir)
        self.downloads_dir = get_downloads_folder()
        
    def download_exports(self, download_ratings=True, download_watchlist=True):
        """Guide user through downloading exports"""
        print("=" * 60)
        print("IMDb Ratings & Watchlist Downloader")
        print("=" * 60)
        print()
        print("This tool will open your browser to IMDb where you can:")
        print("  1. Log in manually (avoiding CAPTCHA)")
        print("  2. Download your ratings and watchlist")
        print()
        print(f"Output directory: {self.output_dir.absolute()}")
        print(f"Downloads folder: {self.downloads_dir}")
        print("=" * 60)
        print()
        
        self.output_dir.mkdir(parents=True, exist_ok=True)
        
        results = {
            'ratings': False,
            'watchlist': False
        }
        
        if download_ratings:
            results['ratings'] = self._download_ratings()
        
        if download_watchlist:
            results['watchlist'] = self._download_watchlist()
        
        return results
    
    def _download_ratings(self):
        """Download ratings export"""
        print("\n" + "=" * 60)
        print("STEP 1: Download Ratings")
        print("=" * 60)
        print()
        print("Opening IMDb ratings page in your browser...")
        print()
        
        # Open ratings page
        url = "https://www.imdb.com/list/ratings"
        try:
            webbrowser.open(url)
            print("✓ Browser opened")
        except Exception as e:
            print(f"✗ Failed to open browser: {e}")
            print(f"  Please open this URL manually: {url}")
        
        print()
        print("Instructions:")
        print("  1. Log in to IMDb if prompted")
        print("  2. Look for the 'Export' button (usually top right)")
        print("  3. Click 'Export' to download ratings.csv")
        print()
        
        input("Press ENTER after you've clicked the Export button...")
        
        # Wait for download
        file_path = wait_for_download(self.downloads_dir, "ratings", timeout=300)
        
        if file_path:
            # Copy to output directory
            dest = self.output_dir / "ratings.csv"
            shutil.copy2(file_path, dest)
            print(f"✓ Ratings saved to: {dest}")
            
            # Optionally remove from downloads
            try:
                file_path.unlink()
                print(f"  Cleaned up: {file_path.name}")
            except:
                pass
            
            return True
        else:
            print("✗ Ratings file not found")
            print("  Please check your downloads folder and copy ratings.csv manually")
            return False
    
    def _download_watchlist(self):
        """Download watchlist export"""
        print("\n" + "=" * 60)
        print("STEP 2: Download Watchlist")
        print("=" * 60)
        print()
        print("Opening IMDb watchlist page in your browser...")
        print()
        
        # Open watchlist page
        url = "https://www.imdb.com/list/watchlist"
        try:
            webbrowser.open(url)
            print("✓ Browser opened")
        except Exception as e:
            print(f"✗ Failed to open browser: {e}")
            print(f"  Please open this URL manually: {url}")
        
        print()
        print("Instructions:")
        print("  1. Look for the 'Export' button (usually top right)")
        print("  2. Click 'Export' to download watchlist.csv")
        print()
        
        input("Press ENTER after you've clicked the Export button...")
        
        # Wait for download
        file_path = wait_for_download(self.downloads_dir, "watchlist", timeout=300)
        
        if file_path:
            # Copy to output directory
            dest = self.output_dir / "watchlist.csv"
            shutil.copy2(file_path, dest)
            print(f"✓ Watchlist saved to: {dest}")
            
            # Optionally remove from downloads
            try:
                file_path.unlink()
                print(f"  Cleaned up: {file_path.name}")
            except:
                pass
            
            return True
        else:
            print("✗ Watchlist file not found")
            print("  Please check your downloads folder and copy watchlist.csv manually")
            return False


def main():
    parser = argparse.ArgumentParser(
        description="Download IMDb ratings and watchlist as CSV files",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog="""
Examples:
  # Download both ratings and watchlist
  python download_imdb.py --output ./output
  
  # Download only ratings
  python download_imdb.py --output ./output --no-watchlist
  
  # Download only watchlist
  python download_imdb.py --output ./output --no-ratings

Note: This tool opens your web browser where you log in manually.
This avoids CAPTCHA issues with automated login.
        """
    )
    
    parser.add_argument("--output", required=True, help="Output directory for CSV files")
    parser.add_argument("--no-ratings", action="store_true", help="Skip downloading ratings")
    parser.add_argument("--no-watchlist", action="store_true", help="Skip downloading watchlist")
    
    args = parser.parse_args()
    
    download_ratings = not args.no_ratings
    download_watchlist = not args.no_watchlist
    
    if not download_ratings and not download_watchlist:
        print("Error: At least one of ratings or watchlist must be enabled")
        sys.exit(1)
    
    downloader = ImdbDownloader(output_dir=args.output)
    
    results = downloader.download_exports(download_ratings, download_watchlist)
    
    # Summary
    print("\n" + "=" * 60)
    print("Summary")
    print("=" * 60)
    
    if download_ratings:
        status = "✓" if results['ratings'] else "✗"
        print(f"{status} Ratings: {'Success' if results['ratings'] else 'Failed'}")
    
    if download_watchlist:
        status = "✓" if results['watchlist'] else "✗"
        print(f"{status} Watchlist: {'Success' if results['watchlist'] else 'Failed'}")
    
    print()
    
    all_success = (
        (not download_ratings or results['ratings']) and
        (not download_watchlist or results['watchlist'])
    )
    
    if all_success:
        print("SUCCESS! All files downloaded to:", args.output)
        sys.exit(0)
    else:
        print("Some downloads failed. Check the output above.")
        sys.exit(1)


if __name__ == "__main__":
    main()
