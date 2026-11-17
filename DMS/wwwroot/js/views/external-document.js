// ExternalDocument. Server values come from externalDocumentConfig in the view.
function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);
    $("#btn_delete").prop("disabled", !selected);
}

// Warna tag External Type konsisten per nilai (bukan master data - free text),
// supaya dokumen dengan tipe sama gampang dikenali sekilas saat listnya panjang.
var externalTypeColorPalette = ["#1976D2", "#7B1FA2", "#C2185B", "#00796B", "#F57C00", "#5D4037", "#455A64", "#689F38", "#0097A7", "#AD1457"];
function colorForExternalType(type) {
    if (!type) return "#9E9E9E";
    var hash = 0;
    for (var i = 0; i < type.length; i++) {
        hash = (hash * 31 + type.charCodeAt(i)) >>> 0;
    }
    return externalTypeColorPalette[hash % externalTypeColorPalette.length];
}
function appendTypeDot(container, type) {
    $("<span>").css({
        display: "inline-block",
        width: "10px",
        height: "10px",
        borderRadius: "50%",
        backgroundColor: colorForExternalType(type),
        marginRight: "6px"
    }).appendTo(container);
}

// ---------- summary panel and Due for Review (2026-11-16) ----------
var gDueForReview = false;

function edReviewState(row) {
    if (!row.NEXT_REVIEW_DATE) return null;
    var next = moment(row.NEXT_REVIEW_DATE).startOf("day"), today = moment().startOf("day");
    if (next.isBefore(today)) return "overdue";
    if (!next.isAfter(moment(today).add(30, "days"))) return "due";
    return null;
}

function edRenderStats(rows) {
    $("#edStatTotal").text(rows.length);
    $("#edStatActive").text(rows.filter(function (r) { return r.STATUS == "1"; }).length);
    $("#edStatDue").text(rows.filter(function (r) { return edReviewState(r) === "due"; }).length);
    $("#edStatOverdue").text(rows.filter(function (r) { return edReviewState(r) === "overdue"; }).length);
}

