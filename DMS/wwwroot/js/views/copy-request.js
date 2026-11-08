// CopyRequest. Server values come from copyRequestConfig in the view.
var gCanEdit = copyRequestConfig.canEdit;
var gCanDelete = copyRequestConfig.canDelete;
var gCanSubmit = copyRequestConfig.canSubmit;
var gCanPrint = copyRequestConfig.canPrint;
var gCanApprove = copyRequestConfig.canApprove;

function updateActionButtons() {
    var row = getSelectedRowData();
    var editable = row != null && row.STATUS == "0";
    var approvable = row != null && row.STATUS == "1";

    $("#btn_edit").prop("disabled", !editable);
    $("#btn_delete").prop("disabled", !editable);
    $("#btn_approve_toolbar").prop("disabled", !approvable);
}

function statusPillHtml(status, display) {
    var cls = "dms-pill-default";
    if (status == "1") cls = "dms-pill-warning";
    else if (status == "2") cls = "dms-pill-success";
    else if (status == "3") cls = "dms-pill-danger";
    else if (status == "4") cls = "dms-pill-default";

    return '<span class="dms-pill ' + cls + '">' + (display || "-") + '</span>';
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);
    $("#btn_approve_toolbar").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "REQUEST_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: copyRequestConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                updateHeaderStats(rows);
                d.resolve(rows);
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
            {
                dataField: "REQUEST_NO",
                caption: "Request No",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    if (!row.REQUEST_NO) {
                        container.text("Draft");
                        return;
                    }

                    $("<button>")
                        .attr("type", "button")
                        .addClass("dp-link")
                        .text(row.REQUEST_NO)
                        .on("click", function (e) {
                            // stopPropagation supaya tidak bentrok dengan
                            // handler klik-di-luar-panel yang menutup
                            // #approvalPanel (lihat _ApprovalForm.cshtml).
                            e.stopPropagation();
                            if (row.STATUS == "0") {
                                $("#datagrid").dxDataGrid("instance").selectRows([row.REQUEST_ID], false);
                                onEditPrepare();
                            } else {
                                onApprovalPrepare(row);
                            }
                        })
                        .appendTo(container);
                }
            },
            {
                dataField: "STATUS_DISPLAY",
                caption: "Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    container.html(statusPillHtml(options.data.STATUS, options.data.STATUS_DISPLAY));
                }
            },
            {
                dataField: "PRINTED_COUNT",
                caption: "Print",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    // Monitoring cetak fisik (PrintTrack) - cuma relevan
                    // setelah QMS Approve, sama seperti kolom Acceptance
                    // (request Hendra 2026-08-15).
                    if (row.STATUS != "2") {
                        container.text("-");
                        return;
                    }

                    var total = row.DETAIL_COUNT || 0;
                    var printed = row.PRINTED_COUNT || 0;
                    var sheets = row.PRINTED_SHEETS || 0;
                    var complete = total > 0 && printed >= total;

                    container.css({ display: "flex", alignItems: "center", justifyContent: "center", gap: "6px", cursor: "pointer" });

                    var $wrap = $("<div>").css({ lineHeight: 1.3 });

                    $("<span>")
                        .css({ fontWeight: 600, color: complete ? "#1E9E63" : "#B36B00" })
                        .html('<i class="fa ' + (complete ? "fa-check-circle" : "fa-hourglass-half") + '"></i> ' + (complete ? "Printed" : "Waiting (" + printed + "/" + total + ")"))
                        .appendTo($wrap);

                    if (complete) {
                        $("<span>")
                            .css({ display: "block", fontSize: "10.5px", color: "#8A94A6", marginTop: "2px" })
                            .text(sheets + " sheet" + (sheets !== 1 ? "s" : ""))
                            .appendTo($wrap);
                    }

                    $wrap.appendTo(container);

                    // Klik untuk lihat rincian eksekusi cetak per dokumen
                    // (printer, jumlah lembar, operator, waktu) lewat side
                    // panel - lihat _PrintLogPanel.cshtml (request Hendra
                    // 2026-08-15).
                    container.on("click", function (e) {
                        e.stopPropagation();
                        crOpenPrintLog(row);
                    });

                    $("<i>").addClass("fa fa-chevron-circle-right")
                        .css({ flex: "none", fontSize: "11px", color: "#B0B8C4" })
                        .attr("title", "Click for detail")
                        .appendTo(container);
                }
            },
            {
                dataField: "REQUEST_DATE",
                caption: "Request Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.REQUEST_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "DIVISION_NAME", caption: "Division" },
            { dataField: "DEPARTMENT_NAME", caption: "Department" },
            { dataField: "SECTION_NAME", caption: "Section" },
            {
                dataField: "DETAIL_COUNT",
                caption: "Documents",
                alignment: "center",
                cellTemplate: function (container, options) {
                    $("<span>").addClass("dp-count-chip").text(options.data.DETAIL_COUNT || 0).appendTo(container);
                }
            },
            { dataField: "REQUESTED_BY", caption: "Requested By" },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY hh:mm:ss") : "");
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

    bindGridToolbar("columns filters refresh resize");

    $("#btn_approve_toolbar").on("click", function (e) {
        // stopPropagation - tanpa ini klik yang sama menyebar ke handler
        // klik-di-luar-panel (_ApprovalForm.cshtml) dan langsung menutup
        // lagi panel yang baru dibuka (bug: tombol Approve di toolbar
        // Actions terlihat tidak berfungsi - ditemukan & diperbaiki
        // 2026-08-18). Pola sama seperti Request No/kebab "View Approval".
        e.stopPropagation();
        var row = getSelectedRowData();
        if (row) onApprovalPrepare(row);
    });

    // ---------- toggle the docked "Document Status" panel (collapses to
    // the right, grid reflows into the freed width via flex) ----------
    $("#btn_toggleStats").on("click", function () {
        setStatsPanelCollapsed(!$("#dpLayoutRow").hasClass("stats-collapsed"));
    });

    $("#btn_closeStatsPanel").on("click", function () {
        setStatsPanelCollapsed(true);
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

// ---------- header stats (Waiting Approval / Approved / Rejected / Approved Today) ----------
function updateHeaderStats(rows) {
    rows = rows || [];
    var waitingApproval = 0, approved = 0, rejected = 0, approvedToday = 0;
    var today = moment().format("YYYY-MM-DD");

    rows.forEach(function (r) {
        if (r.STATUS == 1) waitingApproval++;
        if (r.STATUS == 2) {
            approved++;
            if (r.APPROVED_DT && moment(r.APPROVED_DT).format("YYYY-MM-DD") == today) {
                approvedToday++;
            }
        }
        if (r.STATUS == 3) rejected++;
    });

    $("#crWaitingApproval").text(waitingApproval);
    $("#crApproved").text(approved);
    $("#crRejected").text(rejected);
    $("#crApprovedToday").text(approvedToday);

    // surfaces the pending count on the collapsed toggle too, so it
    // stays useful (not just a bare arrow) even when the panel is closed
    crLastWaitingApproval = waitingApproval;
    refreshToggleBadge();
}

// ---------- docked "Document Status" panel toggle - top-level (not
// inside $(document).ready) so updateHeaderStats() above can reach
// crLastWaitingApproval too ----------
var crLastWaitingApproval = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    $("#dpToggleBadge").text(crLastWaitingApproval).toggle(collapsed && crLastWaitingApproval > 0);
}

function setStatsPanelCollapsed(collapsed) {
    var $row = $("#dpLayoutRow");
    var $icon = $("#btn_toggleStats i");

    $row.toggleClass("stats-collapsed", collapsed);
    $icon.toggleClass("fa-angle-double-right", !collapsed).toggleClass("fa-angle-double-left", collapsed);
    $("#btn_toggleStats").attr("title", collapsed ? "Show Document Status" : "Hide Document Status");
    refreshToggleBadge();

    setTimeout(function () {
        $("#datagrid").dxDataGrid("instance").option("height", computeGridHeight());
    }, 230);
}

// ---------- per-row "more actions" kebab menu ----------
var $crRowMenu = null;

function buildRowActionMenu() {
    if ($crRowMenu) return $crRowMenu;

    $crRowMenu = $('<ul class="dropdown-menu dp-dropdown-menu" role="menu"></ul>')
        .css({ position: "absolute", display: "none", "z-index": 2000, "min-width": "180px" })
        .appendTo("body");

    $(document).on("click", function () {
        $crRowMenu.hide();
    });

    return $crRowMenu;
}

function menuItem(cssClass, icon, label, onClick) {
    var $li = $('<li><a href="#" class="' + cssClass + '"><i class="fa ' + icon + '"></i> ' + label + '</a></li>');
    $li.find("a").on("click", function (e) {
        e.preventDefault();
        // stopPropagation supaya menu item yang membuka side panel (mis.
        // "View Approval" -> #approvalPanel) tidak langsung tertutup
        // lagi oleh handler klik-di-luar-panel pada event klik yang sama.
        e.stopPropagation();
        $crRowMenu.hide();
        onClick();
    });
    return $li;
}

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    var grid = $("#datagrid").dxDataGrid("instance");
    $menu.empty();

    if (row.STATUS == "0") {
        if (gCanEdit) {
            $menu.append(menuItem("cr-menu-edit", "fa-pencil-square-o", "Edit", function () {
                grid.selectRows([row.REQUEST_ID], false);
                onEditPrepare();
            }));
        }

        if (gCanSubmit) {
            $menu.append(menuItem("cr-menu-submit", "fa-paper-plane", "Submit for Approval", function () {
                onSubmitPrepare(row.REQUEST_ID);
            }));
        }

        if (gCanDelete) {
            $menu.append('<li class="divider"></li>');
            $menu.append(menuItem("cr-menu-delete", "fa-trash-o", "Delete", function () {
                grid.selectRows([row.REQUEST_ID], false);
                onDeletePrepare();
            }));
        }
    } else {
        $menu.append(menuItem("cr-menu-view", "fa-eye", "View Approval", function () {
            onApprovalPrepare(row);
        }));
    }

    if (gCanPrint) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("cr-menu-print", "fa-print", "Print Form", function () {
            window.open(copyRequestConfig.urls.printForm + "?requestId=" + row.REQUEST_ID, "_blank");
        }));
        $menu.append(menuItem("cr-menu-export-excel", "fa-file-excel-o", "Export Excel", function () {
            window.open(copyRequestConfig.urls.exportExcel + "?requestId=" + row.REQUEST_ID, "_blank");
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

$(document).ready(function () {
    $("#btn_closeApprovalPanel").on("click", function () {
        $("#approvalPanel").removeClass("open");
    });

    // Klik di luar panel menutup panel - klik pada elemen yang MEMBUKA
    // panel ini (Request No/kebab "View Approval" di grid) sudah
    // stopPropagation() masing-masing jadi tidak langsung menutup ulang
    // panel yang baru dibuka.
    $(document).on("click", function (e) {
        if ($("#approvalPanel").hasClass("open") && !$(e.target).closest("#approvalPanel").length) {
            $("#approvalPanel").removeClass("open");
        }
    });
});

var plpRows = [];

function crOpenPrintLog(row) {
    $("#plpRequestNo").text(row.REQUEST_NO || "Draft");
    $("#plpSearch").val("");
    $("#printLogPanel").addClass("open");

    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.searchPrintLog,
        data: { requestId: row.REQUEST_ID },
        success: function (result) {
            plpRows = (result.status && result.data) ? result.data : [];
            renderPrintLogCards(plpRows);
        },
        error: function () {
            showErrorMesgGrowl("Failed to load print detail");
        }
    });
}

function printLogStatusPill(d) {
    var pillClass = "plp-pill-default";
    var pillText = "Not Printed";

    if (d.PRINT_LOG_ID != null) {
        if (d.LOG_PRINT_STATUS == "Success") {
            pillClass = "plp-pill-success";
            pillText = "Success";
        } else if (d.LOG_PRINT_STATUS == "Failed") {
            pillClass = "plp-pill-danger";
            pillText = "Failed";
        } else {
            pillClass = "plp-pill-default";
            pillText = d.LOG_PRINT_STATUS || "Cancelled";
        }
    }

    return '<span class="plp-pill ' + pillClass + '">' + pillText + '</span>';
}

function renderPrintLogCards(rows) {
    var $list = $("#plpCardList").empty();

    if (!rows || rows.length === 0) {
        $list.html('<div class="plp-empty"><i class="fa fa-inbox" style="font-size:24px; display:block; margin-bottom:8px; color:#C0C6CE;"></i>No documents in this request.</div>');
        return;
    }

    rows.forEach(function (d) {
        var $card = $('<div class="plp-card"></div>');

        var $top = $('<div class="plp-card-top"></div>');
        $('<div>')
            .append($('<div class="plp-doc-code"></div>').text(d.DOCUMENT_CODE || "-"))
            .append($('<div class="plp-doc-name"></div>').text(d.DOCUMENT_NAME || ""))
            .append($('<div class="plp-qty"></div>').text("Qty " + (d.COPY_QTY != null ? d.COPY_QTY : "-")))
            .appendTo($top);
        $(printLogStatusPill(d)).appendTo($top);
        $top.appendTo($card);

        // Baris meta (printer/lembar/operator/waktu) cuma relevan kalau
        // sudah pernah dicetak (punya PRINT_LOG_ID) - dokumen yang belum
        // dicetak sama sekali cukup ditampilkan qty + pill saja.
        if (d.PRINT_LOG_ID != null) {
            var $meta = $('<div class="plp-card-meta"></div>');
            if (d.PRINTER_NAME) {
                $meta.append($('<span>').html('<i class="fa fa-print"></i> ' + d.PRINTER_NAME));
            }
            if (d.TOTAL_SHEETS != null) {
                $meta.append($('<span>').html('<i class="fa fa-file-text-o"></i> ' + d.TOTAL_SHEETS + ' sheets'));
            }
            if (d.PRINTED_BY) {
                $meta.append($('<span>').html('<i class="fa fa-user"></i> ' + d.PRINTED_BY));
            }
            if (d.PRINTED_DT) {
                $meta.append($('<span>').html('<i class="fa fa-clock-o"></i> ' + moment(d.PRINTED_DT).format("DD MMM YYYY HH:mm")));
            }
            $meta.appendTo($card);

            if (d.LOG_PRINT_STATUS == "Failed" && d.ERROR_DETAIL) {
                $('<div class="plp-card-error"></div>').html('<i class="fa fa-exclamation-circle"></i> ' + d.ERROR_DETAIL).appendTo($card);
            }
        }

        $card.appendTo($list);
    });
}

$(document).ready(function () {
    $("#btn_closePrintLogPanel").on("click", function () {
        $("#printLogPanel").removeClass("open");
    });

    // Klik di luar panel menutup panel - klik pada sel Print sendiri sudah
    // stopPropagation() jadi tidak akan langsung menutup ulang panel yang
    // baru dibuka.
    $(document).on("click", function (e) {
        if ($("#printLogPanel").hasClass("open") && !$(e.target).closest("#printLogPanel").length) {
            $("#printLogPanel").removeClass("open");
        }
    });

    $("#plpSearch").on("input", function () {
        var term = $(this).val().toLowerCase();
        if (!term) {
            renderPrintLogCards(plpRows);
            return;
        }

        var filtered = plpRows.filter(function (d) {
            return ((d.DOCUMENT_CODE || "") + " " + (d.DOCUMENT_NAME || "")).toLowerCase().indexOf(term) !== -1;
        });
        renderPrintLogCards(filtered);
    });
});

var gScreenMode = null;
var gRequestId = null;
var gRequestNo = null;
var gApprovalHeaderRow = null;
var gApprovalMode = null;
var gDetailRows = [];
var gDeletedDetailIds = [];
var gEditingDetailIndex = null;

var SESSION_DIVISION = copyRequestConfig.sessionDivision;
var SESSION_DEPARTMENT_ID = copyRequestConfig.sessionDepartmentId;
var SESSION_SECTION_CODE = copyRequestConfig.sessionSectionCode;
var SESSION_USERNAME = copyRequestConfig.sessionUsername;

$(document).ready(function () {
    loadDocumentCategories();

    $("#btn_add").on("click", function () { onAddPrepare(); });
    $("#btn_edit").on("click", function () { onEditPrepare(); });
    $("#btn_delete").on("click", function () { onDeletePrepare(); });
    $("#btn_save").on("click", function () { confirmSaveHeader(); });
    $("#btn_addDetailRow").on("click", function () { onDocumentPickerPrepare(); });
    $("#btn_saveDetailRow").on("click", function () { confirmSaveDetailRow(); });
    $("#btn_addSelectedDocuments").on("click", function () { onAddSelectedDocuments(); });

    $("#btn_approve").on("click", function () {
        gApprovalMode = "approve";
        showConfirmMesg("Are you sure you want to approve this request?", onApproveReject, "Confirmation");
    });

    $("#btn_reject").on("click", function () {
        gApprovalMode = "reject";
        showWarningConfirm("Reject Print Request \"" + confirmText(gRequestNo) + "\"?", onApproveReject, "Reject");
    });
});

// ---------- header add/edit ----------

// Checkbox Jenis Dokumen diambil dari TB_M_SYSTEM (SYSTEM_TYPE =
// DOC_SUBMISSION_CATEGORY, dipakai bersama modul Document Submission -
// direvisi 2026-08-11 supaya konsisten dengan kategori di template Excel
// resmi Copy Request; sempat pakai COPY_REQUEST_DOC_CATEGORY 8-kode
// sendiri tapi dibatalkan) supaya tambah/ubah/hapus kategori cukup lewat
// System Parameters tanpa perlu deploy ulang. Checkbox "Jenis Dokumen" di
// form cetak PDF/Excel TIDAK diisi otomatis oleh sistem (keputusan
// Hendra 2026-08-11 - diisi manual), jadi kode kategori ini murni untuk
// data digital, tidak perlu dipetakan ke posisi cell/shape manapun.
function loadDocumentCategories() {
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.mSystemSearchSystem,
        data: { SYSTEM_TYPE: "DOC_SUBMISSION_CATEGORY", STATUS: 1 },
        success: function (result) {
            var rows = (result.data || []).slice().sort(function (a, b) {
                return (a.SYSTEM_CODE > b.SYSTEM_CODE) ? 1 : -1;
            });

            var $container = $('#docCategoryContainer').empty();
            rows.forEach(function (row) {
                $('<label>')
                    .append($('<input type="checkbox" class="addEditDocCategory">').val(row.SYSTEM_CODE))
                    .append(' ' + row.SYSTEM_VALUE)
                    .appendTo($container);
            });
        }
    });
}

