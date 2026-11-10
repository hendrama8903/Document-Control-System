// DocumentMaster. Server values come from documentMasterConfig in the view.
var gCanEdit = documentMasterConfig.canEdit;
var gCanDelete = documentMasterConfig.canDelete;
var gCanDownload = documentMasterConfig.downloadFilePathFlag == "True";
var gTemplateCodes = documentMasterConfig.templateCodes;
var gTemplateInfo = documentMasterConfig.templateInfo;

function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);

    // Delete only for a category never used; a used category stays as it is.
    var used = selected && row.IS_USED == 1;
    $("#btn_delete")
        .prop("disabled", !selected || used)
        .attr("title", used ? "This category is already used, so it cannot be deleted." : "");
}

function dmRenderStats(data) {
    data = data.filter(function (r) { return r.DELETE_FLAG != 1; });
    var total = data.length;
    var levels = new Set(data.map(function (r) { return r.LEVEL_VAL; }).filter(function (v) { return v != null; })).size;
    var withTemplate = data.filter(function (r) { return gTemplateCodes.indexOf(r.DOCUMENT_CODE) !== -1; }).length;

    var lastUpdated = null;
    data.forEach(function (r) {
        var dt = r.CHANGED_DT || r.CREATED_DT;
        if (dt != null && (lastUpdated == null || moment(dt).isAfter(lastUpdated))) {
            lastUpdated = dt;
        }
    });

    $("#dmStatTotal").text(total);
    $("#dmStatLevels").text(levels);
    $("#dmStatWithTemplate").text(withTemplate);
    $("#dmStatLastUpdated").html(lastUpdated != null
        ? moment(lastUpdated).format("DD MMM YYYY") + "<br><span style='font-size:11px;font-weight:600;color:#66788A;'>" + moment(lastUpdated).format("HH:mm") + "</span>"
        : "-");
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentMasterConfig.urls.searchWithStatus,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                dmRenderStats(data);
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
            { dataField: "DOCUMENT_CODE", caption: "Document Code", fixed: true, fixedPosition: "left" },
            { dataField: "DOCUMENT_NAME", caption: "Document Name" },
            { dataField: "LEVEL_VAL", caption: "Level", alignment: "center" },
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
                    $("<span>").css("color", inactive ? "#C62828" : "#1E9E63").css("font-weight", 600).text(inactive ? "Inactive" : "Active").appendTo(container);
                }
            },
            {
                dataField: "REVIEW_CYCLE_MONTHS",
                caption: "Review Cycle",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.REVIEW_CYCLE_MONTHS;
                    container.text(value ? value + " months" : "-");
                }
            },
            {
                dataField: "DOCUMENT_CODE",
                caption: "Signing Template",
                calculateCellValue: function (row) {
                    var info = gTemplateInfo[row.DOCUMENT_CODE];
                    return info ? info.fileName : "";
                },
                cellTemplate: function (container, options) {
                    var info = gTemplateInfo[options.data.DOCUMENT_CODE];
                    if (!info) {
                        $("<span>").css({ color: "#98A2B3" }).html('<i class="fa fa-minus-circle"></i> No template').appendTo(container);
                        return;
                    }
                    $("<span>").css({ color: "#1E9E63", fontWeight: 600 })
                        .html('<i class="fa fa-file-excel-o"></i> ' + info.fileName)
                        .appendTo(container);
                    $("<span>").css({ color: "#98A2B3", marginLeft: "6px", fontSize: "11px" })
                        .text("(" + info.sizeKb + " KB)")
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
            grid.selectRows([row.DOCUMENT_ID], false);
            onEditPrepare();
        }));
    }

    if (gCanDownload && row.FILE_PATH != null) {
        $menu.append(menuItem("dp-menu-download", "fa-download", "Download", function () {
            onDownload(row.FILE_PATH);
        }));
    }

    if (gTemplateCodes.indexOf(row.DOCUMENT_CODE) !== -1) {
        $menu.append(menuItem("dp-menu-download-template", "fa-file-excel-o", "Download Template", function () {
            onDownloadTemplate(row.DOCUMENT_CODE);
        }));
    }

    // Delete only for categories never used (no documents, history, workflow or
    // template configuration). Used categories cannot be deleted or deactivated.
    if (gCanDelete && row.IS_USED != 1) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash", "Delete", function () {
            onRemovePrepare(row);
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gScreenMode = null;
var gDocumentId = null;
var gFilePath = null;

