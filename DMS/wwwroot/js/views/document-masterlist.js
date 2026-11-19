// DocumentMasterlist. Server values come from documentMasterlistConfig in the view.
// View opens the in-page book viewer, the same in every menu (2026-11-18).
function viewAttachment(filePath, documentTransactionId, row) {
    dmsViewDocument(documentMasterlistConfig.urls.viewAttachment, filePath, documentTransactionId, "1", row);
}

// ---------- summary panel (2026-11-16) ----------
function mlRenderStats(rows) {
    var count = function (status) { return rows.filter(function (r) { return r.STATUS == status; }).length; };
    $("#mlStatPublished").text(count(5));
    $("#mlStatApproved").text(count(1));
    $("#mlStatValid").text(count(5) + count(1));
    $("#mlStatObsolete").text(count(4));
}

var mlCategoryPills = ["dms-pill-info", "dms-pill-success", "dms-pill-warning", "dms-pill-primary", "dms-pill-default"];
function mlCategoryPillClass(code) {
    var sum = 0;
    for (var i = 0; i < (code || "").length; i++) sum += code.charCodeAt(i);
    return mlCategoryPills[sum % mlCategoryPills.length];
}

// Default view: valid documents only (Approved, Published). Reset brings it back.
var ML_DEFAULT_STATUS = [1, 5];

$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentMasterlistConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                mlRenderStats(rows);
                d.resolve(rows);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        filterRow: { visible: false, applyFilter: "auto" },
        columns: [
            {
                caption: "No",
                width: 50,
                alignment: "center",
                allowHiding: false,
                allowFiltering: false,
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var pageIndex = options.component.pageIndex();
                    var pageSize = options.component.pageSize();
                    container.text(pageIndex * pageSize + options.row.rowIndex + 1);
                }
            },
            {
                dataField: "DOCUMENT_CODE",
                caption: "Document Code",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    $("<i>").addClass("fa dms-file-icon " + dmsFileIconClass(row.FILE_PATH)).appendTo(container);
                    $("<span>").addClass("ml-doc-link").text(row.DOCUMENT_CODE || "")
                        .on("click", function () { viewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID, row); })
                        .appendTo(container);
                }
            },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name" },
            {
                dataField: "DOCUMENT_CATEGORY",
                caption: "Category",
                alignment: "center",
                cellTemplate: function (container, options) {
                    if (!options.value) return;
                    $("<span>").addClass("dms-pill " + mlCategoryPillClass(options.value)).text(options.value).appendTo(container);
                }
            },
            {
                caption: "Division",
                calculateCellValue: function (row) {
                    return row.DIVISION != null ? row.DIVISION + " - " + row.DIVISION_NAME : "";
                }
            },
            {
                caption: "Department",
                calculateCellValue: function (row) {
                    return row.DEPARTMENT_CODE != null ? row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME : "";
                }
            },
            { dataField: "CLASSIFIED_VAL", caption: "Classified" },
            {
                dataField: "REVISION",
                caption: "Revision",
                alignment: "center",
                width: 90,
                cellTemplate: function (container, options) {
                    $("<span>").addClass("dms-pill dms-pill-info").text("Rev. " + (options.value != null ? options.value : 0)).appendTo(container);
                }
            },
            {
                dataField: "STATUS",
                caption: "Status",
                alignment: "center",
                // Default view = "current, valid" documents: Approved and Published (Effective).
                // Filter row renders as a multi-select (anyof) so users can still narrow/widen it.
                filterValues: [1, 5],
                selectedFilterOperation: "anyof",
                lookup: {
                    dataSource: [
                        { value: 0, text: "Waiting Approval" },
                        { value: 1, text: "Approved" },
                        { value: 2, text: "Rejected" },
                        { value: 4, text: "Obsolete" },
                        { value: 5, text: "Published (Effective)" }
                    ],
                    valueExpr: "value",
                    displayExpr: "text"
                },
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = "dms-pill-danger";

                    if (row.STATUS == 0) {
                        pillClass = "dms-pill-info";
                    } else if (row.STATUS == 1) {
                        pillClass = "dms-pill-success";
                    } else if (row.STATUS == 4) {
                        pillClass = "dms-pill-default";
                    } else if (row.STATUS == 5) {
                        pillClass = "dms-pill-primary";
                    }

                    $("<span>").addClass("dms-pill " + pillClass).append($("<span>").addClass("dms-dot")).append(document.createTextNode(row.STATUS_DISPLAY || "")).appendTo(container);
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Effective Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                caption: "Last Change",
                calculateCellValue: function (row) {
                    return [row.ITEM_CHANGED, row.REASON].filter(Boolean).join(" - ");
                }
            },
            { dataField: "DOCUMENT_CREATOR", caption: "Owner" },
            { dataField: "CREATED_BY", caption: "Created By", visible: false },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
            {
                dataField: "CHANGED_DT",
                caption: "Changed Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                caption: "Action",
                alignment: "center",
                width: 70,
                allowHiding: false,
                allowFiltering: false,
                fixed: true,
                fixedPosition: "right",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    if (row.FILE_PATH == null) {
                        return;
                    }

                    $("<button>")
                        .attr({ type: "button", title: "More actions" })
                        .addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) {
                            e.stopPropagation();
                            var $menu = buildRowActionMenu();
                            $menu.empty().append(menuItem("dp-menu-preview", "fa-eye", "View", function () {
                                viewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID, row);
                            }));
                            var offset = $(this).offset();
                            $menu.css({ top: offset.top + $(this).outerHeight() + 4, left: offset.left - $menu.outerWidth() + $(this).outerWidth() }).show();
                        })
                        .appendTo(container);
                }
            }
        ]
    }));

    bindGridToolbar("columns filters refresh resize stats");

    // Reset clears the column filters but keeps the default view (valid documents)
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        grid.beginUpdate();
        grid.clearFilter();
        grid.clearSelection();
        grid.columnOption("STATUS", "filterValues", ML_DEFAULT_STATUS);
        grid.endUpdate();
    });

    // Reset clears every filter; bring back the default "current, valid" status filter
    $("#btn_resetFilters").on("click", function () {
        $("#datagrid").dxDataGrid("instance").columnOption("STATUS", "filterValues", [1, 5]);
    });

    $("#btn_export").on("click", function () {
        window.location.href = documentMasterlistConfig.urls.downloadExcel;
    });

});

