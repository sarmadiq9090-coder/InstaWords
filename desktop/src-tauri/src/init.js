// InstaWords desktop: injected by Rust (lib.rs) into every page before the site's scripts.
// The website itself is bundled unchanged; this only adapts browser-only behaviour.
(() => {
  "use strict";
  if (window.__instawordsInit) return;
  Object.defineProperty(window, "__instawordsInit", { value: true });

  const invoke = (cmd, args, options) => window.__TAURI_INTERNALS__.invoke(cmd, args, options);

  // (a) Service worker: the app's files are already local, so registration is a no-op that
  // resolves like a successful one (the page then shows its "works offline" badge).
  try {
    const SWC = window.ServiceWorkerContainer;
    if (SWC) {
      const registration = {
        scope: new URL("./", location.href).href,
        installing: null,
        waiting: null,
        active: null,
        update: () => Promise.resolve(),
        unregister: () => Promise.resolve(true),
        addEventListener() {},
        removeEventListener() {},
      };
      const define = (name, desc) => Object.defineProperty(SWC.prototype, name, { configurable: true, ...desc });
      define("register", { writable: true, value: () => Promise.resolve(registration) });
      define("ready", { get: () => Promise.resolve(registration) });
      define("getRegistration", { writable: true, value: () => Promise.resolve(undefined) });
      define("getRegistrations", { writable: true, value: () => Promise.resolve([]) });
    }
  } catch {
    /* leave the page's own fallback in charge */
  }

  const isExternal = (url) => /^(https?:|mailto:)$/.test(url.protocol) && url.origin !== location.origin;

  const linkOf = (e) => {
    for (const node of e.composedPath()) {
      if (node instanceof HTMLAnchorElement && node.hasAttribute("href")) return node;
    }
    return null;
  };

  // (c) Downloads (Tafqit.bas, CSV export via blob: URLs): native "Save as" dialog.
  async function saveDownload(a) {
    const url = new URL(a.href, location.href);
    const fromPath = decodeURIComponent(url.pathname.split("/").pop() || "");
    const name = a.getAttribute("download") || fromPath || "download";
    try {
      const res = await fetch(url.href);
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const bytes = new Uint8Array(await res.arrayBuffer());
      await invoke("save_file", bytes, { headers: { "X-File-Name": encodeURIComponent(name) } });
    } catch (err) {
      console.error("InstaWords: save failed", err);
      alert(`تعذّر حفظ الملف ${name}.\nCould not save ${name}.\n\n${err && err.message ? err.message : err}`);
    }
  }

  function openExternal(url) {
    invoke("open_external", { url: url.href }).catch((err) => console.error("InstaWords: open failed", err));
  }

  // Bubble phase on window: runs after the page's own handlers, and the link's default
  // action (navigation / download) has not happened yet, so preventDefault still cancels it.
  window.addEventListener("click", (e) => {
    if (e.defaultPrevented || e.button !== 0) return;
    const a = linkOf(e);
    if (!a) return;
    if (a.hasAttribute("download")) {
      e.preventDefault();
      saveDownload(a);
      return;
    }
    let url;
    try {
      url = new URL(a.href, location.href);
    } catch {
      return;
    }
    // (b) External links open in the system browser, never inside the app window.
    if (isExternal(url)) {
      e.preventDefault();
      openExternal(url);
    }
  });

  // Middle-click on an external link: same as a normal click (no in-app new window).
  window.addEventListener("auxclick", (e) => {
    if (e.defaultPrevented || e.button !== 1) return;
    const a = linkOf(e);
    if (!a || a.hasAttribute("download")) return;
    try {
      const url = new URL(a.href, location.href);
      if (isExternal(url)) {
        e.preventDefault();
        openExternal(url);
      }
    } catch {
      /* ignore */
    }
  });
})();
