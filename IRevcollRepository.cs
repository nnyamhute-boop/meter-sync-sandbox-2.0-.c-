using PromunSync.Core.Entities.Inc;
using PromunSync.Core.Models;

namespace PromunSync.Data.Repositories;

/// <summary>
/// Repository for Revcoll database operations
/// ARCHITECTURE: Accepts entities for accounts, models for other types.
/// </summary>
public interface IRevcollRepository
{
    /// <summary>
    /// Bulk upserts customers to Revcoll using TVP and stored procedure.
    /// Uses spAccounts_BulkUpsertWithOutput with TVP_CustomerBatch for efficient bulk operations.
    /// Accepts Muncmf entities - service is responsible for mapping.
    /// </summary>
    /// <param name="accountEntities">List of Muncmf entities to upsert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>BulkUpsertResult containing counts of inserted/updated rows</returns>
    Task<BulkUpsertResult> BulkUpsertCustomersAsync(
        List<Muncmf> accountEntities,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets unprocessed payments from Revcoll
    /// </summary>
    Task<List<RevcollPayment>> GetUnprocessedPaymentsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates payment sync status after successful processing.
    /// Sets SyncDate=GETDATE(), SyncBy=syncUserId (does NOT modify Status field)
    /// </summary>
    Task<int> UpdatePaymentSyncStatusAsync(
        List<int> paymentIds,
        int syncUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts debtor types to Revcoll from Promun consumer types.
    /// Uses MERGE with IDENTITY_INSERT to maintain matching IDs between systems.
    /// Maps muncdesc.type → DebtorTypeID, muncdesc.des_eng → DebtorType
    /// </summary>
    /// <param name="consumerTypes">List of consumer types from Promun INC</param>
    /// <param name="createdByUserId">User ID for new records</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records upserted</returns>
    Task<int> UpsertDebtorTypesAsync(
        List<ConsumerType> consumerTypes,
        int createdByUserId,
        CancellationToken cancellationToken = default);
}