// ---------- Matrix view: form QCD/FR-QMS-00/004 (2026-11-19) ----------
// Revision dates and the full distribution history per division, grouped per owning
// department, in a DevExtreme grid like the other lists (2026-11-19). The Excel and PDF
// buttons export the same matrix from the server, in the grid's filter and order.
var mlMatrix = null;          // last loaded report
var mlMonths = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function mlSeqNumber(code) {
    var m = /(\d+)$/.exec(code || "");
    return m ? parseInt(m[1], 10) : 999999999;
}

function mlShortDate(value, withDash) {
    if (!value) return "";
    var d = new Date(value);
    if (isNaN(d.getTime())) return "";
    var dd = ("0" + d.getDate()).slice(-2), yy = ("" + d.getFullYear()).slice(-2), mmm = mlMonths[d.getMonth()];
    return withDash ? dd + "-" + mmm + "-" + yy : dd + mmm + yy;
}

function mlSetView(view) {
    var matrix = view === "matrix";
    $(".ml-view-switch button").removeClass("active").filter("[data-view='" + view + "']").addClass("active");
    $(".ml-list-only").toggle(!matrix);
    $(".ml-matrix-only").toggle(matrix);
    $("#divGrid").toggle(!matrix);
    $("#divMatrix").toggle(matrix);
    if (matrix) {
        setStatsPanelCollapsed(true);
        if (!mlMatrix) mlLoadMatrix();
        else mlSizeMatrix();
    } else {
        $("#datagrid").dxDataGrid("instance").option("height", computeGridHeight());
    }
    try { localStorage.setItem("dms.masterlist.view", view); } catch (e) { }
}

function mlLoadMatrix() {
    var grid = $("#mlMatrixGrid").data("dxDataGrid");
    if (grid) grid.beginCustomLoading("Loading…");
    $.ajax({ type: "GET", url: documentMasterlistConfig.urls.matrixData, cache: false })
        .done(function (report) {
            mlMatrix = report;
            mlFillDivisionFilter();
            mlBuildGrid();
        })
        .fail(function () {
            if (grid) grid.endCustomLoading();
            showErrorMesgGrowl("Could not load the matrix.");
        });
}

function mlFillDivisionFilter() {
    var $div = $("#mlMatrixDivision"), keep = $div.val(), seen = {};
    $div.find("option:gt(0)").remove();
    mlMatrix.Groups.forEach(function (g) {
        if (!g.Division || seen[g.Division]) return;
        seen[g.Division] = true;
        $("<option>").val(g.Division).text(g.Division + " - " + (g.DivisionName || "")).appendTo($div);
    });
    $div.val(seen[keep] ? keep : "");
    mlFillDepartmentFilter();
}

