using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PromunSync.Core.Models;

namespace PromunSync.Data.Repositories;

/// <summary>
/// Repository for sinking normalized data blocks directly into the new PostgreSQL server instance.
/// </summary>
public interface IPostgresMeterSyncRepository
{
    /// <summary>
    /// Executes a bulk upsert of normalized reading points into the unified target database.
    /// </summary>
    Task<MeterSyncResult> BulkSinkToPostgresAsync(
        List<MeterReadingSyncItem> readings, 
        CancellationToken cancellationToken = default);
}