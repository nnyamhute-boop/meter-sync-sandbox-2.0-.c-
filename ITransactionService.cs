using PromunSync.Core.Models;

namespace PromunSync.Jobs.Services;

/// <summary>
/// Service for processing online payment transactions
/// </summary>
public interface ITransactionService
{
    /// <summary>
    /// Syncs unprocessed transactions from online database to Promun REC
    /// </summary>
    Task<ProcessingResult> SyncTransactionsAsync(CancellationToken cancellationToken = default);
}
