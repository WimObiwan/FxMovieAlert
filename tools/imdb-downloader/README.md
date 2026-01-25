# IMDb Ratings & Watchlist Downloader

This tool automatically downloads your IMDb ratings and watchlist as CSV files using your IMDb credentials.

## Features

- Downloads IMDb ratings as `ratings.csv`
- Downloads IMDb watchlist as `watchlist.csv`
- Uses Selenium for browser automation
- Isolated Python virtual environment
- Simple configuration via config file

## Prerequisites

- Python 3.8 or later
- Chrome or Chromium browser (for Selenium)
- IMDb account with public ratings/watchlist

## Installation

1. Run the setup script to create virtual environment and install dependencies:

```bash
cd tools/imdb-downloader
./setup.sh
```

This will:
- Create a Python virtual environment in `venv/`
- Install required dependencies (Selenium)
- Prepare the tool for use

## Configuration

1. Copy the configuration template:

```bash
cp config.ini.template config.ini
```

2. Edit `config.ini` and add your IMDb credentials:

```ini
email = your-email@example.com
password = your-password-here
```

**Important:** The `config.ini` file is ignored by git to keep your credentials safe.

## Usage

### Simple Usage

Run the download script:

```bash
./download.sh
```

This will download both ratings and watchlist to the `./output` directory.

### Custom Output Directory

Specify a custom output directory:

```bash
./download.sh /path/to/output/directory
```

### Advanced Usage

You can also run the Python script directly for more control:

```bash
# Activate virtual environment
source venv/bin/activate

# Download both ratings and watchlist
python download_imdb.py --email YOUR_EMAIL --password YOUR_PASSWORD --output ./output

# Download only ratings
python download_imdb.py --email YOUR_EMAIL --password YOUR_PASSWORD --output ./output --no-watchlist

# Download only watchlist
python download_imdb.py --email YOUR_EMAIL --password YOUR_PASSWORD --output ./output --no-ratings

# Run with visible browser (for debugging)
python download_imdb.py --email YOUR_EMAIL --password YOUR_PASSWORD --output ./output --no-headless
```

## Output Files

The tool generates the following CSV files in the output directory:

- **ratings.csv** - Your IMDb ratings with columns: Const, Your Rating, Date Rated, Title, URL, Title Type, IMDb Rating, Runtime (mins), Year, Genres, Num Votes, Release Date, Directors
- **watchlist.csv** - Your IMDb watchlist with columns: Position, Const, Created, Modified, Description, Title, URL, Title Type, IMDb Rating, Runtime (mins), Year, Genres, Num Votes, Release Date, Directors

These CSV files are compatible with the existing FxMovieAlert import functionality.

## How It Works

1. The script uses Selenium to automate a Chrome browser
2. Logs into IMDb with your credentials
3. Navigates to your ratings and watchlist pages
4. Triggers the CSV export feature
5. Waits for IMDb to generate the exports
6. Downloads the CSV files to the output directory

## Updating

To update the tool dependencies:

```bash
source venv/bin/activate
pip install --upgrade -r requirements.txt
```

To completely reinstall:

```bash
rm -rf venv/
./setup.sh
```

## Troubleshooting

### Diagnostic Test

First, run the diagnostic script to check your setup:

```bash
./test_selenium.sh
```

This will check:
- Python installation
- Chrome/Chromium browser
- ChromeDriver
- Virtual environment
- Selenium installation
- WebDriver initialization

### Common Issues

#### Setup not completed

If you see "Error: Virtual environment not found":

```bash
./setup.sh
```

#### Missing configuration

If you see "Error: config.ini not found":

```bash
cp config.ini.template config.ini
# Edit config.ini with your IMDb credentials
```

#### Chrome/Chromium not found

If you see "Error: Chrome or Chromium browser not found":

```bash
# Ubuntu/Debian
sudo apt-get install chromium-browser

# Or download Chrome from https://www.google.com/chrome/
```

### Chrome driver issues

If you get errors about Chrome driver not found or initialization failing:

- Selenium 4.x automatically manages ChromeDriver
- Ensure Chrome/Chromium is installed and up to date
- Try updating Selenium: `source venv/bin/activate && pip install --upgrade selenium`

### Login failures

- Verify your credentials in `config.ini`
- Check if IMDb requires 2FA (not currently supported)
- Try running with `--no-headless` to see what's happening

### Empty exports

- Ensure your ratings/watchlist are public
- Check that you actually have items in your lists

## Security Notes

- Never commit `config.ini` to git (it's in .gitignore)
- Use a secure password manager for your credentials
- Consider using environment variables for CI/CD

## Files

- `download_imdb.py` - Main Python script
- `requirements.txt` - Python dependencies
- `setup.sh` - Setup script to create venv and install dependencies
- `download.sh` - Convenience script to run the downloader
- `test_selenium.sh` - Diagnostic test script
- `config.ini.template` - Configuration template
- `README.md` - This file

## License

This tool is part of the FxMovieAlert project.
