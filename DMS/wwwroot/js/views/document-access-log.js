// DocumentAccessLog. Server values come from documentAccessLogConfig in the view.
// moment dimuat di akhir layout, jadi baru tersedia saat document ready.
var dalStart, dalEnd;

function dalFilters() {
    return {
        startDt: dalStart,
        endDt: dalEnd,
        action: $("#dalAction").val() || null,
        username: $("#dalUser").val() || null,
        keyword: $.trim($("#dalKeyword").val()) || null
    };
}

function dalRefresh() { $("#datagrid").dxDataGrid("instance").refresh(); }

function dalEsc(s) { return $('<div>').text(s == null ? "" : s).html(); }

// Ringkas user-agent jadi "Browser · OS" supaya mudah dibaca
function dalDevice(ua) {
    if (!ua) return "-";
    var b = /Edg\//.test(ua) ? "Edge" : /OPR\//.test(ua) ? "Opera" : /Chrome\//.test(ua) ? "Chrome" : /Firefox\//.test(ua) ? "Firefox" : /Safari\//.test(ua) ? "Safari" : "Other";
    var o = /Windows/.test(ua) ? "Windows" : /Android/.test(ua) ? "Android" : /iPhone|iPad/.test(ua) ? "iOS" : /Mac OS/.test(ua) ? "macOS" : /Linux/.test(ua) ? "Linux" : "";
    if (/DMS-PrintTrack|PrintTrack/i.test(ua)) { b = "PrintTrack"; }
    return b + (o ? " · " + o : "");
}

$(document).ready(function () {
    dalStart = moment().subtract(29, 'days').format("YYYY-MM-DD");
    dalEnd = moment().format("YYYY-MM-DD");

    $('#dalDate').daterangepicker({
        startDate: moment(dalStart), endDate: moment(dalEnd), autoUpdateInput: true,
        locale: { cancelLabel: 'Clear', format: 'DD-MM-YYYY' }
    });
    $('#dalDate').on('apply.daterangepicker', function (ev, picker) {
        dalStart = picker.startDate.format("YYYY-MM-DD"); dalEnd = picker.endDate.format("YYYY-MM-DD"); dalRefresh();
    });
    $('#dalDate').on('cancel.daterangepicker', function () {
        dalStart = null; dalEnd = null; $(this).val(''); dalRefresh();
    });

    setDataSelect2WithParam("dalUser", documentAccessLogConfig.urls.userNames, "All User", "");
    $("#dalAction, #dalUser").on("change", dalRefresh);
    $("#dalKeyword").on("keydown", function (e) { if (e.key === "Enter") dalRefresh(); });
    $("#dal_refresh").on("click", dalRefresh);
    $("#dal_export").on("click", function () {
        var f = dalFilters();
        Object.keys(f).forEach(function (k) { if (f[k] == null) delete f[k]; });
        window.location.href = documentAccessLogConfig.urls.downloadExcel + "?" + $.param(f);
    });

    var store = new DevExpress.data.CustomStore({
        key: "ACCESS_LOG_ID",
        load: function (loadOptions) {
            var d = $.Deferred();
            var data = dalFilters();
            data.skip = loadOptions.skip || 0;
            data.take = loadOptions.take || 25;
            $.ajax({ type: "POST", url: documentAccessLogConfig.urls.search, data: data })
                .done(function (r) {
                    if (r.status === true) d.resolve(r.data, { totalCount: r.totalCount });
                    else { showErrorMesgGrowl(r.message || "Failed to load data"); d.resolve([], { totalCount: 0 }); }
                })
                .fail(function () { d.reject("Data loading error"); });
            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid({
        dataSource: store,
        remoteOperations: { paging: true },
        showBorders: true,
        columnAutoWidth: true,
        wordWrapEnabled: true,
        sorting: { mode: "none" },
        noDataText: "No document access recorded for this filter.",
        paging: { pageSize: 25 },
        pager: { visible: true, showPageSizeSelector: true, allowedPageSizes: [25, 50, 100], showInfo: true, showNavigationButtons: true },
        columns: [
            {
                dataField: "ACCESS_DT", caption: "Time", width: 150,
                cellTemplate: function (c, o) { c.text(moment(o.value).format("DD MMM YYYY HH:mm:ss")); }
            },
            {
                dataField: "USERNAME", caption: "User", width: 190,
                cellTemplate: function (c, o) {
                    c.html('<div class="dal-user"><b>' + dalEsc(o.data.USER_FULL_NAME || o.data.USERNAME) + '</b><span>' + dalEsc(o.data.USERNAME) + '</span></div>');
                }
            },
            {
                dataField: "ACTION", caption: "Action", width: 110, alignment: "center",
                cellTemplate: function (c, o) {
                    var label = o.value == "VIEW" ? "View" : o.value == "DOWNLOAD" ? "Download" : "Print";
                    var copies = o.value == "PRINT" && o.data.COPIES ? " ×" + o.data.COPIES : "";
                    c.html('<span class="dal-pill ' + String(o.value).toLowerCase() + '">' + label + copies + '</span>');
                }
            },
            {
                dataField: "DOCUMENT_CODE", caption: "Document",
                cellTemplate: function (c, o) {
                    var code = o.data.DOCUMENT_CODE ? '<b>' + dalEsc(o.data.DOCUMENT_CODE) + '</b>' : '<b>' + dalEsc(o.data.DOCUMENT_TITLE || "-") + '</b>';
                    var ext = o.data.SOURCE_TYPE == "EXTERNAL" ? '<span class="dal-ext">External</span>' : '';
                    var sub = o.data.DOCUMENT_CODE ? dalEsc(o.data.DOCUMENT_TITLE || "") : "";
                    c.html('<div class="dal-doc">' + code + ext + (sub ? '<span>' + sub + '</span>' : '') + '</div>');
                }
            },
            { dataField: "REVISION", caption: "Rev.", width: 60, alignment: "center" },
            { dataField: "DEPARTMENT_NAME", caption: "Department", width: 170 },
            {
                dataField: "SOURCE_PAGE", caption: "Opened From", width: 160,
                cellTemplate: function (c, o) {
                    var p = (o.value || "").replace(/^\//, "").split("/")[0];
                    var names = { DocumentMaintenance: "Document Preparation", UserDashboard: "My Documents", DocumentControlDashboard: "Document Control",
                        P4DMaintenance: "Document Registration", DocumentMasterlist: "Document Masterlist", ExternalDocument: "External Document", Home: "Dashboard" };
                    c.text(o.data.USER_AGENT && /PrintTrack/i.test(o.data.USER_AGENT) ? "PrintTrack" : (names[p] || p || "-"));
                }
            },
            { dataField: "CLIENT_IP", caption: "IP Address", width: 120 },
            {
                dataField: "USER_AGENT", caption: "Device", width: 130,
                cellTemplate: function (c, o) { c.text(dalDevice(o.value)).attr("title", o.value || ""); }
            }
        ]
    });
});
