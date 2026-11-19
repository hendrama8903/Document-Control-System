// In-page document viewer popup (request Hendra 2026-11-18): opens a document preview over the
// page instead of in a new browser tab. The preview itself (stamps, view logging, deterrents)
// is still DocumentMaintenance/ViewAttachment, loaded in an iframe with view=book.
//   dmsOpenDocViewer({ url: "...ViewAttachment?...", title: "QCD/SOP-PQA-00/002", subtitle: "Title", badge: "View only" })
(function () {
    var $overlay = null;

    function build() {
        $overlay = $(
            '<div class="dms-dv" id="dmsDocViewer" role="dialog" aria-modal="true" aria-label="Document viewer">' +
            '  <div class="dms-dv-window">' +
            '    <div class="dms-dv-header">' +
            '      <div class="dms-dv-icon"><i class="fa fa-file-text-o"></i></div>' +
            '      <div class="dms-dv-heading">' +
            '        <div class="dms-dv-title"></div>' +
            '        <div class="dms-dv-sub"></div>' +
            '      </div>' +
            '      <span class="dms-dv-badge"></span>' +
            '      <button type="button" class="dms-dv-btn dms-dv-close" title="Close (Esc)"><i class="fa fa-times"></i></button>' +
            '    </div>' +
            '    <div class="dms-dv-body">' +
            '      <div class="dms-dv-loading"><div class="dms-dv-spinner"></div><span>Preparing document&hellip;</span></div>' +
            '      <iframe class="dms-dv-frame" title="Document" allow="fullscreen" allowfullscreen></iframe>' +
            '    </div>' +
            '  </div>' +
            '</div>').appendTo("body");

        // Closes only with the X button (or Esc); a click on the dimmed area outside the window
        // does nothing, so a stray click does not lose the reader's place (Hendra 2026-11-18).
        $overlay.on("click", ".dms-dv-close", close);
        $overlay.find(".dms-dv-frame").on("load", function () {
            if (this.getAttribute("src") === "about:blank") return;
            $overlay.find(".dms-dv-loading").hide();
            try { this.contentWindow.focus(); } catch (err) { }
        });

        $(document).on("keydown.dmsdv", function (e) { if (e.key === "Escape" && isOpen()) close(); });
        // Esc pressed while the focus is inside the viewer frame
        window.addEventListener("message", function (e) {
            if (e.origin === window.location.origin && e.data && e.data.type === "dms-docviewer-close") close();
        });
    }

    function isOpen() { return $overlay && $overlay.hasClass("open"); }

    function close() {
        if (!isOpen()) return;
        $overlay.removeClass("open");
        $("body").removeClass("dms-dv-lock");
        // stop the preview (and free its memory) once the closing fade is over
        setTimeout(function () { if (!isOpen()) $overlay.find(".dms-dv-frame").attr("src", "about:blank"); }, 220);
    }

    window.dmsOpenDocViewer = function (opts) {
        if (!$overlay) build();
        var url = opts.url + (opts.url.indexOf("?") >= 0 ? "&" : "?") + "view=book";
        $overlay.find(".dms-dv-title").text(opts.title || "Document");
        $overlay.find(".dms-dv-sub").text(opts.subtitle || "").toggle(!!opts.subtitle);
        $overlay.find(".dms-dv-badge").text(opts.badge || "").toggle(!!opts.badge);
        $overlay.find(".dms-dv-loading").show();
        $overlay.find(".dms-dv-frame").attr("src", url);
        $("body").addClass("dms-dv-lock");
        // next frame, so the opening transition runs
        requestAnimationFrame(function () { $overlay.addClass("open"); });
    };

    // The common case of every menu: a ViewAttachment URL template with -1 (file path),
    // -2 (document id) and -3 (view type), plus the grid row for the title (2026-11-18).
    //   dmsViewDocument(config.urls.viewAttachment, row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID, "1", row)
    window.dmsViewDocument = function (urlTemplate, filePath, documentId, viewType, row, badge) {
        if (!filePath || filePath === "null") {
            showErrorMesgGrowl("This data does not have an attachment");
            return;
        }
        var url = urlTemplate
            .replace("-1", encodeURIComponent(filePath))
            .replace("-2", documentId)
            .replace("-3", viewType);
        var r = row || {};
        var name = r.DOCUMENT_NAME || r.DOCUMENT_TRANSACTION_NAME || r.TITLE;
        var rev = r.REVISION != null && r.REVISION !== "" ? "Rev. " + r.REVISION : null;
        window.dmsOpenDocViewer({
            url: url,
            title: r.DOCUMENT_CODE || "Document",
            subtitle: [name, rev].filter(Boolean).join("  ·  "),
            badge: badge
        });
    };
})();
