// MSequence. Server values come from sequenceMasterConfig in _Javascript.cshtml.
var gCanEdit = sequenceMasterConfig.canEdit;
var gCanReset = sequenceMasterConfig.canReset;

function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);
    $("#btn_reset").prop("disabled", !selected);
}

function msqRenderStats(data) {
    var total = data.length;
    var types = new Set(data.map(function (r) { return r.SEQ_TYPE; })).size;
    var highest = data.reduce(function (max, r) { return Math.max(max, parseInt(r.SEQ_NO) || 0); }, 0);

    var lastUpdated = null;
    data.forEach(function (r) {
        var dt = r.CHANGED_DT || r.CREATED_DT;
        if (dt != null && (lastUpdated == null || moment(dt).isAfter(lastUpdated))) {
            lastUpdated = dt;
        }
    });

    $("#msqStatTotal").text(total);
    $("#msqStatTypes").text(types);
    $("#msqStatHighest").text(highest);
    $("#msqStatLastUpdated").html(lastUpdated != null
        ? moment(lastUpdated).format("DD MMM YYYY") + "<br><span style='font-size:11px;font-weight:600;color:#66788A;'>" + moment(lastUpdated).format("HH:mm") + "</span>"
        : "-");
}

$(document).ready(function () {
    $("#btn_edit").prop("disabled", true);
    $("#btn_reset").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: ["SEQ_TYPE", "SEQ_CODE"],
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: sequenceMasterConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                msqRenderStats(data);
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
            { dataField: "SEQ_CODE", caption: "Sequence Code (Document Prefix)", fixed: true, fixedPosition: "left" },
            {
                dataField: "SEQ_TYPE",
                caption: "Type",
                alignment: "center",
                width: 100,
                cellTemplate: function (container, options) {
                    $("<span>").addClass("msq-type-pill").text(options.data.SEQ_TYPE).appendTo(container);
                }
            },
            { dataField: "SEQ_NO", caption: "Last Number Used", alignment: "center", width: 140 },
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
            { dataField: "CHANGED_BY", caption: "Changed By" },
            {
                dataField: "CHANGED_DT",
                caption: "Changed Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY HH:mm") : "");
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
    var key = { SEQ_TYPE: row.SEQ_TYPE, SEQ_CODE: row.SEQ_CODE };

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([key], false);
            onEditPrepare();
        }));
    }

    if (gCanReset) {
        $menu.append(menuItem("dp-menu-history", "fa-undo", "Reset counter", function () {
            grid.selectRows([key], false);
            onResetPrepare();
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gSeqType = null;
var gSeqCode = null;

var gResetSeqType = null;
var gResetSeqCode = null;
var gResetCurrentSeqNo = null;

$(document).ready(function() {
    $("#btn_edit").on("click", function() {
        onEditPrepare();
    });

    $("#btn_save").on("click", function() {
        confirmEditData();
    });

    $("#btn_reset").on("click", function() {
        onResetPrepare();
    });

    $("#btn_reset_save").on("click", function() {
        confirmResetData();
    });

    $("#resetConfirmText").on("input", function() {
        $("#btn_reset_save").prop("disabled", $(this).val() !== "RESET");
    });

    $("#modalReset").on("hidden.bs.modal", function() {
        $("#resetConfirmText").val("");
        $("#btn_reset_save").prop("disabled", true);
    });

    $("#btn_export").on("click", function () {
        window.location.href = sequenceMasterConfig.urls.downloadExcel;
    });

    bindGridToolbar("stats");

});

function onEditPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gSeqType = row.SEQ_TYPE;
    gSeqCode = row.SEQ_CODE;

    $.ajax({
        type: "POST",
        url: sequenceMasterConfig.urls.getByKey,
        async: false,
        data: {
            SEQ_TYPE: gSeqType,
            SEQ_CODE: gSeqCode
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;

                $('#modal-title').html("Edit Document Numbering");
                $('#editSeqCode').val(data.SEQ_CODE);
                $('#editSeqType').val(data.SEQ_TYPE);
                $('#editSeqNo').val(data.SEQ_NO).attr("min", data.SEQ_NO);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#editSeqNo').focus();
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

function confirmEditData() {
    showConfirmMesg("Are you sure you want to continue process?", editData, "Confirmation");
}

function editData() {
    popUpProgressShow();

    $.ajax({
        type: "POST",
        url: sequenceMasterConfig.urls.update,
        dataType: 'json',
        data: {
            OLD_SEQ_TYPE: gSeqType,
            OLD_SEQ_CODE: gSeqCode,
            SEQ_TYPE: $('#editSeqType').val(),
            SEQ_CODE: $('#editSeqCode').val(),
            SEQ_NO: $('#editSeqNo').val()
        },
        success: function(result) {
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

function onResetPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gResetSeqType = row.SEQ_TYPE;
    gResetSeqCode = row.SEQ_CODE;

    $.ajax({
        type: "POST",
        url: sequenceMasterConfig.urls.getByKey,
        async: false,
        data: {
            SEQ_TYPE: gResetSeqType,
            SEQ_CODE: gResetSeqCode
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;
                gResetCurrentSeqNo = data.SEQ_NO;

                $('#resetSeqCode').val(data.SEQ_CODE);
                $('#resetSeqType').val(data.SEQ_TYPE);
                $('#resetSeqNoCurrent').val(data.SEQ_NO);
                $('#resetSeqNoNew').val(data.SEQ_NO);
                $('#resetConfirmText').val("");
                $('#btn_reset_save').prop("disabled", true);

                $("#modalReset").modal();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            showErrorMesgGrowl(result);
        }
    });
}

function confirmResetData() {
    var newSeqNo = $('#resetSeqNoNew').val();
    showWarningConfirm(
        "You are about to force the number \"" + confirmText(gResetSeqCode) + "\" from " + confirmText(gResetCurrentSeqNo) + " to " + confirmText(newSeqNo) +
        ". If this number is lower than the highest number already used, the system may generate a duplicate document code. Continue?",
        resetData,
        "Reset"
    );
}

function resetData() {
    popUpProgressShow();

    $.ajax({
        type: "POST",
        url: sequenceMasterConfig.urls.reset,
        dataType: 'json',
        data: {
            SEQ_TYPE: gResetSeqType,
            SEQ_CODE: gResetSeqCode,
            SEQ_NO: $('#resetSeqNoNew').val()
        },
        success: function(result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
                $("#modalReset").modal('hide');
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