function onAddPrepare() {
    gScreenMode = 'ADD';
    gRequestId = null;
    gDetailRows = [];
    gDeletedDetailIds = [];
    $('#modal-title').text('New Print Request');
    resetHeaderForm();
    renderDetailGrid([]);
    $("#modalAddEdit").modal();
}

// Division/Department selalu ikut sesi login user (read-only, sama seperti
// modul Document Submission) - Section untuk request BARU diambil dari
// sesi; Section untuk EDIT tetap nilai yang sudah tersimpan di baris itu
// (lihat onEditPrepare).
function resetHeaderForm() {
    $('#addEditRequestId').val('');
    $('#addEditSection').val(SESSION_SECTION_CODE || '');
    $('#addEditRequestDate').val(moment().format('YYYY-MM-DD'));
    $('#addEditRemark').val('');
    $('.addEditDocCategory').prop('checked', false);
}

function onEditPrepare() {
    var row = getSelectedRowData();
    if (row == null) return;

    if (row.STATUS != "0") {
        showErrorMesgGrowl("Only Draft requests can be edited");
        return;
    }

    gScreenMode = 'EDIT';
    gRequestId = row.REQUEST_ID;
    gDeletedDetailIds = [];
    $('#modal-title').text('Edit Print Request');

    $('#addEditRequestId').val(row.REQUEST_ID);
    $('#addEditSection').val(row.SECTION_CODE || '');
    $('#addEditRequestDate').val(row.REQUEST_DATE ? moment(row.REQUEST_DATE).format('YYYY-MM-DD') : '');
    $('#addEditRemark').val(row.REMARK || '');

    $('.addEditDocCategory').prop('checked', false);
    (row.DOC_CATEGORY || '').split(',').forEach(function (c) {
        if (c) $('.addEditDocCategory[value="' + c + '"]').prop('checked', true);
    });

    loadDetailGrid();

    $("#modalAddEdit").modal();
}

