// Role. Server values come from roleConfig in the view.
var gCanEdit = roleConfig.canEdit;
var gCanDelete = roleConfig.canDelete;
var gCanAuthorize = roleConfig.canAuthorization;

function updateActionButtons() {
    var selected = getSelectedRowData() != null;
    $("#btn_authorization").prop("disabled", !selected);
}

function computeGridHeight() {
    var gridTop = $("#datagrid").offset().top;
    var infoBarHeight = $(".dp-info-bar").outerHeight(true) || 0;

    return Math.max(300, $(window).height() - gridTop - infoBarHeight - 40);
}

$(document).ready(function () {
    $("#btn_authorization").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "ROLE_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: roleConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000
                }
            }).done(function (result) {
                d.resolve(result.data == "" ? [] : result.data);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid({
        dataSource: gridStore,
        showBorders: true,
        columnAutoWidth: true,
        allowColumnResizing: true,
        columnResizingMode: "widget",
        wordWrapEnabled: false,
        sorting: { mode: "none" },
        noDataText: "Data not found.",
        height: computeGridHeight(),
        filterRow: { visible: false, applyFilter: "auto" },
        headerFilter: { visible: true },
        searchPanel: { visible: false, highlightCaseSensitive: false },
        toolbar: { visible: false },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
        },
        paging: { pageSize: 10 },
        pager: {
            visible: true,
            displayMode: "compact",
            showPageSizeSelector: true,
            allowedPageSizes: [10, 25, 50, 100],
            showInfo: true,
            showNavigationButtons: true
        },
        onSelectionChanged: function (e) {
            if (e.selectedRowKeys.length > 1) {
                e.component.selectRows(e.currentSelectedRowKeys, false);
                return;
            }
            updateActionButtons();
        },
        columns: [
            { type: "selection", width: 40, fixed: true, fixedPosition: "left" },
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
                dataField: "ROLE_ID",
                caption: "Role ID",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    $("<div>").addClass("ra-role-cell").append(
                        $("<b>").text(options.data.ROLE_ID)
                    ).appendTo(container);
                }
            },
            { dataField: "ROLE_NAME", caption: "Role Name" },
            { dataField: "ROLE_DESC", caption: "Description" },
            {
                dataField: "USERS_COUNT",
                caption: "Users",
                alignment: "left",
                cellTemplate: function (container, options) {
                    $("<div>").addClass("ra-count-cell").append(
                        $("<i>").addClass("fa fa-user-o"),
                        $("<span>").text((options.data.USERS_COUNT || 0) + " Users")
                    ).appendTo(container);
                }
            },
            {
                dataField: "PERMISSIONS_COUNT",
                caption: "Permissions",
                alignment: "left",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var $wrap = $("<div>");
                    $("<div>").addClass("ra-count-cell").append(
                        $("<i>").addClass("fa fa-key"),
                        $("<span>").text(row.PERMISSIONS_COUNT || 0)
                    ).appendTo($wrap);

                    if (gCanAuthorize) {
                        $("<button>").attr("type", "button").addClass("dp-link")
                            .text("View details")
                            .on("click", function () { openAuthorization(row.ROLE_ID); })
                            .appendTo($wrap);
                    }

                    $wrap.appendTo(container);
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

                    $("<button>")
                        .attr({ type: "button", title: "More actions" })
                        .addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) {
                            e.stopPropagation();
                            openRowActionMenu($(this), row);
                        })
                        .appendTo(container);
                }
            }
        ]
    });

    bindGridToolbar("filters reset refresh resize");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();

    $menu.append(menuItem("dp-menu-preview", "fa-eye", "View", function () {
        onViewRole(row);
    }));

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            onEditPrepare(row);
        }));
    }

    if (gCanAuthorize) {
        $menu.append(menuItem("dp-menu-position", "fa-key", "Permissions", function () {
            openAuthorization(row.ROLE_ID);
        }));
    }

    if (gCanDelete) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", function () {
            onDeletePrepare(row);
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gScreenMode = null;
var gId = null;

