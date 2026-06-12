package com.axis.revcoll.infrastructure.sync

import android.app.NotificationManager
import android.app.PendingIntent
import android.content.Context
import android.content.Intent
import android.os.Build
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import androidx.work.workDataOf
import com.axis.revcoll.R
import com.axis.revcoll.data.database.modern.repositories.contracts.ConfigurationSyncRepository
import com.axis.revcoll.data.database.modern.repositories.contracts.CustomerSyncRepository
import com.axis.revcoll.data.database.modern.repositories.contracts.InventorySyncRepository
import com.axis.revcoll.infrastructure.DataStoreManager
import com.axis.revcoll.ui.MainActivity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.async
import kotlinx.coroutines.awaitAll
import kotlinx.coroutines.supervisorScope
import kotlinx.coroutines.withContext

/**
 * WorkManager Worker that performs automatic background master data synchronization.
 *
 * This worker integrates with WorkManager to automatically sync master data (products,
 * currencies, accounts, taxes, etc.) from the remote server when optimal conditions are met
 * (network availability, battery not low).
 *
 * Key Features:
 * - Uses CoroutineWorker for seamless integration with suspend functions
 * - Checks for active shift/username before syncing (required for server authentication)
 * - Uses modern sync repositories (Configuration, Inventory, Customer) for API-only sync
 * - Updates sync timestamps on successful sync
 * - Implements exponential backoff retry policy on failures
 * - Reports progress during sync operation
 * - Comprehensive logging at INFO and ERROR levels
 *
 * Architecture:
 * WorkManager → MasterDataSyncWorker → Sync Repositories → REST API
 *
 * Retry Policy:
 * - Initial delay: 30 seconds
 * - Backoff policy: Exponential
 * - Backoff multiplier: 2x
 * - Sequence: 30s → 60s → 120s → 240s → 480s → ...
 *
 * Dependencies are injected via HiltWorkerFactory.
 *
 * @param context Android application context
 * @param params WorkManager worker parameters
 * @param configurationSyncRepository Repository for syncing configuration (company, taxes, UOMs, currencies, payment methods)
 * @param inventorySyncRepository Repository for syncing inventory (products with embedded UOMs and taxes)
 * @param customerSyncRepository Repository for syncing customers (accounts with pagination)
 * @param timestampManager Manager for tracking sync timestamps
 * @param dataStoreManager Manager for accessing DataStore (username, shift data, token)
 */
