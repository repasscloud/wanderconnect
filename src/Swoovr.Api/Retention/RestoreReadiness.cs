using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Swoovr.Infrastructure.Persistence;

namespace Swoovr.Api.Retention;

/// <summary>Operator-only restore marker; excluded from archives so replay cannot import a serving receipt.</summary>
public static class RestoreReadiness
{
    public static async Task<bool> ReadyAsync(SwoovrDbContext db,CancellationToken token)
    {
        var connection=db.Database.GetDbConnection();var opened=connection.State!=ConnectionState.Open;
        try
        {
            if(opened)await connection.OpenAsync(token);
            await using var command=connection.CreateCommand();
            command.CommandText="SELECT to_regclass('swoovr_operations.restore_guard') IS NOT NULL";
            // Fresh provisioning has no restore marker. Every supported restore creates its fence before importing SQL.
            if(await command.ExecuteScalarAsync(token) is not true)return true;
            command.CommandText="""
                SELECT EXISTS(SELECT 1 FROM swoovr_operations.restore_guard
                  WHERE id=1 AND role='api' AND ready AND authority_sha256 ~ '^[0-9a-f]{64}$'
                    AND reconciled_at IS NOT NULL AND reconciled_at>=restored_at)
                """;
            return await command.ExecuteScalarAsync(token) is true;
        }
        catch(DbException){return false;}
        finally{if(opened&&connection.State!=ConnectionState.Closed)await connection.CloseAsync();}
    }
}
