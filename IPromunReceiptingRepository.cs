using PromunSync.Core.Entities.Rec;
using PromunSync.Core.Models;

namespace PromunSync.Data.Repositories;

/// <summary>
/// Repository for writing to Promun REC (Receipting) database.
/// Tables: PUB.munrct (receipts), PUB.munrctctr (receipt control), PUB.municf (income codes)
/// ARCHITECTURE: Provides both entity-based methods (preferred) and legacy model-based methods.
/// </summary>
public interface IPromunReceiptingRepository
{
    /// <summary>
    /// Validation helper: Checks if an income code exists in PUB.municf.
    /// Called by service layer for pre-validation before mapping to entities.
    /// </summary>
    Task<bool> IncomeCodeExistsAsync(string incomeCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validation helper: Gets account details (name) from PUB.muncmf for a single account.
    /// Called by service layer for pre-validation before mapping to entities.
    /// Returns null if account doesn't exist.
    /// </summary>
    Task<ReceiptAccountDetails?> GetAccountDetailsAsync(string accountNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validation helper: Batch fetches account details for multiple accounts.
    /// Optimized for pre-validation of transaction batches.
    /// Returns a dictionary mapping account number -> details (null if not found).
    /// </summary>
    Task<Dictionary<string, ReceiptAccountDetails?>> GetAccountDetailsBatchAsync(
        List<string> accountNumbers,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates receipt entities in Promun REC database (entity-based, preferred method).
    /// Accepts pre-validated Munrct entities from the service layer.
    /// Multi-step process: ensures receipt control file exists, checks for duplicates, inserts receipts.
    /// </summary>
    /// <param name="receiptEntities">Pre-validated receipt entities to insert</param>
    /// <param name="machineNumber">Machine number - used for control file and receipt numbering</param>
    /// <param name="valid">Valid flag for control file - maps to munrctctr.valid (BOOL)</param>
    /// <param name="receiptStatus">Receipt status for control file - maps to munrctctr.rec_status (STRING)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Processing result with success/failure counts and report entries</returns>
    Task<ProcessingResult> CreateReceiptEntitiesAsync(
        List<Munrct> receiptEntities,
        int machineNumber,
        bool valid,
        string receiptStatus,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all income codes from PUB.municf for selection prompts.
    /// Returns (code, description) pairs.
    /// </summary>
    Task<List<(string Code, string Description)>> GetIncomeCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all operators from PUB.munocf for selection prompts.
    /// Returns (op_code, full_name) pairs.
    /// </summary>
    Task<List<(string Code, string Name)>> GetOperatorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all machines from PUB.munmmf for selection prompts.
    /// Returns (mcno, description) pairs.
    /// </summary>
    Task<List<(int MachineNumber, string Description)>> GetMachinesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an operator code exists in PUB.munocf.
    /// </summary>
    Task<bool> OperatorExistsAsync(string operatorCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a machine number exists in PUB.munmmf.
    /// </summary>
    Task<bool> MachineExistsAsync(int machineNumber, CancellationToken cancellationToken = default);

    /// <summary>
    /// Legacy method: Creates receipt transactions in Promun REC database (model-based).
    /// DEPRECATED: Use CreateReceiptEntitiesAsync for new code.
    /// Multi-step process: validates, maps internally, then creates receipts.
    /// </summary>
    Task<ProcessingResult> CreateReceiptsAsync(
        List<Transaction> transactions,
        int machineNumber,
        string laCode,
        bool valid,
        string receiptStatus,
        string operatorCode,
        string paymentType,
        int sequence,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Account details for receipt processing.
/// Used by validation helpers to return account information.
/// </summary>
public class ReceiptAccountDetails
{
    public string? AccountNumber { get; set; }
    public string? CustomerName { get; set; }
}
