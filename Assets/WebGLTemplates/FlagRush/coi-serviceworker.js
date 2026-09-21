// Cross-origin isolation shim. GitHub Pages cannot send the two headers that unlock
// SharedArrayBuffer (and therefore Unity's worker threads), so this service worker adds them to
// every response it serves. First visit: the worker installs and the page reloads itself once.
// Servers that already send the headers (itch.io with "SharedArrayBuffer support", Tools/serve.py)
// never trigger the reload.
if (typeof window === "undefined") {
  self.addEventListener("install", () => self.skipWaiting());
  self.addEventListener("activate", (e) => e.waitUntil(self.clients.claim()));
  self.addEventListener("fetch", (e) => {
    const r = e.request;
    if (r.cache === "only-if-cached" && r.mode !== "same-origin") return;
    e.respondWith(fetch(r).then((res) => {
      if (res.status === 0) return res;
      const h = new Headers(res.headers);
      h.set("Cross-Origin-Embedder-Policy", "require-corp");
      h.set("Cross-Origin-Opener-Policy", "same-origin");
      return new Response(res.body, { status: res.status, statusText: res.statusText, headers: h });
    }));
  });
} else if (!window.crossOriginIsolated && "serviceWorker" in navigator) {
  navigator.serviceWorker.register(new URL("coi-serviceworker.js", location.href).pathname).then((reg) => {
    if (reg.active && !navigator.serviceWorker.controller) location.reload();
    reg.addEventListener("updatefound", () => {
      const w = reg.installing;
      w && w.addEventListener("statechange", () => { if (w.state === "activated") location.reload(); });
    });
  });
}
