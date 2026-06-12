using PromunSync.Core.Entities.Inc;
using PromunSync.Core.Models;

namespace PromunSync.Data.Repositories;

/// <summary>
/// Repository for syncing data to the Online SQL Server database.
/// Uses spSync_BulkUpsert with TVPs for combined account/balance/balance-type upserts.
/// </summary>
public interface IOnlineSyncRepository
{
    /// <summary>
    /// Performs a combined bulk upsert of accounts, balances, and balance types
    /// via spSync_BulkUpsert with 3 TVP parameters in a single transaction.
    /// Pass empty lists for any TVP that is not needed.
    /// </summary>
    Task<BulkSyncResult> BulkSyncAsync(
        List<Muncmf> accounts,
        string laCode,
        List<Munbmf> balances,
        List<BalanceTypeDefinition> balanceTypes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Upserts currencies to tblCurrencies and their exchange rates to tblExchangeRates.
    /// </summary>
    Task<int> UpsertCurrenciesAsync(
        List<Currency> currencies,
        CancellationToken cancellationToken = default);
}
