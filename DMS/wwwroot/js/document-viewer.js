// Document Viewer (Hendra 2026-11-12): shows a document preview in a pop up on the same page.
// Needs Views/Shared/_DocumentViewer.cshtml and css/views/Shared/_DocumentViewer.css.
//   DocViewer.open({ url: "...", title: "HMMI/PDM/2026/002", subtitle: "Rev.00 · Pedoman Mutu" });
// The url is the same one that used to open in a new tab (e.g. DocumentMaintenance/ViewAttachment),
// so stamps, access checks and the access log stay exactly as they were.
var DocViewer = (function () {
    var token = 0;

    function open(options) {
        var $modal = $("#modalDocViewer");
        if (!$modal.length || !options || !options.url) {
            window.open(options && options.url, "_blank"); // page without the viewer: old behaviour
            return;
        }
        var current = ++token;
        $("#dvTitle").text(options.title || "Preview");
        $("#dvSubtitle").text(options.subtitle || "");
        $("#dvNewTab").attr("href", options.url);
        $("#dvLoading").show();

        var frame = document.getElementById("dvFrame");
        frame.onload = function () {
            if (current === token) $("#dvLoading").hide();
        };
        frame.src = options.url;
        $modal.modal("show");
    }

    $(document).ready(function () {
        $("#modalDocViewer").on("hidden.bs.modal", function () {
            token++;
            // Stops loading and frees the PDF when the viewer closes.
            document.getElementById("dvFrame").src = "about:blank";
        });
        $("#dvNewTab").on("click", function () {
            $("#modalDocViewer").modal("hide");
        });
    });

    return { open: open };
})();
