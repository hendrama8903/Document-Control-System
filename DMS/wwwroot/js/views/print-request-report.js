// Print Request Report (2026-11-19). Server values come from printRequestReportConfig.
// Approved Print Requests in the Master List layout, in a DevExtreme grid like the other lists:
// one row per document + revision grouped by owning department, one column per requesting
// division with a line "QTY:DEPT-ddMMMyy" per request. Excel / PDF follow the period, the
// owner filter and the grid's order.
var prReport = null;
var prMonths = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function prShortDate(value) {
    if (!value) return "";
    var d = new Date(value);
    if (isNaN(d.getTime())) return "";
    return ("0" + d.getDate()).slice(-2) + prMonths[d.getMonth()] + ("" + d.getFullYear()).slice(-2);
}

function prIsoDate(d) {
    return d.getFullYear() + "-" + ("0" + (d.getMonth() + 1)).slice(-2) + "-" + ("0" + d.getDate()).slice(-2);
}

function prSeqNumber(code) {
    var m = /(\d+)$/.exec(code || "");
    return m ? parseInt(m[1], 10) : 999999999;
}

function prLoad() {
    var grid = $("#prGrid").data("dxDataGrid");
    if (grid) grid.beginCustomLoading("Loading…");
    $.ajax({
        type: "GET",
        url: printRequestReportConfig.urls.data,
        data: { dateFrom: $("#prDateFrom").val(), dateTo: $("#prDateTo").val() },
        cache: false
    }).done(function (report) {
        prReport = report;
        prFillDivisionFilter();
        prBuildGrid();
    }).fail(function () {
        if (grid) grid.endCustomLoading();
        showErrorMesgGrowl("Could not load the report.");
    });
}

function prFillDivisionFilter() {
    var $div = $("#prDivision"), keep = $div.val(), seen = {};
    $div.find("option:gt(0)").remove();
    prReport.Groups.forEach(function (g) {
        if (!g.Division || seen[g.Division]) return;
        seen[g.Division] = true;
        $("<option>").val(g.Division).text(g.Division + " - " + (g.DivisionName || "")).appendTo($div);
    });
    $div.val(seen[keep] ? keep : "");
    prFillDepartmentFilter();
}

function prFillDepartmentFilter() {
    var $dept = $("#prDepartment"), keep = $dept.val(), division = $("#prDivision").val(), seen = {};
    $dept.find("option:gt(0)").remove();
    prReport.Groups.forEach(function (g) {
        if (!g.DepartmentCode || seen[g.DepartmentCode] || (division && g.Division !== division)) return;
        seen[g.DepartmentCode] = true;
        $("<option>").val(g.DepartmentCode).text(g.DepartmentCode + " - " + (g.DepartmentName || "")).appendTo($dept);
    });
    $dept.val(seen[keep] ? keep : "");
}

// one grid row per document + revision; No = place in its department by document number
function prRows() {
    var rows = [];
    prReport.Groups.forEach(function (g) {
        g.Rows.slice().sort(function (a, b) {
            return (prSeqNumber(a.Code) - prSeqNumber(b.Code)) || (a.Code || "").localeCompare(b.Code || "") || (a.Revision - b.Revision);
        }).forEach(function (r, i) {
            rows.push($.extend({}, r, {
                Key: (g.DepartmentCode || "") + "|" + r.Code + "|" + r.Revision,
                No: i + 1,
                Division: g.Division,
                DepartmentCode: g.DepartmentCode,
                Owner: (g.Division || "-") + " · " + (g.DivisionName || "") + "  /  " + (g.DepartmentCode || "-") + " · " + (g.DepartmentName || "")
            }));
        });
    });
    return rows;
}

function prApplyOwnerFilter() {
    var grid = $("#prGrid").data("dxDataGrid");
    if (!grid) return;
    var division = $("#prDivision").val(), dept = $("#prDepartment").val(), f = [];
    if (division) f.push(["Division", "=", division]);
    if (dept) { if (f.length) f.push("and"); f.push(["DepartmentCode", "=", dept]); }
    grid.filter(f.length ? f : null);
}