$(document).ready(function () {
    $("#btn_add").on("click", function () {
        onAddPrepare();
    });

    $("#btn_save").on("click", function () {
        confirmAddEditData();
    });

    $("#btn_authorization").on("click", function () {
        var row = getSelectedRowData();
        if (row == null) {
            return;
        }
        openAuthorization(row.ROLE_ID);
    });

    $("#btn_save_auth").on("click", function () {
        confirmAddEditAuthorization();
    });

    $("#btn_export").on("click", function () {
        window.location.href = roleConfig.urls.downloadExcel;
    });

});

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Role");
    $('#addEditRoleId').val("");
    $('#addEditRoleName').val("");
    $('#addEditRoleDesc').val("");

    $('#addEditRoleId').prop('disabled', false);
    $('#addEditRoleName').prop('disabled', false);
    $('#addEditRoleDesc').prop('disabled', false);

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditRoleId').focus();
    });
}

function onEditPrepare(row) {
    gId = row.ROLE_ID;
    gScreenMode = 'EDIT';

    $('#modal-title').html("Edit Role");
    $('#addEditRoleId').val(row.ROLE_ID);
    $('#addEditRoleName').val(row.ROLE_NAME);
    $('#addEditRoleDesc').val(row.ROLE_DESC);

    $('#addEditRoleId').prop('disabled', true);
    $('#addEditRoleName').prop('disabled', false);
    $('#addEditRoleDesc').prop('disabled', false);

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditRoleName').focus();
    });
}

function onViewRole(row) {
    $('#viewRoleId').text(row.ROLE_ID || "-");
    $('#viewRoleName').text(row.ROLE_NAME || "-");
    $('#viewRoleDesc').text(row.ROLE_DESC || "-");
    $('#viewRoleUsers').text((row.USERS_COUNT || 0) + " Users");
    $('#viewRolePermissions').text(row.PERMISSIONS_COUNT || 0);

    $("#modalViewRole").modal();
}

function onDeletePrepare(row) {
    gId = row.ROLE_ID;
    showDangerConfirm(deleteMessage("Role", row.ROLE_NAME), deleteData, "Delete");
}

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

