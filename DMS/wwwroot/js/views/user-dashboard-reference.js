// My Documents > Reference Documents tab (request Hendra 2026-11-18): documents of other
// departments shared with the login user for reading through Reference Access. View only.
// Server values come from userDashboardConfig (Views/UserDashboard/_Javascript.cshtml).
var udRefLoaded = false;

$(document).ready(function () {
    $(document).on("click", ".ud-view-switch .ud-tab", function () { udShowTab($(this).data("tab")); });
    $("#btn_refRefresh").on("click", udLoadReference);

    // ?tab=reference (a search result): open the Reference Documents tab
    if (new URLSearchParams(window.location.search).get("tab") === "reference") udShowTab("reference");

    // the count on the tab, so the user sees there is something to read without opening it
    udLoadReference();
});

function udShowTab(tab) {
    $(".ud-view-switch .ud-tab").removeClass("active").filter('[data-tab="' + tab + '"]').addClass("active");
    var reference = tab === "reference";
    $("#udRefPanel").toggle(reference);
    $("#dpLayoutRow").toggle(!reference);
    var grid = $(reference ? "#refGrid" : "#datagrid").data("dxDataGrid");
    if (grid) {
        if (!reference) grid.option("height", computeGridHeight());
        grid.updateDimensions();
    }
}

function udLoadReference() {
    $.getJSON(userDashboardConfig.urls.referenceDocuments).done(function (result) {
        if (!result || result.status !== true) {
            showErrorMesgGrowl((result && result.message) || "Reference documents could not be loaded.");
            return;
        }
        var rows = result.data || [];
        $(".ud-ref-count").text(rows.length).toggle(rows.length > 0);
        udRenderReference(rows);
    });
}

function udOpenReference(row) {
    if (!row.filePath) { showErrorMesgGrowl("This document has no file."); return; }
    var link = userDashboardConfig.urls.viewAttachment
        .replace("-1", encodeURIComponent(row.filePath))
        .replace("-2", row.id)
        .replace("-3", "3");
    // in-page book viewer, like the My Documents tab (2026-11-18)
    dmsOpenDocViewer({
        url: link,
        title: row.code || "Document",
        subtitle: [row.title, row.revision != null && row.revision !== "" ? "Rev. " + row.revision : null, row.owner].filter(Boolean).join("  ·  "),
        badge: "Reference · View Only"
    });
}

function udRenderReference(rows) {
    var $grid = $("#refGrid");
    if ($grid.data("dxDataGrid")) {
        $grid.dxDataGrid("instance").option("dataSource", rows);
        return;
    }
    $grid.dxDataGrid(dmsGridOptions({
        dataSource: rows,
        keyExpr: "id",
        // ?tab=reference&DOCUMENT_CODE=... : exact filter + "Showing ... / Show all" chip, once
        onInitialized: function (e) {
            if (new URLSearchParams(window.location.search).get("tab") === "reference")
                setTimeout(function () { dmsApplyDeepLink(e.component, { param: "DOCUMENT_CODE", column: "code" }); }, 0);
        },
        height: "auto",
        wordWrapEnabled: true,
        selection: { mode: "single" },
        searchPanel: { visible: true, width: 240, placeholder: "Search document no. or title..." },
        noDataText: "No shared documents. Ask Document Control for access.",
        onRowDblClick: function (e) { if (e.rowType === "data") udOpenReference(e.data); },
        columns: [
            {
                caption: "No", width: 50, alignment: "center", allowFiltering: false, allowHeaderFiltering: false,
                cellTemplate: function (c, o) { c.text(o.component.pageIndex() * o.component.pageSize() + o.row.rowIndex + 1); }
            },
            {
                dataField: "code", caption: "Document No.", minWidth: 190,
                cellTemplate: function (c, o) {
                    $("<i>").addClass("fa " + dmsFileIconClass(o.data.filePath)).css("margin-right", "6px").appendTo(c);
                    $("<a href='#' class='ud-ref-open' title='Open (view only)'>").text(o.value)
                        .on("click", function (e) { e.preventDefault(); udOpenReference(o.data); }).appendTo(c);
                }
            },
            { dataField: "title", caption: "Title", minWidth: 220 },
            { dataField: "revision", caption: "Rev", width: 60, alignment: "center" },
            {
                dataField: "category", caption: "Category", minWidth: 120,
                cellTemplate: function (c, o) {
                    if (!o.value) return;
                    $("<span>").addClass("dms-pill " + udCategoryPillClass(o.data.categoryCode)).text(o.value).appendTo(c);
                }
            },
            { dataField: "owner", caption: "Owner Department", minWidth: 170 },
            {
                dataField: "date", caption: "Document Date", width: 115, alignment: "right", allowHeaderFiltering: false,
                cellTemplate: function (c, o) { c.text(o.value ? moment(o.value).format("DD-MM-YYYY") : ""); }
            },
            {
                dataField: "validTo", caption: "Access Until", width: 130, alignment: "right", allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    var days = moment(o.value).startOf("day").diff(moment().startOf("day"), "days");
                    c.text(moment(o.value).format("DD-MM-YYYY"));
                    $("<span class='ud-ref-left'>").toggleClass("soon", days <= 7)
                        .text(days === 0 ? "ends today" : days + " day(s) left").appendTo(c);
                }
            },
            {
                caption: "", width: 90, alignment: "center", allowFiltering: false, allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    $("<button type='button' class='dp-btn dp-btn-outline ud-ref-btn' title='Open (view only)'>")
                        .html('<i class="fa fa-eye"></i> View')
                        .on("click", function () { udOpenReference(o.data); }).appendTo(c);
                }
            }
        ]
    }));
}
