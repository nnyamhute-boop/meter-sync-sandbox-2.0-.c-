using PromunSync.Core.Entities.Inc;
using PromunSync.Core.Models;

namespace PromunSync.Data.Repositories;

/// <summary>
/// Repository for reading from Promun INC (Debtors) database.
/// Tables: PUB.muncmf (accounts), PUB.munbmf (balances), PUB.muntyp (balance types)
/// Uses keyset pagination with primary key indexes for efficient batch processing.
/// ARCHITECTURE: Repository works with entities only - callers must map to domain models.
/// </summary>
public interface IPromunDebtorsRepository
{
    /// <summary>
    /// Gets account entities from PUB.muncmf using keyset pagination.
    /// Uses the (company, acc) primary key index for efficient cursor-based iteration.
    /// Returns database entities - caller must map to PromunAccount model using .ToModel().
    /// </summary>
    /// <param name="company">Company code to filter by (uses PK index)</param>
    /// <param name="batchSize">Maximum number of records to return</param>
    /// <param name="lastAccountNumber">Last account number from previous batch (keyset cursor), null for first batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Muncmf entities with acc > lastAccountNumber, ordered by acc</returns>
    Task<List<Muncmf>> GetAccountEntitiesAsync(
        int company,
        int batchSize,
        decimal? lastAccountNumber,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total account count from PUB.muncmf for a specific company.
    /// </summary>
    /// <param name="company">Company code to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> GetAccountCountAsync(int company, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets balance entities from PUB.munbmf using keyset pagination.
    /// Uses the (company, acc, type) primary key index for efficient cursor-based iteration.
    /// Returns database entities - caller must map to Balance model using .ToModel().
    /// </summary>
    /// <param name="company">Company code to filter by (uses PK index)</param>
    /// <param name="batchSize">Maximum number of records to return</param>
    /// <param name="lastAccountNumber">Last account number from previous batch (keyset cursor), null for first batch</param>
    /// <param name="lastBalanceType">Last balance type from previous batch (for composite keyset), null for first batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of Munbmf entities ordered by (acc, type) after the keyset cursor</returns>
    Task<List<Munbmf>> GetBalanceEntitiesAsync(
        int company,
        int batchSize,
        decimal? lastAccountNumber,
        int? lastBalanceType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets total balance count from PUB.munbmf for a specific company.
    /// </summary>
    /// <param name="company">Company code to filter by</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task<int> GetBalanceCountAsync(int company, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets balance type entities from PUB.muntyp.
    /// Returns database entities - caller must map to BalanceTypeDefinition model.
    /// </summary>
    Task<List<Muntyp>> GetBalanceTypeEntitiesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets consumer type entities from PUB.muncdesc.
    /// Returns database entities - caller must map to ConsumerType model.
    /// </summary>
    Task<List<Muncdesc>> GetConsumerTypeEntitiesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets account entities with their associated balance entities in a single combined operation.
    /// Fetches one batch of accounts from PUB.muncmf, then fetches all balances for
    /// those accounts from PUB.munbmf in a single range query.
    /// Returns entity tuples - caller must map both accounts and balances to models.
    /// </summary>
    /// <param name="company">Company code to filter by</param>
    /// <param name="batchSize">Maximum number of accounts to return</param>
    /// <param name="lastAccountNumber">Last account number from previous batch (keyset cursor), null for first batch</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of (Muncmf, List&lt;Munbmf&gt;) tuples</returns>
    Task<List<(Muncmf Account, List<Munbmf> Balances)>> GetAccountsWithBalanceEntitiesAsync(
        int company,
        int batchSize,
        decimal? lastAccountNumber,
        CancellationToken cancellationToken = default);
}
