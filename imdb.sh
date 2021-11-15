
export TARGET="www"

wget https://datasets.imdbws.com/title.akas.tsv.gz -O ~/tmp/filmoptv/title.akas.tsv.gz
wget https://datasets.imdbws.com/title.basics.tsv.gz -O ~/tmp/filmoptv/title.basics.tsv.gz
wget https://datasets.imdbws.com/title.ratings.tsv.gz -O ~/tmp/filmoptv/title.ratings.tsv.gz

cd Grabber
dotnet ./bin/Release/net5.0/Grabber.dll GenerateImdbDatabase

rsync -av --info=progress2 ~/tmp/filmoptv/imdb.db root@server3.foxinnovations.be:/var/www/$TARGET.filmoptv.be/
