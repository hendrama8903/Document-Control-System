// Document Viewer (Hendra 2026-11-12): shows a document preview in a pop up on the same page.
//   DocViewer.open({ url: "...", title: "HMMI/PDM/2026/002", subtitle: "Rev.00 · Pedoman Mutu" });
// 2026-11-18: every menu now uses the same book-style popup (js/doc-viewer-modal.js, loaded by
// the layout), so this only forwards to it. Kept for the pages that already call DocViewer.
var DocViewer = (function () {
    function open(options) {
        if (!options || !options.url) return;
        if (typeof window.dmsOpenDocViewer === "function") {
            window.dmsOpenDocViewer({ url: options.url, title: options.title, subtitle: options.subtitle, badge: options.badge });
        } else {
            window.open(options.url, "_blank"); // page without the layout: old behaviour
        }
    }
    return { open: open };
})();
