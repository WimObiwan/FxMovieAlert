
# Usage:
#   ./scripts/generate-imdb.sh "net6.0" "user@server.domain.tld" "/var/www/www.domain.tld"

# 2021-12-08: Script takes +/- 36 minutes

export RELEASE=$1
export TARGET_SERVER=$2
export TARGET_PATH=$3

dotnet build --configuration Release

wget https://datasets.imdbws.com/title.akas.tsv.gz -O ~/tmp/filmoptv/imdb-datasets/title.akas.tsv.gz
wget https://datasets.imdbws.com/title.basics.tsv.gz -O ~/tmp/filmoptv/imdb-datasets/title.basics.tsv.gz
wget https://datasets.imdbws.com/title.ratings.tsv.gz -O ~/tmp/filmoptv/imdb-datasets/title.ratings.tsv.gz

cd Grabber
dotnet ./bin/Release/$RELEASE/Grabber.dll GenerateImdbDatabase

rsync -av --info=progress2 ~/tmp/filmoptv/db/imdb.db $TARGET_SERVER:$TARGET_PATH/
