// Shared setup for pages with a DevExtreme grid in #datagrid.
// A view can replace any global function here by declaring one with the same name.

// Options shared by the app's grids. Anything passed in takes priority.
// ---------- Frozen columns on a phone (2026-11-16) ----------
// On a narrow screen the left frozen columns (No, Document No, ...) take most of the width and
// the rest of the row scrolls in a thin strip beside them. Below 577px only the checkbox (left)
// and the Action column (right) stay frozen; the others scroll with the row. They freeze again
// when the screen gets wider (rotation, desktop). Every grid built with dmsGridOptions.
var DMS_PHONE_QUERY = window.matchMedia ? window.matchMedia("(max-width: 576px)") : null;

function dmsIsPhone() {
    return !!(DMS_PHONE_QUERY && DMS_PHONE_QUERY.matches);
}

function dmsKeepsFrozen(col) {
    return col.type === "selection" || col.fixedPosition === "right";
}

// On a phone the row number column ("No") is hidden and the row menu column (Action) is
// narrowed, so the document columns get the width instead.
var DMS_PHONE_ACTION_WIDTH = 52;

function dmsMarkFrozenColumns(columns) {
    var phone = dmsIsPhone();
    (columns || []).forEach(function (col) {
        if (!col || typeof col !== "object") return;
        if (col.fixed && !dmsKeepsFrozen(col)) {
            col.dmsFrozen = true;              // remember it was frozen on a wide screen
            if (phone) col.fixed = false;
        }
        if (col.caption === "No" && !col.dataField && col.visible !== false) {
            col.dmsPhoneHidden = true;
            if (phone) col.visible = false;
        }
        if (col.caption === "Action" && col.fixedPosition === "right" && col.width) {
            col.dmsWideWidth = col.width;
            if (phone) col.width = DMS_PHONE_ACTION_WIDTH;
        }
        if (col.columns) dmsMarkFrozenColumns(col.columns);
    });
}

function dmsApplyFrozenColumns() {
    var phone = dmsIsPhone();
    $(".dx-datagrid").closest(".dx-widget").each(function () {
        var grid = $(this).data("dxDataGrid");
        if (!grid) return;
        var count = grid.columnCount();
        grid.beginUpdate();
        if (this.id === "datagrid") grid.option("height", computeGridHeight());
        for (var i = 0; i < count; i++) {
            if (grid.columnOption(i, "dmsFrozen")) grid.columnOption(i, "fixed", !phone);
            if (grid.columnOption(i, "dmsPhoneHidden")) grid.columnOption(i, "visible", !phone);
            var wide = grid.columnOption(i, "dmsWideWidth");
            if (wide) grid.columnOption(i, "width", phone ? DMS_PHONE_ACTION_WIDTH : wide);
        }
        grid.endUpdate();
    });
}

if (DMS_PHONE_QUERY) {
    if (DMS_PHONE_QUERY.addEventListener) DMS_PHONE_QUERY.addEventListener("change", dmsApplyFrozenColumns);
    else if (DMS_PHONE_QUERY.addListener) DMS_PHONE_QUERY.addListener(dmsApplyFrozenColumns);
}

function dmsGridOptions(options) {
    if (options && options.columns) dmsMarkFrozenColumns(options.columns);
    return $.extend({
        showBorders: true,
        columnAutoWidth: true,
        allowColumnResizing: true,
        columnResizingMode: "widget",
        wordWrapEnabled: false,
        sorting: { mode: "none" },
        noDataText: "Data not found.",
        headerFilter: { visible: true },
        toolbar: { visible: false },
        paging: { pageSize: 10 },
        pager: {
            visible: true,
            displayMode: "compact",
            showPageSizeSelector: true,
            allowedPageSizes: [10, 25, 50, 100],
            showInfo: true,
            showNavigationButtons: true
        },
        columnChooser: { enabled: true, mode: "select" },
        columnFixing: { enabled: true }
    }, options);
}

function computeGridHeight() {
    // Phone: the grid is as tall as its rows. A screen-tall grid left a large empty area
    // under a few rows and pushed the summary panel far down (2026-11-16).
    if (dmsIsPhone()) {
        return "auto";
    }
    var gridTop = $("#datagrid").offset().top;
    var footerHeight = $("#divFooter").outerHeight(true) || 0;

    return Math.max(300, $(window).height() - gridTop - footerHeight - 40);
}

