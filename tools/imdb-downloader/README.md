# IMDb Ratings & Watchlist Downloader

This tool helps you download your IMDb ratings and watchlist as CSV files by opening your web browser where you can log in manually.

## Features

- Downloads IMDb ratings as `ratings.csv`
- Downloads IMDb watchlist as `watchlist.csv`
- Uses your default web browser (avoids CAPTCHA issues)
- No credentials stored - you log in through your browser
- Automatic file detection from your downloads folder
- Isolated Python virtual environment

## Prerequisites

- Python 3.8 or later
- A web browser (Chrome, Firefox, Safari, etc.)
- IMDb account

## Installation

1. Run the setup script to create virtual environment:

```bash
cd tools/imdb-downloader
./setup.sh
```

This will:
- Create a Python virtual environment in `venv/`
- Install dependencies (uses only Python standard library)
- Prepare the tool for use

## Usage

### Simple Usage

Run the download script:

```bash
./download.sh
```

This will:
1. Open your default browser to IMDb
2. Prompt you to log in and click Export buttons
3. Detect downloaded files from your Downloads folder
4. Copy files to the `./output` directory

### Custom Output Directory

Specify a custom output directory:

```bash
./download.sh /path/to/output/directory
```

### Advanced Usage

You can also run the Python script directly:

```bash
# Activate virtual environment
source venv/bin/activate

# Download both ratings and watchlist
python download_imdb.py --output ./output

# Download only ratings
python download_imdb.py --output ./output --no-watchlist

# Download only watchlist
python download_imdb.py --output ./output --no-ratings
```

## How It Works

1. **Opens Browser**: The script opens your default web browser to IMDb's export pages
2. **Manual Login**: You log in to IMDb in your browser (avoiding CAPTCHA)
3. **Manual Export**: You click the "Export" button for each list
4. **Auto Detection**: The script monitors your Downloads folder for the CSV files
5. **File Copy**: Downloaded files are copied to the output directory
6. **Cleanup**: Original files are removed from Downloads folder

## Output Files

The tool generates the following CSV files in the output directory:

- **ratings.csv** - Your IMDb ratings with columns: Const, Your Rating, Date Rated, Title, URL, Title Type, IMDb Rating, Runtime (mins), Year, Genres, Num Votes, Release Date, Directors
- **watchlist.csv** - Your IMDb watchlist with columns: Position, Const, Created, Modified, Description, Title, URL, Title Type, IMDb Rating, Runtime (mins), Year, Genres, Num Votes, Release Date, Directors

These CSV files are compatible with the existing FxMovieAlert import functionality.

## Updating

To update the tool:

```bash
cd tools/imdb-downloader
rm -rf venv/
./setup.sh
```

## Troubleshooting

### Browser doesn't open

If the browser doesn't open automatically:
- The script will show you the URLs to open manually
- Open https://www.imdb.com/list/ratings for ratings
- Open https://www.imdb.com/list/watchlist for watchlist

### Files not detected

If the script doesn't detect your downloaded files:
1. Check your Downloads folder for the CSV files
2. Manually copy them to the output directory:
   ```bash
   cp ~/Downloads/ratings*.csv ./output/ratings.csv
   cp ~/Downloads/watchlist*.csv ./output/watchlist.csv
   ```

### Download location

The script checks these common locations for your Downloads folder:
- `~/Downloads`
- `~/downloads`
- `~/Download`
- `~/download`

If your browser downloads to a different location, you can:
1. Change your browser's download location temporarily
2. Manually copy files after downloading

## Advantages Over Automated Login

**No CAPTCHA Issues**: By using your regular browser with manual login, IMDb doesn't detect automated access and won't show CAPTCHA.

**Always Works**: This method works regardless of IMDb's login page changes or security measures.

**Secure**: Your credentials are never stored or transmitted by the script.

**Simple**: No complex browser automation or ChromeDriver setup needed.

## Files

- `download_imdb.py` - Main Python script
- `requirements.txt` - Python dependencies (none required)
- `setup.sh` - Setup script to create venv
- `download.sh` - Convenience script to run the downloader
- `README.md` - This file

## License

This tool is part of the FxMovieAlert project.