function confirmSaveHeader() {
    if (!$('#addEditRequestDate').val()) {
        showErrorMesgGrowl('Tanggal Permohonan is required');
        return;
    }
    if (gDetailRows.length === 0) {
        showErrorMesgGrowl('Please add at least one document before saving.');
        return;
    }
    showConfirmMesg("Are you sure you want to continue process?", saveHeader, "Confirmation");
}

// Header + seluruh baris dokumen (gDetailRows, di-stage di client selama
// modal terbuka) disimpan sekaligus dalam satu aksi Save - user tidak perlu
// save header dulu baru bisa nambah dokumen. Urutannya: simpan header dulu
// (perlu REQUEST_ID nyata sebagai FK), baru sinkronkan baris dokumen.
function saveHeader() {
    popUpProgressShow();

    var categories = [];
    $('.addEditDocCategory:checked').each(function () { categories.push($(this).val()); });

    var data = {
        screenMode: gScreenMode,
        REQUEST_ID: gRequestId,
        SECTION_CODE: $('#addEditSection').val(),
        REQUEST_DATE: $('#addEditRequestDate').val(),
        DOC_CATEGORY: categories.join(','),
        REMARK: $('#addEditRemark').val()
    };

    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.addEdit,
        data: data,
        success: function (result) {
            if (result.status != true) {
                popUpProgressHide();
                showErrorMesgGrowl(result.message);
                return;
            }

            if (gScreenMode == 'ADD') {
                gScreenMode = 'EDIT';
                gRequestId = result.returnId;
                $('#addEditRequestId').val(gRequestId);
            }

            syncDetailRows(0);
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

// Sinkronkan gDetailRows (baris baru/diubah) + gDeletedDetailIds (baris yang
// dihapus user selagi modal terbuka) ke server satu-satu, lalu tutup modal.
function syncDetailRows(index) {
    if (index === 0 && gDeletedDetailIds.length === 0 && gDetailRows.length === 0) {
        finishSaveHeader();
        return;
    }

    if (index < gDeletedDetailIds.length) {
        $.ajax({
            type: "POST",
            url: copyRequestConfig.urls.deleteDetail,
            data: { REQUEST_DETAIL_ID: gDeletedDetailIds[index] },
            complete: function () { syncDetailRows(index + 1); }
        });
        return;
    }

    var rowIndex = index - gDeletedDetailIds.length;

    if (rowIndex >= gDetailRows.length) {
        finishSaveHeader();
        return;
    }

    var row = gDetailRows[rowIndex];
    row.REQUEST_ID = gRequestId;

    var data = {
        screenMode: row.REQUEST_DETAIL_ID ? 'EDIT' : 'ADD',
        REQUEST_DETAIL_ID: row.REQUEST_DETAIL_ID,
        REQUEST_ID: gRequestId,
        DOCUMENT_TRANSACTION_ID: row.DOCUMENT_TRANSACTION_ID,
        DOCUMENT_CODE: row.DOCUMENT_CODE,
        DOCUMENT_NAME: row.DOCUMENT_NAME,
        REVISION_NO: row.REVISION_NO,
        COPY_TYPE: row.COPY_TYPE,
        COPY_QTY: row.COPY_QTY,
        REASON: row.REASON,
        COUNTERMEASURE: row.COUNTERMEASURE,
        REMARKS: row.REMARKS
    };

    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.addEditDetail,
        data: data,
        success: function (result) {
            if (!result.status) {
                showErrorMesgGrowl((row.DOCUMENT_NAME || 'Document row') + ': ' + result.message);
            }
            syncDetailRows(index + 1);
        },
        error: function () { syncDetailRows(index + 1); }
    });
}

