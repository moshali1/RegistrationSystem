// ═══════════════════════════════════════════════════════════════════════════════
// Direct Azure Blob Upload
// Uploads files directly from browser to Azure Storage using SAS URLs
// Bypasses server for 3-5x faster uploads on mobile
// Persists upload state to localStorage to survive SignalR disconnections
// ═══════════════════════════════════════════════════════════════════════════════

window.directUpload = {
    // Store active uploads for cancellation
    activeUploads: new Map(),

    // Store file reference to prevent loss during Blazor re-renders
    selectedFile: null,
    dotNetReference: null,

    // localStorage key for persisting upload state
    STORAGE_KEY: 'directUpload_pendingVideo',

    /**
     * Save upload state to localStorage
     */
    saveState: function (state) {
        try {
            localStorage.setItem(this.STORAGE_KEY, JSON.stringify(state));
            console.log('[DirectUpload] State saved:', state.status);
        } catch (e) {
            console.warn('[DirectUpload] Failed to save state:', e);
        }
    },

    /**
     * Get saved upload state from localStorage
     */
    getState: function () {
        try {
            const data = localStorage.getItem(this.STORAGE_KEY);
            return data ? JSON.parse(data) : null;
        } catch (e) {
            console.warn('[DirectUpload] Failed to read state:', e);
            return null;
        }
    },

    /**
     * Clear saved upload state
     */
    clearState: function () {
        try {
            localStorage.removeItem(this.STORAGE_KEY);
            console.log('[DirectUpload] State cleared');
        } catch (e) {
            console.warn('[DirectUpload] Failed to clear state:', e);
        }
    },

    /**
     * Check for completed upload that survived a disconnect
     * Call this on page load to recover upload state
     * @returns {object|null} Completed upload info or null
     */
    checkPendingUpload: function () {
        const state = this.getState();
        if (!state) return null;

        // Check if upload completed
        if (state.status === 'completed') {
            console.log('[DirectUpload] Found completed upload from previous session');
            return {
                blobUri: state.blobUri,
                blobName: state.blobName,
                fileName: state.fileName,
                fileSize: state.fileSize,
                contentType: state.contentType,
                completedAt: state.completedAt
            };
        }

        // Check if upload is stale (started more than 30 minutes ago)
        if (state.status === 'uploading' && state.startedAt) {
            const thirtyMinutes = 30 * 60 * 1000;
            if (Date
            () - state.startedAt > thirtyMinutes) {
                console.log('[DirectUpload] Found stale upload, clearing');
                this.clearState();
                return null;
            }
        }

        // Upload was in progress but not completed - it's lost
        if (state.status === 'uploading') {
            console.log('[DirectUpload] Found incomplete upload, clearing');
            this.clearState();
            return null;
        }

        return null;
    },

    /**
     * Initialize file input with change listener
     * Call this once after the input element is rendered
     * @param {string} inputId - ID of the file input element
     * @param {DotNetObjectReference} dotNetRef - Reference to Blazor component
     */
    initializeInput: async function (inputId, dotNetRef) {
        console.log('[DirectUpload] initializeInput called for:', inputId);
        this.dotNetReference = dotNetRef;

        // Wait for element with retries
        let input = null;
        for (let i = 0; i < 20; i++) {
            input = document.getElementById(inputId);
            if (input) break;
            console.log('[DirectUpload] Input not found, waiting... attempt', i + 1);
            await new Promise(r => setTimeout(r, 100));
        }

        if (!input) {
            console.error('[DirectUpload] Input element not found after retries:', inputId);
            throw new Error('Input element not found: ' + inputId);
        }

        // Remove any existing listener
        if (this._handleFileChange && this._currentInput) {
            this._currentInput.removeEventListener('change', this._handleFileChange);
        }

        // Store reference to current input
        this._currentInput = input;

        // Create bound handler
        const self = this;
        this._handleFileChange = async (e) => {
            console.log('[DirectUpload] File change event fired');
            const file = e.target.files?.[0];
            if (!file) {
                console.warn('[DirectUpload] No file in change event');
                return;
            }

            // Store file reference immediately
            self.selectedFile = file;
            console.log('[DirectUpload] File stored:', file.name, 'Size:', file.size, 'Type:', file.type);

            // Notify Blazor with file info
            const fileInfo = {
                name: file.name,
                size: file.size,
                type: file.type || 'application/octet-stream',
                lastModified: file.lastModified
            };

            try {
                console.log('[DirectUpload] Calling Blazor OnFileSelected...');
                await self.dotNetReference.invokeMethodAsync('OnFileSelected', fileInfo);
                console.log('[DirectUpload] Blazor OnFileSelected completed');
            } catch (err) {
                console.error('[DirectUpload] Failed to notify Blazor of file selection:', err);
            }
        };

        input.addEventListener('change', this._handleFileChange);
        console.log('[DirectUpload] Change listener attached to:', inputId);
    },

    /**
     * Start upload using the previously selected file
     * @param {string} uploadId - Unique ID for this upload (for cancellation)
     * @param {string} sasUrl - SAS URL with write permissions
     * @param {string} contentType - MIME type of the file
     * @param {string} blobUri - Final blob URI (for state persistence)
     * @param {string} blobName - Blob name (for state persistence)
     * @returns {Promise<boolean>} - Success status
     */
    startUpload: async function (uploadId, sasUrl, contentType, blobUri, blobName) {
        console.log('[DirectUpload] startUpload called', { uploadId, contentType, hasFile: !!this.selectedFile });

        if (!this.selectedFile) {
            console.error('[DirectUpload] No file stored!');
            if (this.dotNetReference) {
                try {
                    await this.dotNetReference.invokeMethodAsync('OnUploadError', 'No file selected');
                } catch (e) { /* Blazor may be disconnected */ }
            }
            return false;
        }

        const file = this.selectedFile;
        console.log('[DirectUpload] Starting upload for:', file.name, 'Size:', file.size);

        // Save initial state to localStorage
        this.saveState({
            status: 'uploading',
            uploadId: uploadId,
            blobUri: blobUri,
            blobName: blobName,
            fileName: file.name,
            fileSize: file.size,
            contentType: contentType,
            startedAt: Date.now(),
            progress: 0
        });

        return new Promise((resolve) => {
            const xhr = new XMLHttpRequest();
            const self = this;

            // Store for potential cancellation
            this.activeUploads.set(uploadId, xhr);

            // Progress handler
            xhr.upload.addEventListener('progress', async (e) => {
                if (e.lengthComputable) {
                    const percentComplete = Math.round((e.loaded / e.total) * 100);
                    console.log('[DirectUpload] Progress:', percentComplete + '%');

                    // Update progress in localStorage (throttled - only every 10%)
                    if (percentComplete % 10 === 0) {
                        const state = self.getState();
                        if (state) {
                            state.progress = percentComplete;
                            self.saveState(state);
                        }
                    }

                    // Try to notify Blazor (may fail if disconnected)
                    if (self.dotNetReference) {
                        try {
                            await self.dotNetReference.invokeMethodAsync('OnUploadProgress', percentComplete, e.loaded, e.total);
                        } catch (err) {
                            console.warn('[DirectUpload] Progress callback failed (Blazor disconnected?):', err.message);
                        }
                    }
                }
            });

            // Completion handler
            xhr.addEventListener('load', async () => {
                console.log('[DirectUpload] XHR load event, status:', xhr.status, xhr.statusText);
                self.activeUploads.delete(uploadId);

                if (xhr.status >= 200 && xhr.status < 300) {
                    console.log('[DirectUpload] Upload successful!');

                    // Save completed state to localStorage FIRST (survives Blazor disconnect)
                    self.saveState({
                        status: 'completed',
                        uploadId: uploadId,
                        blobUri: blobUri,
                        blobName: blobName,
                        fileName: file.name,
                        fileSize: file.size,
                        contentType: contentType,
                        completedAt: Date.now()
                    });

                    // Try to notify Blazor
                    if (self.dotNetReference) {
                        try {
                            await self.dotNetReference.invokeMethodAsync('OnUploadComplete', true, '');
                        } catch (err) {
                            console.warn('[DirectUpload] Completion callback failed (Blazor disconnected?):', err.message);
                            console.log('[DirectUpload] Upload state saved to localStorage - will recover on reconnect');
                        }
                    }
                    resolve(true);
                } else {
                    const errorMessage = `Upload failed: ${xhr.status} ${xhr.statusText}`;
                    console.error('[DirectUpload]', errorMessage);
                    console.error('[DirectUpload] Response:', xhr.responseText);

                    // Clear state on failure
                    self.clearState();

                    if (self.dotNetReference) {
                        try {
                            await self.dotNetReference.invokeMethodAsync('OnUploadError', errorMessage);
                        } catch (err) {
                            console.warn('[DirectUpload] Error callback failed:', err.message);
                        }
                    }
                    resolve(false);
                }
            });

            // Error handler
            xhr.addEventListener('error', async (e) => {
                console.error('[DirectUpload] XHR error event:', e);
                self.activeUploads.delete(uploadId);
                self.clearState();

                if (self.dotNetReference) {
                    try {
                        await self.dotNetReference.invokeMethodAsync('OnUploadError', 'Network error during upload. Check CORS configuration.');
                    } catch (err) {
                        console.warn('[DirectUpload] Error callback failed:', err.message);
                    }
                }
                resolve(false);
            });

            // Abort handler
            xhr.addEventListener('abort', async () => {
                console.log('[DirectUpload] Upload aborted');
                self.activeUploads.delete(uploadId);
                self.clearState();

                if (self.dotNetReference) {
                    try {
                        await self.dotNetReference.invokeMethodAsync('OnUploadCancelled');
                    } catch (err) {
                        console.warn('[DirectUpload] Cancel callback failed:', err.message);
                    }
                }
                resolve(false);
            });

            // Timeout handler (5 minutes for large files)
            xhr.timeout = 5 * 60 * 1000;
            xhr.addEventListener('timeout', async () => {
                console.error('[DirectUpload] Upload timed out');
                self.activeUploads.delete(uploadId);
                self.clearState();

                if (self.dotNetReference) {
                    try {
                        await self.dotNetReference.invokeMethodAsync('OnUploadError', 'Upload timed out');
                    } catch (err) {
                        console.warn('[DirectUpload] Timeout callback failed:', err.message);
                    }
                }
                resolve(false);
            });

            // Configure and send
            console.log('[DirectUpload] Opening PUT request to:', sasUrl.substring(0, 100) + '...');
            xhr.open('PUT', sasUrl, true);
            xhr.setRequestHeader('x-ms-blob-type', 'BlockBlob');
            xhr.setRequestHeader('Content-Type', contentType);

            console.log('[DirectUpload] Sending file...');
            xhr.send(file);
        });
    },

    /**
     * Cancel an active upload
     * @param {string} uploadId - The upload ID to cancel
     */
    cancelUpload: function (uploadId) {
        const xhr = this.activeUploads.get(uploadId);
        if (xhr) {
            xhr.abort();
            this.activeUploads.delete(uploadId);
        }
        this.clearState();
    },

    /**
     * Clear file input and stored reference
     * @param {string} inputId - ID of the file input element
     */
    clearInput: function (inputId) {
        this.selectedFile = null;
        this.clearState();
        const input = document.getElementById(inputId);
        if (input) {
            input.value = '';
        }
    },

    /**
     * Dispose and cleanup
     */
    dispose: function () {
        this.selectedFile = null;
        this.dotNetReference = null;
        this.activeUploads.clear();
        // Note: Don't clear state on dispose - we want it to survive reconnects
    }
};