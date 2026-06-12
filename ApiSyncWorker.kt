package com.axis.revcoll.infrastructure.sync

import android.content.Context
import android.util.Log
import androidx.work.BackoffPolicy
import androidx.work.Constraints
import androidx.work.CoroutineWorker
import androidx.work.ExistingPeriodicWorkPolicy
import androidx.work.ExistingWorkPolicy
import androidx.work.NetworkType
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.PeriodicWorkRequestBuilder
import androidx.work.WorkManager
import androidx.work.WorkerParameters
import java.util.concurrent.TimeUnit

/**
 * WorkManager worker for processing the API outbox.
 *
 * Runs periodically to submit locally created transactions to RevColl API.
 * Also supports on-demand triggering for immediate processing.
 *
 * Features:
 * - Requires network connectivity
 * - Exponential backoff on failure
 * - 15-minute periodic scheduling
 * - Immediate trigger option
 *
 * Architecture:
 * WorkManager -> ApiSyncWorker -> ApiOutboxProcessor -> TransactionSyncRepository -> API
 */
class ApiSyncWorker(
    context: Context,
    params: WorkerParameters,
    private val outboxProcessor: ApiOutboxProcessor
) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result {
        Log.i(TAG, "Starting API outbox processing")

        val result = outboxProcessor.processOutbox()

        return when {
            result.error != null -> {
                Log.e(TAG, "Outbox processing failed: ${result.error}")
                // Check if it's an auth error - don't retry those
                if (result.error.contains("Token expired", ignoreCase = true) ||
                    result.error.contains("Unauthorized", ignoreCase = true)
                ) {
                    Log.w(TAG, "Auth error - not retrying until user logs in")
                    Result.failure()
                } else {
                    Result.retry()
                }
            }

            result.failureCount > 0 && result.successCount == 0 -> {
                Log.w(TAG, "All submissions failed, will retry")
                Result.retry()
            }

            else -> {
                Log.i(
                    TAG,
                    "Outbox processing complete: ${result.successCount} succeeded, ${result.failureCount} failed"
                )
                Result.success()
            }
        }
    }

    companion object {
        private const val TAG = "ApiSyncWorker"
        const val WORK_NAME_PERIODIC = "api_outbox_sync_periodic"
        const val WORK_NAME_IMMEDIATE = "api_outbox_sync_immediate"

        /**
         * Schedule periodic outbox processing.
         *
         * @param workManager WorkManager instance
         * @param intervalMinutes Interval between runs (default 15 minutes)
         */
        fun schedulePeriodicWork(
            workManager: WorkManager,
            intervalMinutes: Long = 15
        ) {
            val constraints = Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()

            val request = PeriodicWorkRequestBuilder<ApiSyncWorker>(
                intervalMinutes, TimeUnit.MINUTES
            )
                .setConstraints(constraints)
                .setBackoffCriteria(
                    BackoffPolicy.EXPONENTIAL,
                    1, TimeUnit.MINUTES
                )
                .build()

            workManager.enqueueUniquePeriodicWork(
                WORK_NAME_PERIODIC,
                ExistingPeriodicWorkPolicy.KEEP,
                request
            )

            Log.i(TAG, "Scheduled periodic API sync every $intervalMinutes minutes")
        }

        /**
         * Trigger immediate outbox processing.
         *
         * @param workManager WorkManager instance
         */
        fun triggerImmediate(workManager: WorkManager) {
            val constraints = Constraints.Builder()
                .setRequiredNetworkType(NetworkType.CONNECTED)
                .build()

            val request = OneTimeWorkRequestBuilder<ApiSyncWorker>()
                .setConstraints(constraints)
                .build()

            workManager.enqueueUniqueWork(
                WORK_NAME_IMMEDIATE,
                ExistingWorkPolicy.KEEP,
                request
            )

            Log.i(TAG, "Triggered immediate API sync")
        }

        /**
         * Cancel all outbox processing work.
         *
         * @param workManager WorkManager instance
         */
        fun cancelAll(workManager: WorkManager) {
            workManager.cancelUniqueWork(WORK_NAME_PERIODIC)
            workManager.cancelUniqueWork(WORK_NAME_IMMEDIATE)
            Log.i(TAG, "Cancelled all API sync work")
        }
    }
}