$(document).ready(function() {
    setDataSelect2WithParam("addEditLevel", documentMasterConfig.urls.systemValues, "- Choose -", "LEVEL_CODE");

    $("#btn_add").on("click", function() {
        onAddPrepare();
    });

    $("#btn_edit").on("click", function() {
        onEditPrepare();
    });

    $("#btn_delete").on("click", function() {
        var row = getSelectedRowData();
        if (row != null && row.IS_USED != 1) {
            onRemovePrepare(row);
        }
    });

    $("#btn_save").on("click", function() {
        confirmAddEditData();
    });

    $("#btn_delete_filepath").on("click", function() {
        confirmDeleteAttachment()
    });

    bindGridToolbar("stats");

});

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Document Category");
    $('#addEditDocumentCode').val("");
    $('#addEditDocumentName').val("");
    $('#addEditLevel').val("").trigger("change");
    $('#addEditReviewCycleMonths').val("");

    $('#addEditDocumentCode').prop('disabled', false);
    $('#addEditDocumentName').prop('disabled', false);
    $('#addEditLevel').prop('disabled', false);

    resetUploadBox();

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $('#addEditDocumentCode').focus();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentId = row.DOCUMENT_ID;

    $.ajax({
        type: "POST",
        url: documentMasterConfig.urls.getByKey,
        async: false,
        data: {
            DOCUMENT_ID: gDocumentId
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;

                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit Document Category");
                $('#addEditDocumentCode').val(data.DOCUMENT_CODE);
                $('#addEditDocumentName').val(data.DOCUMENT_NAME);
                $('#addEditLevel').select2("trigger", "select", { data: { id: data.LEVEL, text: data.LEVEL_VAL } });
                $('#addEditReviewCycleMonths').val(data.REVIEW_CYCLE_MONTHS);

                resetUploadBox();
                var existingTemplate = gTemplateInfo[data.DOCUMENT_CODE];
                if (existingTemplate) {
                    showUploadBoxFile(existingTemplate.fileName, "(" + existingTemplate.sizeKb + " KB) - existing template");
                    if (documentMasterConfig.deleteFilePathFlag != "False") {
                        $('#btn_delete_filepath').show();
                    }
                    runTemplateCheck(null, data.DOCUMENT_CODE);
                }

                $('#addEditDocumentCode').prop('disabled', true);
                $('#addEditDocumentName').prop('disabled', false);
                $('#addEditLevel').prop('disabled', false);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $('#addEditDocumentName').focus();
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

function confirmAddEditData() {
    if (gTemplateCheckBlocking) {
        showErrorMesgGrowl("The selected template has signing-marker errors. Fix the template (see the check result below the upload box) or choose another file before saving.");
        return;
    }
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

// ---------- Template upload box (replaces the old bootstrap-filestyle input -
// custom-styled to match the mockup: icon + filename/hint + Choose file + trash) ----------
var DMF_MAX_FILE_SIZE = 10 * 1024 * 1024; // 10MB

function resetUploadBox() {
    $('#attachmentPath').val("");
    $('#dmfUploadBox').removeClass("has-file");
    $('#dmfUploadIcon').html('<i class="fa fa-file-excel-o"></i>');
    $('#dmfUploadFilename').text("No file selected");
    $('#dmfUploadHint').text("Choose a template file to upload");
    $('#btn_delete_filepath').hide();
    resetTemplateCheck();
}

// ---------- Cek penanda pengesahan DCS_ (opsi B) ----------
// gTemplateCheckBlocking: hasil cek file BARU yang dipilih punya error -> Save ditahan
// di sisi client (server tetap menolak juga di AddEdit).
var gTemplateCheckBlocking = false;
var gTemplateCheckXhr = null;

function resetTemplateCheck() {
    if (gTemplateCheckXhr) { gTemplateCheckXhr.abort(); gTemplateCheckXhr = null; }
    gTemplateCheckBlocking = false;
    $('#dmfTemplateCheck').hide().empty().removeClass("ok warn err");
    $('#btn_previewTemplate').hide();
}

function escapeHtmlText(text) {
    return $('<div>').text(text == null ? "" : text).html();
}

function renderTemplateCheck(r, isNewFile) {
    var $box = $('#dmfTemplateCheck').empty().removeClass("ok warn err");
    var level = r.errors.length > 0 ? "err" : (r.warnings.length > 0 ? "warn" : "ok");
    var title = level === "err" ? "Template cannot be used yet"
        : level === "warn" ? "Template can be used, with notes"
        : "Signing markers are complete";

    var html = '<div class="dmf-check-title"><i class="fa ' +
        (level === "err" ? "fa-times-circle" : level === "warn" ? "fa-exclamation-triangle" : "fa-check-circle") +
        '"></i> ' + title + '</div>';

    function list(label, items) {
        if (!items || items.length === 0) return "";
        return '<div class="dmf-check-sub">' + label + '</div><ul>' +
            items.map(function (x) { return "<li>" + escapeHtmlText(x) + "</li>"; }).join("") + "</ul>";
    }

    html += list("Errors", r.errors);
    html += list("Warnings", r.warnings);
    if (r.hasMarkers) {
        html += list("Signature boxes found", r.boxes);
        html += list("Document fields found", r.fields);
    }

    $box.html(html).addClass(level).show();
    gTemplateCheckBlocking = isNewFile && r.errors.length > 0;
    $('#btn_previewTemplate').toggle(r.hasMarkers && r.errors.length === 0);
}

function runTemplateCheck(file, code) {
    resetTemplateCheck();
    var data = new FormData();
    if (gScreenMode === 'EDIT' && gDocumentId != null) data.append("documentId", gDocumentId);
    if (file) data.append("file0", file); else data.append("code", code);
    // Level dipakai untuk mencocokkan kotak POS/STEP dengan alur approval (2026-11-06)
    var level = $('#addEditLevel').val();
    if (level && level != "null") data.append("level", level);

    $('#dmfTemplateCheck').removeClass("ok warn err")
        .html('<span class="dmf-check-muted"><i class="fa fa-spinner fa-spin"></i> Checking signing markers...</span>')
        .addClass("ok").show();

    gTemplateCheckXhr = $.ajax({
        type: "POST",
        url: documentMasterConfig.urls.checkTemplate,
        contentType: false,
        processData: false,
        data: data,
        success: function (r) {
            gTemplateCheckXhr = null;
            if (r.status === true) {
                renderTemplateCheck(r, !!file);
            } else {
                $('#dmfTemplateCheck').removeClass("ok").addClass("err").text(r.message);
            }
        },
        error: function (xhr, textStatus) {
            gTemplateCheckXhr = null;
            if (textStatus !== "abort") {
                $('#dmfTemplateCheck').removeClass("ok").addClass("err").text("Failed to check the template.");
            }
        }
    });
}

$(document).on('click', '#btn_previewTemplate', function () {
    var files = $('#attachmentPath')[0].files;
    var code = $('#addEditDocumentCode').val();
    var previewWindow = window.open("", "_blank"); // dibuka dulu supaya tidak diblok popup blocker
    popUpProgressShow();

    var data = new FormData();
    if (files.length > 0) data.append("file0", files[0]); else data.append("code", code);

    var xhr = new XMLHttpRequest();
    xhr.open("POST", documentMasterConfig.urls.previewTemplate + (files.length > 0 ? "" : "?code=" + encodeURIComponent(code)));
    xhr.responseType = "blob";
    xhr.onload = function () {
        popUpProgressHide();
        if (xhr.status === 200) {
            previewWindow.location.href = URL.createObjectURL(xhr.response);
        } else {
            previewWindow.close();
            xhr.response.text().then(function (msg) { showErrorMesgGrowl(msg || "Failed to generate preview."); });
        }
    };
    xhr.onerror = function () {
        popUpProgressHide();
        previewWindow.close();
        showErrorMesgGrowl("Failed to generate preview.");
    };
    xhr.send(data);
});

function showUploadBoxFile(fileName, hint) {
    $('#dmfUploadBox').addClass("has-file");
    $('#dmfUploadFilename').text(fileName);
    $('#dmfUploadHint').text(hint);
}

$(document).on('click', '#btn_chooseFile', function () {
    $('#attachmentPath').trigger('click');
});

$(document).on('change', '#attachmentPath', function (e) {
    var files = e.target.files;
    if (!files || files.length === 0) return;

    var file = files[0];
    if (file.size > DMF_MAX_FILE_SIZE) {
        showErrorMesgGrowl("File size exceeds the 10MB limit.");
        $('#attachmentPath').val("");
        return;
    }

    showUploadBoxFile(file.name, "(" + Math.ceil(file.size / 1024) + " KB) - will be uploaded on Save");
    runTemplateCheck(file, null);
});

// Level berubah setelah template dipilih: cek ulang supaya peringatan kotak
// vs alur approval memakai level yang benar (2026-11-06).
$(document).on('change', '#addEditLevel', function () {
    if (!$('#dmfTemplateCheck').is(":visible")) return;
    var files = $('#attachmentPath')[0].files;
    if (files && files.length > 0) runTemplateCheck(files[0], null);
    else if (gScreenMode === 'EDIT') runTemplateCheck(null, $('#addEditDocumentCode').val());
});

function addEditData() {
    popUpProgressShow();
    var data = new FormData();
    var level = $('#addEditLevel').val();

    data.append("screenMode", gScreenMode);
    data.append("DOCUMENT_CODE", $('#addEditDocumentCode').val());
    data.append("DOCUMENT_NAME", $('#addEditDocumentName').val());
    if (level == null || level == "null") {
        data.append("LEVEL", "");
    } else {
        data.append("LEVEL", $('#addEditLevel').val());
    }
    if (gDocumentId == null || level == "null") {
        data.append("DOCUMENT_ID", "");
    } else {
        data.append("DOCUMENT_ID", gDocumentId);
    }

    var reviewCycleMonths = $('#addEditReviewCycleMonths').val();
    data.append("REVIEW_CYCLE_MONTHS", reviewCycleMonths == "" ? "" : reviewCycleMonths);

    var files = $('#attachmentPath')[0].files;
    if (files.length > 0) {
        data.append("file0", files[0]);
    }

    $.ajax({
        type: "POST",
        url: documentMasterConfig.urls.addEdit,
        contentType: false,
        processData: false,
        data: data,
        success: function(result) {
            //popUpProgressHide();
            if (result.status == true) {
                // gTemplateInfo/gTemplateCodes cuma di-scan sekali saat page load
                // (Index action) - update manual di sini supaya kolom "Signing
                // Template" & KPI "With Template" langsung sinkron tanpa perlu
                // reload halaman.
                if (files.length > 0) {
                    var code = $('#addEditDocumentCode').val();
                    var ext = files[0].name.substring(files[0].name.lastIndexOf('.'));
                    gTemplateInfo[code] = { fileName: code + ext, sizeKb: Math.ceil(files[0].size / 1024) };
                    if (gTemplateCodes.indexOf(code) === -1) {
                        gTemplateCodes.push(code);
                    }
                }

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

function onRemovePrepare(row) {
    var label = $("<div>").text(row.DOCUMENT_CODE + " - " + row.DOCUMENT_NAME).html();
    showDangerConfirm(
        "Delete Document Category <b>" + label + "</b>? It will no longer appear in this list or in any selection. "
        + "This category has never been used, so no document is affected.",
        function () { removeData(row.DOCUMENT_ID); },
        "Delete");
}

function removeData(documentId) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: documentMasterConfig.urls.remove,
        dataType: "json",
        data: { DOCUMENT_ID: documentId },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
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

function onDownload(attachment) {
    gFilePath = attachment;
    downloadAttachment();
}

function onDownloadTemplate(code) {
    var link = documentMasterConfig.urls.downloadTemplate;
    window.location.href = link.replace("-1", code);
}

function downloadAttachment() {
    if (gFilePath !== "null" && gFilePath !== null) {
        var link = documentMasterConfig.urls.downloadAttachment;
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
        url: documentMasterConfig.urls.removeSigningTemplate,
        dataType: 'json',
        data: {
            code: $('#addEditDocumentCode').val()
        },
        success: function(result) {
            if (result.status != false) {
                resetUploadBox();
                var code = $('#addEditDocumentCode').val();
                delete gTemplateInfo[code];
                var idx = gTemplateCodes.indexOf(code);
                if (idx !== -1) {
                    gTemplateCodes.splice(idx, 1);
                }

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
