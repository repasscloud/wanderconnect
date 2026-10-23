using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using Swoovr.Domain;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Infrastructure.Places;

/// <summary>
/// Imports GeoNames cities1000 with admin1CodesASCII and countryInfo (D111) into geo_cities and geo_city_names. The
/// import is an upsert, so running it again (or importing the test sample into a full copy) changes only what differs.
/// Place data © GeoNames, CC BY 4.0.
/// </summary>
public static class GeoNamesImport
{
    public const int MaxNameLength = 200;

    public sealed record Parsed(IReadOnlyList<GeoCity> Cities, IReadOnlyList<GeoCityName> Names);

    /// <summary>Parses the three tab-separated files in a folder. Comment lines start with '#'.</summary>
    public static Parsed Parse(string folder)
    {
        static IEnumerable<string[]> Rows(string path) => File.ReadLines(path).Where(l => l.Length > 0 && l[0] != '#').Select(l => l.Split('\t'));
        var countries = Rows(Path.Combine(folder, "countryInfo.txt")).Where(r => r.Length > 4).ToDictionary(r => r[0], r => r[4]);
        var regions = Rows(Path.Combine(folder, "admin1CodesASCII.txt")).Where(r => r.Length > 1).ToDictionary(r => r[0], r => r[1]);
        var cities = new List<GeoCity>();
        var names = new List<GeoCityName>();
        foreach (var r in Rows(Path.Combine(folder, "cities1000.txt")))
        {
            if (r.Length < 18 || !int.TryParse(r[0], out var id)) continue;
            var admin1 = r[10].Length > 0 ? r[10] : null;
            var city = new GeoCity
            {
                GeoNameId = id, Name = Clip(r[1]), AsciiName = Clip(r[2]), CountryCode = r[8],
                CountryName = countries.GetValueOrDefault(r[8], r[8]), Admin1Code = admin1,
                Admin1Name = admin1 is null ? null : regions.GetValueOrDefault($"{r[8]}.{admin1}"),
                Population = long.TryParse(r[14], out var population) ? population : 0, TimeZone = r[17],
                Latitude = double.Parse(r[4], System.Globalization.CultureInfo.InvariantCulture),
                Longitude = double.Parse(r[5], System.Globalization.CultureInfo.InvariantCulture),
            };
            cities.Add(city);
            names.AddRange(new[] { r[1], r[2] }.Concat(r[3].Split(',')).Select(PlaceNames.Key)
                .Where(k => k.Length is > 0 and <= MaxNameLength).Distinct().Select(k => new GeoCityName { GeoNameId = id, Key = k }));
        }
        return new(cities, names);
    }

    private static string Clip(string value) => value.Length <= MaxNameLength ? value : value[..MaxNameLength];

    /// <summary>Bulk-loads into temporary tables with COPY, then upserts. Returns the city and name counts.</summary>
    public static async Task<(int Cities, int Names)> ImportAsync(SwoovrDbContext db, string folder, CancellationToken cancellationToken = default)
    {
        var parsed = Parse(folder);
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await Execute(connection, "CREATE TEMP TABLE import_cities (LIKE geo_cities) ON COMMIT DROP; CREATE TEMP TABLE import_names (LIKE geo_city_names) ON COMMIT DROP;", cancellationToken);
            await using (var copy = await connection.BeginBinaryImportAsync("COPY import_cities (geo_name_id, name, ascii_name, country_code, country_name, admin1_code, admin1_name, population, time_zone, latitude, longitude) FROM STDIN (FORMAT BINARY)", cancellationToken))
            {
                foreach (var c in parsed.Cities)
                {
                    await copy.StartRowAsync(cancellationToken);
                    await copy.WriteAsync(c.GeoNameId, NpgsqlDbType.Integer, cancellationToken);
                    await copy.WriteAsync(c.Name, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(c.AsciiName, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(c.CountryCode, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(c.CountryName, NpgsqlDbType.Varchar, cancellationToken);
                    if (c.Admin1Code is null) await copy.WriteNullAsync(cancellationToken); else await copy.WriteAsync(c.Admin1Code, NpgsqlDbType.Varchar, cancellationToken);
                    if (c.Admin1Name is null) await copy.WriteNullAsync(cancellationToken); else await copy.WriteAsync(c.Admin1Name, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(c.Population, NpgsqlDbType.Bigint, cancellationToken);
                    await copy.WriteAsync(c.TimeZone, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(c.Latitude, NpgsqlDbType.Double, cancellationToken);
                    await copy.WriteAsync(c.Longitude, NpgsqlDbType.Double, cancellationToken);
                }
                await copy.CompleteAsync(cancellationToken);
            }
            await using (var copy = await connection.BeginBinaryImportAsync("COPY import_names (key, geo_name_id) FROM STDIN (FORMAT BINARY)", cancellationToken))
            {
                foreach (var n in parsed.Names)
                {
                    await copy.StartRowAsync(cancellationToken);
                    await copy.WriteAsync(n.Key, NpgsqlDbType.Varchar, cancellationToken);
                    await copy.WriteAsync(n.GeoNameId, NpgsqlDbType.Integer, cancellationToken);
                }
                await copy.CompleteAsync(cancellationToken);
            }
            await Execute(connection, """
                INSERT INTO geo_cities SELECT * FROM import_cities
                ON CONFLICT (geo_name_id) DO UPDATE SET name = EXCLUDED.name, ascii_name = EXCLUDED.ascii_name, country_code = EXCLUDED.country_code,
                  country_name = EXCLUDED.country_name, admin1_code = EXCLUDED.admin1_code, admin1_name = EXCLUDED.admin1_name,
                  population = EXCLUDED.population, time_zone = EXCLUDED.time_zone, latitude = EXCLUDED.latitude, longitude = EXCLUDED.longitude;
                DELETE FROM geo_city_names n USING import_cities c WHERE n.geo_name_id = c.geo_name_id
                  AND NOT EXISTS (SELECT 1 FROM import_names i WHERE i.key = n.key AND i.geo_name_id = n.geo_name_id);
                INSERT INTO geo_city_names SELECT * FROM import_names ON CONFLICT DO NOTHING;
                """, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        finally { await db.Database.CloseConnectionAsync(); }
        return (parsed.Cities.Count, parsed.Names.Count);
    }

    private static async Task Execute(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.CommandTimeout = 600;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