function finishSaveHeader() {
    popUpProgressHide();
    gDeletedDetailIds = [];
    showSuccessMesgGrowl('Saved Successfully');
    onSearch();
    $("#modalAddEdit").modal('hide');
}

function onDeletePrepare() {
    var row = getSelectedRowData();
    if (row == null) return;

    if (row.STATUS != "0") {
        showErrorMesgGrowl("Only Draft requests can be deleted");
        return;
    }

    showDangerConfirm(deleteMessage("Print Request", row.REQUEST_NO), function () { deleteHeader(row); }, "Delete");
}

function deleteHeader(row) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.deleteData,
        data: { REQUEST_ID: row.REQUEST_ID },
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

function onSubmitPrepare(requestId) {
    showConfirmMesg("Submit this request for approval? You won't be able to edit the documents after this.", function () { submitForm(requestId); }, "Confirmation");
}

function submitForm(requestId) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.submit,
        data: { requestId: requestId },
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

// ---------- detail rows (documents) ----------
// Baris dokumen di-stage di gDetailRows selama modal terbuka (tidak langsung
// ke server) - baru benar-benar disimpan saat user klik Save di header,
// lihat syncDetailRows(). Identitas dokumen (kode/nama/revisi) SELALU dari
// picker (dokumen yang sudah approved) - tidak ada opsi ketik manual,
// karena Controlled Copy Request harus merujuk dokumen yang sudah ada.

