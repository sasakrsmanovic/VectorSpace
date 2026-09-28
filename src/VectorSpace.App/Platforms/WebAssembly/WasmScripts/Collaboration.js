(() => {
    'use strict';
    const open = () => new Promise((resolve, reject) => {
        const request = indexedDB.open('VectorSpace.Collaboration.Recovery', 1);
        request.onupgradeneeded = () => request.result.createObjectStore('journals');
        request.onsuccess = () => resolve(request.result);
        request.onerror = () => reject(request.error);
    });
    globalThis.vectorSpaceCollaborationRecovery = Object.freeze({
        async read() {
            const db = await open();
            try {
                return await new Promise((resolve, reject) => {
                    const tx = db.transaction('journals', 'readonly');
                    const values = Object.create(null);
                    const request = tx.objectStore('journals').openCursor();
                    request.onsuccess = () => {
                        const cursor = request.result;
                        if (cursor) { values[cursor.key] = cursor.value; cursor.continue(); }
                    };
                    tx.oncomplete = () => resolve(JSON.stringify(values));
                    tx.onabort = tx.onerror = () => reject(tx.error || new Error('Recovery read failed'));
                });
            } finally { db.close(); }
        },
        async write(id, journal) {
            if (!/^[a-f0-9]{32}$/i.test(id) || typeof journal !== 'string' || journal.length > 128 * 1024 * 1024)
                throw new Error('Invalid or oversized collaboration recovery journal');
            const db = await open();
            try {
                return await new Promise((resolve, reject) => {
                    const tx = db.transaction('journals', 'readwrite');
                    const store = tx.objectStore('journals');
                    if (!journal) store.delete(id);
                    else {
                        const keys = store.getAllKeys();
                        keys.onsuccess = () => {
                            if (keys.result.length >= 64 && !keys.result.includes(id)) { tx.abort(); return; }
                            store.put(journal, id);
                        };
                    }
                    tx.oncomplete = () => resolve('');
                    tx.onabort = tx.onerror = () => reject(tx.error || new Error('Recovery capacity reached or write failed; download a copy'));
                });
            } finally { db.close(); }
        },
        takeInvitation() {
            const hash = location.hash;
            if (!hash.startsWith('#collaboration=')) return '';
            // Never send an invitation token to the static host or preserve it in a query.
            const link = location.origin + location.pathname + location.search + hash;
            history.replaceState(null, '', location.pathname + location.search);
            return link;
        },
        applicationUrl() { return location.origin + location.pathname + location.search; }
    });
})();
