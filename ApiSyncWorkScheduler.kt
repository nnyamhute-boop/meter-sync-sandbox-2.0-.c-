package com.axis.revcoll.infrastructure.sync

import android.util.Log
import androidx.work.WorkManager
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Scheduler for API sync WorkManager tasks.
 *
 * Provides a convenient interface for scheduling and triggering API sync work.
 * Should be called during app startup to ensure periodic syncing is active.
 *
 * Usage:
 * - Call schedulePeriodicSync() during app initialization
 * - Call triggerImmediateSync() when user requests manual sync
 */
@Singleton
class ApiSyncWorkScheduler @Inject constructor(
    private val workManager: WorkManager
) {
    companion object {
        private const val TAG = "ApiSyncWorkScheduler"
        private const val DEFAULT_INTERVAL_MINUTES = 15L
    }

    /**
     * Schedule periodic API sync.
     *
     * Should be called once during app initialization (e.g., in Application.onCreate).
     * Uses ExistingPeriodicWorkPolicy.KEEP to avoid re-scheduling if already active.
     *
     * @param intervalMinutes Interval between sync runs (default: 15 minutes)
     */
    fun schedulePeriodicSync(intervalMinutes: Long = DEFAULT_INTERVAL_MINUTES) {
        Log.d(TAG, "Scheduling periodic API sync every $intervalMinutes minutes")
        ApiSyncWorker.schedulePeriodicWork(workManager, intervalMinutes)
    }

    /**
     * Trigger immediate API sync.
     *
     * Call this when:
     * - User taps "Sync Now" button
     * - A new transaction is created and network is available
     * - App comes back online after being offline
     *
     * Uses ExistingWorkPolicy.KEEP to avoid duplicate immediate syncs.
     */
    fun triggerImmediateSync() {
        Log.d(TAG, "Triggering immediate API sync")
        ApiSyncWorker.triggerImmediate(workManager)
    }

    /**
     * Cancel all API sync work.
     *
     * Call this when:
     * - User logs out
     * - App needs to stop background sync for any reason
     */
    fun cancelAllSync() {
        Log.d(TAG, "Cancelling all API sync work")
        ApiSyncWorker.cancelAll(workManager)
    }
}
