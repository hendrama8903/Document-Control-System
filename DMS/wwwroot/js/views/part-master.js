// PartMaster. Server values come from partMasterConfig in _Javascript.cshtml.
function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);
    $("#btn_delete").prop("disabled", !selected);
    $("#btn_download").prop("disabled", !(selected && row.ATTACHMENT_PATH != null));
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);
    $("#btn_download").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "PART_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: partMasterConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                d.resolve(result.data == "" ? [] : result.data);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        filterRow: { visible: true, applyFilter: "auto" },
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
            { dataField: "PART_NO", caption: "Part No", fixed: true, fixedPosition: "left" },
            { dataField: "COLOR_SUFFIX", caption: "Color SFX", alignment: "center" },
            { dataField: "MIN_CHANGE", caption: "Min", alignment: "center" },
            { dataField: "PART_NAME", caption: "Part Name" },
            { dataField: "PART_SOURCE_NAME", caption: "Source" },
            { dataField: "HSCODE", caption: "HS Code" },
            {
                caption: "Status",
                alignment: "center",
                calculateCellValue: function (row) {
                    return row.STATUS == 0 ? "Inactive" : "Active";
                },
                cellTemplate: function (container, options) {
                    var inactive = options.data.STATUS == 0;

                    $("<span>")
                        .addClass(inactive ? "label label-danger" : "label label-success")
                        .text(inactive ? "Inactive" : "Active")
                        .appendTo(container);
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
            }
        ]
    }));

    bindGridToolbar("columns resize");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

var gScreenMode = null;
var gPartId = null;
var gAttachmentPath = null;

$(document).ready(function() {
    $("#addEditPartNetWeight").on("keyup change", function (e) {
        this.value = formatMoney(this.value);
    });

    setDataSelect2WithParam("addEditPartCategory", partMasterConfig.urls.systemValues, "- Choose -", "PART_CATEGORY");
    setDataSelect2WithParam("addEditPartType", partMasterConfig.urls.systemValues, "- Choose -", "PART_TYPE");
    setDataSelect2WithParam("addEditPartUnit", partMasterConfig.urls.systemValues, "- Choose -", "PART_UNIT");
    setDataOldPartNo();

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
        setDataOldPartNo();
    });

    $("#btn_delete").on("click", function() {
        onDeletePrepare();
    });

    $("#btn_save").on("click", function() {
        confirmAddEditData();
    });

    $("#btn_delete_attachment").on("click", function() {
        confirmDeleteAttachment()
    });
});

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Part");
    $('#addEditOldPartNo').val("");
    $('#addEditPartNo').val("");
    $('#addEditPartNoSfx').val("");
    $('#addEditPartNoMin').val("");
    $('#addEditPartName').val("");
    $('#addEditPartCategory').val("").trigger("change");
    $('#addEditPartType').val("").trigger("change");
    $('#addEditPartUnit').val("").trigger("change");
    //$('#addEditPartNetWeight').val("");
    $('#addEditPartNetWeight').val(0);
    $('#addEditOldPartNo').val("").trigger("change");
    $('#addEditValidFrom').val("");
    $('#addEditValidTo').datepicker("setDate", new Date(9999, 11, 31));
    $('#inputContainer :input[type="text"]').val("");

    $('#addEditOldPartNo').prop('disabled', false);
    $('#addEditPartNo').prop('disabled', false);
    $('#addEditPartNoSfx').prop('disabled', false);
    $('#addEditPartNoMin').prop('disabled', false);
    $('#addEditPartName').prop('disabled', false);
    $('#addEditPartCategory').prop('disabled', false);
    $('#addEditPartType').prop('disabled', false);
    $('#addEditValidFrom').prop('disabled', false);
    $('#addEditValidTo').prop('disabled', false);

    $('#fileNameContainer').removeClass();
    $('#fileNameContainer').addClass('col-sm-8');
    $('#btn_delete_attachment').hide();

    gAttachmentPath = null;

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $('#addEditOldPartNo').focus();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gPartId = row.PART_ID;

    $.ajax({
        type: "POST",
        url: partMasterConfig.urls.getByKey,
        async: false,
        data: {
            PART_ID: gPartId
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;
                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit Part");
                $('#addEditPartNo').val(data.PART_NO);
                $('#addEditPartNoSfx').val(data.COLOR_SUFFIX);
                $('#addEditPartNoMin').val(data.MIN_CHANGE);
                $('#addEditPartName').val(data.PART_NAME);
                $('#addEditPartCategory').select2("trigger", "select", { data: { id: data.PART_CATEGORY, text: data.PART_CATEGORY_NAME } });
                $('#addEditPartType').select2("trigger", "select", { data: { id: data.PART_TYPE, text: data.PART_TYPE + ' - ' + data.PART_TYPE_NAME } });
                $('#addEditPartUnit').select2("trigger", "select", { data: { id: data.PART_UNIT, text: data.PART_UNIT_NAME } });
                $('#addEditPartNetWeight').val(data.PART_NETWEIGHT);
                if (data.OLD_PART_NO != null) {
                    $('#addEditOldPartNo').select2("trigger", "select", { data: { id: data.OLD_PART_NO, text: data.OLD_PART_NO } });
                } else {
                    $('#addEditOldPartNo').val("").trigger("change");
                }
                $('#addEditValidFrom').val(moment(data.VALID_FROM).format("DD-MM-YYYY"));
                $('#addEditValidTo').val(moment(data.VALID_TO).format("DD-MM-YYYY"));
                gAttachmentPath = data.ATTACHMENT_PATH;
                $('#inputContainer :input[type="text"]').val(data.ATTACHMENT_PATH);
                if (data.ATTACHMENT_PATH == null) {
                    $('#fileNameContainer').removeClass();
                    $('#fileNameContainer').addClass('col-sm-8');
                    $('#btn_delete_attachment').hide();
                } else {
                    if (partMasterConfig.deleteAttachmentFlag == "False"){
                        $('#fileNameContainer').removeClass();
                        $('#fileNameContainer').addClass('col-sm-8');
                        $('#btn_delete_attachment').hide();
                    } else {
                        $('#fileNameContainer').removeClass();
                        $('#fileNameContainer').addClass('col-sm-7');
                        $('#btn_delete_attachment').show();
                    }
                }

                $("#addEditPartNo").prop("disabled", true);
                $("#addEditPartNoSfx").prop("disabled", true);
                $("#addEditPartNoMin").prop("disabled", true);
                $("#addEditOldPartNo").prop("disabled", false);
                $("#addEditValidFrom").prop("disabled", true);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#addEditPartName').focus();
                });

                $('#addEditPartNetWeight').val(formatMoney($("#addEditPartNetWeight").val()));
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
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gPartId = row.PART_ID;
    gAttachmentPath = row.ATTACHMENT_PATH;

    showDangerConfirm(deleteMessage("Part", row.PART_NO), deleteData, "Delete");
}

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