function edApplyDueFilter() {
    var grid = $("#datagrid").dxDataGrid("instance");
    // overdue documents are due too
    grid.filter(gDueForReview ? [function (r) { return edReviewState(r) !== null; }, "=", true] : null);
    $("#btn_dueForReview").toggleClass("active", gDueForReview);
}

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();
    var grid = $("#datagrid").dxDataGrid("instance");

    if (row.FILE_PATH && externalDocumentConfig.downloadFilePathFlag != "False") {
        $menu.append(menuItem("dp-menu-download", "fa-download", "Download", function () { onDownload(row.FILE_PATH); }));
    }
    if ($("#btn_edit").length) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([row.EXTERNAL_DOCUMENT_ID], false);
            onEditPrepare();
        }));
    }
    if ($("#btn_delete").length) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", function () {
            grid.selectRows([row.EXTERNAL_DOCUMENT_ID], false);
            onDeletePrepare();
        }));
    }
    if ($menu.children().length === 0) return;

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() }).show();
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "EXTERNAL_DOCUMENT_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: externalDocumentConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                edRenderStats(rows);
                d.resolve(rows);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    // Shared grid defaults (toolbar, paging, column chooser, phone layout) like the other
    // list pages. The filter row opens with More Filters; rows stay grouped by External Type.
    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        filterRow: { visible: false, applyFilter: "auto" },
        grouping: { autoExpandAll: true, contextMenuEnabled: true },
        summary: {
            groupItems: [
                { column: "DOCUMENT_NAME", summaryType: "count", displayFormat: "{0} documents" }
            ]
        },
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
            { dataField: "DOCUMENT_NAME", caption: "Document Name", fixed: true, fixedPosition: "left" },
            {
                dataField: "EXTERNAL_TYPE",
                caption: "External Type",
                groupIndex: 0,
                cellTemplate: function (container, options) {
                    appendTypeDot(container, options.value);
                    container.append(document.createTextNode(options.value || ""));
                },
                groupCellTemplate: function (container, options) {
                    appendTypeDot(container, options.value);
                    container.append(document.createTextNode(options.text));
                }
            },
            { dataField: "REVISION_EDITION", caption: "Revision/Edition", alignment: "center" },
            {
                dataField: "RECEIVED_DATE",
                caption: "Received Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.RECEIVED_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "STORAGE_LOCATION", caption: "Storage Location" },
            { dataField: "STORAGE_PIC", caption: "Storage PIC" },
            { dataField: "REVIEW_FREQUENCY", caption: "Review Frequency" },
            { dataField: "REVIEW_SOURCE", caption: "Review Source" },
            {
                dataField: "LAST_CHECK_DATE",
                caption: "Last Check Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.LAST_CHECK_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                dataField: "NEXT_REVIEW_DATE",
                caption: "Next Review Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.NEXT_REVIEW_DATE;
                    if (value == null) return;
                    var state = edReviewState(options.data);
                    var $t = $("<span>").text(moment(value).format("DD-MM-YYYY")).appendTo(container);
                    if (state) $t.addClass("dms-pill " + (state === "overdue" ? "dms-pill-danger" : "dms-pill-warning"))
                        .attr("title", state === "overdue" ? "Review overdue" : "Review due within 30 days");
                }
            },
            {
                dataField: "STATUS_DISPLAY",
                caption: "Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    if (!options.value) return;
                    $("<span>").addClass("dms-pill " + (options.data.STATUS == "1" ? "dms-pill-success" : "dms-pill-default"))
                        .text(options.value).appendTo(container);
                }
            },
            { dataField: "INTERNAL_IMPACT_DISPLAY", caption: "Internal Impact" },
            {
                caption: "Impacted Document",
                calculateCellValue: function (data) {
                    if (!data.IMPACTED_DOCUMENT_CODE) return "";
                    return data.IMPACTED_DOCUMENT_CODE + " - " + data.IMPACTED_DOCUMENT_NAME;
                }
            },
            { dataField: "DIVISION_NAME", caption: "Division", visible: false },
            { dataField: "DEPARTMENT_NAME", caption: "Department", visible: false },
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

    bindGridToolbar("columns filters refresh resize stats");

    $("#btn_dueForReview").on("click", function () {
        gDueForReview = !gDueForReview;
        edApplyDueFilter();
    });

    // Reset also turns Due for Review off
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        gDueForReview = false;
        grid.clearFilter();
        grid.clearSelection();
        edApplyDueFilter();
    });

    $("#btn_export").on("click", function () {
        window.location.href = externalDocumentConfig.urls.downloadExcel;
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

var gScreenMode = null;
var gExternalDocumentId = null;
var gFilePath = null;

$(document).ready(function() {
    setDataSelect2WithParam("addEditStatus", externalDocumentConfig.urls.systemValues, "- Choose -", "EXT_DOC_STATUS");
    setDataSelect2WithParam("addEditInternalImpact", externalDocumentConfig.urls.systemValues, "- Choose -", "EXT_DOC_IMPACT");
    setDataSelect2("addEditImpactedDocument", externalDocumentConfig.urls.getImpactedDocument, "- Choose -");

    $("#addEditInternalImpact").on("change", function() {
        toggleImpactedDocument();
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

    $("#btn_delete_filepath").on("click", function() {
        confirmDeleteAttachment()
    });

});

function toggleImpactedDocument() {
    var impact = $("#addEditInternalImpact").val();
    if (impact != null && impact != "1" && impact != "") {
        $("#impactedDocumentContainer").show();
    } else {
        $("#impactedDocumentContainer").hide();
        $("#addEditImpactedDocument").val(null).trigger("change");
    }
}

function toDateInputValue(value) {
    if (value == null) return "";
    return moment(value).format("YYYY-MM-DD");
}

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New External Document");
    $('#addEditDocumentName').val("");
    $('#addEditRevisionEdition').val("");
    $('#addEditExternalType').val("");
    $('#addEditReceivedDate').val("");
    $('#addEditStatus').val("").trigger("change");
    $('#addEditStorageLocation').val("");
    $('#addEditStoragePic').val("");
    $('#addEditReviewFrequency').val("");
    $('#addEditReviewSource').val("");
    $('#addEditLastCheckDate').val("");
    $('#addEditReviewCycleMonths').val("12");
    $('#addEditInternalImpact').val("").trigger("change");
    $('#addEditImpactedDocument').val(null).trigger("change");
    $('#fileNameContainer :input[type="text"]').val("");

    toggleImpactedDocument();

    $('#fileNameContainer').removeClass();
    $('#fileNameContainer').addClass('col-sm-8');
    $('#btn_delete_filepath').hide();

    gFilePath = null;
    gExternalDocumentId = null;

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $('#addEditDocumentName').focus();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gExternalDocumentId = row.EXTERNAL_DOCUMENT_ID;

    $.ajax({
        type: "POST",
        url: externalDocumentConfig.urls.getByKey,
        async: false,
        data: {
            EXTERNAL_DOCUMENT_ID: gExternalDocumentId
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;

                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit External Document");
                $('#addEditDocumentName').val(data.DOCUMENT_NAME);
                $('#addEditRevisionEdition').val(data.REVISION_EDITION);
                $('#addEditExternalType').val(data.EXTERNAL_TYPE);
                $('#addEditReceivedDate').val(toDateInputValue(data.RECEIVED_DATE));
                $('#addEditStatus').select2("trigger", "select", { data: { id: data.STATUS != null ? data.STATUS.toString() : "", text: data.STATUS_DISPLAY } });
                $('#addEditStorageLocation').val(data.STORAGE_LOCATION);
                $('#addEditStoragePic').val(data.STORAGE_PIC);
                $('#addEditReviewFrequency').val(data.REVIEW_FREQUENCY);
                $('#addEditReviewSource').val(data.REVIEW_SOURCE);
                $('#addEditLastCheckDate').val(toDateInputValue(data.LAST_CHECK_DATE));
                $('#addEditReviewCycleMonths').val(data.REVIEW_CYCLE_MONTHS);
                $('#addEditInternalImpact').select2("trigger", "select", { data: { id: data.INTERNAL_IMPACT != null ? data.INTERNAL_IMPACT.toString() : "", text: data.INTERNAL_IMPACT_DISPLAY } });

                if (data.IMPACTED_DOCUMENT_TRANSACTION_ID != null) {
                    $('#addEditImpactedDocument').select2("trigger", "select", { data: { id: data.IMPACTED_DOCUMENT_TRANSACTION_ID.toString(), text: data.IMPACTED_DOCUMENT_CODE + " - " + data.IMPACTED_DOCUMENT_NAME } });
                } else {
                    $('#addEditImpactedDocument').val(null).trigger("change");
                }

                toggleImpactedDocument();

                gFilePath = data.FILE_PATH;
                $('#inputContainer :input[type="text"]').val(data.FILE_PATH);
                if (data.FILE_PATH == null) {
                    $('#fileNameContainer').removeClass();
                    $('#fileNameContainer').addClass('col-sm-8');
                    $('#btn_delete_filepath').hide();
                } else {
                    if (externalDocumentConfig.deleteFilePathFlag == "False") {
                        $('#fileNameContainer').removeClass();
                        $('#fileNameContainer').addClass('col-sm-8');
                        $('#btn_delete_filepath').hide();
                    } else {
                        $('#fileNameContainer').removeClass();
                        $('#fileNameContainer').addClass('col-sm-7');
                        $('#btn_delete_filepath').show();
                    }
                }

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#addEditDocumentName').focus();
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

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gExternalDocumentId = row.EXTERNAL_DOCUMENT_ID;
    gFilePath = row.FILE_PATH;

    showDangerConfirm(deleteMessage("External Document", row.DOCUMENT_NAME), deleteData, "Delete");
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

    data.append("screenMode", gScreenMode);
    data.append("EXTERNAL_DOCUMENT_ID", gExternalDocumentId == null ? "" : gExternalDocumentId);
    data.append("DOCUMENT_NAME", $('#addEditDocumentName').val());
    data.append("REVISION_EDITION", $('#addEditRevisionEdition').val());
    data.append("EXTERNAL_TYPE", $('#addEditExternalType').val());
    data.append("RECEIVED_DATE", $('#addEditReceivedDate').val());
    data.append("STORAGE_LOCATION", $('#addEditStorageLocation').val());
    data.append("STORAGE_PIC", $('#addEditStoragePic').val());
    data.append("REVIEW_FREQUENCY", $('#addEditReviewFrequency').val());
    data.append("REVIEW_SOURCE", $('#addEditReviewSource').val());
    data.append("LAST_CHECK_DATE", $('#addEditLastCheckDate').val());
    data.append("REVIEW_CYCLE_MONTHS", $('#addEditReviewCycleMonths').val());

    var status = $('#addEditStatus').val();
    data.append("STATUS", status == null || status == "null" ? "" : status);

    var internalImpact = $('#addEditInternalImpact').val();
    data.append("INTERNAL_IMPACT", internalImpact == null || internalImpact == "null" ? "" : internalImpact);

    var impactedDocument = $('#addEditImpactedDocument').val();
    data.append("IMPACTED_DOCUMENT_TRANSACTION_ID", impactedDocument == null || impactedDocument == "null" || internalImpact == "1" ? "" : impactedDocument);

    if (gFilePath !== null) {
        data.append("FILE_PATH", gFilePath);
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
        url: externalDocumentConfig.urls.addEdit,
        contentType: false,
        processData: false,
        data: data,
        success: function(result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
                $("#modalAddEdit").modal('hide');
            } else {
                popUpProgressHide();
            }
        },
        error: function(result) {
            popUpProgressHide();
        }
    });
}

function deleteData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: externalDocumentConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            EXTERNAL_DOCUMENT_ID: gExternalDocumentId,
            FILE_PATH: gFilePath
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

function onDownload(attachment) {
    gFilePath = attachment;
    downloadAttachment();
}

function downloadAttachment() {
    if (gFilePath !== "null" && gFilePath !== null) {
        var link = externalDocumentConfig.urls.downloadAttachment;
        link = link.replace("-1", gFilePath);
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
        url: externalDocumentConfig.urls.removeAttachment,
        dataType: 'json',
        data: {
            EXTERNAL_DOCUMENT_ID: gExternalDocumentId,
            FILE_PATH: gFilePath
        },
        success: function(result) {
            if (result.status != false) {
                $('#inputContainer :input[type="text"]').val("");
                $('#fileNameContainer').removeClass();
                $('#fileNameContainer').addClass('col-sm-8');
                $('#btn_delete_filepath').hide();

                gFilePath = null;
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
