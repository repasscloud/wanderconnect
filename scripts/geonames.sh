#!/usr/bin/env bash
# GeoNames place data for Travel and home cities (D111). Downloads cities1000, admin1CodesASCII and countryInfo from
# download.geonames.org into .local/geonames (ignored, never committed), then imports them into the local database
# through the API's operator command. Data © GeoNames, CC BY 4.0 (https://www.geonames.org/).
#   ./scripts/geonames.sh download   fetch the three files
#   ./scripts/geonames.sh import     import .local/geonames into the local database (needs ./scripts/docker-local.sh up)
#   ./scripts/geonames.sh sample     import the small committed test sample (tests/fixtures/geonames) instead
set -euo pipefail
cd "$(dirname "$0")/.."
dir=.local/geonames
val(){ grep "^$1=" .local/docker.env | cut -d= -f2; }
run_import() {
  dotnet build src/Swoovr.Api -c Debug -v quiet -nologo >/dev/null
  ConnectionStrings__Swoovr="Host=127.0.0.1;Port=5433;Database=swoovr;Username=swoovr;Password=$(val SWOOVR_DB_PASSWORD)" \
    DOTNET_ENVIRONMENT=Development dotnet src/Swoovr.Api/bin/Debug/net10.0/Swoovr.Api.dll geonames import "$1"
}
case "${1:-}" in
  download)
    mkdir -p "$dir"
    for file in cities1000.zip admin1CodesASCII.txt countryInfo.txt; do
      curl -fsSL --retry 3 -o "$dir/$file.part" "https://download.geonames.org/export/dump/$file" && mv "$dir/$file.part" "$dir/$file"
    done
    (cd "$dir" && unzip -oq cities1000.zip cities1000.txt)
    wc -l "$dir/cities1000.txt" "$dir/admin1CodesASCII.txt" "$dir/countryInfo.txt" ;;
  import) run_import "$dir" ;;
  sample) run_import tests/fixtures/geonames ;;
  *) echo "usage: $0 download|import|sample" >&2; exit 2 ;;
esac
