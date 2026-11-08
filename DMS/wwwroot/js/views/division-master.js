// Division Master. Server values come from divisionMasterConfig in _Javascript.cshtml.
var gCanEdit = divisionMasterConfig.canEdit;
var gCanDelete = divisionMasterConfig.canDelete;

function updateActionButtons() {
    var count = getSelectedRowsData().length;

    $("#btn_edit").prop("disabled", count != 1);
    syncMasterStatusButton(getSelectedRowsData());
}

function dvRenderStats(data) {
    var total = data.length;
    var inactive = data.filter(function (r) { return r.DELETE_FLAG == 1; }).length;
    var active = total - inactive;

    var lastUpdated = null;
    data.forEach(function (r) {
        var dt = r.CHANGED_DT || r.CREATED_DT;
        if (dt != null && (lastUpdated == null || moment(dt).isAfter(lastUpdated))) {
            lastUpdated = dt;
        }
    });

    $("#dvStatTotal").text(total);
    $("#dvStatActive").text(active);
    $("#dvStatInactive").text(inactive);
    $("#dvStatLastUpdated").html(lastUpdated != null
        ? moment(lastUpdated).format("DD MMM YYYY") + "<br><span style='font-size:11px;font-weight:600;color:#66788A;'>" + moment(lastUpdated).format("HH:mm") + "</span>"
        : "-");
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "DIVISION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: divisionMasterConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false,
                    showAll: true
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                dvRenderStats(data);
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
        onSelectionChanged: function () {
            updateActionButtons();
        },
        onRowPrepared: function (e) {
            if (e.rowType == "data" && e.data.VALID_TO != null && moment(e.data.VALID_TO).isBefore(moment())) {
                e.rowElement.addClass("row-danger");
            }
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
            { dataField: "DIVISION_CODE", caption: "Division Code", fixed: true, fixedPosition: "left" },
            { dataField: "DIVISION_NAME", caption: "Division Name" },
            {
                dataField: "DELETE_FLAG",
                caption: "Status",
                alignment: "center",
                width: 130,
                calculateCellValue: function (row) {
                    return row.DELETE_FLAG == 1 ? "Inactive" : "Active";
                },
                lookup: {
                    dataSource: [{ v: "Active" }, { v: "Inactive" }],
                    valueExpr: "v",
                    displayExpr: "v"
                },
                cellTemplate: function (container, options) {
                    var inactive = options.data.DELETE_FLAG == 1;
                    var $cell = $("<div>").addClass("dv-status-cell");
                    $("<span>").addClass("dv-status-dot").toggleClass("inactive", inactive).appendTo($cell);
                    $("<span>").text(inactive ? "Inactive" : "Active").appendTo($cell);
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

    bindGridToolbar("columns filters reset refresh resize stats");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();
    var grid = $("#datagrid").dxDataGrid("instance");

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([row.DIVISION_ID], false);
            onEditPrepare();
        }));
    }

    if (gCanDelete) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-edit", "fa-power-off", row.DELETE_FLAG == 1 ? "Activate" : "Deactivate", function () {
            grid.selectRows([row.DIVISION_ID], false);
            onDeletePrepare();
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gScreenMode = null;
var gId = null;

$(document).ready(function() {
    setDataSelect2WithParam("addEditDivision", divisionMasterConfig.urls.systemValues, "- Choose -", "DIVISION");

    $("#btn_add").on("click", function() {
        onAddPrepare();
    });

    $("#btn_edit").on("click", function() {
        onEditPrepare();
    });

    $("#btn_delete").on("click", function() {
        onDeletePrepare();
    });

    $("#btn_save").on("click", function() {
        confirmAddEditData();
    });

    $("#btn_export").on("click", function () {
        window.location.href = divisionMasterConfig.urls.downloadExcel;
    });

});

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Division");
    $('#modal-subtitle').html("Add new division data");
    $('#addEditDivisionNm').val("");
    $('#addEditDivisionCd').val("");

    $('#addEditDivisionNm').prop('disabled', false);
    $('#addEditDivisionCd').prop('disabled', false);

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $('#addEditDivisionNm').focus();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gId = row.DIVISION_ID;

    $.ajax({
        type: "POST",
        url: divisionMasterConfig.urls.getByKey,
        async: false,
        data: {
            DIVISION_ID: gId
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;
                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit Division");
                $('#modal-subtitle').html("Update division data");
                $('#addEditId').val(data.DIVISION_ID);
                $('#addEditDivisionCd').val(data.DIVISION_CODE);
                $('#addEditDivisionNm').val(data.DIVISION_NAME);

                $("#addEditId").prop("disabled", true);
                $("#addEditDivisionCd").prop("disabled", true);
                $("#addEditDivisionNm").prop("disabled", false);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#addEditDivisionNm').focus();
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            showErrorMesgGrowl(result);
        }
    });
}

function onDeletePrepare() {
    if (getSelectedRowsData().length < 1) {
        return;
    }
    confirmMasterStatus(getSelectedRowsData(), {
        entity: "DIVISION", idField: "DIVISION_ID", nameField: "DIVISION_NAME", label: "Division", plural: "divisions",
        activateUrl: divisionMasterConfig.urls.activate, onDeactivate: deleteData
    });
}

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

 function addEditData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: divisionMasterConfig.urls.addEdit,
        dataType: 'json',
        traditional: true,
        data: {
            screenMode: gScreenMode,
            DIVISION_ID: $('#addEditId').val(),
            DIVISION_CODE: $('#addEditDivisionCd').val(),
            DIVISION_NAME: $('#addEditDivisionNm').val()
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
    var selectedIds = getSelectedRowsData().map(function (row) {
        return row.DIVISION_ID;
    });

    $.ajax({
        type: "POST",
        url: divisionMasterConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            divisionIds: selectedIds // Send array of division IDs to the server
        },
        success: function (result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
            } else {
                showErrorMesgGrowl(result.message);
                popUpProgressHide();
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}