function copyTypeDisplay(code) {
    if (code === '1') return 'HP';
    if (code === '2') return 'W';
    return '-';
}

// Cuma dipakai saat buka Edit request yang sudah ada - ambil baris yang
// sudah tersimpan supaya bisa terus diedit/dihapus/ditambah di client
// sebelum akhirnya di-sync ulang saat Save.
function loadDetailGrid() {
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.searchDetail,
        data: { REQUEST_ID: gRequestId },
        success: function (result) {
            gDetailRows = result.data || [];
            renderDetailGrid(gDetailRows);
        }
    });
}

function renderDetailGrid(rows) {
    if ($.fn.DataTable.isDataTable('#detailGrid')) {
        $('#detailGrid').DataTable().destroy();
    }

    $('#detailGrid').DataTable({
        "ordering": false,
        "paging": false,
        "info": false,
        "searching": false,
        "bDestroy": true,
        "oLanguage": { "sEmptyTable": "<i class=\"fa fa-inbox\" style=\"font-size:26px; color:#C0C6CE; display:block; margin-bottom:8px;\"></i><b>No documents added yet.</b><br><span style=\"font-size:11.5px; color:#98A2B3;\">Click \"Add Document\" to select documents.</span>" },
        "data": rows,
        "columns": [
            { render: function (data, type, row, meta) { return meta.row + 1; } },
            { render: function (data, type, row) { return row.DOCUMENT_CODE || "-"; } },
            { data: "DOCUMENT_NAME" },
            {
                // Qty langsung bisa diketik di tabel (bukan cuma lewat
                // popup edit ikon pensil) - ini yang jadi jumlah cetak
                // sungguhan di PrintTrack (PrintLogPayload.CopyCount),
                // jadi harus gampang diubah tanpa buka popup dulu
                // (request Hendra 2026-08-15).
                render: function (data, type, row, meta) {
                    if (type !== "display") return row.COPY_QTY || 1;
                    return '<input type="number" class="form-control input-sm detail-qty-input" data-index="' + meta.row + '" min="1" step="1" style="width:70px;" value="' + (row.COPY_QTY || 1) + '">';
                }
            },
            { render: function (data, type, row) { return copyTypeDisplay(row.COPY_TYPE); } },
            {
                render: function (data, type, row, meta) {
                    return '<button type="button" class="btn btn-warning btn-xs detail-edit-btn" data-index="' + meta.row + '"><i class="fa fa-pencil"></i></button> ' +
                        '<button type="button" class="btn btn-danger btn-xs detail-delete-btn" data-index="' + meta.row + '"><i class="fa fa-trash"></i></button>';
                }
            }
        ]
    });

    $('#detailGrid').off('click', '.detail-edit-btn').on('click', '.detail-edit-btn', function () {
        var index = $(this).data('index');
        var row = gDetailRows[index];
        if (row) onEditDetailRowPrepare(row, index);
    });

    // Update gDetailRows langsung di tempat (TANPA renderDetailGrid ulang)
    // supaya fokus/kursor input tidak hilang saat user mengetik.
    $('#detailGrid').off('input', '.detail-qty-input').on('input', '.detail-qty-input', function () {
        var index = $(this).data('index');
        var value = parseInt($(this).val(), 10);
        if (gDetailRows[index] && value >= 1) {
            gDetailRows[index].COPY_QTY = value;
        }
    });

    // Rapikan nilai kosong/tidak valid begitu user pindah dari field-nya.
    $('#detailGrid').off('blur', '.detail-qty-input').on('blur', '.detail-qty-input', function () {
        var index = $(this).data('index');
        var value = parseInt($(this).val(), 10);
        if (!value || value < 1) value = 1;
        $(this).val(value);
        if (gDetailRows[index]) {
            gDetailRows[index].COPY_QTY = value;
        }
    });

    $('#detailGrid').off('click', '.detail-delete-btn').on('click', '.detail-delete-btn', function () {
        var index = $(this).data('index');
        showDangerConfirm(deleteMessage("document row", (gDetailRows[index] || {}).DOCUMENT_NAME), function () { deleteDetailRow(index); }, "Delete");
    });
}

