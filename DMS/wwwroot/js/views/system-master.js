// MSystem. Server values come from systemMasterConfig in _Javascript.cshtml.
var gCanEdit = systemMasterConfig.canEdit;
var gCanDelete = systemMasterConfig.canDelete;

function updateActionButtons() {
    var selected = getSelectedRowData() != null;

    $("#btn_edit").prop("disabled", !selected);
    $("#btn_delete").prop("disabled", !selected);
}

function msRenderStats(data) {
    var total = data.length;
    var active = data.filter(function (r) { return r.STATUS == 1; }).length;
    var inactive = total - active;
    var types = new Set(data.map(function (r) { return r.SYSTEM_TYPE; })).size;

    $("#msStatTotal").text(total);
    $("#msStatActive").text(active);
    $("#msStatInactive").text(inactive);
    $("#msStatTypes").text(types);
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: ["SYSTEM_TYPE", "SYSTEM_CODE"],
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: systemMasterConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                msRenderStats(data);
                d.resolve(data);
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
        searchPanel: { visible: false, highlightCaseSensitive: false },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
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
            { dataField: "SYSTEM_TYPE", caption: "System Type", fixed: true, fixedPosition: "left" },
            { dataField: "SYSTEM_CODE", caption: "System Code" },
            { dataField: "SYSTEM_VALUE", caption: "System Value" },
            {
                dataField: "STATUS",
                caption: "Status",
                alignment: "center",
                width: 130,
                calculateCellValue: function (row) {
                    return row.STATUS == 1 ? "Active" : "Inactive";
                },
                lookup: {
                    dataSource: [{ v: "Active" }, { v: "Inactive" }],
                    valueExpr: "v",
                    displayExpr: "v"
                },
                cellTemplate: function (container, options) {
                    var active = options.data.STATUS == 1;
                    var $cell = $("<div>").addClass("ms-status-cell");
                    $("<span>").addClass("ms-status-dot").toggleClass("inactive", !active).appendTo($cell);
                    $("<span>").text(active ? "Active" : "Inactive").appendTo($cell);
                    $cell.appendTo(container);
                }
            },
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
    }));

    bindGridToolbar("columns filters reset refresh resize");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();
    var grid = $("#datagrid").dxDataGrid("instance");
    var key = { SYSTEM_TYPE: row.SYSTEM_TYPE, SYSTEM_CODE: row.SYSTEM_CODE };

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([key], false);
            onEditPrepare();
        }));
    }

    if (gCanDelete) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", function () {
            grid.selectRows([key], false);
            onDeletePrepare();
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

    $("#btn_edit").on("click", function () {
        onEditPrepare();
    });

    $("#btn_delete").on("click", function () {
        onDeletePrepare();
    });

    $("#btn_save").on("click", function () {
        confirmAddEditData();
    });

    //$("#btn_save_permission").on("click", function () {
    //    confirmAddEditPermission();
    //});

    $("#btn_export").on("click", function () {
        window.location.href = systemMasterConfig.urls.downloadExcel;
    });

    bindGridToolbar("stats");

});

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New System Parameter");
    $('#addEditSystemType').val("");
    $('#addEditSystemValue').val("");
    $('#addEditSystemCode').val("");
    $('#addEditSystemStatus').val("1").trigger("change");

    $('#addEditSystemType').prop('disabled', false);
    $('#addEditSystemValue').prop('disabled', false);
    $('#addEditSystemCode').prop('disabled', false);
    $('#addEditSystemStatus').prop('disabled', false);

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditSystemType').focus();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gSystemType = row.SYSTEM_TYPE;
    gSystemCode = row.SYSTEM_CODE;

    $.ajax({
        type: "POST",
        url: systemMasterConfig.urls.getByKey,
        async: false,
        data: {
            SYSTEM_TYPE: gSystemType,
            SYSTEM_CODE: gSystemCode,
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;

                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit System Parameter");
                $('#addEditSystemType').val(data.SYSTEM_TYPE);
                $('#addEditSystemValue').val(data.SYSTEM_VALUE);
                $('#addEditSystemCode').val(data.SYSTEM_CODE);
                $('#addEditSystemStatus').val(data.STATUS).trigger("change");

                $('#addEditSystemType').prop('disabled', true);
                $('#addEditSystemValue').prop('disabled', false);
                $('#addEditSystemCode').prop('disabled', true);
                $('#addEditSystemStatus').prop('disabled', false);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function () {
                    $('#addEditSystemValue').focus();
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

function onDeletePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gSystemType = row.SYSTEM_TYPE;
    gSystemCode = row.SYSTEM_CODE;

    showDangerConfirm(deleteMessage("System Parameter", row.SYSTEM_TYPE + " / " + row.SYSTEM_CODE), deleteData, "Delete");
}

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

function addEditData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: systemMasterConfig.urls.addEdit,
        dataType: 'json',
        traditional: true,
        data: {
            screenMode: gScreenMode,
            SYSTEM_TYPE: $('#addEditSystemType').val(),
            SYSTEM_VALUE: $('#addEditSystemValue').val(),
            SYSTEM_CODE: $('#addEditSystemCode').val(),
            STATUS: $('#addEditSystemStatus').val(),
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
        url: systemMasterConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            SYSTEM_TYPE: gSystemType,
            SYSTEM_CODE: gSystemCode,
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
