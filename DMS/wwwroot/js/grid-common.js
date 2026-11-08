// Shared setup for pages with a DevExtreme grid in #datagrid.
// A view can replace any global function here by declaring one with the same name.

// Options shared by the app's grids. Anything passed in takes priority.
function dmsGridOptions(options) {
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