function deleteDetailRow(index) {
    var row = gDetailRows[index];
    if (!row) return;

    if (row.REQUEST_DETAIL_ID) {
        gDeletedDetailIds.push(row.REQUEST_DETAIL_ID);
    }

    gDetailRows.splice(index, 1);
    renderDetailGrid(gDetailRows);
}

// ---------- bulk document picker ("Add Document") ----------
var gPickerRows = {};

function onDocumentPickerPrepare() {
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.searchApprovedDocuments,
        success: function (result) {
            renderPickerGrid(result.data || []);
            $('#modalDocumentPicker').modal();
        }
    });
}

function renderPickerGrid(rows) {
    gPickerRows = {};
    updatePickerSelectedCount();

    if ($.fn.DataTable.isDataTable('#pickerGrid')) {
        $('#pickerGrid').DataTable().destroy();
    }

    $('#pickerGrid').DataTable({
        "bDestroy": true,
        "ordering": false,
        "oLanguage": { "sEmptyTable": "No approved documents found." },
        "data": rows,
        "columns": [
            {
                render: function (data, type, row) {
                    return '<input type="checkbox" class="picker-row-check" data-id="' + row.DOCUMENT_TRANSACTION_ID + '">';
                }
            },
            { data: 'DOCUMENT_CODE' },
            { data: 'DOCUMENT_NAME' },
            { render: function (data, type, row) { return row.REVISION != null ? row.REVISION : "-"; } }
        ]
    });

    $('#pickerGrid').off('change', '.picker-row-check').on('change', '.picker-row-check', function () {
        var id = $(this).data('id');
        var row = rows.filter(function (r) { return r.DOCUMENT_TRANSACTION_ID == id; })[0];

        if (this.checked) {
            gPickerRows[id] = row;
        } else {
            delete gPickerRows[id];
        }

        updatePickerSelectedCount();
    });
}

