/**
 * Kaya TTL Storage Utility
 * 
 * Provides localStorage wrapper with automatic expiration (TTL).
 * All Kaya UIs should use this for consistent storage behavior.
 * 
 * Usage:
 *   KayaStorage.set('myKey', { foo: 'bar' }, { ttlMs: 3600000 }); // 1 hour
 *   const data = KayaStorage.get('myKey'); // Returns null if expired
 *   KayaStorage.remove('myKey');
 *   KayaStorage.cleanup(); // Remove all expired entries
 */

const KayaStorage = (function() {
    'use strict';

    const STORAGE_VERSION = 1;
    const PREFIX = 'kaya_'; // All keys prefixed to avoid collision with other apps

    // Default TTLs in milliseconds
    const DEFAULT_TTLS = {
        auth: 24 * 60 * 60 * 1000,           // 24 hours
        history: 7 * 24 * 60 * 60 * 1000,    // 7 days
        connections: 4 * 60 * 60 * 1000,      // 4 hours
        preference: null                       // Never expires
    };

    /**
     * Set a value with optional TTL
     * @param {string} key - Storage key (will be prefixed with 'kaya_')
     * @param {any} data - Data to store (will be JSON serialized)
     * @param {Object} options - { ttlMs: number|null, ttlType: 'auth'|'history'|'connections'|'preference' }
     */
    function set(key, data, options = {}) {
        const prefixedKey = PREFIX + key;
        const now = Date.now();
        
        // Determine TTL
        let ttlMs = null;
        if (options.ttlMs !== undefined) {
            ttlMs = options.ttlMs;
        } else if (options.ttlType && DEFAULT_TTLS[options.ttlType] !== undefined) {
            ttlMs = DEFAULT_TTLS[options.ttlType];
        }

        const wrapper = {
            _meta: {
                version: STORAGE_VERSION,
                createdAt: now,
                expiresAt: ttlMs ? now + ttlMs : null,
                ttlMs: ttlMs
            },
            data: data
        };

        try {
            localStorage.setItem(prefixedKey, JSON.stringify(wrapper));
            return true;
        } catch (e) {
            console.warn('[KayaStorage] Failed to save:', key, e);
            // If quota exceeded, try to cleanup and retry once
            if (e.name === 'QuotaExceededError') {
                cleanup();
                try {
                    localStorage.setItem(prefixedKey, JSON.stringify(wrapper));
                    return true;
                } catch (e2) {
                    console.error('[KayaStorage] Still failed after cleanup:', key, e2);
                    return false;
                }
            }
            return false;
        }
    }

    /**
     * Get a value, returns null if not found or expired
     * @param {string} key - Storage key (without prefix)
     * @returns {any|null} - The stored data or null
     */
    function get(key) {
        const prefixedKey = PREFIX + key;
        
        try {
            const raw = localStorage.getItem(prefixedKey);
            if (!raw) return null;

            const wrapper = JSON.parse(raw);
            
            // Check if it's our format (has _meta)
            if (!wrapper._meta) {
                // Legacy data without wrapper - return as-is but consider migrating
                return wrapper;
            }

            // Check expiration
            if (wrapper._meta.expiresAt && Date.now() > wrapper._meta.expiresAt) {
                // Expired - remove and return null
                localStorage.removeItem(prefixedKey);
                return null;
            }

            return wrapper.data;
        } catch (e) {
            console.warn('[KayaStorage] Failed to read:', key, e);
            return null;
        }
    }

    /**
     * Remove a value
     * @param {string} key - Storage key (without prefix)
     */
    function remove(key) {
        const prefixedKey = PREFIX + key;
        localStorage.removeItem(prefixedKey);
    }

    /**
     * Check if a key exists and is not expired
     * @param {string} key - Storage key (without prefix)
     * @returns {boolean}
     */
    function has(key) {
        return get(key) !== null;
    }

    /**
     * Get metadata about a stored value
     * @param {string} key - Storage key (without prefix)
     * @returns {Object|null} - { createdAt, expiresAt, ttlMs, remainingMs } or null
     */
    function getMeta(key) {
        const prefixedKey = PREFIX + key;
        
        try {
            const raw = localStorage.getItem(prefixedKey);
            if (!raw) return null;

            const wrapper = JSON.parse(raw);
            if (!wrapper._meta) return null;

            const now = Date.now();
            return {
                createdAt: wrapper._meta.createdAt,
                expiresAt: wrapper._meta.expiresAt,
                ttlMs: wrapper._meta.ttlMs,
                remainingMs: wrapper._meta.expiresAt ? Math.max(0, wrapper._meta.expiresAt - now) : null,
                isExpired: wrapper._meta.expiresAt ? now > wrapper._meta.expiresAt : false
            };
        } catch (e) {
            return null;
        }
    }

    /**
     * Remove all expired Kaya entries from localStorage
     * Call this on page load to clean up stale data
     */
    function cleanup() {
        const now = Date.now();
        const keysToRemove = [];

        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (!key || !key.startsWith(PREFIX)) continue;

            try {
                const raw = localStorage.getItem(key);
                const wrapper = JSON.parse(raw);
                
                if (wrapper._meta && wrapper._meta.expiresAt && now > wrapper._meta.expiresAt) {
                    keysToRemove.push(key);
                }
            } catch (e) {
                // Invalid JSON, remove it
                keysToRemove.push(key);
            }
        }

        keysToRemove.forEach(key => localStorage.removeItem(key));
        
        if (keysToRemove.length > 0) {
            console.log('[KayaStorage] Cleaned up', keysToRemove.length, 'expired entries');
        }
    }

    /**
     * Update the TTL of an existing entry (extends expiration from now)
     * @param {string} key - Storage key (without prefix)
     * @param {number} ttlMs - New TTL in milliseconds from now
     * @returns {boolean} - True if successful
     */
    function touch(key, ttlMs) {
        const data = get(key);
        if (data === null) return false;
        return set(key, data, { ttlMs });
    }

    /**
     * List all Kaya storage keys with their metadata
     * @returns {Array} - [{ key, meta, size }]
     */
    function list() {
        const result = [];
        
        for (let i = 0; i < localStorage.length; i++) {
            const fullKey = localStorage.key(i);
            if (!fullKey || !fullKey.startsWith(PREFIX)) continue;

            const key = fullKey.substring(PREFIX.length);
            const raw = localStorage.getItem(fullKey);
            
            result.push({
                key: key,
                meta: getMeta(key),
                sizeBytes: raw ? raw.length * 2 : 0 // UTF-16 = 2 bytes per char
            });
        }

        return result;
    }

    /**
     * Clear all Kaya storage (use with caution)
     */
    function clearAll() {
        const keysToRemove = [];
        
        for (let i = 0; i < localStorage.length; i++) {
            const key = localStorage.key(i);
            if (key && key.startsWith(PREFIX)) {
                keysToRemove.push(key);
            }
        }

        keysToRemove.forEach(key => localStorage.removeItem(key));
    }

    // Run cleanup on load
    if (typeof localStorage !== 'undefined') {
        cleanup();
    }

    // Public API
    return {
        set,
        get,
        remove,
        has,
        getMeta,
        cleanup,
        touch,
        list,
        clearAll,
        DEFAULT_TTLS,
        PREFIX
    };
})();

// Also expose for ES modules if needed
if (typeof module !== 'undefined' && module.exports) {
    module.exports = KayaStorage;
}