function addEditData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: roleConfig.urls.addEdit,
        dataType: 'json',
        traditional: true,
        data: {
            screenMode: gScreenMode,
            ROLE_ID: $('#addEditRoleId').val(),
            ROLE_NAME: $('#addEditRoleName').val(),
            ROLE_DESC: $('#addEditRoleDesc').val(),
        },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                $("#modalAddEdit").modal('hide');
            }else {
               showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function deleteData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: roleConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            ROLE_ID: gId
        },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
            }else {
               showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

var gAuthAllMenu = [];
var gAuthAllFunction = [];
var gAuthCheckedMenus = new Set();
var gAuthCheckedFunctions = new Set();
var gAuthSelectedTopMenu = null;
var gAuthSelectedSubMenu = null;

function openAuthorization(roleId) {
    gId = roleId;
    getAuthorization();
}

function getAuthorization(){
    $.ajax({
        type: "POST",
        url: roleConfig.urls.getAuthByRole,
        async: false,
        data: {
            ROLE_ID: gId
        },
        success: function (result) {
            if (result.status == true) {
                gAuthAllMenu = result.param[0];
                gAuthAllFunction = result.param[2];
                gAuthCheckedMenus = new Set(result.param[1].map(function (x) { return x.MENU_ID; }));
                gAuthCheckedFunctions = new Set(result.param[3].map(function (x) { return x.FUNCTION_ID; }));

                var topMenus = authGetTopMenus();
                gAuthSelectedTopMenu = topMenus.length > 0 ? topMenus[0].MENU_ID : null;
                var subMenus = authGetSubMenus(gAuthSelectedTopMenu);
                gAuthSelectedSubMenu = subMenus.length > 0 ? subMenus[0].MENU_ID : null;

                $('#modal-title-auth').html("Authorization : " + gId);
                $("#authSearchMenu, #authSearchSubMenu, #authSearchPermission").val("");
                renderAuthModal();

                $("#modalAuth").modal({
                    backdrop: 'static',
                    keyboard: false
                });

            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result){
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function authGetTopMenus() {
    return gAuthAllMenu.filter(function (m) { return m.PARENT_ID == null; });
}

function authGetSubMenus(topId) {
    return gAuthAllMenu.filter(function (m) { return m.PARENT_ID == topId; });
}

function authGetFunctionsForMenu(menuId) {
    return gAuthAllFunction.filter(function (f) { return f.MENU_ID == menuId; });
}

function authGetFunctionsForTopMenu(topId) {
    var subMenus = authGetSubMenus(topId);
    var result = [];
    subMenus.forEach(function (s) { result = result.concat(authGetFunctionsForMenu(s.MENU_ID)); });
    return result;
}

function authCategorize(functionId) {
    var id = (functionId || "").toUpperCase();
    if (id.indexOf("ADD") !== -1) return { color: "#2450D8", icon: "fa-plus-circle" };
    if (id.indexOf("EDIT") !== -1) return { color: "#B36B00", icon: "fa-pencil" };
    if (id.indexOf("DELETE") !== -1) return { color: "#C62828", icon: "fa-trash-o" };
    if (id.indexOf("APPROVE") !== -1) return { color: "#6C3FB5", icon: "fa-check-circle" };
    if (id.indexOf("REJECT") !== -1) return { color: "#6C3FB5", icon: "fa-times-circle" };
    if (id.indexOf("VIEW") !== -1) return { color: "#1E9E63", icon: "fa-eye" };
    return { color: "#0E8074", icon: "fa-cog" };
}

function renderAuthModal() {
    renderAuthMenuColumn();
    renderAuthSubMenuColumn();
    renderAuthPermissionColumn();
    renderAuthStats();
}

function renderAuthStats() {
    var totalMenus = gAuthAllMenu.length;
    var totalPermissions = gAuthAllFunction.length;
    var enabled = gAuthCheckedFunctions.size;
    $("#authStatMenus").text(totalMenus);
    $("#authStatPermissions").text(totalPermissions);
    $("#authStatEnabled").text(enabled);
    $("#authStatDisabled").text(totalPermissions - enabled);
}

function renderAuthMenuColumn() {
    var search = ($("#authSearchMenu").val() || "").toLowerCase();
    var $list = $("#authMenuList").empty();

    authGetTopMenus().filter(function (m) {
        return m.MENU_NAME.toLowerCase().indexOf(search) !== -1;
    }).forEach(function (m) {
        var functions = authGetFunctionsForTopMenu(m.MENU_ID);
        var checkedCount = functions.filter(function (f) { return gAuthCheckedFunctions.has(f.FUNCTION_ID); }).length;

        var $item = $("<div>").addClass("ra-auth-list-item")
            .toggleClass("selected", m.MENU_ID === gAuthSelectedTopMenu);

        $("<input>").attr("type", "checkbox").prop("checked", gAuthCheckedMenus.has(m.MENU_ID))
            .on("click", function (e) { e.stopPropagation(); })
            .on("change", function () { authToggleTopMenu(m.MENU_ID, this.checked); })
            .appendTo($item);
        $("<i>").addClass("ra-auth-item-icon fa " + (m.MENU_ICON || "fa-circle-o")).appendTo($item);
        $("<span>").addClass("ra-auth-item-name").text(m.MENU_NAME).appendTo($item);
        $("<span>").addClass("ra-auth-fraction").text(checkedCount + "/" + functions.length).appendTo($item);

        $item.on("click", function () {
            gAuthSelectedTopMenu = m.MENU_ID;
            var subMenus = authGetSubMenus(m.MENU_ID);
            gAuthSelectedSubMenu = subMenus.length > 0 ? subMenus[0].MENU_ID : null;
            renderAuthMenuColumn();
            renderAuthSubMenuColumn();
            renderAuthPermissionColumn();
        });

        $list.append($item);
    });
}

function renderAuthSubMenuColumn() {
    var search = ($("#authSearchSubMenu").val() || "").toLowerCase();
    var $list = $("#authSubMenuList").empty();

    authGetSubMenus(gAuthSelectedTopMenu).filter(function (m) {
        return m.MENU_NAME.toLowerCase().indexOf(search) !== -1;
    }).forEach(function (m) {
        var functions = authGetFunctionsForMenu(m.MENU_ID);
        var checkedCount = functions.filter(function (f) { return gAuthCheckedFunctions.has(f.FUNCTION_ID); }).length;

        var $item = $("<div>").addClass("ra-auth-list-item")
            .toggleClass("selected", m.MENU_ID === gAuthSelectedSubMenu);

        $("<input>").attr("type", "checkbox").prop("checked", gAuthCheckedMenus.has(m.MENU_ID))
            .on("click", function (e) { e.stopPropagation(); })
            .on("change", function () { authToggleSubMenu(m.MENU_ID, this.checked); })
            .appendTo($item);
        $("<span>").addClass("ra-auth-item-name").text(m.MENU_NAME).appendTo($item);
        $("<span>").addClass("ra-auth-fraction").text(checkedCount + "/" + functions.length).appendTo($item);

        $item.on("click", function () {
            gAuthSelectedSubMenu = m.MENU_ID;
            renderAuthSubMenuColumn();
            renderAuthPermissionColumn();
        });

        $list.append($item);
    });
}

function renderAuthPermissionColumn() {
    var search = ($("#authSearchPermission").val() || "").toLowerCase();
    var $grid = $("#authPermissionGrid").empty();
    var subMenu = gAuthAllMenu.find(function (m) { return m.MENU_ID === gAuthSelectedSubMenu; });

    $("#authPermTargetLabel").text("Set permission for: " + (subMenu ? subMenu.MENU_NAME : "-"));

    var functions = authGetFunctionsForMenu(gAuthSelectedSubMenu).filter(function (f) {
        return f.FUNCTION_NAME.toLowerCase().indexOf(search) !== -1 || (f.FUNCTION_DESC || "").toLowerCase().indexOf(search) !== -1;
    });

    if (functions.length === 0) {
        $("<div>").addClass("ra-auth-empty").text("No permissions in this sub menu.").appendTo($grid);
        $("#authCheckAllPermission").prop("checked", false);
        return;
    }

    functions.forEach(function (f) {
        var cat = authCategorize(f.FUNCTION_ID);
        var checked = gAuthCheckedFunctions.has(f.FUNCTION_ID);

        var $card = $("<div>").addClass("ra-auth-permcard");
        $("<div>").addClass("ra-auth-permcard-icon").css("background", cat.color).html('<i class="fa ' + cat.icon + '"></i>').appendTo($card);

        var $body = $("<div>").addClass("ra-auth-permcard-body");
        $("<b>").text(f.FUNCTION_NAME).appendTo($body);
        $("<span>").text(f.FUNCTION_DESC || "").appendTo($body);
        $body.appendTo($card);

        var $toggle = $("<label>").addClass("ra-auth-toggle");
        $("<input>").attr("type", "checkbox").prop("checked", checked)
            .on("change", function () { authToggleFunction(f.FUNCTION_ID, this.checked); })
            .appendTo($toggle);
        $("<span>").addClass("ra-auth-toggle-slider").appendTo($toggle);
        $toggle.appendTo($card);

        $grid.append($card);
    });

    var allChecked = functions.every(function (f) { return gAuthCheckedFunctions.has(f.FUNCTION_ID); });
    $("#authCheckAllPermission").prop("checked", allChecked);
}

function authToggleFunction(functionId, on) {
    if (on) {
        gAuthCheckedFunctions.add(functionId);
        var func = gAuthAllFunction.find(function (f) { return f.FUNCTION_ID === functionId; });
        if (func) {
            gAuthCheckedMenus.add(func.MENU_ID);
            var subMenu = gAuthAllMenu.find(function (m) { return m.MENU_ID === func.MENU_ID; });
            if (subMenu) { gAuthCheckedMenus.add(subMenu.PARENT_ID); }
        }
    } else {
        gAuthCheckedFunctions.delete(functionId);
        var func2 = gAuthAllFunction.find(function (f) { return f.FUNCTION_ID === functionId; });
        if (func2) {
            var siblingChecked = authGetFunctionsForMenu(func2.MENU_ID).some(function (f) { return gAuthCheckedFunctions.has(f.FUNCTION_ID); });
            if (!siblingChecked) {
                gAuthCheckedMenus.delete(func2.MENU_ID);
                var subMenu2 = gAuthAllMenu.find(function (m) { return m.MENU_ID === func2.MENU_ID; });
                if (subMenu2) {
                    var topStillActive = authGetSubMenus(subMenu2.PARENT_ID).some(function (m) { return gAuthCheckedMenus.has(m.MENU_ID); });
                    if (!topStillActive) { gAuthCheckedMenus.delete(subMenu2.PARENT_ID); }
                }
            }
        }
    }
    renderAuthModal();
}

function authToggleSubMenu(menuId, on) {
    var functions = authGetFunctionsForMenu(menuId);
    if (on) {
        gAuthCheckedMenus.add(menuId);
        functions.forEach(function (f) { gAuthCheckedFunctions.add(f.FUNCTION_ID); });
        var subMenu = gAuthAllMenu.find(function (m) { return m.MENU_ID === menuId; });
        if (subMenu) { gAuthCheckedMenus.add(subMenu.PARENT_ID); }
    } else {
        gAuthCheckedMenus.delete(menuId);
        functions.forEach(function (f) { gAuthCheckedFunctions.delete(f.FUNCTION_ID); });
        var subMenu2 = gAuthAllMenu.find(function (m) { return m.MENU_ID === menuId; });
        if (subMenu2) {
            var topStillActive = authGetSubMenus(subMenu2.PARENT_ID).some(function (m) { return gAuthCheckedMenus.has(m.MENU_ID); });
            if (!topStillActive) { gAuthCheckedMenus.delete(subMenu2.PARENT_ID); }
        }
    }
    renderAuthModal();
}

function authToggleTopMenu(topId, on) {
    var subMenus = authGetSubMenus(topId);
    if (on) {
        gAuthCheckedMenus.add(topId);
        subMenus.forEach(function (s) {
            gAuthCheckedMenus.add(s.MENU_ID);
            authGetFunctionsForMenu(s.MENU_ID).forEach(function (f) { gAuthCheckedFunctions.add(f.FUNCTION_ID); });
        });
    } else {
        gAuthCheckedMenus.delete(topId);
        subMenus.forEach(function (s) {
            gAuthCheckedMenus.delete(s.MENU_ID);
            authGetFunctionsForMenu(s.MENU_ID).forEach(function (f) { gAuthCheckedFunctions.delete(f.FUNCTION_ID); });
        });
    }
    renderAuthModal();
}

$(document).on("input", "#authSearchMenu", function () { renderAuthMenuColumn(); });
$(document).on("input", "#authSearchSubMenu", function () { renderAuthSubMenuColumn(); });
$(document).on("input", "#authSearchPermission", function () { renderAuthPermissionColumn(); });

$(document).on("change", "#authCheckAllPermission", function () {
    var on = this.checked;
    authGetFunctionsForMenu(gAuthSelectedSubMenu).forEach(function (f) {
        if (on) { gAuthCheckedFunctions.add(f.FUNCTION_ID); } else { gAuthCheckedFunctions.delete(f.FUNCTION_ID); }
    });
    if (on) {
        gAuthCheckedMenus.add(gAuthSelectedSubMenu);
        var subMenu = gAuthAllMenu.find(function (m) { return m.MENU_ID === gAuthSelectedSubMenu; });
        if (subMenu) { gAuthCheckedMenus.add(subMenu.PARENT_ID); }
    } else {
        gAuthCheckedMenus.delete(gAuthSelectedSubMenu);
        var subMenu2 = gAuthAllMenu.find(function (m) { return m.MENU_ID === gAuthSelectedSubMenu; });
        if (subMenu2) {
            var topStillActive = authGetSubMenus(subMenu2.PARENT_ID).some(function (m) { return gAuthCheckedMenus.has(m.MENU_ID); });
            if (!topStillActive) { gAuthCheckedMenus.delete(subMenu2.PARENT_ID); }
        }
    }
    renderAuthModal();
});

$(document).on("click", "#btn_auth_clearall", function () {
    showWarningConfirm("Are you sure you want to clear all menus and permissions for this role?", function () {
        gAuthCheckedMenus.clear();
        gAuthCheckedFunctions.clear();
        renderAuthModal();
    }, "Clear");
});

function confirmAddEditAuthorization() {
    showConfirmMesg("Are you sure you want to continue process?", addEditAuthorization, "Confirmation");
}

function addEditAuthorization() {
    var dataMenu = [];
    var dataFunc = [];

    if (gAuthCheckedMenus.size > 0) {
        gAuthCheckedMenus.forEach(function (id) { dataMenu.push({ ROLE_ID: gId, MENU_ID: id }); });
    } else {
        dataMenu.push({ ROLE_ID: gId });
    }

    if (gAuthCheckedFunctions.size > 0) {
        gAuthCheckedFunctions.forEach(function (id) { dataFunc.push({ ROLE_ID: gId, FUNCTION_ID: id }); });
    } else {
        dataFunc.push({ ROLE_ID: gId });
    }

    popUpProgressShow();
    saveAuthMenu(dataMenu);
    saveAuthFunction(dataFunc);
}

function saveAuthMenu(data){
    $.ajax({
        ContentType: 'application/json; charset=utf-8',
        dataType: 'json',
        type: "POST",
        url: roleConfig.urls.addEditMenu,
        data: { data: data },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);

                $("#modalAuth").modal('hide');
                onSearch();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function saveAuthFunction(data){
    $.ajax({
        ContentType: 'application/json; charset=utf-8',
        dataType: 'json',
        type: "POST",
        url: roleConfig.urls.addEditFunction,
        data: { data: data },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}