function updatePickerSelectedCount() {
    var count = Object.keys(gPickerRows).length;
    $('#pickerSelectedCount').text(count);
    $('#btn_addSelectedDocuments').prop('disabled', count === 0);
}

// Cuma nambah ke gDetailRows di client (tidak ke server) - baru benar-benar
// tersimpan saat header di-Save, lihat syncDetailRows().
function onAddSelectedDocuments() {
    var rows = Object.keys(gPickerRows).map(function (id) { return gPickerRows[id]; });

    if (rows.length === 0) {
        showErrorMesgGrowl('Select at least one document');
        return;
    }

    rows.forEach(function (doc) {
        var alreadyAdded = gDetailRows.some(function (r) { return r.DOCUMENT_TRANSACTION_ID == doc.DOCUMENT_TRANSACTION_ID; });
        if (alreadyAdded) return;

        gDetailRows.push({
            REQUEST_DETAIL_ID: null,
            DOCUMENT_TRANSACTION_ID: doc.DOCUMENT_TRANSACTION_ID,
            DOCUMENT_CODE: doc.DOCUMENT_CODE,
            DOCUMENT_NAME: doc.DOCUMENT_NAME,
            REVISION_NO: doc.REVISION,
            COPY_TYPE: '1',
            COPY_QTY: 1,
            REASON: null,
            COUNTERMEASURE: null,
            REMARKS: null
        });
    });

    renderDetailGrid(gDetailRows);
    $('#modalDocumentPicker').modal('hide');
}

function onEditDetailRowPrepare(row, index) {
    gEditingDetailIndex = index;
    $('#modalDetailRow-title').text('Edit Document');
    $('#detailRequestId').val(gRequestId);
    $('#detailRequestDetailId').val(row.REQUEST_DETAIL_ID);
    $('#detailDocumentTransactionId').val(row.DOCUMENT_TRANSACTION_ID);

    $('#detailDocumentCode').val(row.DOCUMENT_CODE);
    $('#detailDocumentName').val(row.DOCUMENT_NAME);
    $('#detailRevisionNo').val(row.REVISION_NO);

    $('input[name="detailCopyType"][value="' + row.COPY_TYPE + '"]').prop('checked', true);
    $('#detailCopyQty').val(row.COPY_QTY);
    $('#detailReason').val(row.REASON);
    $('#detailCountermeasure').val(row.COUNTERMEASURE);
    $('#detailRemarks').val(row.REMARKS);

    $('#modalDetailRow').modal();
}

function confirmSaveDetailRow() {
    if (!$('#detailCopyQty').val()) {
        showErrorMesgGrowl('Qty is required');
        return;
    }
    saveDetailRow();
}

// Cuma update gDetailRows di client (tidak ke server) - baru benar-benar
// tersimpan saat header di-Save, lihat syncDetailRows().
function saveDetailRow() {
    var existing = gEditingDetailIndex != null ? gDetailRows[gEditingDetailIndex] : null;

    var row = {
        REQUEST_DETAIL_ID: existing ? existing.REQUEST_DETAIL_ID : null,
        DOCUMENT_TRANSACTION_ID: $('#detailDocumentTransactionId').val(),
        DOCUMENT_CODE: $('#detailDocumentCode').val(),
        DOCUMENT_NAME: $('#detailDocumentName').val(),
        REVISION_NO: $('#detailRevisionNo').val(),
        COPY_TYPE: $('input[name="detailCopyType"]:checked').val(),
        COPY_QTY: $('#detailCopyQty').val(),
        REASON: $('#detailReason').val(),
        COUNTERMEASURE: $('#detailCountermeasure').val(),
        REMARKS: $('#detailRemarks').val()
    };

    if (gEditingDetailIndex != null) {
        gDetailRows[gEditingDetailIndex] = row;
    } else {
        gDetailRows.push(row);
    }

    renderDetailGrid(gDetailRows);
    $('#modalDetailRow').modal('hide');
}

