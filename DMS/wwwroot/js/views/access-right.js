// Access Rights Review. Server values come from accessRightConfig in the view.
// One standard #datagrid (grid-common.js); each tab rebuilds it with its own columns.
var arData = { users: [], items: [], roles: [], trackingSince: null };
var arTab = "users";
var arLoginStart, arLoginEnd;

// Same role colours as User Management
var AR_ROLE_COLORS = [
    { bg: "#E7F1FB", fg: "#1C3FA6" },
    { bg: "#F1EAFB", fg: "#6C3FB5" },
    { bg: "#E4F7EC", fg: "#1E9E63" },
    { bg: "#FFF4DC", fg: "#B36B00" },
    { bg: "#FDECEC", fg: "#C62828" },
    { bg: "#E6F7F5", fg: "#0E8074" }
];

function arRoleColor(roleId) {
    var id = roleId || "", hash = 0;
    for (var i = 0; i < id.length; i++) { hash = (hash * 31 + id.charCodeAt(i)) >>> 0; }
    return AR_ROLE_COLORS[hash % AR_ROLE_COLORS.length];
}

function arGranted(item, role) {
    return ("," + (item.GRANTED_ROLES || "") + ",").toUpperCase().indexOf("," + String(role).toUpperCase() + ",") >= 0;
}

function arDate(value, format) { return value != null ? moment(value).format(format || "DD-MM-YYYY") : ""; }

// ---------------------------------------------------------------- shared columns

function arNoColumn() {
    return {
        caption: "No", width: 50, alignment: "center", allowHiding: false, allowFiltering: false, fixed: true, fixedPosition: "left",
        cellTemplate: function (container, options) {
            container.text(options.component.pageIndex() * options.component.pageSize() + options.row.rowIndex + 1);
        }
    };
}

function arRoleColumn() {
    return {
        caption: "Role", dataField: "ROLE_NAME",
        calculateCellValue: function (row) { return row.ROLE_NAME || row.ROLE_ID || ""; },
        cellTemplate: function (container, options) {
            if (!options.value) return;
            var c = arRoleColor(options.data.ROLE_ID);
            $("<span>").addClass("um-role-pill").css({ background: c.bg, color: c.fg }).text(options.value).appendTo(container);
        }
    };
}

function arStatusColumn(isActive, labels) {
    labels = labels || ["Active", "Inactive"];
    return {
        caption: "Status",
        calculateCellValue: function (row) { return isActive(row) ? labels[0] : labels[1]; },
        cellTemplate: function (container, options) {
            var $cell = $("<div>").addClass("um-status-cell");
            $("<span>").addClass("um-status-dot").toggleClass("inactive", !isActive(options.data)).appendTo($cell);
            $("<span>").text(options.value).appendTo($cell);
            $cell.appendTo(container);
        }
    };
}

function arUserActive(row) { return row.IS_ACTIVE == 1; }

// ---------------------------------------------------------------- per-tab grids

function arUsersGrid() {
    return {
        dataSource: arData.users,
        keyExpr: "USERNAME",
        columns: [
            arNoColumn(),
            { dataField: "USERNAME", caption: "Username", fixed: true, fixedPosition: "left" },
            { dataField: "REG_NO", caption: "Employee ID" },
            { dataField: "FULL_NAME", caption: "Full Name" },
            { dataField: "EMAIL", caption: "Email", visible: false },
            { dataField: "DEPARTMENTS", caption: "Department" },
            { dataField: "POSITIONS", caption: "Position" },
            { dataField: "DIVISIONS", caption: "Division", visible: false },
            arRoleColumn(),
            {
                caption: "Login Type", alignment: "center",
                calculateCellValue: function (row) { return row.ACCOUNT_TYPE === "Active Directory" ? "AD" : "Local"; }
            },
            { dataField: "MENU_COUNT", caption: "Menus", alignment: "center", dataType: "number" },
            { dataField: "FUNCTION_COUNT", caption: "Functions", alignment: "center", dataType: "number" },
            {
                dataField: "LAST_LOGIN_DT", caption: "Last Login", alignment: "right", allowHeaderFiltering: false,
                cellTemplate: function (container, options) { container.text(arDate(options.value, "DD-MM-YYYY HH:mm")); }
            },
            { dataField: "LOGIN_COUNT_90D", caption: "Logins (90 Days)", alignment: "center", dataType: "number" },
            { dataField: "FAILED_LOGIN_30D", caption: "Failed Logins (30 Days)", alignment: "center", dataType: "number" },
            arStatusColumn(arUserActive),
            { dataField: "CREATED_BY", caption: "Created By", visible: false },
            {
                dataField: "CREATED_DT", caption: "Created Date", alignment: "right", visible: false,
                cellTemplate: function (container, options) { container.text(arDate(options.value)); }
            },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
            {
                dataField: "CHANGED_DT", caption: "Changed Date", alignment: "right", visible: false,
                cellTemplate: function (container, options) { container.text(arDate(options.value)); }
            }
        ]
    };
}