$(document).on('change', '#attachmentPath', function(e) {
    ex = e;
});

function addEditData() {
    popUpProgressShow();
    var data = new FormData();
    var oldPartNo = $('#addEditOldPartNo').val();
    let netWeight = $('#addEditPartNetWeight').val().replaceAll(',', '');
    var category = $('#addEditPartCategory').val();
    var type = $('#addEditPartType').val();
    var unit = $('#addEditPartUnit').val();

    data.append("screenMode", gScreenMode);
    data.append("PART_ID", gPartId);
    data.append("PART_NO", $('#addEditPartNo').val());
    data.append("COLOR_SUFFIX", $('#addEditPartNoSfx').val());
    data.append("MIN_CHANGE", $('#addEditPartNoMin').val());
    data.append("PART_NAME", $('#addEditPartName').val());
    if (category == null || category == "null") {
        data.append("PART_CATEGORY", "");
    } else {
        data.append("PART_CATEGORY", $('#addEditPartCategory').val());
    }
    if (type == null || type == "null") {
        data.append("PART_TYPE", "");
    } else {
        data.append("PART_TYPE", $('#addEditPartType').val());
    }
    if (unit == null || unit == "null") {
        data.append("PART_UNIT", "");
    } else {
        data.append("PART_UNIT", $('#addEditPartUnit').val());
    }
    if (netWeight == null || netWeight == "") {
        data.append("PART_NETWEIGHT", 0);
    } else {
        data.append("PART_NETWEIGHT", netWeight); //For AWS Ezio
        //data.append("PART_NETWEIGHT", netWeight.replace('.', ',') ); //For Local
    }
    if (oldPartNo == null || oldPartNo == "null") {
        data.append("OLD_PART_NO", "");
    } else {
        data.append("OLD_PART_NO", $('#addEditOldPartNo').val());
    }
    data.append("VALID_FROM", moment($('#addEditValidFrom').val(), "DD-MM-YYYY").format("YYYY-MM-DD"));
    data.append("VALID_TO", moment($('#addEditValidTo').val(), "DD-MM-YYYY").format("YYYY-MM-DD"));

    if (gAttachmentPath !== null) {
        data.append("ATTACHMENT_PATH", gAttachmentPath);
    }

    if ($('#attachmentPath').val() != '') {
        e = ex;
        var files = e.target.files;
        if (files.length > 0) {
            if (window.FormData !== undefined)
                for (var x = 0; x < files.length; x++) {
                    data.append("file" + x, files[x]);
                }
        }
    }

    $.ajax({
        type: "POST",
        url: partMasterConfig.urls.addEdit,
        contentType: false,
        processData: false,
        data: data,
        success: function(result) {
            //popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
                $("#modalAddEdit").modal('hide');
            } else {
                showErrorMesgGrowl(result.message);
                popUpProgressHide();
            }
        },
        error: function(result) {
            showErrorMesgGrowl(result);
            popUpProgressHide();

        }
    });
}

function deleteData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: partMasterConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            PART_ID: gPartId,
            ATTACHMENT_PATH: gAttachmentPath
            //path: gAttachmentPath
        },
        success: function(result) {
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
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}

function setDataOldPartNo() {
    var partNo = $("#addEditPartNo").val();
    setDataSelect2WithParam("addEditOldPartNo", partMasterConfig.urls.getListOldPartNo, "- Choose -", partNo);
}

function onDownload() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gAttachmentPath = row.ATTACHMENT_PATH;

    downloadAttachment();
}

function downloadAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = partMasterConfig.urls.downloadAttachment;
        link = link.replace("-1", gAttachmentPath);
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function confirmDeleteAttachment() {
    showDangerConfirm("Delete this attachment? The file will be removed from the record. This cannot be undone.", deleteAttachment, "Delete");
}

function deleteAttachment() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: partMasterConfig.urls.deleteAttachment,
        dataType: 'json',
        data: {
            PART_ID: gPartId,
            ATTACHMENT_PATH: gAttachmentPath
            //path: gAttachmentPath
        },
        success: function(result) {
            if (result.status != false) {
                $('#inputContainer :input[type="text"]').val("");
                $('#fileNameContainer').removeClass();
                $('#fileNameContainer').addClass('col-sm-8');
                $('#btn_delete_attachment').hide();

                gAttachmentPath = null;
                $(":file").filestyle('clear');

                onSearch();
                showSuccessMesgGrowl(result.message);
                popUpProgressHide();
            } else {
                showErrorMesgGrowl(result.message);
                popUpProgressHide();
            }
        },
        error: function(result) {
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}