class MasterDataSyncWorker(
    private val context: Context,
    params: WorkerParameters,
    private val configurationSyncRepository: ConfigurationSyncRepository,
    private val inventorySyncRepository: InventorySyncRepository,
    private val customerSyncRepository: CustomerSyncRepository,
    private val timestampManager: SyncTimestampManager,
    private val dataStoreManager: DataStoreManager
) : CoroutineWorker(context, params) {

    /**
     * Performs the background sync work.
     *
     * This method is called by WorkManager when constraints are met and the work is scheduled.
     * It uses modern sync repositories (Configuration, Inventory, Customer) to pull all master data from the REST API.
     *
     * Flow:
     * 1. Check for active shift/username (required for server authentication)
     * 2. Set progress to indicate sync is starting
     * 3. Execute sync operation on IO dispatcher
     * 4. Update timestamps on success
     * 5. Return success or retry based on result
     *
     * @return Result.success() if sync completes successfully, Result.retry() if sync fails,
     *         Result.failure() if no active shift (permanent failure, don't retry)
     */
    override suspend fun doWork(): Result = withContext(Dispatchers.IO) {
        Log.i(TAG, "Starting master data sync worker")

        try {
            // 1. Validate token before attempting sync
            val isTokenValid = dataStoreManager.isTokenValid()
            if (!isTokenValid) {
                val errorMessage = "Authentication token expired. Please log in to sync data."
                Log.w(TAG, "Master data sync skipped: $errorMessage")

                // Check if we should notify user about token expiry
                if (runAttemptCount >= TOKEN_EXPIRY_NOTIFICATION_THRESHOLD) {
                    sendTokenExpiredNotification()
                }

                // Return failure (not retry) - user needs to log in first
                // This prevents unnecessary API calls with expired tokens
                return@withContext Result.failure(
                    workDataOf(ERROR_KEY to errorMessage)
                )
            }

            Log.i(TAG, "Token validation passed, proceeding with sync")

            // 2. Check for active shift/username (required for server authentication)
            val username = dataStoreManager.getShiftUsername()

            if (username.isNullOrBlank()) {
                Log.w(TAG, "Master data sync skipped: No active shift/username found")
                // Return failure (not retry) - user needs to open a shift first
                // This is a permanent condition until user logs in/opens shift
                return@withContext Result.failure(
                    workDataOf(ERROR_KEY to "No active shift. Please log in to sync master data.")
                )
            }

            Log.i(TAG, "Master data sync proceeding for user: $username")

            // Report progress
            setProgress(workDataOf(PROGRESS_KEY to "Syncing master data..."))

            // Get marshal ID for API calls
            val marshalId = dataStoreManager.getLoggedInUserId()

            // Execute sync operations in parallel for better performance
            // Using supervisorScope to allow individual failures without cancelling others
            setProgress(workDataOf(PROGRESS_KEY to "Syncing master data..."))
            Log.i(TAG, "Starting parallel sync of configuration, inventory, and customers...")

            val errors = mutableListOf<String>()

            supervisorScope {
                val configDeferred = async {
                    Log.i(TAG, "Syncing configuration...")
                    configurationSyncRepository.syncConfiguration(marshalId)
                }
                val inventoryDeferred = async {
                    Log.i(TAG, "Syncing inventory...")
                    inventorySyncRepository.syncInventory(marshalId)
                }
                val customersDeferred = async {
                    Log.i(TAG, "Syncing customers...")
                    customerSyncRepository.syncCustomers(marshalId)
                }

                // Await all results
                val results = awaitAll(configDeferred, inventoryDeferred, customersDeferred)

                // Process results
                results[0].onFailure { e ->
                    Log.w(TAG, "Configuration sync failed: ${e.message}")
                    errors.add("Configuration: ${e.message}")
                }
                results[1].onFailure { e ->
                    Log.w(TAG, "Inventory sync failed: ${e.message}")
                    errors.add("Inventory: ${e.message}")
                }
                results[2].onFailure { e ->
                    Log.w(TAG, "Customer sync failed: ${e.message}")
                    errors.add("Customers: ${e.message}")
                }
            }

            // Handle overall result
            if (errors.isEmpty()) {
                // All syncs succeeded - update timestamp
                val currentTime = System.currentTimeMillis()
                timestampManager.updateAllMasterDataSync(currentTime)

                Log.i(TAG, "Master data sync completed successfully at $currentTime")
                Result.success()
            } else {
                // At least one sync failed
                val errorMessage = errors.joinToString("; ")
                Log.e(TAG, "Master data sync partially failed: $errorMessage")

                // Check if we should notify user about repeated failures
                if (runAttemptCount >= NOTIFICATION_THRESHOLD) {
                    sendSyncFailureNotification(errorMessage)
                }

                // Retry with exponential backoff
                Result.retry()
            }
        } catch (e: Exception) {
            // Catch any unexpected exceptions
            Log.e(TAG, "Unexpected error during master data sync", e)

            // Store error message
            workDataOf(
                ERROR_KEY to (e.message ?: "Unexpected error")
            )

            // Check if we should notify user about repeated failures
            if (runAttemptCount >= NOTIFICATION_THRESHOLD) {
                sendSyncFailureNotification(e.message ?: "Unexpected error")
            }

            Result.retry()
        }
    }

    /**
     * Sends a notification to the user when authentication token has expired.
     *
     * This helps users become aware that they need to log in to enable data synchronization.
     * The notification opens the Login screen when tapped.
     */
    private fun sendTokenExpiredNotification() {
        val notificationManager =
            context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

        // Create intent to open app when notification is tapped
        // MainActivity will route to auth screen if token is invalid
        val mainIntent = Intent(context, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            context,
            0,
            mainIntent,
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
            } else {
                PendingIntent.FLAG_UPDATE_CURRENT
            }
        )

        // Build notification
        val notification = NotificationCompat.Builder(
            context,
            context.getString(R.string.sync_notification_channel_id)
        )
            .setSmallIcon(R.drawable.baseline_sync_24)
            .setContentTitle("Data sync unavailable")
            .setContentText("Your session has expired. Please log in to refresh data.")
            .setStyle(
                NotificationCompat.BigTextStyle()
                    .bigText("Your session has expired. Please log in to enable data synchronization.")
            )
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setContentIntent(pendingIntent)
            .setAutoCancel(true)
            .build()

        // Post notification with unique ID to prevent spam
        notificationManager.notify(TOKEN_EXPIRED_NOTIFICATION_ID, notification)

        Log.i(TAG, "Token expired notification sent after $runAttemptCount attempts")
    }

    /**
     * Sends a notification to the user when sync has failed repeatedly.
     *
     * This helps users become aware of persistent sync issues that require attention.
     * The notification opens the Settings screen when tapped so users can check
     * their configuration or manually trigger a sync.
     *
     * @param errorMessage The error message to display in the notification
     */
    private fun sendSyncFailureNotification(errorMessage: String) {
        val notificationManager =
            context.getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

        // Create intent to open app when notification is tapped
        // MainActivity will handle routing based on app state
        val mainIntent = Intent(context, MainActivity::class.java)
        val pendingIntent = PendingIntent.getActivity(
            context,
            0,
            mainIntent,
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) {
                PendingIntent.FLAG_IMMUTABLE or PendingIntent.FLAG_UPDATE_CURRENT
            } else {
                PendingIntent.FLAG_UPDATE_CURRENT
            }
        )

        // Build notification
        val notification = NotificationCompat.Builder(
            context,
            context.getString(R.string.sync_notification_channel_id)
        )
            .setSmallIcon(R.drawable.baseline_sync_24)
            .setContentTitle(context.getString(R.string.sync_notification_title))
            .setContentText(context.getString(R.string.sync_notification_content, errorMessage))
            .setStyle(
                NotificationCompat.BigTextStyle()
                    .bigText(context.getString(R.string.sync_notification_content, errorMessage))
            )
            .setPriority(NotificationCompat.PRIORITY_DEFAULT)
            .setContentIntent(pendingIntent)
            .setAutoCancel(true)
            .build()

        // Post notification
        notificationManager.notify(SYNC_FAILURE_NOTIFICATION_ID, notification)

        Log.i(TAG, "Sync failure notification sent after $runAttemptCount attempts")
    }

    companion object {
        private val TAG = MasterDataSyncWorker::class.simpleName

        /**
         * Unique work name for periodic master data sync operations.
         * Using a unique name ensures only one periodic sync job is active at a time.
         */
        const val PERIODIC_WORK_NAME = "master_data_periodic_sync"

        /**
         * Unique work name for one-time master data sync operations.
         * Used for startup sync and manual sync triggers.
         */
        const val ONE_TIME_WORK_NAME = "master_data_onetime_sync"

        /**
         * Key for progress data reporting current operation.
         */
        const val PROGRESS_KEY = "progress"

        /**
         * Key for error message in output data.
         */
        const val ERROR_KEY = "error"

        /**
         * Number of retry attempts before sending a notification to the user.
         * After 3 failed attempts, the user will be notified about the sync failure.
         */
        private const val NOTIFICATION_THRESHOLD = 3

        /**
         * Number of retry attempts before sending a token expiry notification.
         * After 3 failed attempts due to token expiry, user will be notified to log in.
         */
        private const val TOKEN_EXPIRY_NOTIFICATION_THRESHOLD = 3

        /**
         * Notification ID for sync failure notifications.
         * Using a constant ID ensures that repeated failures update the same notification.
         */
        private const val SYNC_FAILURE_NOTIFICATION_ID = 1001

        /**
         * Notification ID for token expiry notifications.
         * Using a unique ID ensures token expiry notifications are separate from sync failures.
         */
        private const val TOKEN_EXPIRED_NOTIFICATION_ID = 1002
    }
}
