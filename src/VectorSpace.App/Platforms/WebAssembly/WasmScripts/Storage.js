"use strict";
// Browser capabilities only. The workbench and drawing engine are Uno/C#/Skia.
(() => {
  let database;
  const connect = () => database ??= new Promise((resolve, reject) => {
    const request = indexedDB.open("VectorSpace", 1);
    request.onupgradeneeded = () => request.result.createObjectStore("workspace");
    request.onsuccess = () => resolve(request.result);
    request.onerror = () => reject(request.error);
  });
  const transaction = async (mode, execute) => {
    const db = await connect();
    return await new Promise((resolve, reject) => {
      const tx = db.transaction("workspace", mode); let result = "";
      const request = execute(tx.objectStore("workspace"));
      request.onsuccess = () => { result = request.result ?? ""; };
      tx.oncomplete = () => resolve(typeof result === "string" ? result : "");
      tx.onerror = tx.onabort = () => reject(tx.error ?? new Error("Storage transaction failed."));
    });
  };
  globalThis.vectorSpaceStorage = Object.freeze({
    load: () => transaction("readonly", store => store.get("autosave")),
    save: document => transaction("readwrite", store => store.put(document, "autosave")),
    open: () => new Promise((resolve, reject) => {
      const input = document.createElement("input"); input.type = "file";
      input.accept = ".vectorspace,.json,.svg"; input.style.display = "none";
      document.body.append(input);
      input.oncancel = () => { input.remove(); resolve(""); };
      input.onchange = async () => {
        try {
          const file = input.files?.[0];
          if (!file) { resolve(""); return; }
          if (file.size > 32 * 1024 * 1024) throw new Error("This file exceeds the 32 MiB import limit.");
          resolve(JSON.stringify({ name: file.name, text: await file.text() }));
        } catch (error) { reject(error); } finally { input.remove(); }
      };
      input.click();
    }),
    download: async (name, base64, contentType) => {
      const binary = atob(base64); const bytes = new Uint8Array(binary.length);
      for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);
      const url = URL.createObjectURL(new Blob([bytes], { type: contentType }));
      const a = document.createElement("a"); a.href = url; a.download = name;
      document.body.append(a); a.click(); a.remove(); setTimeout(() => URL.revokeObjectURL(url), 5000);
      return "";
    },
    isTestMode: () => new URLSearchParams(location.search).get("test") === "1",
    publishControls: json => { if (new URLSearchParams(location.search).get("test") === "1") globalThis.__vectorSpaceControls = Object.freeze(JSON.parse(json)); },
    publishDiagnostics: json => { if (new URLSearchParams(location.search).get("test") === "1") globalThis.__vectorSpaceState = Object.freeze(JSON.parse(json)); }
  });
  document.addEventListener("contextmenu", e => { if (e.target instanceof HTMLCanvasElement) e.preventDefault(); });
  document.addEventListener("wheel", e => { if (e.ctrlKey && e.target instanceof HTMLCanvasElement) e.preventDefault(); }, { passive: false });
})();