function getSelectedRowData() {
    var rows = $("#datagrid").dxDataGrid("instance").getSelectedRowsData();
    return rows.length > 0 ? rows[0] : null;
}

function getSelectedRowsData() {
    return $("#datagrid").dxDataGrid("instance").getSelectedRowsData();
}

function onSearch() {
    var grid = $("#datagrid").dxDataGrid("instance");
    grid.clearSelection();
    grid.refresh();
}

function setStatsPanelCollapsed(collapsed) {
    var $row = $("#dpLayoutRow");
    var $icon = $("#btn_toggleStats i");

    $row.toggleClass("stats-collapsed", collapsed);
    $icon.toggleClass("fa-angle-double-right", !collapsed).toggleClass("fa-angle-double-left", collapsed);
    $("#btn_toggleStats").attr("title", collapsed ? "Show Summary" : "Hide Summary");

    setTimeout(function () {
        $("#datagrid").dxDataGrid("instance").option("height", computeGridHeight());
    }, 230);
}

// Binds the standard toolbar behaviour. Pass only the parts a page uses, for
// example "columns filters reset refresh resize stats".
function bindGridToolbar(parts) {
    var grid = function () { return $("#datagrid").dxDataGrid("instance"); };
    var binders = {
        columns: function () {
            $("#btn_columns").on("click", function () {
                grid().showColumnChooser();
            });
        },
        filters: function () {
            $("#btn_toggleFilters").on("click", function () {
                var visible = !grid().option("filterRow.visible");
                grid().option("filterRow.visible", visible);
                $(this).toggleClass("active", visible);
            });
        },
        reset: function () {
            $("#btn_resetFilters").on("click", function () {
                grid().clearFilter();
                grid().clearSelection();
            });
        },
        refresh: function () {
            $("#btn_refresh").on("click", function () {
                grid().clearSelection();
                grid().refresh();
            });
        },
        resize: function () {
            $(window).on("resize", function () {
                grid().option("height", computeGridHeight());
            });
        },
        stats: function () {
            $("#btn_toggleStats").on("click", function () {
                setStatsPanelCollapsed(!$("#dpLayoutRow").hasClass("stats-collapsed"));
            });
            $("#btn_closeStatsPanel").on("click", function () {
                setStatsPanelCollapsed(true);
            });
        }
    };

    parts.split(" ").forEach(function (part) {
        binders[part]();
    });
}

// Per-row "more actions" menu.
var $dmsRowMenu = null;

function buildRowActionMenu() {
    if ($dmsRowMenu) return $dmsRowMenu;

    $dmsRowMenu = $('<ul class="dropdown-menu dp-dropdown-menu" role="menu"></ul>')
        .css({ position: "absolute", display: "none", "z-index": 2000, "min-width": "170px" })
        .appendTo("body");

    $(document).on("click", function () {
        $dmsRowMenu.hide();
    });

    return $dmsRowMenu;
}

function menuItem(cssClass, icon, label, onClick) {
    var $li = $('<li><a href="#" class="' + cssClass + '"><i class="fa ' + icon + '"></i> ' + label + '</a></li>');
    $li.find("a").on("click", function (e) {
        e.preventDefault();
        $dmsRowMenu.hide();
        onClick();
    });
    return $li;
}

// Font Awesome classes for a file-type icon in front of a document number, like File
// Explorer (My Documents and Document Control Dashboard, 2026-11-14). Colours: admin-theme.css.
function dmsFileIconClass(path) {
    var ext = ((path || "").split(".").pop() || "").toLowerCase();
    if (ext === "pdf") return "fa-file-pdf-o dms-file-pdf";
    if (ext === "xls" || ext === "xlsx" || ext === "xlsm") return "fa-file-excel-o dms-file-excel";
    if (ext === "doc" || ext === "docx") return "fa-file-word-o dms-file-word";
    if (["png", "jpg", "jpeg", "gif", "bmp"].indexOf(ext) >= 0) return "fa-file-image-o dms-file-image";
    return "fa-file-o dms-file-other";
}
