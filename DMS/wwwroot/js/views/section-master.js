// SectionMaster. Server values come from sectionMasterConfig in _Javascript.cshtml.
var gCanEdit = sectionMasterConfig.canEdit;
var gCanDelete = sectionMasterConfig.canDelete;

function updateActionButtons() {
    var count = getSelectedRowsData().length;

    $("#btn_edit").prop("disabled", count != 1);
    syncMasterStatusButton(getSelectedRowsData());
}

function smRenderStats(data) {
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

    $("#smStatTotal").text(total);
    $("#smStatActive").text(active);
    $("#smStatInactive").text(inactive);
    $("#smStatLastUpdated").html(lastUpdated != null
        ? moment(lastUpdated).format("DD MMM YYYY") + "<br><span style='font-size:11px;font-weight:600;color:#66788A;'>" + moment(lastUpdated).format("HH:mm") + "</span>"
        : "-");
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "SECTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: sectionMasterConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false,
                    showAll: true
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                smRenderStats(data);
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
            { dataField: "SECTION_CODE", caption: "Section", fixed: true, fixedPosition: "left" },
            { dataField: "SECTION_NAME", caption: "Section Name" },
            { dataField: "DEPARTMENT_CODE", caption: "Department" },
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
                    var $cell = $("<div>").addClass("sm-status-cell");
                    $("<span>").addClass("sm-status-dot").toggleClass("inactive", inactive).appendTo($cell);
                    $("<span>").text(inactive ? "Inactive" : "Active").appendTo($cell);
                    $cell.appendTo(container);
                }
            },
            {
                dataField: "VALID_FROM",
                caption: "Valid From",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.VALID_FROM;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                dataField: "VALID_TO",
                caption: "Valid To",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.VALID_TO;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
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

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([row.SECTION_ID], false);
            onEditPrepare();
        }));
    }

    if (gCanDelete) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-edit", "fa-power-off", row.DELETE_FLAG == 1 ? "Activate" : "Deactivate", function () {
            grid.selectRows([row.SECTION_ID], false);
            onDeletePrepare();
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gScreenMode = null;
var gDocumentId = null;
var gTemplate = null;
var selectedIds = [];

$(document).ready(function() {
    setDataSelect2WithParam("addEditDivision", sectionMasterConfig.urls.systemValues, "Select division", "DIVISION");

    $('#addEditValidFrom').datepicker({
        autoclose: true,
        todayHighlight: true,
        format: "dd-mm-yyyy",
    });

    $('#addEditValidTo').datepicker({
        autoclose: true,
        todayHighlight: true,
        format: "dd-mm-yyyy",
    });

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

    $("#btn_delete_template").on("click", function() {
        confirmDeleteAttachment()
    });

    $("#btn_reset_form").on("click", function() {
        resetForm();
    });

    $("#btn_export").on("click", function () {
        window.location.href = sectionMasterConfig.urls.downloadExcel;
    });

    bindGridToolbar("stats");

});

var gLastEditData = null;

function onAddPrepare() {
    gScreenMode = 'ADD';
    gLastEditData = null;
    $('#modal-title').html("New Section");
    $('#modal-subtitle').html("Add a new section to the system");
    resetFormFields();

    $('#addEditSectionCode').prop('disabled', false);
    $('#addEditSectionName').prop('disabled', false);
    $('#addEditDivision').prop('disabled', false);
    $('#addEditDepartmentCode').prop('disabled', true);

    gTemplate = null;

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $('#addEditSectionCode').focus();
    });
}

// Reset ke ADD kosong atau EDIT ke data terakhir di-load, dipakai onAddPrepare
// dan tombol Reset di footer (request Hendra 2026-08-19 - form ini sekarang
// punya lebih banyak field jadi berguna utk batalkan perubahan tanpa tutup modal).
function resetFormFields() {
    $('#addEditSectionCode').val("");
    $('#addEditSectionName').val("");
    $('#addEditDivision').val("").trigger("change");
    $('#addEditDepartmentCode').val("").trigger("change");
    $('#addEditValidFrom').val("");
    $('#addEditValidTo').val("");
}

function resetForm() {
    if (gScreenMode === 'EDIT' && gLastEditData != null) {
        populateFormFields(gLastEditData);
    } else {
        resetFormFields();
        $('#addEditDepartmentCode').prop('disabled', true);
    }
}