function prBuildGrid() {
    var divColumns = prReport.Divisions.map(function (d) {
        return {
            caption: d.Code, name: "div" + d.Code, minWidth: 118, allowSorting: false,
            headerCellTemplate: function (h) { h.text(d.Code).attr("title", d.Name || ""); },
            calculateCellValue: function (row) { return (row.Requests[d.Code] || []).map(function (x) { return x.Department; }).join(" "); },
            cellTemplate: function (c, o) {
                c.addClass("ml-dist");
                (o.data.Requests[d.Code] || []).forEach(function (x) {
                    $("<div>").toggleClass("ml-pending", !x.Printed)
                        .attr("title", (x.RequestNo || "") + (x.Printed ? " · printed " + prShortDate(x.PrintedDate) : " · not printed yet"))
                        .text(x.Qty + (x.Colour ? "W" : "") + ":" + x.Department + (x.Date ? "-" + prShortDate(x.Date) : ""))
                        .appendTo(c);
                });
            }
        };
    });

    var options = {
        dataSource: prRows(),
        keyExpr: "Key",
        height: prGridHeight(),
        wordWrapEnabled: false,
        sorting: { mode: "multiple" },
        filterRow: { visible: false, applyFilter: "auto" },
        paging: { pageSize: 50 },
        pager: { visible: true, displayMode: "compact", showPageSizeSelector: true, allowedPageSizes: [25, 50, 100], showInfo: true, showNavigationButtons: true },
        grouping: { autoExpandAll: true },
        groupPanel: { visible: false },
        noDataText: "No approved print requests for this filter.",
        // total copies per owning department on its group row
        summary: {
            groupItems: [{ column: "TotalQty", summaryType: "sum", displayFormat: "{0} copies", alignByColumn: true }]
        },
        columns: [
            { dataField: "Owner", caption: "Department", groupIndex: 0, allowHiding: false },
            { dataField: "Division", visible: false, showInColumnChooser: false },
            { dataField: "DepartmentCode", visible: false, showInColumnChooser: false },
            { dataField: "No", caption: "No.", width: 52, alignment: "center", allowSorting: false, allowFiltering: false, fixed: true, fixedPosition: "left" },
            {
                dataField: "Code", caption: "No. Dokumen", width: 190, fixed: true, fixedPosition: "left", sortIndex: 0, sortOrder: "asc",
                calculateSortValue: function (row) { return ("000000000" + prSeqNumber(row.Code)).slice(-9) + "|" + (row.Code || "") + "|" + row.Revision; },
                cellTemplate: function (c, o) { c.addClass("ml-col-code").text(o.value || ""); }
            },
            { dataField: "Name", caption: "Nama Dokumen", width: 260, fixed: true, fixedPosition: "left",
              cellTemplate: function (c, o) { c.css("white-space", "normal").text(o.value || ""); } },
            { dataField: "Category", caption: "Jenis Dokumen", width: 90, alignment: "center", fixed: true, fixedPosition: "left" },
            { dataField: "Revision", caption: "No. Revisi", width: 80, alignment: "center", fixed: true, fixedPosition: "left" },
            { dataField: "TotalQty", caption: "Total QTY", width: 84, alignment: "center", fixed: true, fixedPosition: "left", cssClass: "ml-total" },
            { caption: "Permintaan Print per Divisi (QTY:Dept-Tgl Request)", columns: divColumns.length ? divColumns : [{ caption: "-", allowSorting: false }] }
        ],
        onContentReady: function () { prSize(); }
    };

    var $g = $("#prGrid");
    if ($g.data("dxDataGrid")) $g.dxDataGrid("dispose");
    $g.empty().dxDataGrid(dmsGridOptions(options));
    prApplyOwnerFilter();
}

function prGridHeight() {
    if (dmsIsPhone()) return "auto";
    var footer = $("#divFooter").outerHeight(true) || 0;
    return Math.max(320, $(window).height() - $("#prGrid").offset().top - footer - 40);
}

function prSize() {
    var grid = $("#prGrid").data("dxDataGrid");
    if (!grid) return;
    var h = prGridHeight();
    if (grid.option("height") !== h) grid.option("height", h);
}

// the export follows the grid's order: No. Dokumen = running number, Nama, Jenis, Total QTY
function prExportSort() {
    var grid = $("#prGrid").data("dxDataGrid"), result = { field: "number", dir: "asc" };
    if (!grid) return result;
    var map = { Code: "number", Name: "name", Category: "type", TotalQty: "qty" }, best = null;
    for (var i = 0; i < grid.columnCount(); i++) {
        var c = grid.columnOption(i);
        if (c.sortOrder && map[c.dataField] && (best === null || c.sortIndex < best.sortIndex)) best = c;
    }
    if (best) result = { field: map[best.dataField], dir: best.sortOrder };
    return result;
}

function prExportUrl(base) {
    var s = prExportSort();
    return base + "?" + $.param({
        dateFrom: $("#prDateFrom").val(), dateTo: $("#prDateTo").val(),
        division: $("#prDivision").val() || "", department: $("#prDepartment").val() || "",
        sort: s.field, dir: s.dir
    });
}

$(document).ready(function () {
    // default period: from 1 January of this year to today
    var today = new Date();
    $("#prDateFrom").val(prIsoDate(new Date(today.getFullYear(), 0, 1)));
    $("#prDateTo").val(prIsoDate(today));

    $("#prDateFrom, #prDateTo").on("change", prLoad);
    $("#btn_refresh").on("click", prLoad);
    $("#prDivision").on("change", function () { prFillDepartmentFilter(); prApplyOwnerFilter(); });
    $("#prDepartment").on("change", prApplyOwnerFilter);
    $("#btn_excel").on("click", function () { window.location.href = prExportUrl(printRequestReportConfig.urls.excel); });
    $("#btn_pdf").on("click", function () { window.open(prExportUrl(printRequestReportConfig.urls.pdf), "_blank"); });
    $("#btn_prColumns").on("click", function () { var g = $("#prGrid").data("dxDataGrid"); if (g) g.showColumnChooser(); });
    $("#btn_prFilters").on("click", function () {
        var g = $("#prGrid").data("dxDataGrid");
        if (!g) return;
        var v = !g.option("filterRow.visible");
        g.option("filterRow.visible", v);
        $(this).toggleClass("active", v);
    });
    $(window).on("resize", prSize);

    prLoad();
});
