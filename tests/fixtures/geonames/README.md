# GeoNames test sample

A small, unmodified extract of the GeoNames `cities1000.txt`, `admin1CodesASCII.txt` and `countryInfo.txt` files (downloaded 24 October 2026), used by the persistence checks and the Travel browser suite (D111). It has look-alike names on purpose (several Norwoods, Mount Barkers, Springfields and Parises), places inside and outside the Adelaide market, and cities in other time zones.

Place data © [GeoNames](https://www.geonames.org/), licensed under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The full files are never committed: `./scripts/geonames.sh download` fetches them into `.local/geonames`.

Darwin, Palmerston and Zürich are also extracted unchanged from the cached public download for the owned market and traveller-search browser suites; no member or operational database data is included.

Magill (an Adelaide suburb about 7 km from the centre) is extracted unchanged the same way for the location-privacy browser suite, which needs a suburb 6–9 km away (8 November 2026).
