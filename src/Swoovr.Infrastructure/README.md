# Infrastructure
Persistence (`Persistence/`: `SwoovrDbContext`, EF Core migrations for PostgreSQL, D68) lives here. Media, billing and notification integrations will join it. No provider credentials are included.

Add a migration after changing the model:

```sh
dotnet ef migrations add <Name> --project src/Swoovr.Infrastructure --output-dir Persistence/Migrations
```