// One row per user per menu / function their role opens
function arAccessRows() {
    var rows = [];
    arData.users.forEach(function (u) {
        if (!u.ROLE_ID) return;
        arData.items.forEach(function (i) {
            if (!arGranted(i, u.ROLE_ID)) return;
            rows.push({
                KEY: u.USERNAME + "|" + i.ITEM_TYPE + "|" + i.ITEM_ID,
                USERNAME: u.USERNAME, FULL_NAME: u.FULL_NAME, ROLE_ID: u.ROLE_ID, ROLE_NAME: u.ROLE_NAME, IS_ACTIVE: u.IS_ACTIVE,
                PARENT_NAME: i.PARENT_NAME, MENU_NAME: i.MENU_NAME, ITEM_TYPE: i.ITEM_TYPE,
                FUNCTION_ID: i.FUNCTION_ID, FUNCTION_NAME: i.FUNCTION_NAME
            });
        });
    });
    return rows;
}

function arAccessGrid() {
    return {
        dataSource: arAccessRows(),
        keyExpr: "KEY",
        columns: [
            arNoColumn(),
            { dataField: "USERNAME", caption: "Username", fixed: true, fixedPosition: "left" },
            { dataField: "FULL_NAME", caption: "Full Name" },
            arRoleColumn(),
            { dataField: "PARENT_NAME", caption: "Module" },
            { dataField: "MENU_NAME", caption: "Menu" },
            {
                dataField: "ITEM_TYPE", caption: "Type", alignment: "center",
                calculateCellValue: function (row) { return row.ITEM_TYPE === "MENU" ? "Menu" : "Function"; }
            },
            { dataField: "FUNCTION_ID", caption: "Function ID", visible: false },
            { dataField: "FUNCTION_NAME", caption: "Function" },
            arStatusColumn(arUserActive)
        ]
    };
}

// Menus and functions down the side, one ✓ column per role
function arMatrixGrid() {
    var columns = [
        arNoColumn(),
        { dataField: "PARENT_NAME", caption: "Module", fixed: true, fixedPosition: "left" },
        { dataField: "MENU_NAME", caption: "Menu", fixed: true, fixedPosition: "left" },
        {
            dataField: "ITEM_TYPE", caption: "Type", alignment: "center",
            calculateCellValue: function (row) { return row.ITEM_TYPE === "MENU" ? "Menu" : "Function"; }
        },
        { dataField: "FUNCTION_ID", caption: "Function ID", visible: false },
        { dataField: "FUNCTION_NAME", caption: "Function" }
    ];
    arData.roles.forEach(function (r) {
        columns.push({
            caption: r.ROLE_NAME + " (" + r.ACTIVE_USERS + ")",
            alignment: "center",
            calculateCellValue: function (row) { return arGranted(row, r.ROLE_ID) ? "✓" : ""; },
            cssClass: "ar-matrix-cell"
        });
    });
    return {
        dataSource: arData.items,
        keyExpr: "ITEM_ID",
        columns: columns,
        onRowPrepared: function (e) {
            if (e.rowType === "data" && e.data.ITEM_TYPE === "MENU") e.rowElement.addClass("ar-matrix-menu-row");
        }
    };
}