function mlFillDepartmentFilter() {
    var $dept = $("#mlMatrixDepartment"), keep = $dept.val(), division = $("#mlMatrixDivision").val(), seen = {};
    $dept.find("option:gt(0)").remove();
    mlMatrix.Groups.forEach(function (g) {
        if (!g.DepartmentCode || seen[g.DepartmentCode] || (division && g.Division !== division)) return;
        seen[g.DepartmentCode] = true;
        $("<option>").val(g.DepartmentCode).text(g.DepartmentCode + " - " + (g.DepartmentName || "")).appendTo($dept);
    });
    $dept.val(seen[keep] ? keep : "");
}

// one grid row per document; No = place in its department by document number (form order)
function mlRows() {
    var rows = [];
    mlMatrix.Groups.forEach(function (g) {
        g.Documents.slice().sort(function (a, b) { return (mlSeqNumber(a.Code) - mlSeqNumber(b.Code)) || (a.Code || "").localeCompare(b.Code || ""); })
            .forEach(function (doc, i) {
                rows.push($.extend({}, doc, {
                    No: i + 1,
                    Division: g.Division,
                    DepartmentCode: g.DepartmentCode,
                    Owner: (g.Division || "-") + " · " + (g.DivisionName || "") + "  /  " + (g.DepartmentCode || "-") + " · " + (g.DepartmentName || "")
                }));
            });
    });
    return rows;
}

function mlApplyOwnerFilter() {
    var grid = $("#mlMatrixGrid").data("dxDataGrid");
    if (!grid) return;
    var division = $("#mlMatrixDivision").val(), dept = $("#mlMatrixDepartment").val(), f = [];
    if (division) f.push(["Division", "=", division]);
    if (dept) { if (f.length) f.push("and"); f.push(["DepartmentCode", "=", dept]); }
    grid.filter(f.length ? f : null);
}

function mlBuildGrid() {
    var revCols = mlMatrix.RevisionColumns, divisions = mlMatrix.Divisions;
    var revColumns = [];
    for (var r = 0; r < revCols; r++) (function (rv) {
        revColumns.push({
            caption: String(rv), name: "rev" + rv, width: 82, alignment: "center", allowSorting: false,
            calculateCellValue: function (row) { var x = row.Revisions[rv]; return x && x.Date ? x.Date : null; },
            cellTemplate: function (c, o) {
                var x = o.data.Revisions[rv];
                if (!x || !x.Date) return;
                c.text(mlShortDate(x.Date, true)).toggleClass("ml-rev-current", rv === o.data.Revision);
                if (rv > 0 && x.Reason) c.addClass("ml-has-reason").attr("title", x.Reason);
            }
        });
    })(r);

    var divColumns = divisions.map(function (d) {
        return {
            caption: d.Code, name: "div" + d.Code, minWidth: 104, allowSorting: false, headerCellTemplate: function (h) { h.text(d.Code).attr("title", d.Name || ""); },
            calculateCellValue: function (row) { return (row.Distribution[d.Code] || []).map(function (x) { return x.Revision + ":" + x.Department; }).join(" "); },
            cellTemplate: function (c, o) {
                c.addClass("ml-dist");
                (o.data.Distribution[d.Code] || []).forEach(function (x) {
                    $("<div>").toggleClass("ml-outdated", !!x.Outdated)
                        .attr("title", x.Outdated ? "This department's newest copy is Rev. " + x.Revision + "; current is Rev. " + o.data.Revision : "")
                        .text(x.Revision + ":" + x.Department + (x.Date ? "-" + mlShortDate(x.Date) : "")).appendTo(c);
                });
            }
        };
    });

    var check = function (field) {
        return { caption: field ? "Rahasia" : "Umum", width: 74, alignment: "center", allowSorting: false,
            calculateCellValue: function (row) { return row.Confidential === field ? "✓" : ""; },
            cssClass: "ml-check" };
    };

    var options = {
        dataSource: mlRows(),
        keyExpr: "TransactionId",
        height: mlMatrixHeight(),
        wordWrapEnabled: false,
        sorting: { mode: "multiple" },
        filterRow: { visible: false, applyFilter: "auto" },
        paging: { pageSize: 50 },
        pager: { visible: true, displayMode: "compact", showPageSizeSelector: true, allowedPageSizes: [25, 50, 100], showInfo: true, showNavigationButtons: true },
        grouping: { autoExpandAll: true },
        groupPanel: { visible: false },
        noDataText: "No valid documents for this filter.",
        columns: [
            { dataField: "Owner", caption: "Department", groupIndex: 0, allowHiding: false },
            { dataField: "Division", visible: false, showInColumnChooser: false },
            { dataField: "DepartmentCode", visible: false, showInColumnChooser: false },
            { dataField: "No", caption: "No.", width: 52, alignment: "center", allowSorting: false, allowFiltering: false, fixed: true, fixedPosition: "left" },
            {
                dataField: "Code", caption: "No. Dokumen", width: 190, fixed: true, fixedPosition: "left", sortIndex: 0, sortOrder: "asc",
                // default order = the form: the running number at the end, smallest first
                calculateSortValue: function (row) { return ("000000000" + mlSeqNumber(row.Code)).slice(-9) + "|" + (row.Code || ""); },
                cellTemplate: function (c, o) {
                    c.addClass("ml-col-code").text(o.value || "");
                    if (o.data.OldCodes) $("<span class='ml-old-code'>").text("(dulu: " + o.data.OldCodes + ")").appendTo(c);
                }
            },
            { dataField: "Name", caption: "Nama Dokumen", width: 260, fixed: true, fixedPosition: "left",
              cellTemplate: function (c, o) { c.css("white-space", "normal").text(o.value || ""); } },
            { dataField: "Category", caption: "Jenis Dokumen", width: 90, alignment: "center", fixed: true, fixedPosition: "left" },
            { caption: "Status Dokumen", fixed: true, fixedPosition: "left", columns: [check(false), check(true)] },
            { caption: "Revisi Ke-", columns: revColumns },
            { caption: "Distribusi Dokumen", columns: divColumns.length ? divColumns : [{ caption: "-", allowSorting: false }] }
        ],
        onContentReady: function () { mlSizeMatrix(); }
    };

    var $g = $("#mlMatrixGrid");
    if ($g.data("dxDataGrid")) $g.dxDataGrid("dispose");
    $g.empty().dxDataGrid(dmsGridOptions(options));
    mlApplyOwnerFilter();
}