function populateFormFields(data) {
    $('#addEditSectionId').val(data.SECTION_ID);
    $('#addEditSectionCode').val(data.SECTION_CODE);
    $('#addEditSectionName').val(data.SECTION_NAME);
    $('#addEditDivision').select2("trigger", "select", { data: { id: data.DIVISION, text: data.DIVISION } });
    $('#addEditDepartmentCode').select2("trigger", "select", { data: { id: data.DEPARTMENT_CODE, text: data.DEPARTMENT_CODE } });

    $('#addEditValidFrom').val(moment(data.VALID_FROM).format("DD-MM-YYYY"));
    $('#addEditValidTo').val(moment(data.VALID_TO).format("DD-MM-YYYY"));
}
function addEditData() {
    popUpProgressShow();

    // Field kosong = berlaku tanpa batas waktu (request Hendra 2026-08-19) -
    // sp_SectionMaster_Insert/Update tetap wajib VALID_TO terisi, jadi
    // di-default ke tanggal sentinel "selamanya" di sini kalau dikosongkan.
    var validTo = $('#addEditValidTo').val();
    if (!validTo) {
        validTo = "31-12-9999";
    }

    $.ajax({
        type: "POST",
        url: sectionMasterConfig.urls.addEdit,
        dataType: 'json',
        traditional: true,
        data: {
            screenMode: gScreenMode,
            SECTION_ID  : $('#addEditSectionId').val(),
            SECTION_CODE: $('#addEditSectionCode').val(),
            SECTION_NAME: $('#addEditSectionName').val(),
            DEPARTMENT_CODE: $('#addEditDepartmentCode').val(),
            VALID_FROM: moment($('#addEditValidFrom').val(), "DD-MM-YYYY").format("YYYY-MM-DD"),
            VALID_TO: moment(validTo, "DD-MM-YYYY").format("YYYY-MM-DD"),
        },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                $("#modalAddEdit").modal('hide');
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

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentId = row.SECTION_ID;

    $.ajax({
        type: "POST",
        url: sectionMasterConfig.urls.getByKey,
        async: false,
        data: {
            SECTION_ID: gDocumentId
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;
                gLastEditData = data;

                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit Section");
                $('#modal-subtitle').html("Update section data");
                populateFormFields(data);

                $('#addEditSectionCode').prop('disabled', true);
                $('#addEditValidFrom').prop('disabled', false);
                $('#addEditValidTo').prop('disabled', false);
                $('#addEditSectionName').prop('disabled', false);
                $('#addEditDivision').prop('disabled', false);
                $('#addEditDepartmentCode').prop('disabled', false);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#addEditSectionName').focus();
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onDeletePrepare() {
    selectedIds = getSelectedRowsData().map(function (row) {
        return row.SECTION_ID;
    });

    if (selectedIds.length < 1) {
        return;
    }

    confirmMasterStatus(getSelectedRowsData(), {
        entity: "SECTION", idField: "SECTION_ID", nameField: "SECTION_NAME", label: "Section", plural: "sections",
        activateUrl: sectionMasterConfig.urls.activate, onDeactivate: deleteData
    });
}

function confirmAddEditData() {
    if (!$('#addEditSectionCode').val() || !$('#addEditSectionName').val() ||
        !$('#addEditDivision').val() || !$('#addEditDepartmentCode').val() ||
        !$('#addEditValidFrom').val()) {
        showErrorMesgGrowl("Please fill in all required fields.");
        return;
    }

    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

$(document).on('change', '#template', function(e) {
    ex = e;
});

function deleteData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: sectionMasterConfig.urls.deleteMultiple,
        dataType: 'json',
        traditional: true,
        data: {
            selectedIds: selectedIds
        },
        success: function(result) {
            //popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
            } else {
                showErrorMesgGrowl(result.message);
                popUpProgressHide();
            }
        },
        error: function(result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}

 function setDepartmentCode(val) {
    gModelCode  = val;

    $('#addEditDepartmentCode').val("").trigger("change");
    $('#addEditDepartmentCode').prop('disabled', false);
    setDataSelect2WithParam("addEditDepartmentCode", sectionMasterConfig.urls.departmentsByDivision, "Select department", gModelCode);
}