// ---------- Approval (single step by QMS) ----------

var STATUS_PANEL_BY_STATUS = {
    "1": { cls: "adf-status-warning", icon: "fa-clock-o", pre: "Status", text: "Waiting Approval", hint: "Please review the documents and take action." },
    "2": { cls: "adf-status-success", icon: "fa-check", pre: "Status", text: "Approved", hint: "This request has been approved." },
    "3": { cls: "adf-status-danger", icon: "fa-times", pre: "Status", text: "Rejected", hint: "This request was rejected. See remark below." },
    "4": { cls: "adf-status-info", icon: "fa-ban", pre: "Status", text: "Cancelled", hint: "This request was cancelled." }
};

function onApprovalPrepare(row) {
    if (row == null) return;

    gRequestId = row.REQUEST_ID;
    gRequestNo = row.REQUEST_NO;
    gApprovalHeaderRow = row;

    $('#approvalRequestNo').html(row.REQUEST_NO || '-');
    $('#approvalDivision').html(row.DIVISION_NAME ? (row.DIVISION + ' - ' + row.DIVISION_NAME) : (row.DIVISION || '-'));
    $('#approvalDepartment').html(row.DEPARTMENT_NAME || '-');
    $('#approvalSection').html(row.SECTION_NAME || '-');
    $('#approvalRequestedBy').html(row.REQUESTED_BY || '-');
    $('#approvalRequestDate').html(row.REQUEST_DATE ? moment(row.REQUEST_DATE).format('DD-MM-YYYY') : '-');
    $('#approvalTotalDocuments').html(row.DETAIL_COUNT != null ? row.DETAIL_COUNT : '-');

    var panelInfo = STATUS_PANEL_BY_STATUS[row.STATUS] || STATUS_PANEL_BY_STATUS["1"];
    var $panel = $('#approvalStatusPanel');
    $panel.removeClass('adf-status-info adf-status-success adf-status-warning adf-status-danger').addClass(panelInfo.cls);
    $('#approvalStatusIcon').attr('class', 'fa ' + panelInfo.icon);
    $('#approvalStatusPre').text(panelInfo.pre);
    $('#approvalStatusText').text(panelInfo.text);
    $('#approvalStatusHint').text(panelInfo.hint);

    var canAct = gCanApprove && row.STATUS == "1";
    $('#approvalActionInput').toggle(canAct);
    $('#approvalActionReadonly').toggle(!canAct);
    $('#approvalActionTitle').html(row.STATUS == "1"
        ? '<i class="fa fa-check-circle-o"></i> QMS Review'
        : '<i class="fa fa-check-circle-o"></i> QMS Decision');

    if (canAct) {
        $('#approvalRemark').val('');
    } else if (row.STATUS != "0") {
        $('#approvalRemarkReadonly').text(row.APPROVAL_REMARK
            ? row.APPROVAL_REMARK + (row.APPROVED_BY ? ' — ' + row.APPROVED_BY : '')
            : 'No remark.');
    }

    loadApprovalDocumentList();
    $("#approvalPanel").addClass("open");
}

// Kartu daftar dokumen yang diminta di popup review, supaya QMS bisa
// cocokkan langsung tanpa buka layar lain.
function loadApprovalDocumentList() {
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.searchDetail,
        data: { REQUEST_ID: gRequestId },
        success: function (result) {
            var rows = result.data || [];
            var html = '';

            rows.forEach(function (r, idx) {
                html += '<div style="display:flex; gap:10px; padding:6px 0; border-bottom:1px solid #EEF1F4; font-size:12.5px;">' +
                    '<div style="width:22px; color:#98A2B3;">' + (idx + 1) + '</div>' +
                    '<div style="flex:1;">' + (r.DOCUMENT_CODE || '-') + ' &mdash; ' + r.DOCUMENT_NAME + '</div>' +
                    '<div style="width:60px; text-align:right; color:#5A6472;">Qty ' + (r.COPY_QTY || '-') + '</div>' +
                    '<div style="width:50px; text-align:right; color:#98A2B3;">' + copyTypeDisplay(r.COPY_TYPE) + '</div>' +
                    '</div>';
            });

            $('#approvalDocumentList').html(html || '<div class="text-muted" style="font-size:12.5px;">No documents.</div>');
        }
    });
}

function onApproveReject() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: copyRequestConfig.urls.approveReject,
        data: {
            requestId: gRequestId,
            mode: gApprovalMode,
            remark: $('#approvalRemark').val()
        },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                $("#approvalPanel").removeClass("open");
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