function arLoginGrid() {
    return {
        dataSource: new DevExpress.data.CustomStore({
            key: "LOGIN_LOG_ID",
            loadMode: "raw",
            load: function () {
                var d = $.Deferred();
                $.ajax({ type: "POST", url: accessRightConfig.urls.loginLog, data: { startDt: arLoginStart, endDt: arLoginEnd } })
                    .done(function (r) {
                        if (r.status === true) d.resolve(r.data);
                        else { showErrorMesgGrowl(r.message || "Failed to load data"); d.resolve([]); }
                    })
                    .fail(function () { d.reject("Data loading error"); });
                return d.promise();
            }
        }),
        columns: [
            arNoColumn(),
            {
                dataField: "LOGIN_DT", caption: "Time", alignment: "right", allowHeaderFiltering: false,
                cellTemplate: function (container, options) { container.text(arDate(options.value, "DD-MM-YYYY HH:mm:ss")); }
            },
            { dataField: "USERNAME", caption: "Username" },
            { dataField: "FULL_NAME", caption: "Full Name" },
            arStatusColumn(function (row) { return row.RESULT === "SUCCESS"; }, ["Success", "Failed"]),
            { dataField: "MESSAGE", caption: "Message" },
            { dataField: "CLIENT_IP", caption: "IP Address" },
            {
                caption: "Browser / Device",
                calculateCellValue: function (row) {
                    var ua = row.USER_AGENT || "";
                    if (!ua) return "";
                    var b = /Edg\//.test(ua) ? "Edge" : /OPR\//.test(ua) ? "Opera" : /Chrome\//.test(ua) ? "Chrome" : /Firefox\//.test(ua) ? "Firefox" : /Safari\//.test(ua) ? "Safari" : "Other";
                    var os = /Windows/.test(ua) ? "Windows" : /Android/.test(ua) ? "Android" : /iPhone|iPad/.test(ua) ? "iOS" : /Mac OS/.test(ua) ? "macOS" : /Linux/.test(ua) ? "Linux" : "";
                    return b + (os ? " - " + os : "");
                }
            }
        ],
        noDataText: arData.trackingSince
            ? "No login recorded for this period."
            : "No login recorded yet. Login history starts with this release."
    };
}

// ---------------------------------------------------------------- page

function arBuildGrid() {
    var $grid = $("#datagrid");
    var old = $grid.data("dxDataGrid");
    if (old) { old.dispose(); $grid.empty(); }

    var options = { users: arUsersGrid, access: arAccessGrid, matrix: arMatrixGrid, logins: arLoginGrid }[arTab]();
    $grid.dxDataGrid(dmsGridOptions($.extend({
        height: computeGridHeight(),
        filterRow: { visible: $("#btn_toggleFilters").hasClass("active"), applyFilter: "auto" }
    }, options)));
}

function arLoad() {
    var d = $.Deferred();
    $.ajax({ type: "POST", url: accessRightConfig.urls.data })
        .done(function (r) {
            if (r.status !== true) { showErrorMesgGrowl(r.message || "Failed to load data"); d.reject(); return; }
            arData = r;
            d.resolve();
        })
        .fail(function () { showErrorMesgGrowl("Failed to load data"); d.reject(); });
    return d.promise();
}

function arShowTab(tab) {
    arTab = tab;
    $(".ar-tab").removeClass("active").filter('[data-tab="' + tab + '"]').addClass("active");
    $("#arLoginRange").toggle(tab === "logins");
    arBuildGrid();
}

$(document).ready(function () {
    arLoginStart = moment().subtract(29, "days").format("YYYY-MM-DD");
    arLoginEnd = moment().format("YYYY-MM-DD");
    $("#arLoginDate").daterangepicker({
        startDate: moment(arLoginStart), endDate: moment(arLoginEnd), autoUpdateInput: true,
        locale: { cancelLabel: "Clear", format: "DD-MM-YYYY" }
    });
    $("#arLoginDate").on("apply.daterangepicker", function (ev, p) {
        arLoginStart = p.startDate.format("YYYY-MM-DD"); arLoginEnd = p.endDate.format("YYYY-MM-DD");
        $("#datagrid").dxDataGrid("instance").refresh();
    });
    $("#arLoginDate").on("cancel.daterangepicker", function () {
        arLoginStart = null; arLoginEnd = null; $(this).val("");
        $("#datagrid").dxDataGrid("instance").refresh();
    });

    $(".ar-tab").on("click", function () { arShowTab($(this).data("tab")); });

    // Refresh reloads the data behind the open tab
    $("#btn_refresh").on("click", function () {
        if (arTab === "logins") { $("#datagrid").dxDataGrid("instance").refresh(); return; }
        arLoad().done(arBuildGrid);
    });
    $("#btn_export").on("click", function () {
        window.location.href = accessRightConfig.urls.downloadExcel;
    });
    bindGridToolbar("columns filters reset resize");

    arBuildGrid();
    arLoad().done(arBuildGrid);
});