function mlMatrixHeight() {
    if (dmsIsPhone()) return "auto";
    var $g = $("#mlMatrixGrid");
    var top = $g.is(":visible") ? $g.offset().top : 220;
    var footer = $("#divFooter").outerHeight(true) || 0;
    return Math.max(320, $(window).height() - top - footer - 40);
}

function mlSizeMatrix() {
    var grid = $("#mlMatrixGrid").data("dxDataGrid");
    if (grid && $("#mlMatrixGrid").is(":visible")) {
        var h = mlMatrixHeight();
        if (grid.option("height") !== h) grid.option("height", h);
    }
}

// the export follows the grid's order: No. Dokumen = running number, Nama, Jenis
function mlExportSort() {
    var grid = $("#mlMatrixGrid").data("dxDataGrid"), result = { field: "number", dir: "asc" };
    if (!grid) return result;
    var map = { Code: "number", Name: "name", Category: "type" }, best = null;
    for (var i = 0; i < grid.columnCount(); i++) {
        var c = grid.columnOption(i);
        if (c.sortOrder && map[c.dataField] && (best === null || c.sortIndex < best.sortIndex)) best = c;
    }
    if (best) result = { field: map[best.dataField], dir: best.sortOrder };
    return result;
}

function mlExportUrl(base) {
    var division = $("#mlMatrixDivision").val(), dept = $("#mlMatrixDepartment").val(), s = mlExportSort();
    return base + "?" + $.param({ division: division || "", department: dept || "", sort: s.field, dir: s.dir });
}

$(document).ready(function () {
    $(".ml-view-switch button").on("click", function () { mlSetView($(this).data("view")); });
    $("#mlMatrixDivision").on("change", function () { mlFillDepartmentFilter(); mlApplyOwnerFilter(); });
    $("#mlMatrixDepartment").on("change", mlApplyOwnerFilter);
    // Refresh reloads whichever view is open (the list grid binds its own handler too)
    $("#btn_refresh").on("click", function () { if ($("#divMatrix").is(":visible")) mlLoadMatrix(); else mlMatrix = null; });
    $("#btn_matrixExcel").on("click", function () { window.location.href = mlExportUrl(documentMasterlistConfig.urls.matrixExcel); });
    $("#btn_matrixPdf").on("click", function () { window.open(mlExportUrl(documentMasterlistConfig.urls.matrixPdf), "_blank"); });
    $("#btn_matrixColumns").on("click", function () { var g = $("#mlMatrixGrid").data("dxDataGrid"); if (g) g.showColumnChooser(); });
    $("#btn_matrixFilters").on("click", function () {
        var g = $("#mlMatrixGrid").data("dxDataGrid");
        if (!g) return;
        var v = !g.option("filterRow.visible");
        g.option("filterRow.visible", v);
        $(this).toggleClass("active", v);
    });
    $(window).on("resize", mlSizeMatrix);

    var saved = null;
    try { saved = localStorage.getItem("dms.masterlist.view"); } catch (e) { }
    if (saved === "matrix") mlSetView("matrix");
});
