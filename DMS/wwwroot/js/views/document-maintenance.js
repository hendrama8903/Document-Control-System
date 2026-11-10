// DocumentMaintenance. Server values come from documentMaintenanceConfig in the view.
var gCanDelete = documentMaintenanceConfig.canDelete;

// ---------- "My Approval": dokumen yang sedang menunggu approval user login
// (request Hendra 2026-11-05 - approver dengan banyak dokumen susah menemukan
// mana yang gilirannya). Baris di-highlight, dinaikkan ke atas, dan bisa
// difilter lewat tombol "My Approval" di toolbar. ----------
var gLoginUser = documentMaintenanceConfig.sessionUsernameJson;
var dpMyApprovalOnly = false;

function isMyApproval(row) {
    return row != null && row.STATUS == 0 && gLoginUser !== ""
        && (row.CURRENT_APPROVER || "").toLowerCase() === gLoginUser.toLowerCase();
}

function updateMyApprovalButton(count) {
    $("#dpMyApprovalCount").text(count);
    $("#btn_myApproval").toggle(count > 0 || dpMyApprovalOnly);
}

function setMyApprovalOnly(enabled) {
    dpMyApprovalOnly = enabled;
    $("#btn_myApproval").toggleClass("active", enabled);
    $("#datagrid").dxDataGrid("instance").columnOption("MY_APPROVAL", "filterValue", enabled ? "Y" : undefined);
}

// Shortcut dari pill Status (giliran user login): pilih baris lalu buka form Approval yang sama
// dengan Actions > Approval
function openMyApproval(row) {
    var grid = $("#datagrid").dxDataGrid("instance");
    grid.selectRows([row.DOCUMENT_TRANSACTION_ID], false).then(function () {
        onApprovalPrepare();
    });
}

function updateActionButtons() {
    var row = getSelectedRowData();
    var editable = row != null && row.STATUS != 1 && row.STATUS != 5;

    $("#btn_edit").prop("disabled", !editable);
    $("#btn_delete").prop("disabled", !editable);
    $("#btn_approval").prop("disabled", row == null);
    $("#btn_history").prop("disabled", row == null);
}

// computeGridHeight() cuma bisa MENDUGA tinggi yang pas (padding/border di
// bawah grid - box-body, .adm-content, dst - bervariasi tergantung tema/
// halaman) - kalau dugaannya sedikit kelebihan, browser jadi selalu muncul
// scrollbar walau datanya cuma dikit (dilaporkan Hendra 2026-08-18). Jadi
// dikoreksi ulang di sini SETELAH grid benar-benar dirender: kalau halaman
// ternyata masih overflow dari viewport, kurangi tinggi grid persis sebesar
// overflow itu. Aman dipanggil berkali-kali (tiap onContentReady) - begitu
// overflow-nya 0 dia otomatis berhenti menyesuaikan.
function correctGridHeightOverflow() {
    var overflow = document.documentElement.scrollHeight - window.innerHeight;
    if (overflow <= 2) return;

    var grid = $("#datagrid").dxDataGrid("instance");
    var currentHeight = grid.option("height");
    if (typeof currentHeight === "number") {
        grid.option("height", Math.max(300, currentHeight - overflow - 2));
    }
}

// ---------- docked "Document Status" panel toggle - top-level (not inside
// $(document).ready) so updateHeaderStats() below can reach dpLastWaitingApproval too ----------
var dpLastWaitingApproval = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    $("#dpToggleBadge").text(dpLastWaitingApproval).toggle(collapsed && dpLastWaitingApproval > 0);
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

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);
    $("#btn_approval").prop("disabled", true);
    $("#btn_history").prop("disabled", true);
    $("#btn_download").prop("disabled", false);

    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentMaintenanceConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                updateHeaderStats(rows);

                // Dokumen giliran approval user login dinaikkan ke atas; urutan
                // asli dari server tetap dipertahankan di dalam masing-masing kelompok
                var mine = rows.filter(isMyApproval);
                var others = rows.filter(function (r) { return !isMyApproval(r); });
                updateMyApprovalButton(mine.length);
                d.resolve(mine.concat(others));
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        onContentReady: function () {
            // Delay kecil - onContentReady DevExtreme sendiri kadang nembak
            // sebelum browser selesai reflow parent container (padding
            // .adm-content dkk), jadi scrollHeight yang diukur langsung di
            // sini masih nilai lama/belum settle.
            setTimeout(correctGridHeightOverflow, 50);
        },
        filterRow: { visible: false, applyFilter: "auto" },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
        },
        onRowPrepared: function (e) {
            if (e.rowType === "data" && isMyApproval(e.data)) {
                e.rowElement.addClass("dp-row-my-approval");
            }
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
            { dataField: "DOCUMENT_CODE", caption: "Document No", fixed: true, fixedPosition: "left" },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name" },
            {
                dataField: "STATUS_DISPLAY",
                caption: "Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = "dms-pill dms-pill-danger";
                    // Ikon per status (bukan dot generik - kurang terlihat, request
                    // Hendra 2026-08-18) - pola sama seperti kolom Registration Status
                    // di grid ini juga, supaya konsisten.
                    var icon = "fa-times-circle"; // Rejected/Deleted (default)

                    if (row.STATUS == 0) {
                        pillClass = "dms-pill dms-pill-info";
                        icon = "fa-clock-o";
                        // Giliran user login: jam diganti lonceng + pill oranye sebagai
                        // penanda (pengganti tag "Your Turn", request Hendra 2026-11-05)
                        if (isMyApproval(row)) {
                            pillClass = "dms-pill dp-pill-my-approval";
                            icon = "fa-bell";
                        }
                    } else if (row.STATUS == 1) {
                        pillClass = "dms-pill dms-pill-success";
                        icon = "fa-check-circle";
                    } else if (row.STATUS == 3) {
                        icon = "fa-trash-o";
                    } else if (row.STATUS == 4) {
                        pillClass = "dms-pill dms-pill-default";
                        icon = "fa-archive";
                    } else if (row.STATUS == 5) {
                        pillClass = "dms-pill dms-pill-primary";
                        icon = "fa-bullhorn";
                    } else if (row.STATUS == 6) {
                        // Waiting Acknowledgment (Related Division "Mengetahui" -
                        // SPR Level 2, request Hendra 2026-08-20): approval asli
                        // sudah selesai, masih menunggu Div Head terkait.
                        pillClass = "dms-pill dms-pill-warning";
                        icon = "fa-users";
                    }

                    // Tampilan khusus grid ini saja - "Published" tanpa "(Effective)",
                    // master data TB_M_SYSTEM tidak diubah (request Hendra 2026-08-15).
                    var statusText = (row.STATUS_DISPLAY || "").replace(" (Effective)", "");
                    // Giliran user login tampil "My Approval" - sama dengan tombol di toolbar
                    // (request Hendra 2026-11-05); filter/export tetap pakai STATUS_DISPLAY asli
                    if (isMyApproval(row)) {
                        statusText = "My Approval";
                    }
                    var $pill = $("<span>").addClass(pillClass).css("cursor", "pointer");
                    $("<i>").addClass("fa " + icon).css("fontSize", "11px").appendTo($pill);
                    $pill.append(document.createTextNode(" " + statusText));

                    // Posisi approval berjenjang dibuat ringkas di dalam pill ("2/3") +
                    // nama approver via tooltip - sengaja tidak jadi baris tambahan supaya
                    // tinggi baris grid tetap seragam (request Hendra 2026-11-05)
                    if (row.STATUS == 0 && row.CURRENT_APPROVAL_SEQ && row.TOTAL_APPROVAL_SEQ) {
                        $("<span>").addClass("dp-pill-step")
                            .text(row.CURRENT_APPROVAL_SEQ + "/" + row.TOTAL_APPROVAL_SEQ)
                            .appendTo($pill);
                        $pill.attr("title", "Step " + row.CURRENT_APPROVAL_SEQ + " of " + row.TOTAL_APPROVAL_SEQ
                            + " - waiting for " + (isMyApproval(row) ? "you (click to open Approval)" : (row.CURRENT_APPROVER_NAME || "-")));
                    }

                    // Ikon panah kecil menandakan pill ini bisa diklik untuk buka
                    // side panel (request Hendra 2026-08-15).
                    $("<i>").addClass("fa fa-chevron-circle-right").css({ fontSize: "10px", opacity: .6 }).appendTo($pill);

                    // Klik untuk lihat state approval-nya saja (badge + stepper) lewat
                    // side panel - beda dari modal "Approval List" lengkap yang dibuka
                    // lewat menu Actions > Approval (request Hendra 2026-08-15).
                    // Kalau giliran user login, klik langsung buka form Approval.
                    $pill.on("click", function (e) {
                        e.stopPropagation();
                        if (isMyApproval(row)) {
                            openMyApproval(row);
                        } else {
                            dmOpenApprovalState(row);
                        }
                    });

                    $pill.appendTo(container);
                }
            },
            {
                dataField: "P4D_STATUS_VAL",
                caption: "Registration Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var value = row.P4D_STATUS_VAL;
                    var key = (value || "").toUpperCase();

                    if (value == null || value == "") {
                        container.text("-");
                        return;
                    }

                    var pillClass = "dms-pill dms-pill-danger";
                    var icon = "fa-times-circle";

                    if (key == "DRAFT") {
                        pillClass = "dms-pill dms-pill-info";
                        icon = "fa-clock-o";
                    } else if (key == "CHECKING" || key == "PARTIALLY SENT") {
                        pillClass = "dms-pill dms-pill-warning";
                        icon = "fa-hourglass-half";
                    } else if (key == "SENT") {
                        pillClass = "dms-pill dms-pill-primary";
                        icon = "fa-paper-plane";
                    } else if (key == "ACCEPTED") {
                        pillClass = "dms-pill dms-pill-success";
                        icon = "fa-check-circle";
                    } else if (key == "DELETED") {
                        pillClass = "dms-pill dms-pill-default";
                        icon = "fa-archive";
                    }

                    // Klik untuk lihat posisi lengkap di pipeline P4D (+ rincian
                    // penerimaan per department kalau sudah Sent) lewat side panel -
                    // lihat _RegistrationStatusPanel.cshtml (request Hendra 2026-08-15).
                    var $regPill = $("<span>")
                        .addClass(pillClass)
                        .css("cursor", "pointer")
                        .html('<i class="fa ' + icon + '"></i> ' + value);

                    // Ikon panah kecil menandakan pill ini bisa diklik untuk buka
                    // side panel (request Hendra 2026-08-15).
                    $("<i>").addClass("fa fa-chevron-circle-right").css({ fontSize: "10px", opacity: .6 }).appendTo($regPill);

                    $regPill
                        .on("click", function (e) {
                            e.stopPropagation();
                            dmOpenRegistrationStatus(row);
                        })
                        .appendTo(container);
                }
            },
            {
                // Kategori dokumen QMS (Pedoman/Prosedur/SOP/Instruksi Kerja/dst,
                // dari TB_M_DOCUMENT via DCAT join di sp_DocumentMaintenance_Search)
                // - beda dari kolom "Document Type" (New/Revision) di bawah, yang
                // soal status pengajuan bukan jenis dokumennya (request Hendra
                // 2026-08-14).
                dataField: "CATEGORY_CODE",
                caption: "Category",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.CATEGORY_CODE;
                    if (!value) { container.text("-"); return; }
                    $("<span>")
                        .addClass("dms-pill dms-pill-warning")
                        .attr("title", options.data.DOCUMENT_NAME || "")
                        .text(value)
                        .appendTo(container);
                }
            },
            {
                dataField: "LEVEL_CODE",
                caption: "Document Level",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.LEVEL_CODE;
                    if (value === null || value === undefined || value === "") {
                        container.text("-");
                        return;
                    }
                    container.text("Level " + value);
                }
            },
            {
                dataField: "REVISION",
                caption: "Revision",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.REVISION;
                    if (value === null || value === undefined || value === "") {
                        container.text("-");
                        return;
                    }
                    $("<span>").addClass("dms-pill dms-pill-info").text("Rev. " + value).appendTo(container);
                }
            },
            {
                // Tanggal dokumen dibuat di sistem (bukan Document Date isian form) -
                // tampil default sejak 2026-11-04; dataType date supaya sort/filter per tanggal
                dataField: "CREATED_DT",
                caption: "Created Date",
                dataType: "date",
                alignment: "center",
                width: 150,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD MMM YYYY HH:mm") : "-");
                }
            },
            {
                dataField: "DIVISION_NAME",
                caption: "Division",
                width: 220,
                visible: false,
                calculateDisplayValue: function (row) {
                    return row.DIVISION + " - " + row.DIVISION_NAME;
                }
            },
            {
                dataField: "DEPARTMENT_NAME",
                caption: "Department",
                width: 260,
                visible: false,
                calculateDisplayValue: function (row) {
                    return row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME;
                }
            },
            {
                dataField: "DOCUMENT_TYPE",
                caption: "Submission Type",
                alignment: "center",
                visible: false,
                calculateCellValue: function (row) {
                    if (row.DOCUMENT_TYPE == "01") return "New";
                    if (row.DOCUMENT_TYPE == "02") return "Revision";
                    return row.DOCUMENT_TYPE;
                }
            },
            { dataField: "CLASSIFIED_VAL", caption: "Classified", visible: false },
            { dataField: "ITEM_CHANGED", caption: "Item Changed", visible: false },
            { dataField: "REASON", caption: "Reason", visible: false },
            { dataField: "REFERENCE_NO", caption: "Reference No", visible: false },
            { dataField: "SOURCE", caption: "Source", visible: false },
            {
                // Approver yang sedang ditunggu - disembunyikan default, bisa ditampilkan
                // lewat Show/Hide Columns untuk follow-up / filter per approver
                name: "CURRENT_APPROVER_NAME",
                caption: "Current Approver",
                visible: false,
                calculateCellValue: function (row) {
                    return row.STATUS == 0 ? (row.CURRENT_APPROVER_NAME || "") : "";
                },
                cellTemplate: function (container, options) {
                    container.text(options.value || "-");
                }
            },
            {
                // Kolom bantu untuk filter tombol "My Approval" - tidak ditampilkan
                name: "MY_APPROVAL",
                caption: "My Approval",
                visible: false,
                showInColumnChooser: false,
                calculateCellValue: function (row) { return isMyApproval(row) ? "Y" : "N"; }
            },
            { dataField: "CREATED_BY", caption: "Created By", visible: false },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
            {
                dataField: "CHANGED_DT",
                caption: "Changed Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY HH:mm:ss") : "");
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

                    container.css("white-space", "nowrap");

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

    // ---------- toggle the docked "Document Status" panel (collapses to the
    // right, grid reflows into the freed width via flex - no manual height
    // animation needed since the panel sits beside the grid, not above it) ----------
    $("#btn_toggleStats").on("click", function () {
        setStatsPanelCollapsed(!$("#dpLayoutRow").hasClass("stats-collapsed"));
    });

    $("#btn_closeStatsPanel").on("click", function () {
        setStatsPanelCollapsed(true);
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });

    // ---------- toolbar: reset ----------
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        grid.clearFilter();
        grid.clearSelection();
        setMyApprovalOnly(false);
    });

    // ---------- toolbar: My Approval (filter dokumen giliran user login) ----------
    $("#btn_myApproval").on("click", function () {
        setMyApprovalOnly(!dpMyApprovalOnly);
    });

    // ---------- link dari Dashboard / email: ?myApproval=1 atau ?DOCUMENT_CODE=xxx ----------
    var urlParams = new URLSearchParams(window.location.search);
    if (urlParams.get("myApproval") === "1") {
        setMyApprovalOnly(true);
    }
    var linkedCode = urlParams.get("DOCUMENT_CODE");
    if (linkedCode && linkedCode !== "null") {
        $("#datagrid").dxDataGrid("instance").columnOption("DOCUMENT_CODE", "filterValue", linkedCode);
    }

});

// ---------- header stats (Waiting Approval / Approved / Published / Rejected) ----------
function updateHeaderStats(rows) {
    rows = rows || [];
    var waitingApproval = 0, approved = 0, published = 0, rejected = 0;

    rows.forEach(function (r) {
        if (r.STATUS == 0 || r.STATUS == 6) waitingApproval++;
        else if (r.STATUS == 1) approved++;
        else if (r.STATUS == 5) published++;
        else if (r.STATUS == 2) rejected++;
    });

    $("#dpWaitingApproval").text(waitingApproval);
    $("#dpApproved").text(approved);
    $("#dpPublished").text(published);
    $("#dpRejected").text(rejected);

    // surfaces the pending count on the collapsed toggle too, so it
    // stays useful (not just a bare arrow) even when the panel is closed
    dpLastWaitingApproval = waitingApproval;
    refreshToggleBadge();
}

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();

    $menu.append(menuItem("dp-menu-preview", "fa-file-pdf-o", "Preview PDF", function () {
        onViewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
    }));

    $menu.append(menuItem("dp-menu-preview", "fa-file-excel-o", "Preview Excel", function () {
        onExcelPreview(row.FILE_PATH);
    }));

    var downloadLabel = "Download File";
    if (row.FILE_PATH && /\.(xlsx|xls)$/i.test(row.FILE_PATH)) downloadLabel = "Download Excel";
    else if (row.FILE_PATH && /\.pdf$/i.test(row.FILE_PATH)) downloadLabel = "Download PDF";

    $menu.append(menuItem("dp-menu-download", "fa-download", downloadLabel, function () {
        onDownloadAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
    }));

    $menu.append(menuItem("dp-menu-distribution", "fa-share-square-o", "Distribution", function () {
        onclickDistribution(row.DOCUMENT_TRANSACTION_ID);
    }));

    $menu.append(menuItem("dp-menu-history", "fa-history", "Revision History", function () {
        gHistoryDocumentCode = row.DOCUMENT_CODE;
        $("#modal-title-history").text("Document History - " + row.DOCUMENT_CODE);
        $("#datagridHistory").dxDataGrid("instance").refresh();
        $("#modalDocumentHistory").modal();
    }));

    $menu.append(menuItem("dp-menu-history", "fa-list-alt", "Activity Log", function () {
        onclickLog(row.DOCUMENT_TRANSACTION_ID);
    }));

    if (gCanDelete && row.STATUS != 1 && row.STATUS != 5) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", function () {
            var grid = $("#datagrid").dxDataGrid("instance");
            grid.selectRows([row.DOCUMENT_TRANSACTION_ID], false);
            onDeletePrepare();
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

$(document).ready(function () {
    // drag & drop upload
    var $dropzone = $('#addEditDropzone');
    var $fileInput = $('#addEditDocumentUpload');

    $dropzone.on('click', function () {
        if (!$fileInput.prop('disabled')) {
            $fileInput.trigger('click');
        }
    });

    $dropzone.on('dragover', function (e) {
        e.preventDefault();
        e.stopPropagation();
        if (!$fileInput.prop('disabled')) {
            $dropzone.addClass('dragover');
        }
    });

    $dropzone.on('dragleave drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $dropzone.removeClass('dragover');
    });

    $dropzone.on('drop', function (e) {
        if ($fileInput.prop('disabled')) return;
        var files = e.originalEvent.dataTransfer.files;
        if (files && files.length) {
            $fileInput[0].files = files;
            $fileInput.trigger('change');
        }
    });

    $fileInput.on('change', function () {
        var fileName = this.files && this.files.length ? this.files[0].name : '';
        if (fileName) {
            $('#addEditDropzoneText').html('<span class="dz-filename"><i class="fa fa-file-excel-o"></i> ' + fileName + '</span>');
        } else {
            $('#addEditDropzoneText').html('Drag &amp; drop file here<br>or <span class="dz-browse">Browse File</span>');
        }
    });

    // keep dropzone visually in sync with the (hidden) input's disabled state
    var fileInputEl = document.getElementById('addEditDocumentUpload');
    new MutationObserver(function () {
        $dropzone.toggleClass('disabled', fileInputEl.disabled);
    }).observe(fileInputEl, { attributes: true, attributeFilter: ['disabled'] });
});

function clearDocumentUpload() {
    $('#addEditDocumentUpload').val('');
    $('#addEditDropzoneText').html('Drag &amp; drop file here<br>or <span class="dz-browse">Browse File</span>');
}

$(document).ready(function () {
    $('#approvalHistoryToggle').on('click', function () {
        $(this).toggleClass('collapsed');
        $('#approvalHistoryBody').slideToggle(180, function () {
            if ($(this).is(':visible')) {
                $("#historyGrid").DataTable().columns.adjust();
            }
        });
    });
});

$(document).ready(function () {
    var $editDropzone = $('#editDropzone');
    var $editFileInput = $('#editDocumentUpload');

    $editDropzone.on('click', function () {
        $editFileInput.trigger('click');
    });

    $editDropzone.on('dragover', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $editDropzone.addClass('dragover');
    });

    $editDropzone.on('dragleave drop', function (e) {
        e.preventDefault();
        e.stopPropagation();
        $editDropzone.removeClass('dragover');
    });

    $editDropzone.on('drop', function (e) {
        var files = e.originalEvent.dataTransfer.files;
        if (files && files.length) {
            $editFileInput[0].files = files;
            $editFileInput.trigger('change');
        }
    });

    $editFileInput.on('change', function () {
        var fileName = this.files && this.files.length ? this.files[0].name : '';
        if (fileName) {
            $('#editDropzoneText').html('<i class="fa fa-file-excel-o"></i> ' + fileName);
        } else {
            $('#editDropzoneText').html('Drag &amp; drop file here or <span class="dz-browse">click to browse</span>');
        }
    });

    $('#btn_preview_filepath').on('click', function (e) {
        e.preventDefault();
        if (gFilePath) {
            onViewAttachment(gFilePath, gDocumentId);
        }
    });

    $('#btn_download_filepath').on('click', function (e) {
        e.preventDefault();
        if (gFilePath) {
            onDownloadAttachment(gFilePath, gDocumentId);
        }
    });

    $('#btn_view_history_edit').on('click', function () {
        onHistoryPrepare();
    });
});

function updateEditCurrentFileDisplay(filePath) {
    var hasFile = filePath !== null && filePath !== undefined && filePath !== "";

    $('#editCurrentFileIcon, #editCurrentFileName, #btn_preview_filepath, #btn_download_filepath').toggle(hasFile);
    $('#editCurrentFileEmpty').toggle(!hasFile);

    if (hasFile) {
        var parts = filePath.split("/");
        $('#editCurrentFileName').text(parts[parts.length - 1]);
    }
}

// Same color coding as the grid's STATUS pill (_GridView.cshtml), applied
// to the "Document Status" panel next to Document Information.
function updateEditStatusPanel(data) {
    var status = data.STATUS;
    var cls = "adf-status-danger";
    var icon = "fa-times-circle";
    var hint = "This document was rejected. Update and resubmit for approval.";

    if (status == 0) {
        cls = "adf-status-info";
        icon = "fa-clock-o";
        hint = "Draft, not yet submitted for approval.";
    } else if (status == 4) {
        cls = "adf-status-default";
        icon = "fa-archive";
        hint = "";
    }

    $('#editStatusPanel')
        .removeClass("adf-status-info adf-status-success adf-status-warning adf-status-primary adf-status-default adf-status-danger")
        .addClass(cls);
    $('#editStatusIcon').attr("class", "fa " + icon);
    $('#editStatusText').text(data.STATUS_DISPLAY || "-");
    $('#editStatusHint').text(hint);
}

//$(document).ready(function () {
//    if ($.fn.DataTable.isDataTable('#datagridDistribution')) {
//        $('#datagridDistribution').DataTable().destroy();
//    }
//    $('#datagridDistribution').dataTable({
//        "bSort": false,
//        "bDestroy": true
//    });
//});

var gDetailRow = null;

function onViewDetail(row) {
    gDetailRow = row;

    function setVal(id, v) {
        var el = $("#" + id);
        var isEmpty = (v === null || v === undefined || v === "");
        el.text(isEmpty ? "-" : v);
        el.toggleClass("dd-empty", isEmpty);
    }

    function dateVal(v) { return v ? moment(v).format("DD-MM-YYYY") : null; }

    function auditVal(by, dt) {
        var user = (by === null || by === undefined || by === "") ? "-" : by;
        var when = dt ? moment(dt).format("DD-MM-YYYY HH:mm:ss") : "-";
        return "<em>" + user + "</em> &middot; " + when;
    }

    function typeLabel(v) {
        if (v == "01") return "New";
        if (v == "02") return "Revision";
        return (v === null || v === undefined || v === "") ? null : v;
    }

    $("#modalViewDetail-subtitle").text(row.DOCUMENT_CODE ? row.DOCUMENT_CODE : "-");
    $("#vdCode").text(row.DOCUMENT_CODE ? row.DOCUMENT_CODE : "-");
    $("#vdCategoryHero").text(row.DOCUMENT_NAME ? row.DOCUMENT_NAME : "-");

    setVal("vdCategory", row.DOCUMENT_NAME);
    setVal("vdLevel", row.LEVEL_CODE);
    setVal("vdClassified", row.CLASSIFIED_VAL);
    setVal("vdProcessCode", row.PROCESS_CODE);
    setVal("vdCompanyCode", row.COMPANY_CODE);
    setVal("vdSection", row.SECTION_NAME);
    setVal("vdItemChanged", row.ITEM_CHANGED);
    setVal("vdReason", row.REASON);
    setVal("vdReferenceNo", row.REFERENCE_NO);
    setVal("vdSource", row.SOURCE);
    setVal("vdDocumentDate", dateVal(row.DOCUMENT_DATE));
    setVal("vdFirstRevisionDate", dateVal(row.DOCUMENT_REVISION_0_DATE));
    setVal("vdNextReviewDate", dateVal(row.NEXT_REVIEW_DATE));
    setVal("vdCreator", row.DOCUMENT_CREATOR);

    var type = typeLabel(row.DOCUMENT_TYPE);
    $("#vdType").html(type === null ? '<span class="dd-empty">-</span>' : type);

    $("#vdCreated").html(auditVal(row.CREATED_BY, row.CREATED_DT));
    $("#vdChanged").html(auditVal(row.CHANGED_BY, row.CHANGED_DT));

    // hero badges
    var $status = $("#vdStatusBadge");
    if (row.STATUS_DISPLAY) {
        var statusClass = "dd-badge-status-def";
        var statusIcon = "fa-times-circle";
        if (row.STATUS == 0) { statusClass = "dd-badge-status-0"; statusIcon = "fa-clock-o"; }
        else if (row.STATUS == 1) { statusClass = "dd-badge-status-1"; statusIcon = "fa-check-circle"; }
        else if (row.STATUS == 4) { statusClass = "dd-badge-status-4"; statusIcon = "fa-archive"; }
        else if (row.STATUS == 5) { statusClass = "dd-badge-status-5"; statusIcon = "fa-flag-checkered"; }
        $status.attr("class", "dd-badge " + statusClass)
            .html('<i class="fa ' + statusIcon + '"></i> ' + row.STATUS_DISPLAY)
            .show();
    } else {
        $status.hide();
    }

    var $type = $("#vdTypeBadge");
    if (type !== null) {
        $type.text(type).show();
    } else {
        $type.hide();
    }

    var $level = $("#vdLevelBadge");
    if (row.LEVEL_CODE) {
        $level.html('<i class="fa fa-star"></i> Level ' + row.LEVEL_CODE).show();
    } else {
        $level.hide();
    }

    var $classified = $("#vdClassifiedBadge");
    if (row.CLASSIFIED_VAL) {
        $classified.html('<i class="fa fa-shield"></i> ' + row.CLASSIFIED_VAL).show();
    } else {
        $classified.hide();
    }

    $("#modalViewDetail").modal();
}

$(document).ready(function () {
    $("#btnDdPreview").on("click", function () {
        if (!gDetailRow) return;
        onViewAttachment(gDetailRow.FILE_PATH, gDetailRow.DOCUMENT_TRANSACTION_ID);
    });

    $("#btnDdDownload").on("click", function () {
        if (!gDetailRow) return;
        onDownloadAttachment(gDetailRow.FILE_PATH, gDetailRow.DOCUMENT_TRANSACTION_ID);
    });

    $("#btnDdHistory").on("click", function () {
        if (!gDetailRow) return;
        gHistoryDocumentCode = gDetailRow.DOCUMENT_CODE;
        $("#modal-title-history").text("Document History - " + gDetailRow.DOCUMENT_CODE);
        $("#datagridHistory").dxDataGrid("instance").refresh();
        $("#modalViewDetail").modal("hide");
        $("#modalDocumentHistory").modal();
    });

    $("#btnDdPrint").on("click", function () {
        if (!gDetailRow || !gDetailRow.FILE_PATH) {
            showErrorMesgGrowl("This data does not have an attachment");
            return;
        }
        var link = documentMaintenanceConfig.urls.viewAttachment;
        link = link.replace("-1", gDetailRow.FILE_PATH);
        link = link.replace("-2", gDetailRow.DOCUMENT_TRANSACTION_ID);
        link = link.replace("-3", "1");
        var printWin = window.open(link, '_blank');
        if (printWin) {
            printWin.addEventListener('load', function () {
                printWin.print();
            });
        }
    });
});

var aspHeaderData = null;
var aspDetailData = null;
var aspAckData = null;
var aspRow = null;

function dmOpenApprovalState(row) {
    aspRow = row;
    $("#aspDocumentCode").text(row.DOCUMENT_CODE || "-");
    $("#approvalStatePanel").addClass("open");

    aspAckData = null;
    $("#aspAckSection").hide();
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getRelatedDivisionAckStatus,
        data: { documentTransactionId: row.DOCUMENT_TRANSACTION_ID },
        success: function (result) {
            if (result.status && result.data && result.data.length > 0) {
                aspAckData = result.data;
                renderAspAckList();
                renderAspStatusPanel();
            }
        }
    });

    if (!row.APPROVAL_ID) {
        $("#aspStepper").html('<div style="color:#98A2B3; font-size:12.5px;">No approval workflow has been started for this document yet.</div>');
        $("#aspStatusPanel").attr("class", "asp-status-panel asp-status-info");
        $("#aspStatusIcon").attr("class", "fa fa-info-circle");
        $("#aspStatusPre").text("Status");
        $("#aspStatusText").text("Not Started");
        $("#aspStatusHint").text("");
        return;
    }

    aspHeaderData = null;
    aspDetailData = null;

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getApprovalHeader,
        data: { approvalId: row.APPROVAL_ID },
        success: function (result) {
            if (result.status) {
                aspHeaderData = result.data;
                renderAspStatusPanel();
                renderAspStepper(row);
            }
        }
    });

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getApprovalDetail,
        data: { approvalId: row.APPROVAL_ID },
        success: function (result) {
            if (result.status) {
                aspDetailData = result.data;
                renderAspStatusPanel();
                renderAspStepper(row);
            }
        }
    });
}

// Sama persis logikanya dengan getApprovalStepState() di modal "Approval List"
// (_Javascript.cshtml) supaya warna/state konsisten di kedua tempat.
function aspStepState(row) {
    if (row["STATUS"] == 1) return "done";

    if (row["STATUS"] == 0) {
        if (aspHeaderData != null &&
            aspHeaderData["CURRENT_SEQ"] == row["WORKFLOW_SEQ"] &&
            aspHeaderData["APPROVAL_STATUS"] == 0) {
            return "current";
        }
        return "upcoming";
    }

    return "rejected";
}

function renderAspStatusPanel() {
    if (aspHeaderData == null) return;

    var $panel = $("#aspStatusPanel");
    $panel.removeClass("asp-status-info asp-status-success asp-status-warning asp-status-danger");

    var status = aspHeaderData["APPROVAL_STATUS"];

    // Related Division "Mengetahui" (SPR/SIPOCOR Level 2, request Hendra
    // 2026-08-21) - approval berjenjang biasa (TB_R_APPROVAL_H) bisa saja
    // sudah APPROVAL_STATUS=1 (selesai), tapi dokumennya sendiri
    // (TB_R_DOCUMENT.STATUS) masih 6 "Waiting Acknowledgment" selama ada
    // Related Division ber-role RELATED yang belum klik Acknowledge. Tanpa
    // cabang ini, panel salah bilang "APPROVED"/"completed" padahal masih
    // menggantung menunggu Div Head divisi terkait.
    var waitingAck = aspRow && aspRow["STATUS"] == 6;

    if (status == 1 && waitingAck) {
        $panel.addClass("asp-status-warning");
        $("#aspStatusIcon").attr("class", "fa fa-users");
        $("#aspStatusPre").text("Status");
        $("#aspStatusText").text("Waiting Acknowledgment");
        $("#aspStatusPosition").text("");
        $("#aspStatusHint").text("Approval is complete - waiting for related division(s) to acknowledge (\"Mengetahui\") before this document is fully approved. See Related Division Acknowledgment below.");
    } else if (status == 1) {
        $panel.addClass("asp-status-success");
        $("#aspStatusIcon").attr("class", "fa fa-check");
        $("#aspStatusPre").text("Status");
        $("#aspStatusText").text("Approved");
        $("#aspStatusPosition").text("");
        $("#aspStatusHint").text("This document has completed the approval process.");
    } else if (status == 0) {
        var currentRow = (aspDetailData || []).filter(function (r) {
            return r["WORKFLOW_SEQ"] == aspHeaderData["CURRENT_SEQ"];
        })[0];

        $panel.addClass("asp-status-warning");
        $("#aspStatusIcon").attr("class", "fa fa-clock-o");
        $("#aspStatusPre").text("Waiting For");
        $("#aspStatusText").text(currentRow ? (currentRow["APPROVER_NAME"] || "").toUpperCase() : "-");
        $("#aspStatusPosition").text(currentRow ? (currentRow["APPROVER_POSITION"] || "") : "");
        $("#aspStatusHint").text("Please review and take action.");
    } else {
        $panel.addClass("asp-status-danger");
        $("#aspStatusIcon").attr("class", "fa fa-times");
        $("#aspStatusPre").text("Status");
        $("#aspStatusText").text("Rejected");
        $("#aspStatusPosition").text("");
        $("#aspStatusHint").text("This document was rejected.");
    }
}

// Informasional saja - tombol Acknowledge SENGAJA tidak ada di sini
// (panel ini "ringkas", aksinya tetap lewat modal "Approval List" lengkap
// via menu Actions > Approval, sama seperti sebelumnya).
function renderAspAckList() {
    if (!aspAckData || aspAckData.length === 0) {
        $("#aspAckSection").hide();
        return;
    }

    var html = "";
    aspAckData.forEach(function (d) {
        var done = d["ACKNOWLEDGED_FLAG"] == 1;
        var headName = d["HEAD_FULL_NAME"] || "(Div. Head not assigned)";

        html += '<div class="asp-ack-item">' +
                    '<div>' +
                        '<div class="asp-ack-division">' + d["DIVISION_CODE"] + (d["DIVISION_NAME"] ? " - " + d["DIVISION_NAME"] : "") + '</div>' +
                        '<div class="asp-ack-head">' + headName + '</div>' +
                    '</div>' +
                    '<span class="asp-ack-pill ' + (done ? "done" : "pending") + '">' +
                        (done ? '<i class="fa fa-check"></i> ACKNOWLEDGED' : '<i class="fa fa-clock-o"></i> PENDING') +
                    '</span>' +
                '</div>';
    });

    $("#aspAckList").html(html);
    $("#aspAckSection").show();
}

// The first approval step is usually the creator, approved automatically on submit. The
// "Created" step already shows that person, so that step is not listed again as "Approved"
// (request Hendra 2026-11-10).
function isCreatorSelfApproval(detailRow, sortedDetail, header) {
    return header != null && sortedDetail.length > 0 && detailRow === sortedDetail[0]
        && detailRow["STATUS"] == 1 && detailRow["APPROVER"] != null
        && detailRow["APPROVER"] === header["CREATOR"];
}

function renderAspStepper(row) {
    if (aspHeaderData == null || aspDetailData == null) return;

    var steps = [];
    steps.push({
        title: "Created",
        sub: aspHeaderData["CREATOR_NAME"] || "",
        position: aspHeaderData["CREATOR_POSITION"] || "",
        date: row.CREATED_DT,
        state: "done"
    });

    var sortedDetail = aspDetailData.slice().sort(function (a, b) {
        return a["WORKFLOW_SEQ"] - b["WORKFLOW_SEQ"];
    });

    sortedDetail.forEach(function (d) {
        if (isCreatorSelfApproval(d, sortedDetail, aspHeaderData)) return;
        var state = aspStepState(d);
        var title = state === "done" ? "Approved"
            : state === "current" ? "Waiting"
            : state === "rejected" ? "Rejected"
            : "Pending";

        steps.push({ title: title, sub: d["APPROVER_NAME"], position: d["APPROVER_POSITION"] || "", date: d["CHANGED_DT"], state: state });
    });

    var iconByState = { done: "fa-check", current: "fa-clock-o", rejected: "fa-times", upcoming: "" };

    var html = "";
    steps.forEach(function (step, idx) {
        var dateHtml = step.date ? '<div class="stepper-date">' + moment(step.date).format("DD-MM-YYYY HH:mm") + "</div>" : "";
        var positionHtml = step.position ? '<div class="stepper-position">' + step.position + "</div>" : "";
        var iconHtml = iconByState[step.state] ? '<i class="fa ' + iconByState[step.state] + '"></i>' : "";
        var badgeHtml = step.state === "current" ? '<div class="stepper-current-badge">Current Step</div>' : "";
        var connectorState = (step.state == "done" || step.state == "rejected") ? step.state : "upcoming";

        html += '<div class="stepper-step stepper-' + step.state + '">' +
                    '<div>' +
                        '<div class="stepper-icon">' + iconHtml + '</div>' +
                        (idx < steps.length - 1 ? '<div class="stepper-connector-v ' + connectorState + '"></div>' : '') +
                    '</div>' +
                    '<div class="stepper-body">' +
                        '<div class="stepper-title">' + (idx + 1) + ". " + step.title + "</div>" +
                        '<div class="stepper-sub">' + (step.sub || "") + "</div>" +
                        positionHtml +
                        dateHtml +
                        badgeHtml +
                    "</div>" +
                "</div>";
    });

    $("#aspStepper").html(html);
}

$(document).ready(function () {
    $("#btn_closeApprovalStatePanel").on("click", function () {
        $("#approvalStatePanel").removeClass("open");
    });

    $(document).on("click", function (e) {
        if ($("#approvalStatePanel").hasClass("open") && !$(e.target).closest("#approvalStatePanel").length) {
            $("#approvalStatePanel").removeClass("open");
        }
    });
});

// Urutan tahap normal P4D (lihat DOC_CTRL_STATUS di TB_M_SYSTEM):
// Draft(0) -> Checking(1) -> Accepted(2) -> Sent(5)/Partially Sent(6).
// Rejected(3) dan belum-terdaftar ditangani terpisah di bawah.
// Cuma tampilkan alur proses saja, tanpa rincian distribusi per department -
// sudah cukup untuk kebutuhan grid ini (request Hendra 2026-08-15).
var RSP_STAGES = ["DRAFT", "CHECKING", "ACCEPTED", "SENT"];
var RSP_STAGE_LABELS = { "DRAFT": "Draft", "CHECKING": "Checking", "ACCEPTED": "Accepted", "SENT": "Sent" };

function dmOpenRegistrationStatus(row) {
    $("#rspDocumentCode").text(row.DOCUMENT_CODE || "-");
    $("#registrationStatusPanel").addClass("open");

    var value = (row.P4D_STATUS_VAL || "").toUpperCase();
    renderRspStages(value);
}

function renderRspStages(value) {
    var $wrap = $("#rspStages").empty();

    if (!value) {
        $wrap.append('<div class="rsp-empty">This document has not been registered to P4D yet.</div>');
        return;
    }

    if (value == "DELETED") {
        $wrap.append('<div class="rsp-empty">This registration entry has been deleted.</div>');
        return;
    }

    if (value == "REJECTED") {
        $wrap.append(
            '<div class="rsp-stage rsp-rejected" style="flex:none; width:100%;">' +
                '<div class="rsp-stage-icon"><i class="fa fa-times"></i></div>' +
                '<div class="rsp-stage-label">Rejected by QMS</div>' +
            '</div>'
        );
        return;
    }

    // "PARTIALLY SENT" secara tahapan sama dengan "SENT" (distribusi sudah
    // mulai jalan), bedanya cuma rincian per-department di bawah.
    var normalized = value == "PARTIALLY SENT" ? "SENT" : value;
    var currentIdx = RSP_STAGES.indexOf(normalized);
    if (currentIdx < 0) currentIdx = 0;

    RSP_STAGES.forEach(function (stage, idx) {
        var state = idx < currentIdx ? "done" : (idx == currentIdx ? "current" : "upcoming");
        var icon = state == "done" ? "fa-check" : (state == "current" ? "fa-hourglass-half" : "");

        $wrap.append(
            '<div class="rsp-stage rsp-' + state + '">' +
                '<div class="rsp-stage-icon">' + (icon ? '<i class="fa ' + icon + '"></i>' : (idx + 1)) + '</div>' +
                '<div class="rsp-stage-label">' + RSP_STAGE_LABELS[stage] + '</div>' +
            '</div>'
        );

        if (idx < RSP_STAGES.length - 1) {
            var connectorDone = idx < currentIdx ? "done" : "";
            $wrap.append('<div class="rsp-connector-wrap"><div class="rsp-connector ' + connectorDone + '"></div></div>');
        }
    });

    if (value == "PARTIALLY SENT") {
        $wrap.append('<div class="rsp-empty" style="flex-basis:100%; padding-top:12px;">Partially Sent - not all target departments have been sent yet.</div>');
    }
}

$(document).ready(function () {
    $("#btn_closeRegistrationStatusPanel").on("click", function () {
        $("#registrationStatusPanel").removeClass("open");
    });

    $(document).on("click", function (e) {
        if ($("#registrationStatusPanel").hasClass("open") && !$(e.target).closest("#registrationStatusPanel").length) {
            $("#registrationStatusPanel").removeClass("open");
        }
    });
});

//RECOMMIT DINA
//RECOMMIT DINA 2
var gScreenMode = null;
var gId = null;
var gDocumentId = null;
var gDocumentTypeId = null;
var gDocumentNo = null;
var gIsApproved = null;
var gFilePath = null;
var gApprovalId = null;
var approvalSeq = null;
var approver = null;
var gDocumentName = null;
var gDocumentDepartment = null;
var gDocumentClassified = null;
var gDocumentCreatedDate = null;
var gApprovalMode = null;
var gDocumentLevel = null;

// Level 4 (Checksheet / Form): approval without signatures; the server applies the same rule.
function isUnsignedLevel(level) {
    return String(level) === "4";
}
var gImpactOther = null;
var gReffNo = null;
var gSourceNo = null;
var documentLevel = null;
var gAttachmentPath = null;
var approvalDetailData = null;
var gTemplateCodes = documentMaintenanceConfig.templateCodes;
var SESSION_POSITION_ID = documentMaintenanceConfig.sessionPositionId;
var SESSION_DIVISION = documentMaintenanceConfig.sessionDivision;
var SESSION_DIVISION_NAME = documentMaintenanceConfig.sessionDivisionName;
var SESSION_DEPARTMENT_ID = documentMaintenanceConfig.sessionDepartmentId;
var SESSION_DEPARTMENT_CODE = documentMaintenanceConfig.sessionDepartmentCode;
var SESSION_DEPARTMENT_NAME = documentMaintenanceConfig.sessionDepartmentName;
var SESSION_SECTION_ID = documentMaintenanceConfig.sessionSectionId;
var SESSION_SECTION_CODE = documentMaintenanceConfig.sessionSectionCode;
var SESSION_SECTION_NAME = documentMaintenanceConfig.sessionSectionName;

$(document).ready(function () {
    //Updated By Arie (2023-08-24)
    setDataSelect2("addEditDocumentNo2", documentMaintenanceConfig.urls.getDocumentNoAllowedRevision, "- Choose -");
    $('#addEditDocumentNo2').data('select2').$container.addClass('hide');
    setDataSelect2WithParam("addEditDocumentLevel", documentMaintenanceConfig.urls.mSystemGetSystemCodeBySystemType, "- Choose -", "LEVEL_CODE");

    setDataSelect2WithParam("addEditDocumentType", documentMaintenanceConfig.urls.systemValues, "- Choose -", "DOCUMENT_TYPE");
    setDataSelect2("addEditDocumentCode", documentMaintenanceConfig.urls.getDocumentCodeByLevel, "- Choose -");
    setDataSelect2WithParam("addEditClassified", documentMaintenanceConfig.urls.systemValues, "- Choose -", "CLASSIFIED");
    setDataSelect2WithParam("addEditProcessCode", documentMaintenanceConfig.urls.systemValues, "- Choose -", "PROCESS_CODE");
    setDataSelect2WithParam("addEditCompanyCode", documentMaintenanceConfig.urls.systemValues, "- Choose -", "COMPANY_CODE");
    setDataSelect2WithParam("addEditDivision", documentMaintenanceConfig.urls.systemValues, "- Choose -", "DIVISION");
    setDataSelect2WithParam("addEditDepartment", documentMaintenanceConfig.urls.departmentMasterGetDepartmentIdAndNameByDivision, "- Choose -", "");
    setDataSelect2WithParam("addEditDocumentCreator", documentMaintenanceConfig.urls.userGetUserNameByDepartmentId, "- Choose -", "");
    setDataSelect2("addEditSection", documentMaintenanceConfig.urls.sectionMasterGetSectionCodeAndName, "- Choose -");

    $("#btn_add").on("click", function () {
        onAddPrepare();
    });

    initDownloadTemplateDropdown();

    $("#btn_templateCenter").on("click", function () {
        openTemplateCenter();
    });

    $("#tplSearchInput").on("keyup", function () {
        renderTemplateList(gTemplateListCache);
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

    $("#btn_saveEdit").on("click", function () {
        confirmAddEditData();
    });

    $("#btn_cancel").on("click", function () {
        $('#addEditDocumentType').val("").trigger("change");
        reset();
    });

    //$("#btn_delete").on("click", function () {
    //    onDeletePrepare();
    //});

    //$("#btn_edit").on("click", function () {
    //    onEditPrepare();
    //});

    $("#btn_download").on("click", function () {
        onDownload();
    });

    $("#btn_approval").on("click", function () {
        onApprovalPrepare();
    });

    // Tombol di banner status popup Approval List (hanya tampil saat giliran user login)
    $("#btn_approvalReview").on("click", function () {
        var currentRow = getCurrentApprovalRow();
        if (currentRow != null) {
            onApprovalForm(currentRow["WORKFLOW_SEQ"], currentRow["APPROVER"]);
        }
    });

    $("#btn_approve").on("click", function () {
        gApprovalMode = "approve";
        onApproveRejectPrepare();
    });

    $("#btn_reject").on("click", function () {
        gApprovalMode = "reject";
        onApproveRejectPrepare();
    });

    $("#btn_download_pengesahan").on("click", function () {
        downloadPengesahan();
    });

    // Manual numbering (Jul 2026): refresh prefix preview kalau salah satu
    // field yang membentuk prefix berubah, sementara mode Manual aktif.
    $('#addEditDocumentLevel, #addEditDocumentCode, #addEditDivision, #addEditDepartment, #addEditSection, #addEditProcessCode, #addEditCompanyCode').on('change', function () {
        refreshManualNoPrefix();
    });

    // Fitur "Divisi Terkait" (awalnya SPR/SIPOCOR Level 2 only, request Hendra
    // 2026-08-20; diperluas ke SEMUA Document Code di Level 2, request Hendra
    // 2026-08-22) - muncul kalau Document Level = 2, apapun Document Code-nya.
    // CATATAN: print/export PDF (WriteRelatedDivisionSection) MASIH khusus
    // DOCUMENT_ID==15/SPR (koordinat sel di-hardcode ke template SPR2.xlsx) -
    // tipe dokumen lain bisa isi Divisi Terkait & tersimpan, tapi belum
    // tercetak di PDF sampai template lain disesuaikan.
    $('#addEditDocumentLevel, #addEditDocumentCode').on('change', function () {
        updateRelatedDivisionVisibility();
    });

    // Pratinjau approval chain (2026-11-06) - ikut berubah saat Level,
    // Document Code (kategori), atau Creator berubah.
    $('#addEditDocumentLevel, #addEditDocumentCode, #addEditDocumentCreator').on('change', function () {
        scheduleApprovalPreview();
    });
    $('#addEditDivision').on('change', function () {
        lockCreatorDivisionRole();
    });

    // Tutup dropdown peran Related Division kalau klik di luar tombolnya
    // (request Hendra 2026-08-20 revisi ke dropdown) - dibind sekali di
    // sini, bukan di dalam renderRelatedDivisionCheckboxes(), supaya tidak
    // dobel-bind tiap kali tile di-render ulang.
    $(document).on('click', function () { closeRelatedDivisionMenus(); });
    $('#addEditDate').on('change blur', function () {
        refreshManualNoPrefix();
    });

    // Fokus lebih jelas untuk field wajib (Jul 2026) - select2 menyembunyikan
    // <select> aslinya di balik widget sendiri, jadi CSS :required:focus
    // biasa tidak kena. Tandai manual saat widget-nya dibuka/ditutup.
    $('#modalAddEdit').on('select2:open', function (e) {
        var $select = $(e.target);
        if ($select.prop('required')) {
            $select.data('select2').$container.find('.select2-selection').addClass('required-focus');
        }
    });
    $('#modalAddEdit').on('select2:close', function (e) {
        var $select = $(e.target);
        var instance = $select.data('select2');
        if (instance) {
            instance.$container.find('.select2-selection').removeClass('required-focus');
        }
    });
});

function onclickClose() {
    $('#datagridApproval').DataTable({
        "bDestroy": true
    });
}

//update approval - Hafidz Ezio
function onApprovalPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gId = row.DOCUMENT_TRANSACTION_ID;
    gDocumentNo = row.DOCUMENT_CODE;
    gDocumentName = row.DOCUMENT_TRANSACTION_NAME;
    gDocumentDepartment = row.DEPARTMENT_NAME;
    gDocumentClassified = row.CLASSIFIED_VAL;
    gDocumentCreatedDate = row.CREATED_DT;
    gApprovalId = row.APPROVAL_ID;
    gDocumentLevel = row.LEVEL_CODE;

    getApprovalHeader();
    getApprovalDetail();
    getApprovalHistoryDetail();
    getRelatedDivisionAckStatus();
    $("#modalApproval").modal();
}

function getApprovalHeader() {
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getApprovalHeader,
        async: false,
        data: {
            approvalId: gApprovalId
        },
        success: function (result) {
            if (result.status == true) {
                approvalHeaderData = result.data;

                $('#approvalDocumentNo').html(gDocumentNo);
                $('#approvalDocumentName').html(gDocumentName);
                $('#approvalDocumentDepartment').html(gDocumentDepartment);
                $('#approvalDocumentClassified').html(gDocumentClassified);
                $('#approvalCreator').html(approvalHeaderData["CREATOR_NAME"]);
                $('#approvalCreatedDate').html(gDocumentCreatedDate ? moment(gDocumentCreatedDate).format("DD MMM YYYY HH:mm") : "-");

                $('#btn_download_pengesahan').toggle(approvalHeaderData["APPROVAL_STATUS"] == 1);
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

// Satu sumber kebenaran untuk status per baris approval detail - dipakai
// stepper, tabel Approval Route, dan panel Approval Status supaya warnanya
// konsisten. "current" = ini langkah aktif (WORKFLOW_SEQ == CURRENT_SEQ,
// header masih berjalan); "upcoming" = STATUS 0 tapi belum giliran.
function getApprovalStepState(row) {
    if (row["STATUS"] == 1) return "done";

    if (row["STATUS"] == 0) {
        if (approvalHeaderData != null &&
            approvalHeaderData["CURRENT_SEQ"] == row["WORKFLOW_SEQ"] &&
            approvalHeaderData["APPROVAL_STATUS"] == 0) {
            return "current";
        }
        return "upcoming";
    }

    return "rejected";
}

function approvalStatusPillHtml(state) {
    var cls = "dms-pill-info", icon = "fa-hourglass-half", text = "Pending";

    if (state === "done") { cls = "dms-pill-success"; icon = "fa-check"; text = "Approved"; }
    else if (state === "current") { cls = "dms-pill-warning"; icon = "fa-clock-o"; text = "Waiting"; }
    else if (state === "rejected") { cls = "dms-pill-danger"; icon = "fa-times"; text = "Rejected"; }

    return '<span class="dms-pill ' + cls + '"><i class="fa ' + icon + '"></i> ' + text + '</span>';
}

// Baris approval yang sedang aktif (null kalau approval sudah selesai/ditolak)
function getCurrentApprovalRow() {
    if (approvalHeaderData == null || approvalHeaderData["APPROVAL_STATUS"] != 0) return null;
    return (approvalDetailData || []).filter(function (r) {
        return r["WORKFLOW_SEQ"] == approvalHeaderData["CURRENT_SEQ"];
    })[0] || null;
}

function isMyApprovalTurn() {
    var currentRow = getCurrentApprovalRow();
    return currentRow != null && currentRow["APPROVER"] === gCurrentUsername;
}

function escapeApprovalHtml(value) {
    return $("<div>").text(value == null ? "" : value).html();
}

// Banner status di atas popup Approval List - dipanggil setelah
// approvalHeaderData & approvalDetailData dua-duanya siap. Tombol
// Approve/Reject hanya muncul kalau sekarang giliran user login.
function updateApprovalStatusPanel() {
    if (approvalHeaderData == null) return;

    var $panel = $('#approvalStatusPanel');
    $panel.removeClass('adf-status-info adf-status-success adf-status-mine adf-status-danger');
    $('#btn_approvalReview').hide();

    var status = approvalHeaderData["APPROVAL_STATUS"];
    var total = (approvalDetailData || []).length;

    if (status == 1) {
        $panel.addClass('adf-status-success');
        $('#approvalStatusIcon').attr('class', 'fa fa-check');
        $('#approvalStatusText').text('Approved');
        $('#approvalStatusHint').text('All ' + total + ' approval steps have been completed.');
    } else if (status == 0) {
        var currentRow = getCurrentApprovalRow();
        var stepText = 'Step ' + approvalHeaderData["CURRENT_SEQ"] + ' of ' + total;

        if (isMyApprovalTurn()) {
            $panel.addClass('adf-status-mine');
            $('#approvalStatusIcon').attr('class', 'fa fa-bell');
            $('#approvalStatusText').text('Waiting for your approval');
            $('#approvalStatusHint').text(stepText + ' - please review this document and approve or reject it.');
            $('#btn_approvalReview').show();
        } else {
            $panel.addClass('adf-status-info');
            $('#approvalStatusIcon').attr('class', 'fa fa-clock-o');
            $('#approvalStatusText').text('Waiting for ' + (currentRow ? currentRow["APPROVER_NAME"] : '-'));
            $('#approvalStatusHint').text(stepText + (currentRow && currentRow["LABEL"] ? ' - ' + currentRow["LABEL"] : ''));
        }
    } else {
        $panel.addClass('adf-status-danger');
        $('#approvalStatusIcon').attr('class', 'fa fa-times');
        $('#approvalStatusText').text('Rejected');
        $('#approvalStatusHint').text('This document was rejected. See the remark in the approval flow below.');
    }
}

function getApprovalDetail() {
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getApprovalDetail,
        async: false,
        data: {
            approvalId: gApprovalId
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;
                approvalDetailData = data;
                renderApprovalStepper();
                updateApprovalStatusPanel();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function getApprovalHistoryDetail() {
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getApprovalHistoryDetail,
        async: false,
        data: {
            MENU_ID: "M00006-01",
            TRANSACTION_ID: gId
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;
                $("#approvalHistoryCount").text(data && data.length ? "(" + data.length + ")" : "");

                $("#historyGrid").dataTable().fnDestroy();
                $('#historyGrid').DataTable({
                    "ordering": false,
                    "oLanguage": { "sEmptyTable": "Data not found." },
                    "scrollCollapse": false,
                    "paging": false,
                    "info": false,
                    "searching": false,
                    "data": data,
                    "columns": [
                        {
                            render: function (data, type, row) {
                                var approved = row["STATUS"] == 1;
                                return '<div class="history-approver">' +
                                    '<div class="history-icon' + (approved ? '' : ' rejected') + '"><i class="fa ' + (approved ? 'fa-check' : 'fa-times') + '"></i></div>' +
                                    '<div><div class="history-name">' + row["APPROVER_NAME"] + '</div>' +
                                    '<div class="history-role">' + (row["LABEL"] || "") + '</div></div>' +
                                    '</div>';
                            }
                        },
                        {
                            render: function (data, type, row) {
                                var approved = row["STATUS"] == 1;
                                var cls = approved ? "dms-pill-success" : "dms-pill-danger";
                                var icon = approved ? "fa-check" : "fa-times";
                                return '<span class="dms-pill ' + cls + '"><i class="fa ' + icon + '"></i> ' + (approved ? "Approved" : "Rejected") + '</span>';
                            }
                        },
                        {
                            render: function (data, type, row) {
                                return row["CHANGED_DT"]
                                    ? moment(row["CHANGED_DT"]).format("DD-MM-YYYY HH:mm:ss")
                                    : '<span class="cell-muted">&ndash;</span>';
                            }
                        },
                        {
                            render: function (data, type, row) {
                                return row["REMARK"] ? row["REMARK"] : '<span class="cell-muted">&ndash;</span>';
                            }
                        },
                    ],
                    "columnDefs": [
                        { "targets": [1], "className": "text-center" },
                        { "width": "150px", "targets": 2 },
                    ]
                });

                $('#modalApproval').on('shown.bs.modal', function () {
                    $("#historyGrid").DataTable().columns.adjust().draw();
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

// Related Division Acknowledgment (SPR/SIPOCOR Level 2 only, request Hendra
// 2026-08-20) - TERPISAH dari Approval Route/History di atas: Div Head cuma
// konfirmasi "Mengetahui", bukan approval berjenjang (tidak ada opsi reject,
// tidak menyentuh TB_R_APPROVAL_D/H sama sekali). Section disembunyikan
// kalau dokumen ini tidak punya Related Division (data kosong).
var gCurrentUsername = documentMaintenanceConfig.sessionUsername;

function getRelatedDivisionAckStatus() {
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getRelatedDivisionAckStatus,
        data: { documentTransactionId: gId },
        success: function (result) {
            if (result.status == true && result.data && result.data.length > 0) {
                $("#relatedDivisionAckSection").show();
                renderRelatedDivisionAckGrid(result.data);
            } else {
                $("#relatedDivisionAckSection").hide();
            }
        }
    });
}

function renderRelatedDivisionAckGrid(data) {
    $("#relatedDivisionAckGrid").dataTable().fnDestroy();
    $("#relatedDivisionAckGrid").DataTable({
        "ordering": false,
        "oLanguage": { "sEmptyTable": "Data not found." },
        "scrollCollapse": false,
        "paging": false,
        "info": false,
        "searching": false,
        "data": data,
        "columns": [
            {
                render: function (data, type, row) {
                    return '<span class="approver-name">' + row["DIVISION_CODE"] + (row["DIVISION_NAME"] ? " - " + row["DIVISION_NAME"] : "") + '</span>';
                }
            },
            {
                render: function (data, type, row) {
                    return row["HEAD_FULL_NAME"] || '<span class="cell-muted">(Div. Head not assigned)</span>';
                }
            },
            {
                render: function (data, type, row) {
                    return row["ACKNOWLEDGED_FLAG"] == 1
                        ? '<span class="dms-pill dms-pill-success"><i class="fa fa-check"></i> Acknowledged</span>'
                        : '<span class="dms-pill dms-pill-warning"><i class="fa fa-clock-o"></i> Pending</span>';
                }
            },
            {
                render: function (data, type, row) {
                    return row["ACKNOWLEDGED_DT"]
                        ? moment(row["ACKNOWLEDGED_DT"]).format("DD-MM-YYYY HH:mm:ss")
                        : '<span class="cell-muted">&ndash;</span>';
                }
            },
            {
                render: function (data, type, row) {
                    if (row["ACKNOWLEDGED_FLAG"] != 1 && row["HEAD_USERNAME"] === gCurrentUsername) {
                        return '<button class="btn-review" onClick="onAcknowledgeRelatedDivisionPrepare(\'' + row["DIVISION_CODE"] + '\')"><i class="fa fa-check"></i> Acknowledge</button>';
                    }
                    return '<span class="cell-muted">&ndash;</span>';
                }
            }
        ],
        "columnDefs": [
            { "targets": [2], "className": "text-center" },
            { "width": "150px", "targets": 3 },
            { "width": "130px", "targets": 4, "className": "text-center" }
        ]
    });

    $('#modalApproval').on('shown.bs.modal', function () {
        $("#relatedDivisionAckGrid").DataTable().columns.adjust().draw();
    });
}

// "Mengetahui" wajib tanda tangan elektronik (2026-11-06)
function onAcknowledgeRelatedDivisionPrepare(divisionCode) {
    if (isUnsignedLevel(gDocumentLevel)) {
        showConfirmMesg("Acknowledge this document on behalf of division " + divisionCode + "?", function () { onAcknowledgeRelatedDivision(divisionCode, null); }, "Confirmation", "Acknowledge", "Cancel");
        return;
    }
    openSignaturePad({
        title: "Sign to acknowledge",
        subtitle: "Acknowledge this document on behalf of division " + divisionCode + ".",
        confirmText: "Sign & Acknowledge",
        onSigned: function (sig) { onAcknowledgeRelatedDivision(divisionCode, sig); }
    });
}

function onAcknowledgeRelatedDivision(divisionCode, signature) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.acknowledgeRelatedDivision,
        data: {
            documentTransactionId: gId,
            divisionCode: divisionCode,
            remark: "",
            SIGN_METHOD: signature ? signature.SIGN_METHOD : "",
            SIGN_DATA: signature ? signature.SIGN_DATA : "",
            SIGN_FONT: signature ? (signature.SIGN_FONT || "") : ""
        },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                getRelatedDivisionAckStatus();
                getApprovalHeader();
                getApprovalDetail();
                getApprovalHistoryDetail();
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

// Timeline vertikal "Approval Flow" (request Hendra 2026-11-05) - gabungan
// stepper + tabel Approval Route yang dulu terpisah: satu baris per langkah
// (Created + tiap approver) berisi posisi, nama, status, tanggal & remark.
// State per langkah dari getApprovalStepState() supaya sama dengan side panel.
function renderApprovalStepper() {
    if (approvalHeaderData == null || approvalDetailData == null) {
        return;
    }

    var steps = [];
    steps.push({
        stepLabel: "Created",
        name: approvalHeaderData["CREATOR_NAME"] || "-",
        date: gDocumentCreatedDate,
        state: "done",
        pill: '<span class="dms-pill dms-pill-default"><i class="fa fa-pencil"></i> Submitted</span>'
    });

    var sortedDetail = approvalDetailData.slice().sort(function (a, b) {
        return a["WORKFLOW_SEQ"] - b["WORKFLOW_SEQ"];
    });

    sortedDetail.forEach(function (row) {
        if (isCreatorSelfApproval(row, sortedDetail, approvalHeaderData)) return;
        var state = getApprovalStepState(row);
        var mine = state === "current" && row["APPROVER"] === gCurrentUsername;

        steps.push({
            stepLabel: "Step " + row["WORKFLOW_SEQ"] + (row["LABEL"] ? " · " + row["LABEL"] : ""),
            name: row["APPROVER_NAME"] + (mine ? " (You)" : ""),
            date: state === "done" || state === "rejected" ? row["CHANGED_DT"] : null,
            remark: row["REMARK"],
            state: state,
            mine: mine,
            seq: row["WORKFLOW_SEQ"],
            pill: mine
                ? '<span class="dms-pill dp-pill-my-approval"><i class="fa fa-bell"></i> My Approval</span>'
                : approvalStatusPillHtml(state)
        });
    });

    var iconByState = {
        done: '<i class="fa fa-check"></i>',
        current: '<i class="fa fa-clock-o"></i>',
        rejected: '<i class="fa fa-times"></i>'
    };

    var html = "";
    steps.forEach(function (step, idx) {
        var dot = step.mine ? '<i class="fa fa-bell"></i>'
            : (iconByState[step.state] || idx);
        var dateHtml = step.date ? '<div class="adf-flow-date">' + moment(step.date).format("DD MMM YYYY HH:mm") + '</div>' : '';
        var remarkHtml = step.remark
            ? '<div class="adf-flow-remark"><i class="fa fa-comment-o"></i>' + escapeApprovalHtml(step.remark) + '</div>'
            : '';

        html += '<div class="adf-flow-item ' + step.state + (step.mine ? ' mine' : '') + '">' +
                    '<div class="adf-flow-dot">' + dot + '</div>' +
                    '<div class="adf-flow-main">' +
                        '<div class="adf-flow-head">' +
                            '<div class="adf-flow-who">' +
                                '<div class="adf-flow-step">' + escapeApprovalHtml(step.stepLabel) + '</div>' +
                                '<div class="adf-flow-name">' + escapeApprovalHtml(step.name) + '</div>' +
                            '</div>' +
                            '<div class="adf-flow-right">' + step.pill + dateHtml + '</div>' +
                        '</div>' +
                        remarkHtml +
                    '</div>' +
                '</div>';
    });

    $("#approvalFlow").html(html);
}

function onApprovalForm(approval_seq, approver_name) {
    approvalSeq = approval_seq;
    approver = approver_name;
    $("#approvalRemark").val("");
    $("#modalApprovalForm").modal();
}

function maxLengthCheck(object) {
    if (object.value.length > object.maxLength)
        object.value = object.value.slice(0, object.maxLength)
}

function onDownload() {
    download();
}

function download() {
    var link = documentMaintenanceConfig.urls.downloadReportRaw;
    var param = '?DOCUMENT_CODE=null&DOCUMENT_TRANSACTION_NAME=null&DOCUMENT_YEAR=null';

    window.location.href = link + param;
}

var gTemplateListCache = null;

function initDownloadTemplateDropdown() {
    if (!gTemplateCodes || gTemplateCodes.length === 0) return;
    $('#btn_templateCenter').css('display', 'inline-block');
}

function openTemplateCenter() {
    $('#tplSearchInput').val('');
    $('#modalTemplateCenter').modal('show');

    if (gTemplateListCache) {
        renderTemplateList(gTemplateListCache);
        return;
    }

    $.ajax({
        url: documentMaintenanceConfig.urls.getTemplateList,
        type: 'GET',
        success: function (result) {
            if (result.status) {
                gTemplateListCache = result.data;
                renderTemplateList(gTemplateListCache);
            }
        }
    });
}

function renderTemplateList(data) {
    var $tbody = $('#tplTableBody');
    $tbody.empty();

    if (!data || data.length === 0) {
        $('#tplEmptyState').show();
        return;
    }

    var keyword = ($('#tplSearchInput').val() || '').toLowerCase();
    var filtered = data.filter(function (row) {
        return row.code.toLowerCase().indexOf(keyword) !== -1 || (row.name || '').toLowerCase().indexOf(keyword) !== -1;
    });

    if (filtered.length === 0) {
        $('#tplEmptyState').show();
        return;
    }
    $('#tplEmptyState').hide();

    filtered.forEach(function (row) {
        var $tr = $('<tr></tr>');
        $tr.append($('<td></td>').append(
            $('<span class="tpl-doc-code"></span>')
                .append('<i class="fa fa-file-excel-o"></i>')
                .append($('<span></span>').text(row.code))
        ));
        $tr.append($('<td></td>').text(row.name));
        $tr.append($('<td class="tpl-format"></td>').text(row.format));
        $tr.append($('<td class="tpl-size"></td>').text(row.sizeKb + ' KB'));

        var $btn = $('<button type="button" class="tpl-btn-download"><i class="fa fa-download"></i> Download</button>');
        $btn.on('click', function () { onDownloadTemplate(row.code); });
        $tr.append($('<td></td>').append($btn));

        $tbody.append($tr);
    });
}

function onDownloadTemplate(code) {
    var link = documentMaintenanceConfig.urls.documentMasterDownloadTemplate;
    window.location.href = link.replace("-1", code);
}

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Document");
    $('#addEditDocumentType').val("").trigger("change");

    reset();
    $('#addEditApprovalPreview').hide().empty();
    $('#addEditImpactPanel, #addEditFileCheck').hide().empty();

    gFilePath = null;

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditDocumentType').focus();
    });

}

function generateDocumentNo() {

    //Updated By Arie (2023-08-24)
    $("#addEditDocumentNo").val("AUTOMATIC");
}

// Manual numbering (Jul 2026): toggle antara nomor otomatis ("Generated",
// behavior lama) dan nomor manual - user cuma isi 3 digit terakhir,
// prefix (mis. "ITD/SOP-APP-02/") tetap dibentuk dari master seperti biasa.
function onChangeNumberingMode() {
    var isManual = $('#addEditNumberingModeManual').is(':checked');

    if (isManual) {
        $('#addEditDocumentNo').addClass('hide');
        $('#addEditManualNoGroup').removeClass('hide');
        refreshManualNoPrefix();
    } else {
        $('#addEditDocumentNo').removeClass('hide');
        $('#addEditManualNoGroup').addClass('hide');
        $('#addEditManualSeqNo').val('');
    }
}

// ---------- "Related Division" (awalnya SPR/SIPOCOR Level 2 only, request
// Hendra 2026-08-20; diperluas ke semua Document Code di Level 2, request
// Hendra 2026-08-22) - creator pilih divisi lain yang harus "Mengetahui"
// dokumen ini. Daftar divisinya diambil dari Division Master (bukan dropdown
// Division biasa di form ini - dua sumber itu tidak sinkron), divisi milik
// dokumen sendiri (Main PIC) ditandai & tidak bisa dipilih. ----------
var gRelatedDivisionOptions = null;
var gRelatedDivisionExpanded = false;
// Tile sekarang cuma kode (tanpa nama), jadi 1 baris muat 6 - limit
// dinaikkan ke 18 supaya 17 divisi saat ini muat semua tanpa "Show more"
// (request Hendra 2026-08-20); mekanismenya tetap ada utk jaga-jaga kalau
// daftar divisi bertambah banyak nanti.
var RELATED_DIVISION_VISIBLE_LIMIT = 18;

function updateRelatedDivisionVisibility() {
    var level = $('#addEditDocumentLevel').val();

    var isLevel2 = (level == '2');

    if (isLevel2) {
        $('#adfSectionRelatedDivision').show();
        loadRelatedDivisionCheckboxes();
    } else {
        $('#adfSectionRelatedDivision').hide();
        $('#relatedDivisionCheckboxes').empty();
    }
}

function loadRelatedDivisionCheckboxes() {
    if (gRelatedDivisionOptions !== null) {
        renderRelatedDivisionCheckboxes();
        return;
    }

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getRelatedDivisionOptions,
        success: function (result) {
            if (result.status) {
                gRelatedDivisionOptions = result.data;
                renderRelatedDivisionCheckboxes();
            }
        }
    });
}

// Label & ikon peran per tile (request Hendra 2026-08-20, revisi jadi
// dropdown) - MAIN_PIC boleh lebih dari satu divisi, RELATED wajib
// Acknowledge ("Mengetahui"). NOTE_RELATED (peran ke-3, cuma informasi)
// SENGAJA DIHAPUS lagi (request Hendra 2026-08-20 revisi ke-3) - karena
// sudah ada peran default "Not Related (-)" sendiri, NOTE_RELATED jadi
// ganda/membingungkan (ejaannya terlalu mirip). Ikon (◎/○/-) SENGAJA
// disamakan dengan marker di export Excel (DocumentMaintenanceController.
// WriteRelatedDivisionSection) supaya konsisten antar tampilan. Satu tile
// cuma bisa punya SATU peran (atau tidak sama sekali, direpresentasikan
// sebagai peran semu "Not Related" - BUKAN nilai yang dikirim ke server,
// cuma dipetakan ke roleCode kosong lewat RD_NOT_RELATED_ICON di bawah).
var RD_ROLES = [
    { code: 'MAIN_PIC', label: 'Main PIC', icon: '◎' },
    { code: 'RELATED', label: 'Related', icon: '○' }
];
var RD_NOT_RELATED_ICON = '-';

function renderRelatedDivisionCheckboxes() {
    var $container = $('#relatedDivisionCheckboxes');
    $container.empty();
    gRelatedDivisionExpanded = false;
    $('#relatedDivisionSearch').val('');

    if (!gRelatedDivisionOptions) return;

    gRelatedDivisionOptions.forEach(function (div, idx) {
        // Cuma tampilkan kode (bukan nama lengkap divisi) - request Hendra
        // 2026-08-20 supaya lebih hemat ruang; nama lengkap tetap ada di
        // atribut title (tooltip hover) & tetap ikut dicocokkan saat search.
        var $col = $('<div class="col-md-2 rd-col"></div>')
            .attr('data-search', (div.code + ' ' + div.name).toLowerCase())
            .attr('data-index', idx);
        var $tile = $('<div class="rd-tile"></div>')
            .attr('title', div.name)
            .attr('data-name', div.name)
            .attr('data-code', div.code)
            .attr('data-role', '');

        // Satu tombol bulat, ICON SAJA (tanpa label teks) yang menampilkan
        // peran terpilih - diklik untuk membuka dropdown pilihan peran
        // (request Hendra 2026-08-20 revisi ke-2, supaya lebih hemat ruang).
        var $btn = $('<button type="button" class="rd-role-btn"></button>')
            .append($('<span class="rd-role-icon"></span>').text(RD_NOT_RELATED_ICON));

        var $menu = $('<div class="rd-role-menu"></div>');
        RD_ROLES.forEach(function (role) {
            $menu.append(
                $('<div class="rd-role-menu-item"></div>')
                    .attr('data-role', role.code)
                    .append($('<span class="rd-role-icon"></span>').text(role.icon))
                    .append($('<span></span>').text(role.label))
                    .on('click', function (e) {
                        e.stopPropagation();
                        setRelatedDivisionRole($tile, role.code);
                        closeRelatedDivisionMenus();
                    })
            );
        });
        // "Not Related" - peran semu (roleCode kosong), diberi ikon yang
        // sama seperti marker "-" di export Excel supaya keempat pilihan
        // konsisten (bukan lagi "Clear selection" polos tanpa ikon).
        $menu.append(
            $('<div class="rd-role-menu-item rd-role-menu-clear"></div>')
                .attr('data-role', '')
                .append($('<span class="rd-role-icon"></span>').text(RD_NOT_RELATED_ICON))
                .append($('<span></span>').text('Not Related'))
                .on('click', function (e) {
                    e.stopPropagation();
                    setRelatedDivisionRole($tile, '');
                    closeRelatedDivisionMenus();
                })
        );

        $btn.on('click', function (e) {
            e.stopPropagation();
            var wasOpen = $menu.hasClass('open');
            closeRelatedDivisionMenus();
            if (!wasOpen) $menu.addClass('open');
        });

        var $select = $('<div class="rd-role-select"></div>').append($btn).append($menu);
        var $info = $('<div class="rd-tile-info"></div>')
            .append($('<b></b>').text(div.code))
            .append($('<span class="rd-lock-label"></span>'));
        $tile.append($info).append($select);
        $col.append($tile);
        $container.append($col);
    });

    applyRelatedDivisionVisibility();
    gLockedCreatorTileCode = null;
    lockCreatorDivisionRole();
}

function closeRelatedDivisionMenus() {
    $('#relatedDivisionCheckboxes .rd-role-menu').removeClass('open');
}

// Set/ubah peran satu tile - dipanggil dari klik item dropdown ("Not
// Related" mengirim roleCode kosong untuk membatalkan pilihan/kembali ke
// default).
function setRelatedDivisionRole($tile, roleCode) {
    $tile.attr('data-role', roleCode || '');
    $tile.removeClass('role-main_pic role-related');
    $tile.find('.rd-role-menu-item').removeClass('active');

    var $btn = $tile.find('.rd-role-btn');
    var role = roleCode ? RD_ROLES.find(function (r) { return r.code === roleCode; }) : null;

    $btn.toggleClass('set', !!role).attr('data-role', roleCode || '');
    $btn.find('.rd-role-icon').text(role ? role.icon : RD_NOT_RELATED_ICON);

    // Item "Not Related" (data-role="") ikut di-highlight saat memang
    // sedang jadi peran aktif tile ini, sama seperti 3 peran lainnya.
    $tile.find('.rd-role-menu-item[data-role="' + (roleCode || '') + '"]').addClass('active');

    if (role) {
        $tile.addClass('role-' + roleCode.toLowerCase());
    }
}

// Tile divisi PEMBUAT dokumen sendiri dikunci selalu Main PIC & read-only
// (request Hendra 2026-08-20 revisi ke-2) - beda dari revisi sebelumnya
// yang cuma menyarankan Main PIC tapi tetap bisa diubah bebas. Divisi lain
// tetap bebas dipilih manual (termasuk boleh ikut jadi Main PIC juga) &
// default-nya tetap kosong/Not Related (-). gLockedCreatorTileCode
// melacak tile mana yang sedang terkunci supaya bisa dibuka lagi (reset ke
// kosong) begitu Division di step Organization diganti ke divisi lain.
var gLockedCreatorTileCode = null;

function lockCreatorDivisionRole() {
    if (gLockedCreatorTileCode) {
        var $prevTile = $('#relatedDivisionCheckboxes .rd-tile[data-code="' + gLockedCreatorTileCode + '"]');
        if ($prevTile.length) {
            $prevTile.removeClass('rd-locked');
            $prevTile.find('.rd-role-btn').prop('disabled', false);
            $prevTile.find('.rd-lock-label').text('');
            setRelatedDivisionRole($prevTile, '');
        }
    }

    var division = $('#addEditDivision').val();
    gLockedCreatorTileCode = division || null;
    if (!division) return;

    var $tile = $('#relatedDivisionCheckboxes .rd-tile[data-code="' + division + '"]');
    if (!$tile.length) return;

    setRelatedDivisionRole($tile, 'MAIN_PIC');
    $tile.addClass('rd-locked');
    $tile.find('.rd-role-btn').prop('disabled', true);
    $tile.find('.rd-lock-label').text('Creator');
    $tile.attr('title', $tile.attr('data-name') + ' - document creator, locked as Main PIC');
}

// Search box & "Show more" (mockup 2026-08-20) - daftar 17 divisi
// ditampilkan max RELATED_DIVISION_VISIBLE_LIMIT dulu; saat user mengetik
// pencarian, batasan itu diabaikan & semua yang cocok ditampilkan.
function applyRelatedDivisionVisibility() {
    var query = $.trim($('#relatedDivisionSearch').val() || '').toLowerCase();
    var $cols = $('#relatedDivisionCheckboxes .rd-col');
    var $showMoreWrap = $('#relatedDivisionShowMore');

    if (query) {
        $cols.each(function () {
            var $c = $(this);
            $c.toggle($c.data('search').indexOf(query) !== -1);
        });
        $showMoreWrap.hide();
        return;
    }

    $cols.each(function () {
        var idx = $(this).data('index');
        $(this).toggle(gRelatedDivisionExpanded || idx < RELATED_DIVISION_VISIBLE_LIMIT);
    });

    if ($cols.length > RELATED_DIVISION_VISIBLE_LIMIT) {
        $showMoreWrap.show();
        $('#relatedDivisionShowMoreText').text(gRelatedDivisionExpanded ? 'Show less' : 'Show more');
        $showMoreWrap.find('i').attr('class', gRelatedDivisionExpanded ? 'fa fa-chevron-up' : 'fa fa-chevron-down');
    } else {
        $showMoreWrap.hide();
    }
}

function filterRelatedDivisionCheckboxes() {
    applyRelatedDivisionVisibility();
}

function toggleRelatedDivisionShowMore() {
    gRelatedDivisionExpanded = !gRelatedDivisionExpanded;
    applyRelatedDivisionVisibility();
}

function getSelectedRelatedDivisions() {
    if (!$('#adfSectionRelatedDivision').is(':visible')) return '';

    var pairs = [];
    $('#relatedDivisionCheckboxes .rd-tile').each(function () {
        var $tile = $(this);
        var role = $tile.attr('data-role');
        if (role) pairs.push($tile.attr('data-code') + ':' + role);
    });
    return pairs.join(',');
}

function resetRelatedDivision() {
    $('#adfSectionRelatedDivision').hide();
    $('#relatedDivisionCheckboxes').empty();
    $('#relatedDivisionSearch').val('');
    gRelatedDivisionExpanded = false;
    gLockedCreatorTileCode = null;
}

var gManualNoPrefixRequestSeq = 0;

// ---------- OCR tahap 2 (2026-11-06): dampak revisi & cek file upload ----------
function insightEsc(s) { return $('<div>').text(s == null ? "" : s).html(); }

// Dokumen lain (termasuk dokumen scan yang dibaca OCR) yang menyebut nomor
// dokumen yang sedang direvisi - bahan pertimbangan dokumen mana yang ikut ditinjau.
function loadDocumentReferences(documentCode) {
    var $panel = $('#addEditImpactPanel').removeClass('has-refs').hide().empty();
    if (!documentCode) return;
    $.ajax({
        type: "GET",
        url: documentMaintenanceConfig.urls.getDocumentReferences,
        data: { documentCode: documentCode },
        success: function (result) {
            if (result.status !== true) return;
            var refs = result.data || [];
            var $title = $('<div class="adf-insight-title"><i class="fa fa-link"></i> Documents that refer to ' + insightEsc(documentCode) + '</div>');
            $panel.append($title);
            if (refs.length === 0) {
                $panel.append('<div class="adf-insight-sub">No other approved document mentions this document number. Revising it should not affect other documents.</div>');
                $panel.show();
                return;
            }
            $panel.addClass('has-refs');
            $title.append('<span class="adf-tag" style="background:#FEF0C7;color:#93370D;">' + refs.length + '</span>');
            $panel.append('<div class="adf-insight-sub">These documents mention this number in their title or content. Check whether they need to be revised together with this document.</div>');
            var codeRe = new RegExp(documentCode.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'gi');
            refs.forEach(function (r) {
                var open = r.SourceType === "EXTERNAL"
                    ? documentMaintenanceConfig.urls.externalDocumentDownloadAttachment + '?path=' + encodeURIComponent(r.FilePath || "")
                    : documentMaintenanceConfig.urls.viewAttachmentRaw + '?FILE_PATH=' + encodeURIComponent(r.FilePath || "") + '&DOCUMENT_TRANSACTION_ID=' + r.SourceId + '&type=1';
                var name = (r.DocumentCode ? r.DocumentCode + ' - ' : '') + (r.Title || '');
                var meta = [r.SourceType === "EXTERNAL" ? "External" : (r.Revision != null ? "Rev. " + r.Revision : null), r.StatusName, r.DepartmentName].filter(Boolean).join(' · ');
                var snippet = r.Snippet ? '<div class="adf-ref-snip">… ' + insightEsc(r.Snippet).replace(codeRe, function (m) { return '<mark>' + m + '</mark>'; }) + ' …</div>' : '';
                $panel.append('<div class="adf-ref"><div class="adf-ref-main"><a onclick="window.open(\'' + open.replace(/'/g, "%27") + '\', \'_blank\')">' + insightEsc(name) + '</a> <span>' + insightEsc(meta) + '</span>' +
                    (r.ByOcr ? ' <span class="adf-tag">OCR</span>' : '') + snippet + '</div></div>');
            });
            $panel.show();
        }
    });
}

// Cek isi file sebelum disimpan: identik dengan revisi sebelumnya, memuat nomor
// dokumen lain, atau tidak memuat judul. Hanya peringatan, Save tetap bisa.
var gFileCheckSeq = 0;
$(document).on('change', '#addEditDocumentUpload', function () {
    var $box = $('#addEditFileCheck').hide().empty();
    var files = this.files;
    if (gScreenMode !== "ADD" || !files || files.length === 0) return;
    var ext = (files[0].name.split('.').pop() || "").toLowerCase();
    if (ext !== "xls" && ext !== "xlsx") return;

    var data = new FormData();
    data.append("file", files[0]);
    data.append("revisedDocumentCode", $('#addEditDocumentType').val() == "02" ? ($('#addEditDocumentNo').val() || "") : "");
    data.append("title", $('#addEditDocumentName').val() || "");
    $box.html('<div class="note"><i class="fa fa-spinner fa-spin"></i> Checking file content...</div>').show();

    var seq = ++gFileCheckSeq;
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.checkUploadFile,
        data: data, contentType: false, processData: false,
        success: function (r) {
            if (seq !== gFileCheckSeq) return;
            $box.empty();
            if (r.status !== true) { $box.hide(); return; }
            (r.warnings || []).forEach(function (w) { $box.append('<div class="warn"><i class="fa fa-exclamation-triangle"></i><span>' + insightEsc(w) + '</span></div>'); });
            (r.notes || []).forEach(function (n) { $box.append('<div class="note"><i class="fa fa-info-circle"></i><span>' + insightEsc(n) + '</span></div>'); });
            if ((r.warnings || []).length === 0 && (r.notes || []).length === 0) {
                $box.append('<div class="ok"><i class="fa fa-check-circle"></i><span>File content checked - no issues found.</span></div>');
            }
            $box.show();
        },
        error: function () { if (seq === gFileCheckSeq) $box.hide(); }
    });
});

// ---------- Pratinjau approval chain (2026-11-06) ----------
var gApprovalPreviewTimer = null;
var gApprovalPreviewSeq = 0;

function scheduleApprovalPreview() {
    clearTimeout(gApprovalPreviewTimer);
    gApprovalPreviewTimer = setTimeout(refreshApprovalPreview, 250);
}

function refreshApprovalPreview() {
    var $box = $('#addEditApprovalPreview');
    var level = $('#addEditDocumentLevel').val();
    var documentId = (($('#addEditDocumentCode').val() || "").split('|')[0]) || "";
    var creator = $('#addEditDocumentCreator').val();

    if (!level || !creator || level == "null" || creator == "null") {
        $box.hide().empty();
        return;
    }

    var requestSeq = ++gApprovalPreviewSeq;
    $.ajax({
        type: "GET",
        url: documentMaintenanceConfig.urls.getApprovalPreview,
        data: { levelCode: level, documentId: documentId, creator: creator },
        success: function (result) {
            if (requestSeq !== gApprovalPreviewSeq) return;
            if (result.status != true || result.ready != true) { $box.hide().empty(); return; }
            renderApprovalPreview(result.steps || []);
        },
        error: function () {
            if (requestSeq !== gApprovalPreviewSeq) return;
            $box.hide().empty();
        }
    });
}

function renderApprovalPreview(steps) {
    var $box = $('#addEditApprovalPreview').removeClass('has-error').empty();
    var esc = function (s) { return $('<div>').text(s == null ? "" : s).html(); };
    var first = steps[0] || {};

    var $title = $('<div class="adf-ap-title"><i class="fa fa-sitemap"></i> Approval Flow</div>');
    if (first.STATUS == "NO_CREATOR") {
        $box.addClass('has-error').append($title)
            .append('<div class="adf-ap-msg"><i class="fa fa-times-circle"></i> The selected creator has no position in User Position - the document cannot be submitted.</div>')
            .show();
        return;
    }
    if (first.STATUS == "NO_WORKFLOW") {
        $box.addClass('has-error').append($title)
            .append('<div class="adf-ap-msg"><i class="fa fa-times-circle"></i> No approval workflow is set up for this Document Level and creator position (' + esc(first.CREATOR_POSITION) + '). Ask the admin to add it in Approval Workflow Setup.</div>')
            .show();
        return;
    }

    $title.append(first.IS_CATEGORY_WORKFLOW
        ? '<span class="adf-ap-tag cat">Category workflow</span>'
        : '<span class="adf-ap-tag">Default workflow for this level</span>');
    $box.append($title);

    var $steps = $('<div class="adf-ap-steps"></div>');
    var missing = [], skipped = [];
    steps.forEach(function (s, i) {
        if (i > 0) $steps.append('<span class="adf-ap-arrow"><i class="fa fa-chevron-right"></i></span>');
        var cls = s.STATUS == "AUTO" ? "auto" : s.STATUS == "SKIP" ? "skip" : s.STATUS == "MISSING" ? "missing" : "";
        var who = s.APPROVER
            ? esc(s.APPROVER_NAME || s.APPROVER) + (s.STATUS == "AUTO" ? ' <i class="fa fa-check" title="Creator - approved automatically"></i>' : '')
            : (s.STATUS == "MISSING" ? 'Not found (mandatory)' : 'Not found - skipped');
        $steps.append('<div class="adf-ap-step ' + cls + '"><b>' + esc(s.LABEL || s.POSITION_NAME) +
            (s.IS_MANDATORY ? ' *' : '') + '</b>' + esc(s.POSITION_NAME) + '<br>' + who + '</div>');
        if (s.STATUS == "MISSING") missing.push(s.POSITION_NAME);
        if (s.STATUS == "SKIP") skipped.push(s.POSITION_NAME);
    });
    $box.append($steps);

    if (missing.length > 0) {
        $box.addClass('has-error').append('<div class="adf-ap-msg"><i class="fa fa-times-circle"></i> Mandatory approver not found: ' + esc(missing.join(", ")) +
            '. The document cannot be saved until User Position is completed.</div>');
    } else if (skipped.length > 0) {
        $box.append('<div class="adf-ap-note"><i class="fa fa-info-circle"></i> ' + esc(skipped.join(", ")) + ' will be skipped (nobody holds this position for the selected creator).</div>');
    }
    $box.show();
}

function refreshManualNoPrefix() {
    if (!$('#addEditNumberingModeManual').is(':checked')) return;

    var documentCodeArray = ($('#addEditDocumentCode').val() || "").split('|');
    var documentId = documentCodeArray[0];

    // Beberapa field pemicu bisa berubah hampir bersamaan (mis. Level lalu
    // Document Code), masing-masing menembak AJAX sendiri - tanpa penanda
    // urutan ini, response yang datang belakangan bisa jadi punya nilai
    // lebih lama (request lebih awal tapi baru selesai duluan), sehingga
    // menimpa prefix yang seharusnya sudah lebih baru & benar.
    var requestSeq = ++gManualNoPrefixRequestSeq;

    $.ajax({
        type: "GET",
        url: documentMaintenanceConfig.urls.getManualNoPrefix,
        data: {
            levelCode: $('#addEditDocumentLevel').val(),
            division: $('#addEditDivision').val(),
            departmentId: $('#addEditDepartment').val(),
            sectionCode: $('#addEditSection').val(),
            documentId: documentId,
            processCode: $('#addEditProcessCode').val(),
            companyCode: $('#addEditCompanyCode').val(),
            documentDate: $('#addEditDate').val()
        },
        success: function (result) {
            if (requestSeq !== gManualNoPrefixRequestSeq) return;
            $('#addEditManualNoPrefix').text(result.status == true ? (result.prefix + '/') : '-');
        },
        error: function () {
            if (requestSeq !== gManualNoPrefixRequestSeq) return;
            $('#addEditManualNoPrefix').text('-');
        }
    });
}

function onChangeDocumentType() {
    reset();
    $('#addEditImpactPanel, #addEditFileCheck').hide().empty();  // OCR tahap 2

    if ($('#addEditDocumentType').val() == "01") //NEW
    {
        $('#addEditDocumentType').prop('disabled', false);
        $('#addEditClassified').prop('disabled', true);
        $('#addEditProcessCode').prop('disabled', true);
        $('#addEditCompanyCode').prop('disabled', true);
        // Date sekarang aktif begitu Document Type dipilih (bukan menunggu
        // Document Level) - satu baris dengan Document Type & Document No
        // di step Basic Information, jadi wajar semuanya aktif bersamaan
        // (request Hendra 2026-08-20).
        $('#addEditDate').prop('disabled', false);
        $('#addEditItemChanged').prop('disabled', true);
        $('#addEditExternal').prop('disabled', false);

        $('#addEditDocumentNo').prop('disabled', true);
        $('#addEditDocumentNo2').prop('disabled', true);
        $('#addEditDocumentCode').prop('disabled', true);
        $('#addEditDocumentName').prop('disabled', true);
        $('#addEditDocumentUpload').prop('disabled', true);
        $('#addEditReason').prop('disabled', true);
        $('#addEditSection').prop('disabled', true);
        $('#addEditDocumentCreator').prop('disabled', true);

        $('#addEditReferenceNo').prop('disabled', true);
        $('#addEditImpactToOtherDocument').prop('disabled', true);
        $('#addEditSource').prop('disabled', true);

        generateDocumentNo();

        //Updated By Arie (2023-08-24)
        $('#addEditDocumentNo').removeClass('hide');
        $('#addEditDocumentNo2').data('select2').$container.addClass('hide');
        $('#addEditDocumentLevel').prop('disabled', false);
        $('#addEditNumberingModeGroup').removeClass('hide');
    }

    if ($('#addEditDocumentType').val() == "02") //REVISION
    {
        $('#addEditDocumentType').prop('disabled', false);
        $('#addEditClassified').prop('disabled', true);
        $('#addEditProcessCode').prop('disabled', true);
        $('#addEditCompanyCode').prop('disabled', true);
        $('#addEditDate').prop('disabled', true);
        $('#addEditItemChanged').prop('disabled', false);
        $('#addEditExternal').prop('disabled', false);

        $('#addEditDocumentNo').prop('disabled', false);
        $('#addEditDocumentNo2').prop('disabled', false);
        $('#addEditDocumentCode').prop('disabled', true);
        $('#addEditDocumentName').prop('disabled', true);
        $('#addEditDocumentUpload').prop('disabled', true);
        $('#addEditReason').prop('disabled', false);
        $('#addEditSection').prop('disabled', true);
        $('#addEditDocumentCreator').prop('disabled', true);

        $('#addEditReferenceNo').prop('disabled', true);
        $('#addEditImpactToOtherDocument').prop('disabled', true);
        $('#addEditSource').prop('disabled', true);

        //Updated By Arie (2023-08-24)
        $('#addEditDocumentNo').addClass('hide');
        $('#addEditDocumentNo2').data('select2').$container.removeClass('hide');
        $('#addEditDocumentNo2').val("").trigger("change");
        $('#addEditDocumentLevel').prop('disabled', true);
        $('#addEditNumberingModeGroup').addClass('hide');
    }
}

function onChangeDocumentLevel() {
    if ($('#addEditDocumentType').val() == "01") //NEW
    {
        onChangeDocumentCode();
    }

    setDataSelect2WithParam("addEditDocumentCode", documentMaintenanceConfig.urls.getDocumentCodeByLevel, "- Choose -", $('#addEditDocumentLevel').val());
}

function onChangeDocumentNo() {
    gImpactOther = null;
    gReffNo = null;
    gSourceNo = null;

    //Updated By Arie (2023-08-24)
    $("#addEditDocumentNo").val($("#addEditDocumentNo2").val());

    if ($('#addEditDocumentType').val() == "02" && $("#addEditDocumentNo").val() != null && $("#addEditDocumentNo").val() != "" && $("#addEditDocumentNo").val() != "null") //REVISION
    {
        $.ajax({
            type: "POST",
            url: documentMaintenanceConfig.urls.getDataByDocumentNo,
            async: false,
            data: {
                DOCUMENT_CODE: $("#addEditDocumentNo").val()
            },
            success: function (result) {
                if (result.status == true) {
                    var data = result.data;

                    gImpactOther = data.IMPACT_OTHER_FLAG;
                    gReffNo = data.REFERENCE_NO;
                    gSourceNo = data.SOURCE;

                    $("#addEditDocumentLevel").select2("trigger", "select", { data: { id: data.LEVEL_CODE, text: data.LEVEL_CODE } });//.val(data.CLASSIFIED);
                    $("#addEditClassified").select2("trigger", "select", { data: { id: data.CLASSIFIED, text: data.CLASSIFIED + ' - ' + data.CLASSIFIED_VAL } });//.val(data.CLASSIFIED);
                    $("#addEditDocumentCode").select2("trigger", "select", { data: { id: data.DOCUMENT_CODE, text: data.DOCUMENT_CODE } });//.val(data.DOCUMENT_CODE);
                    $("#addEditDocumentName").val(data.DOCUMENT_TRANSACTION_NAME);
                    $("#addEditCompanyCode").select2("trigger", "select", { data: { id: data.COMPANY_CODE, text: data.COMPANY_CODE } });//.val(data.COMPANY_CODE);
                    $("#addEditDivision").select2("trigger", "select", { data: { id: data.DIVISION, text: data.DIVISION + " - " + data.DIVISION_NAME } });//.val(data.DIVISION_CODE);
                    $("#addEditDepartment").select2("trigger", "select", { data: { id: data.DEPARTMENT_ID, text: data.DEPARTMENT_CODE + " - " + data.DEPARTMENT_NAME } });//.val(data.DEPARTMENT_CODE);
                    $("#addEditDocumentCreator").select2("trigger", "select", { data: { id: data.DOCUMENT_CREATOR, text: data.DOCUMENT_CREATOR } });//.val(data.DEPARTMENT_CODE);
                    $("#addEditDate").val(data.DOCUMENT_DATE.substring(0, 10));
                    $('#addEditItemChanged').val(data.ITEM_CHANGED);
                    $('#addEditReason').val(data.REASON);

                    if (data.PROCESS_CODE != null) {
                        $("#addEditProcessCode").select2("trigger", "select", { data: { id: data.PROCESS_CODE, text: data.PROCESS_CODE } });//.val(data.PROCESS_CODE);
                    } else {
                        $("#addEditProcessCode").val("").trigger("change");
                    }

                    if (data.SECTION_CODE != null) {
                        $("#addEditSection").select2("trigger", "select", { data: { id: data.SECTION_CODE, text: data.SECTION_NAME } });//.val(data.SECTION_CODE);
                    } else {
                        $("#addEditSection").val("").trigger("change");
                    }

                    $('#addEditDocumentName').prop('disabled', false);
                    $('#addEditDate').prop('disabled', false);
                    $('#addEditDocumentUpload').prop('disabled', false);
                    $('#addEditExternal').prop('disabled', false);
                    $('#addEditReferenceNo').prop('disabled', true);

                    if (data.EXTERNAL_FLAG == "1") {
                        $('#addEditImpactToOtherDocument').prop('disabled', false);
                        $("#addEditExternal").prop("checked", true);
                    } else {
                        $('#addEditImpactToOtherDocument').prop('disabled', true);
                        $("#addEditExternal").prop("checked", false);
                    }

                    if (data.IMPACT_OTHER_FLAG == "1") {
                        $("#addEditImpactToOtherDocument").prop("checked", true);
                    } else {
                        $("#addEditImpactToOtherDocument").prop("checked", false);
                    }

                    $('#addEditReferenceNo').val(data.REFERENCE_NO);
                    $('#addEditSource').val(data.SOURCE);

                    // Analisis dampak revisi (OCR tahap 2, 2026-11-06)
                    loadDocumentReferences(data.DOCUMENT_CODE);
                } else {
                    showErrorMesgGrowl(result.message);
                }
            },
            error: function (result) {
                //popUpProgressHide();
                showErrorMesgGrowl(result);
            }
        });
    }
}

function onChangeDocumentCode() {
    // Date is NOT level-dependent - it used to be cleared here too, but this
    // function fires on every Document Level change (see onChangeDocumentLevel
    // below), wiping out a date the user already typed just because they
    // picked/changed the level (bug reported by Hendra 2026-08-29).
    $("#addEditDocumentUpload").val('');
    $('#addEditDocumentCode').val("").trigger("change");

    $('#addEditDocumentCode').prop('disabled', false);
    //$('#addEditDivision').prop('disabled', true);
    $('#addEditClassified').prop('disabled', false);

    documentLevel = $("#addEditDocumentLevel").val();

    //showErrorMesgGrowl(selectedDocumentCode);
    //showErrorMesgGrowl(myArray[0]);
    //showErrorMesgGrowl(myArray[1]);

    /*
    -	IF Level = 1
-	Enable :					-	Company Code
                        -	Document Name
                        -	Date
                        -	Document Upload

     -	IF Level = 2
-	Enable :					-	Company Code
                        -	Process Code
                        -	Document Name
                        -	Date
                        -	Document Upload

-	IF Level = 3 or 4
-	Enable :					-	Department
                        -	Section
                        -	Document Name
                        -	Date
                        -	Document Upload

    */

    if (documentLevel == "1") {
        //showErrorMesgGrowl(documentLevel);

        $('#addEditCompanyCode').prop('disabled', false);
        $('#addEditDocumentName').prop('disabled', false);
        $('#addEditDate').prop('disabled', false);
        $('#addEditDocumentUpload').prop('disabled', false);
        $('#addEditSection').prop('disabled', true);

        $('#addEditProcessCode').prop('disabled', true);
        $('#addEditProcessCode').val("").trigger("change");

        $('#mandatoryMarkProcessCode').hide();
        $('#mandatoryMarkSection').hide();

        //Updated By Arie (2023-08-24)
        //$('#addEditSection').prop('disabled', true);
        //$('#addEditSection').val("").trigger("change");
        //$('#addEditDepartment').prop('disabled', true);
        //$('#addEditSection').prop('disabled', true);
        setPositionState(documentLevel)
    }

    else if (documentLevel == "2") {
        //showErrorMesgGrowl(documentLevel);

        $('#addEditCompanyCode').prop('disabled', false);
        $('#addEditProcessCode').prop('disabled', false);
        $('#addEditDocumentName').prop('disabled', false);
        $('#addEditDate').prop('disabled', false);
        $('#addEditDocumentUpload').prop('disabled', false);
        $('#addEditSection').prop('disabled', true);

        $('#mandatoryMarkProcessCode').show();
        $('#mandatoryMarkSection').hide();

        //Updated By Arie (2023-08-24)
        //$('#addEditSection').prop('disabled', true);
        //$('#addEditSection').val("").trigger("change");
        //$('#addEditDepartment').prop('disabled', true);
        //$('#addEditSection').prop('disabled', true);
        setPositionState(documentLevel)
    }

    else if (documentLevel == "3" || documentLevel == "4") {
        //showErrorMesgGrowl(documentLevel);

        //Updated By Arie (2023-08-24)
        //$('#addEditDepartment').prop('disabled', false);
        //$('#addEditSection').prop('disabled', false);
        $('#addEditCompanyCode').prop('disabled', false);
        $('#addEditDocumentName').prop('disabled', false);
        $('#addEditDate').prop('disabled', false);
        $('#addEditDocumentUpload').prop('disabled', false);

        //$('#addEditCompanyCode').prop('disabled', true);
        $('#addEditProcessCode').prop('disabled', true);
        $('#addEditProcessCode').val("").trigger("change");

        $('#mandatoryMarkProcessCode').hide();
        $('#mandatoryMarkSection').show();
        setPositionState(documentLevel)
    }
}

function setPositionState(documentLevel) {
    if (documentLevel == 1 || documentLevel == 2)
    {
        $('#addEditSection').prop('disabled', true);

        if ((documentLevel == 1 || documentLevel == 2) && (SESSION_SECTION_ID == null || SESSION_SECTION_ID == "" || SESSION_SECTION_ID == 0)) {
            $('#addEditSection').val("").trigger("change");
        }

        var departmentId = $('#addEditDepartment').val();

        if (departmentId != null && departmentId != "") {
            $('#addEditDocumentCreator').prop('disabled', false);
        }
    }

    if (documentLevel == 3 || documentLevel == 4) {
        var departmentId = $('#addEditDepartment').val();

        if (departmentId != null && departmentId != "") {
            $('#addEditSection').prop('disabled', false);
            $('#addEditDocumentCreator').prop('disabled', false);
        }
    }

    if (SESSION_DEPARTMENT_ID == null || SESSION_DEPARTMENT_ID == "" || SESSION_DEPARTMENT_ID == 0) {
        var division = $('#addEditDivision').val();

        if (division != null && division != "") {
            $('#addEditDepartment').prop('disabled', false);
        }
    }

    if (SESSION_DIVISION == null || SESSION_DIVISION == "" || SESSION_DIVISION == 0 || SESSION_POSITION_ID == "5") {
        $('#addEditDivision').prop('disabled', false);
    }
}

function setDepartment(val) {
    var division = val;

    if (division != null && division != "") {
        if (SESSION_DIVISION == null || SESSION_DIVISION == "" || SESSION_DIVISION == 0 || SESSION_POSITION_ID == "5") {
            $('#addEditDepartment').prop('disabled', false);
        }

        $('#addEditDepartment').val("").trigger("change");
        setDataSelect2WithParam("addEditDepartment", documentMaintenanceConfig.urls.departmentMasterGetDepartmentIdAndNameByDivision, "- Choose -", division);
    }
}

function setSection(val) {
    var departmentId = val;

    if (departmentId == null || departmentId == "" || (documentLevel != "3" && documentLevel != "4")) {
        $('#addEditSection').prop('disabled', true);
    } else {
        $('#addEditSection').prop('disabled', false);
    }

    $('#addEditSection').val("").trigger("change");
    setDataSelect2WithParam("addEditSection", documentMaintenanceConfig.urls.sectionMasterGetSectionCodeAndNameByDepartment, "- Choose -", departmentId);

    setDocumentCreator(val)
}

function setDocumentCreator(val) {
    var departmentId = val;

    if (departmentId == null || departmentId == "" || documentLevel == null || documentLevel == "") {
        $('#addEditDocumentCreator').prop('disabled', true);
    } else {
        $('#addEditDocumentCreator').prop('disabled', false);
    }

    $('#addEditDocumentCreator').val("").trigger("change");
    setDataSelect2WithParam("addEditDocumentCreator", documentMaintenanceConfig.urls.userGetUserNameByDepartmentId, "- Choose -", val);
}

function confirmAddEditData() {
    // Manual numbering (Jul 2026): kalau mode Manual dipilih tapi nomornya
    // dibiarkan kosong, server menganggap itu SAMA DENGAN mode Generated
    // (parameter MANUAL_SEQ_NO kosong = null = auto) - jadi tolak di sini
    // dulu supaya user tidak diam-diam dapat nomor otomatis padahal
    // maksudnya mau pilih sendiri.
    if (gScreenMode == "ADD" && $('#addEditNumberingModeManual').is(':checked')) {
        var manualSeq = $('#addEditManualSeqNo').val();
        if (manualSeq == null || manualSeq.trim() == '') {
            showErrorMesgGrowl("Please enter the manual document number, or switch back to Generated.");
            return;
        }
    }

    // Dokumen baru yang disimpan oleh pembuatnya sendiri wajib ditandatangani
    // (Draw/Type, 2026-11-06) - dialog tanda tangan sekaligus jadi konfirmasi.
    // Kalau dibuatkan orang lain, kotak pembuat dibiarkan kosong (tidak diwakilkan).
    gCreatorSignature = null;
    var creator = $('#addEditDocumentCreator').val();
    // Level 4 (Checksheet / Form) is approved without signatures (2026-11-09).
    if (gScreenMode == "ADD" && creator && String(creator).toLowerCase() == String(gCurrentUsername).toLowerCase()
        && !isUnsignedLevel($("#addEditDocumentLevel").val())) {
        openSignaturePad({
            title: "Sign as document creator",
            subtitle: "Your signature will appear in the creator box of the approval sheet.",
            confirmText: "Sign & Save",
            onSigned: function (sig) { gCreatorSignature = sig; addEditData(); }
        });
        return;
    }

    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

var gCreatorSignature = null;

function addEditData() {
    popUpProgressShow();
    var data = new FormData();
    data.append("screenMode", gScreenMode);
    if (gScreenMode == "ADD" && gCreatorSignature) {
        appendSignature(data, gCreatorSignature);
    }

    if (gScreenMode == "ADD") {
        var documentId = $('#addEditDocumentId').val();
        var documentType = $('#addEditDocumentType').val();
        var documentNo = $('#addEditDocumentNo').val();
        var classified = $('#addEditClassified').val();
        var documentCode = $('#addEditDocumentCode').val();
        var processCode = $('#addEditProcessCode').val();
        var documentName = $('#addEditDocumentName').val();
        var companyCode = $('#addEditCompanyCode').val();
        var division = $('#addEditDivision').val();
        var department = $('#addEditDepartment').val();
        var section = $('#addEditSection').val();
        var date = $('#addEditDate').val();
        var documentUpload = $('#addEditDocumentUpload').val();
        var itemChanged = $('#addEditItemChanged').val();
        var reason = $('#addEditReason').val();
        var external = $('#addEditExternal').val();
        var referenceNo = $('#addEditReferenceNo').val();
        var source = $('#addEditSource').val();
        var impactToOtherDocument = $('#addEditImpactToOtherDocument').val();
        var levelCode = $('#addEditDocumentLevel').val();
        // Document Code belum dipilih -> val() null; jangan sampai .split() error
        // setelah loading tampil (popup progress jadi tertahan). Biarkan SP yang
        // mengembalikan pesan validasi field kosong.
        var documentCategoryArray = ($('#addEditDocumentCode').val() || '').split('|');
        var documentCreator = $('#addEditDocumentCreator').val();

        if ($('#addEditExternal').is(":checked")) {
            data.append("EXTERNAL_FLAG", '1');
        } else {
            data.append("EXTERNAL_FLAG", '0');
        }

        if ($('#addEditImpactToOtherDocument').is(":checked")) {
            data.append("IMPACT_OTHER_FLAG", '1');
        } else {
            data.append("IMPACT_OTHER_FLAG", '0');
        }

        if (gDocumentId != null && gDocumentId != "null" && gDocumentId != "") {
            data.append("DOCUMENT_TRANSACTION_ID", gDocumentId);
        }

        if (documentType != null && documentType != "null" && documentType != "") {
            data.append("DOCUMENT_TYPE", $('#addEditDocumentType').val());
        }

        if (documentNo != null && documentNo != "null" && documentNo != "") {
            data.append("DOCUMENT_CODE", $('#addEditDocumentNo').val());
        }

        if (classified != null && classified != "null" && classified != "") {
            data.append("CLASSIFIED", $('#addEditClassified').val());
        }

        if (documentCode != null && documentCode != "null" && documentCode != "") {
            data.append("DOCUMENT_CODE", $('#addEditDocumentCode').val());
        }

        if (processCode != null && processCode != "null" && processCode != "") {
            data.append("PROCESS_CODE", $('#addEditProcessCode').val());
        }

        if (documentName != null && documentName != "null" && documentName != "") {
            data.append("DOCUMENT_TRANSACTION_NAME", $('#addEditDocumentName').val());
        }

        if (companyCode != null && companyCode != "null") {
            data.append("COMPANY_CODE", $('#addEditCompanyCode').val());
        }

        if (division != null && division != "null" && division != "") {
            data.append("DIVISION", $('#addEditDivision').val());
        }

        data.append("RELATED_DIVISIONS", getSelectedRelatedDivisions());

        if (department != null && department != "null" && department != "") {
            data.append("DEPARTMENT_ID", $('#addEditDepartment').val());
        }

        if (section != null && section != "null" && section != "") {
            data.append("SECTION_CODE", $('#addEditSection').val());
        }

        if (date != null && date != "null" && date != "") {
            data.append("DOCUMENT_DATE", $('#addEditDate').val());
        }

        if (itemChanged != null && itemChanged != "null" && itemChanged != "") {
            data.append("ITEM_CHANGED", $('#addEditItemChanged').val());
        }

        if (reason != null && reason != "null" && reason != "") {
            data.append("REASON", $('#addEditReason').val());
        }

        if (referenceNo != null && referenceNo != "null" && referenceNo != "") {
            data.append("REFERENCE_NO", $('#addEditReferenceNo').val());
        }

        if (source != null && source != "null" && source != "") {
            data.append("SOURCE", $('#addEditSource').val());
        }

        if (levelCode != null && levelCode != "null" && levelCode != "") {
            data.append("LEVEL_CODE", $('#addEditDocumentLevel').val());
        }

        if (documentCreator != null && documentCreator != "null" && documentCreator != "") {
            data.append("DOCUMENT_CREATOR", $('#addEditDocumentCreator').val());
        }

        if (documentCategoryArray != null && documentCategoryArray != "null" && documentCategoryArray != "") {
            data.append("DOCUMENT_ID", documentCategoryArray[0]);
        }

        if ($('#addEditNumberingModeManual').is(':checked')) {
            data.append("MANUAL_SEQ_NO", $('#addEditManualSeqNo').val());
        }

        if ($('#addEditDocumentUpload').val() != '') {
            //data.append("FILE_PATH", $('#addEditDocumentUpload').val());

            e = ex;
            var files = e.target.files;
            if (files.length > 0) {
                if (window.FormData !== undefined)
                    for (var x = 0; x < files.length; x++) {
                        data.append("FILE_PATH", files[x]);
                    }
            }
        }
    } else if (gScreenMode == "EDIT") {
        var documentId = $('#editDocumentId').val();
        var documentNo = $('#editDocumentNo').val();
        var documentName = $('#editDocumentName').val();
        var date = $('#editDate').val();
        var documentUpload = $('#editDocumentUpload').val();
        var itemChanged = $('#editItemChanged').val();
        var reason = $('#editReason').val();
        var external = $('#editExternal').val();
        var referenceNo = $('#editReferenceNo').val();
        var source = $('#editSource').val();
        var impactToOtherDocument = $('#editImpactToOtherDocument').val();
        var remark = $('#editRemark').val();

        // data.append("DOCUMENT_TRANSACTION_ID", $('#editDocumentId').val());
        // data.append("DOCUMENT_CODE", $('#editDocumentNo').val());
        // data.append("DOCUMENT_TRANSACTION_NAME", $('#editDocumentName').val());
        // data.append("DOCUMENT_DATE", $('#editDate').val());
        // data.append("DOCUMENT_UPLOAD", $('#editDocumentUpload').val());
        // // data.append("ITEM_CHANGED", $('#editItemChanged').val());
        // // data.append("REASON", $('#editReason').val());
        // data.append("REFERENCE_NO", $('#editReferenceNo').val());
        // data.append("SOURCE", $('#editSource').val());

        if ($('#editExternal').is(":checked")) {
            data.append("EXTERNAL_FLAG", '1');
        } else {
            data.append("EXTERNAL_FLAG", '0');
        }

        if ($('#editImpactToOtherDocument').is(":checked")) {
            data.append("IMPACT_OTHER_FLAG", '1');
        } else {
            data.append("IMPACT_OTHER_FLAG", '0');
        }

        if (gDocumentId != null && gDocumentId != "null" && gDocumentId != "") {
            data.append("DOCUMENT_TRANSACTION_ID", gDocumentId);
        }

        if (documentNo != null && documentNo != "null" && documentNo != "") {
            data.append("DOCUMENT_CODE", $('#editDocumentNo').val());
        }

        if (documentName != null && documentName != "null" && documentName != "") {
            data.append("DOCUMENT_TRANSACTION_NAME", $('#editDocumentName').val());
        }

        if (date != null && date != "null" && date != "") {
            data.append("DOCUMENT_DATE", $('#editDate').val());
        }

        // if (itemChanged != null && itemChanged != "null" && itemChanged != "") {
        //     data.append("ITEM_CHANGED", $('#editItemChanged').val());
        // }

        // if (reason != null && reason != "null" && reason != "") {
        //     data.append("REASON", $('#editReason').val());
        // }

        if (referenceNo != null && referenceNo != "null" && referenceNo != "") {
            data.append("REFERENCE_NO", $('#editReferenceNo').val());
        }

        if (source != null && source != "null" && source != "") {
            data.append("SOURCE", $('#editSource').val());
        }

        if (remark != null && remark != "null" && remark != "") {
            data.append("REMARK", $('#editRemark').val());
        }

        if (gFilePath !== null) {
            data.append("FILE_PATH", gFilePath);
        }

        if (gDocumentTypeId != null) {
            data.append("DOCUMENT_ID", gDocumentTypeId);
        }

        if ($('#editDocumentUpload').val() != '') {
            //data.append("FILE_PATH", $('#editDocumentUpload').val());

            e = ex;
            var files = e.target.files;
            if (files.length > 0) {
                if (window.FormData !== undefined)
                    for (var x = 0; x < files.length; x++) {
                        data.append("file", files[x]);
                    }
            }
        }
    }

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.addEdit,
        contentType: false,
        processData: false,
        data: data,
        success: function (result) {
            //popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                popUpProgressHide();
                if (gScreenMode == "ADD") {
                    $("#modalAddEdit").modal('hide');
                } else if (gScreenMode == "EDIT") { $("#modalEdit").modal('hide'); }
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

    //reset();
}

$(document).on('change', '#addEditDocumentUpload', function (e) {
    ex = e;

    validateExtension($('#addEditDocumentUpload').val(), ["xlsx", "xls"]);
});

$(document).on('change', '#editDocumentUpload', function (e) {
    ex = e;

    validateExtension($('#editDocumentUpload').val(), ["xlsx", "xls"]);
});

function reset() {
    $('#addEditClassified').val("").trigger("change");
    $('#addEditProcessCode').val("").trigger("change");
    $('#addEditCompanyCode').val("").trigger("change");
    $('#addEditDepartment').val("").trigger("change");
    $('#addEditDivision').val("").trigger("change");
    $('#addEditSection').val("").trigger("change");
    $('#addEditDocumentNo2').val("").trigger("change");
    $('#addEditDocumentLevel').val("").trigger("change");
    $('#addEditDocumentCode').val("").trigger("change");

    setValueFromLogin();

    $('#addEditDate').val(moment().format('YYYY-MM-DD'));
    $('#addEditItemChanged').val("");
    $('#addEditDocumentLevel').val("");
    $('#addEditDocumentNo').val("");
    $('#addEditDocumentName').val("");
    //$('#addEditDocumentUpload').val("");
    $('#addEditReason').val("");
    $('#addEditReferenceNo').val("");
    $('#addEditImpactToOtherDocument').val("");
    $('#addEditSource').val("");

    $("#addEditExternal").prop("checked", false);
    $("#addEditImpactToOtherDocument").prop("checked", false);

    clearDocumentUpload();

    $('#addEditDocumentType').prop('disabled', false);
    $('#addEditClassified').prop('disabled', true);
    $('#addEditProcessCode').prop('disabled', true);
    $('#addEditCompanyCode').prop('disabled', true);
    $('#addEditDate').prop('disabled', true);
    $('#addEditItemChanged').prop('disabled', true);
    $('#addEditExternal').prop('disabled', false);
    $('#addEditDocumentLevel').prop('disabled', true);

    $('#addEditDocumentNo').prop('disabled', true);
    $('#addEditDocumentNo2').prop('disabled', true);
    $('#addEditDocumentCode').prop('disabled', true);
    $('#addEditDocumentName').prop('disabled', true);
    $('#addEditDocumentUpload').prop('disabled', true);
    $('#addEditReason').prop('disabled', true);
    $('#addEditDocumentLevel').prop('disabled', true);

    $('#addEditReferenceNo').prop('disabled', true);
    $('#addEditImpactToOtherDocument').prop('disabled', true);
    $('#addEditSource').prop('disabled', true);

    gFilePath = null;
    documentLevel = null;

    $('#addEditNumberingModeAuto').prop('checked', true);
    $('#addEditNumberingModeGroup').addClass('hide');
    $('#addEditDocumentNo').removeClass('hide');
    $('#addEditManualNoGroup').addClass('hide');
    $('#addEditManualSeqNo').val('');
    $('#addEditManualNoPrefix').text('-');
}

function setValueFromLogin() {
    $('#addEditDivision').prop('disabled', true);
    $('#addEditDepartment').prop('disabled', true);
    $('#addEditSection').prop('disabled', true);
    $('#addEditDocumentCreator').prop('disabled', true);

    if (SESSION_DIVISION != "" && SESSION_DIVISION != null) {
        if (SESSION_POSITION_ID == "5") //if user login position is eo
        {
            setDataSelect2("addEditDivision", documentMaintenanceConfig.urls.departmentMasterGetDivisionByUsername, "- Choose -");
        } else 
        {
            $("#addEditDivision").select2("trigger", "select", {
                data: {
                    id: SESSION_DIVISION,
                    text: SESSION_DIVISION + " - " + SESSION_DIVISION_NAME
                }
            });
        }
    }

    if (SESSION_DEPARTMENT_ID != "" && SESSION_DEPARTMENT_ID != 0) {
        $("#addEditDepartment").select2("trigger", "select", {
            data: {
                id: SESSION_DEPARTMENT_ID,
                text: SESSION_DEPARTMENT_CODE + " - " + SESSION_DEPARTMENT_NAME
            }
        });
    }

    if (SESSION_SECTION_ID != "" && SESSION_SECTION_ID != 0) {
        $("#addEditSection").select2("trigger", "select", {
            data: {
                id: SESSION_SECTION_CODE,
                text: SESSION_SECTION_NAME
            }
        });
    }

    //if (SESSION_DIVISION != "" && SESSION_DIVISION != 0) {
    //    $('#addEditDivision').prop('disabled', true);

    //    if (SESSION_DEPARTMENT_ID != "" && SESSION_DEPARTMENT_ID != 0) {
    //        $('#addEditDepartment').prop('disabled', true);

    //        if (SESSION_SECTION_CODE != "" && SESSION_SECTION_CODE != 0) {
    //            $('#addEditSection').prop('disabled', true);
    //        } else {
    //            $('#addEditSection').prop('disabled', false);
    //        }
    //    } else {
    //        $('#addEditDepartment').prop('disabled', false);
    //    }
    //} else {
    //    $('#addEditDivision').prop('disabled', false);
    //    $('#addEditDepartment').prop('disabled', true);
    //    $('#addEditSection').prop('disabled', true);
    //}
}

$('#addEditExternal').click(function () {
    if ($(this).is(':checked')) {
        $('#addEditReferenceNo').val(gReffNo);
        if (gImpactOther == "1") {
            $("#addEditImpactToOtherDocument").prop("checked", true);
        } else {
            $("#addEditImpactToOtherDocument").prop("checked", false);
        }
        $('#addEditSource').val(gSourceNo);

        $('#mandatoryMarkReferenceNo').show();
        $('#mandatoryMarkSource').show();

        $('#addEditReferenceNo').prop('disabled', false);
        $('#addEditImpactToOtherDocument').prop('disabled', false);
        $('#addEditSource').prop('disabled', false);
    } else {
        $('#addEditReferenceNo').val("");
        $('#addEditImpactToOtherDocument').val("");
        $('#addEditSource').val("");

        $('#mandatoryMarkReferenceNo').hide();
        $('#mandatoryMarkSource').hide();

        $('#addEditReferenceNo').prop('disabled', true);
        $('#addEditImpactToOtherDocument').prop('disabled', true);
        $('#addEditSource').prop('disabled', true);

    }
});

$('#editExternal').click(function () {
    if ($(this).is(':checked')) {
        $('#editReferenceNo').val(gReffNo);
        if (gImpactOther == "1") {
            $("#editImpactToOtherDocument").prop("checked", true);
        } else {
            $("#editImpactToOtherDocument").prop("checked", false);
        }
        $('#editSource').val(gSourceNo);

        $('#mandatoryMarkEditReferenceNo').show();
        $('#mandatoryMarkEditSource').show();

        $('#editReferenceNo').prop('disabled', false);
        $('#editImpactToOtherDocument').prop('disabled', false);
        $('#editSource').prop('disabled', false);
    } else {
        $('#editReferenceNo').val("");
        $("#editImpactToOtherDocument").prop("checked", false);
        $('#editSource').val("");

        $('#mandatoryMarkEditReferenceNo').hide();
        $('#mandatoryMarkEditSource').hide();

        $('#editReferenceNo').prop('disabled', true);
        $('#editImpactToOtherDocument').prop('disabled', true);
        $('#editSource').prop('disabled', true);

    }
});

function onDeletePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentId = row.DOCUMENT_TRANSACTION_ID;
    gFilePath = row.FILE_PATH;

    showDangerConfirm(deleteMessage("Document", row.DOCUMENT_CODE || row.DOCUMENT_TRANSACTION_NAME), deleteData, "Delete");

}

function deleteData() {
    popUpProgressShow();

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_TRANSACTION_ID: gDocumentId,
            FILE_PATH: gFilePath
        },
        success: function (result) {
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
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}

function onEditPrepare() {
    gImpactOther = null;
    gSourceNo = null;
    gReffNo = null;

    $('#editRemark').val("");

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentId = row.DOCUMENT_TRANSACTION_ID;

    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.getByKey,
        async: false,
        data: {
            DOCUMENT_TRANSACTION_ID: gDocumentId
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;
                //showErrorMesgGrowl(data.STATUS);
                if (data.STATUS == "1" || data.STATUS == "5") {
                    showErrorMesgGrowl("Edit is not allowed");
                } else {
                    gScreenMode = 'EDIT';
                    gImpactOther = data.IMPACT_OTHER_FLAG;
                    gReffNo = data.REFERENCE_NO;
                    gSourceNo = data.SOURCE;

                    $('#editDocumentNo').prop('disabled', true);
                    $('#editRevision').prop('disabled', true);

                    $('#modalEdit-title').html("Edit Document");
                    $('#modalEdit-subtitle').text(data.DOCUMENT_CODE + " • Rev. " + data.REVISION);
                    updateEditStatusPanel(data);
                    $('#editDocumentId').val(data.DOCUMENT_TRANSACTION_ID);
                    $('#editDocumentNo').val(data.DOCUMENT_CODE);
                    $('#editRevision').val(data.REVISION);
                    $('#editDocumentName').val(data.DOCUMENT_TRANSACTION_NAME);
                    $('#editDate').val(data.DOCUMENT_DATE.substring(0, 10));
                    $('#editItemChanged').val(data.ITEM_CHANGED);
                    $('#editReason').val(data.REASON);

                    gFilePath = data.FILE_PATH;
                    gDocumentTypeId = data.DOCUMENT_ID;

                    updateEditCurrentFileDisplay(data.FILE_PATH);
                    if (data.FILE_PATH != null && documentMaintenanceConfig.deleteFilePathFlag != "False") {
                        $('#btn_delete_filepath').show();
                    } else {
                        $('#btn_delete_filepath').hide();
                    }

                    if (data.EXTERNAL_FLAG == "1") {
                        $("#editExternal").prop("checked", true);
                        $('#editReferenceNo').prop('disabled', false);
                        $('#editSource').prop('disabled', false);
                        $('#editImpactToOtherDocument').prop('disabled', false);

                        $('#mandatoryMarkEditReferenceNo').show();
                        $('#mandatoryMarkEditSource').show();
                    } else {
                        $("#editExternal").prop("checked", false);
                        $('#editReferenceNo').prop('disabled', true);
                        $('#editSource').prop('disabled', true);
                        $('#editImpactToOtherDocument').prop('disabled', true);

                        $('#mandatoryMarkEditReferenceNo').hide();
                        $('#mandatoryMarkEditSource').hide();
                    }

                    if (data.IMPACT_OTHER_FLAG == "1") {
                        $("#editImpactToOtherDocument").prop("checked", true);
                    } else {
                        $("#editImpactToOtherDocument").prop("checked", false);
                    }

                    $('#editReferenceNo').val(data.REFERENCE_NO);
                    $('#editSource').val(data.SOURCE);

                    $("#modalEdit").modal();
                    $('#modalEdit').on('shown.bs.modal', function () {
                        $('#editDocumentName').focus();
                    });
                }
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onclickDistribution(documentTransactionId) {
    $('#modalDistribution-title').html("Document Master Distribution");

    $("#datagridDistribution").dataTable().fnDestroy();
    $('#datagridDistribution').DataTable({
        "ordering": false,
        "oLanguage": { "sEmptyTable": "Data not found." },
        "dom": "<t><r><'row mt-10'<'col-lg-2'i><'col-lg-1'><'col-lg-5'l><'col-lg-4'p>>",
        "processing": true,
        "serverSide": true,
        "ajax": {
            "url": documentMaintenanceConfig.urls.p4DMaintenanceSearchDocumentApproval,
            "type": "POST",
            "datatype": "json",
            "data": function (d) {
                d.DOCUMENT_TRANSACTION_ID = documentTransactionId
            },
        },
        "columnDefs": [
            {
                "targets": [0],
                "className": "text-center",
            },
            {
                "targets": [1],
                "className": "text-left",
            },
            {
                "targets": [2],
                "className": "text-left",
            },
            {
                "targets": [3],
                "className": "text-right",
            }
            ,
            {
                "targets": [4],
                "className": "text-center",
            }
        ],
        "columns": [
            {
                render: function (data, type, row, meta) {
                    return meta.row + meta.settings._iDisplayStart + 1;
                }
            },
            {
                render: function (data, type, row) {
                    return row["DIVISION"] + " - " + row["DIVISION_NAME"];
                }
            },
            {
                render: function (data, type, row) {
                    return row["DEPARTMENT_CODE"] + " - " + row["DEPARTMENT_NAME"];
                }
            },
            { "data": "DATE", "name": "DATE" },
            {
                render: function (data, type, row) {
                    if (row["STATUS"] == 1) {
                        return '<span class="label label-success">' + row["STATUS_DISPLAY"] + '</span>';
                    } else {
                        return '<span class="label label-danger">' + row["STATUS_DISPLAY"] + '</span>';
                    }
                }
            },
        ],
        "fnDrawCallback": function (oSettings) {
            $('#datagridDistribution tbody > tr').removeClass('selected');
            $('.grid-checkbox').not(this).prop('checked', false);

            $("th").removeClass('sorting_asc');
        }
    });

    $("#datagridDistribution").on("click", "tbody tr", function (e) {
        if (event.target.type !== 'checkbox') {
            $(':checkbox', this).trigger('click');
        }
    });

    $("#datagridDistribution").on('change', "input[type='checkbox']", function (e) {
        $('#datagridDistribution tbody > tr').removeClass('selected');
        $('.grid-checkbox').not(this).prop('checked', false);
    });

    $("#modalDistribution").modal();
}

// Approve wajib tanda tangan elektronik (Draw/Type, 2026-11-06) - dialognya
// sekaligus jadi konfirmasi; Reject tetap pakai konfirmasi biasa.
function onApproveRejectPrepare() {
    if (gApprovalMode == "approve" && isUnsignedLevel(gDocumentLevel)) {
        showConfirmMesg("Approve Document \"" + confirmText(gDocumentNo) + "\"?", function () { onApproveReject(null); }, "Confirmation", "Approve", "Cancel");
        return;
    }
    if (gApprovalMode == "approve") {
        openSignaturePad({
            title: "Sign to approve",
            subtitle: "Your signature will appear on the approval sheet of this document.",
            confirmText: "Sign & Approve",
            onSigned: function (sig) { onApproveReject(sig); }
        });
        return;
    }
    showWarningConfirm("Reject Document \"" + confirmText(gDocumentNo) + "\"?", function () { onApproveReject(null); }, "Reject");
}

function onApproveReject(signature) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.approveReject,
        async: false,
        data: {
            approvalId: gApprovalId,
            workflowSeq: approvalSeq,
            approver: approver,
            remark: $('#approvalRemark').val(),
            mode: gApprovalMode,
            DOCUMENT_TRANSACTION_ID: gId,
            SIGN_METHOD: signature ? signature.SIGN_METHOD : "",
            SIGN_DATA: signature ? signature.SIGN_DATA : "",
            SIGN_FONT: signature ? (signature.SIGN_FONT || "") : "",
        },
        success: function (result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                getApprovalHeader();
                getApprovalDetail();
                getApprovalHistoryDetail();
                popUpProgressHide();
                $("#modalApprovalForm").modal('hide');
            } else {
                popUpProgressHide();
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onDownloadAttachment(attachment, documentId) {
    gAttachmentPath = attachment;
    gDocumentId = documentId;
    downloadAttachment();
}

function downloadAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = documentMaintenanceConfig.urls.downloadAttachment;
        link = link.replace("-1", gAttachmentPath);
        link = link.replace("-2", gDocumentId);
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function onViewAttachment(attachment, documentId) {
    gAttachmentPath = attachment;
    gDocumentId = documentId;
    viewAttachment();
}

function viewAttachment()
{
    if (gAttachmentPath !== "null" && gAttachmentPath !== null && gAttachmentPath !== "") {
        var link = documentMaintenanceConfig.urls.viewAttachment;
        link = link.replace("-1", gAttachmentPath);
        link = link.replace("-2", gDocumentId);
        link = link.replace("-3", "1");
        window.open(link, '_blank');
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function onExcelPreview(filePath) {
    if (filePath !== "null" && filePath !== null) {
        var link = documentMaintenanceConfig.urls.excelViewerPreview;
        link = link.replace("-1", encodeURIComponent(filePath));
        window.open(link, '_blank');
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function downloadPengesahan() {
    var link = documentMaintenanceConfig.urls.downloadPengesahan;
    link = link.replace("-1", gId);
    link = link.replace("-2", gApprovalId);
    window.location.href = link;
}

function onclickLog(documentId) {
    countTotalLog(documentId);
    openLogModal(documentId)
}

function countTotalLog(documentId) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: documentMaintenanceConfig.urls.countTotalLog,
        data: {
            DOCUMENT_TRANSACTION_ID : documentId
        },
        success: function (result) {
            if (result.status == true) {
                popUpProgressHide();
                $('#logTotalView').html(result.totalView);
                $('#logTotalDownload').html(result.totalDownload);
                $('#logTotalPrint').html(result.totalPrint);
                $('#logTotalPublished').html(result.totalPublished);
            } else {
                popUpProgressHide();
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function openLogModal(documentId) {
    $('#modalDocumentLog-title').html("Document Log");

    $("#datagridDocumentLog").dataTable().fnDestroy();
    $('#datagridDocumentLog').DataTable({
        "ordering": false,
        "oLanguage": { "sEmptyTable": "Data not found." },
        "dom": "<t><r><'row mt-10'<'col-lg-2'i><'col-lg-1'><'col-lg-5'l><'col-lg-4'p>>",
        "processing": true,
        "serverSide": true,
        "ajax": {
            "url": documentMaintenanceConfig.urls.searchDocumentLog,
            "type": "POST",
            "datatype": "json",
            "data": function (d) {
                d.DOCUMENT_TRANSACTION_ID = documentId
            },
        },
        "columnDefs": [
            {
                "targets": [0],
                "className": "text-center",
            },
            {
                "targets": [1],
                "className": "text-center",
            },
            {
                "targets": [2],
                "className": "text-left",
            },
            {
                "targets": [3],
                "className": "text-right",
            }
        ],
        "columns": [
            {
                render: function (data, type, row, meta) {
                    return meta.row + meta.settings._iDisplayStart + 1;
                }
            },
            { "data": "LOG_TYPE_VAL", "name": "LOG_TYPE_VAL" },
            { "data": "CREATED_BY", "name": "CREATED_BY" },
            {
                render: function (data, type, row) {
                    if (row["CREATED_DT"] != null) {
                        return moment(row["CREATED_DT"]).format("DD-MM-YYYY HH:mm:ss");
                    } else {
                        return "";
                    }
                }
            },
        ],
        "fnDrawCallback": function (oSettings) {
            $('#datagridDocumentLog tbody > tr').removeClass('selected');
            $('.grid-checkbox').not(this).prop('checked', false);

            $("th").removeClass('sorting_asc');
        }
    });

    $("#datagridDocumentLog").on("click", "tbody tr", function (e) {
        if (event.target.type !== 'checkbox') {
            $(':checkbox', this).trigger('click');
        }
    });

    $("#datagridDocumentLog").on('change', "input[type='checkbox']", function (e) {
        $('#datagridDocumentLog tbody > tr').removeClass('selected');
        $('.grid-checkbox').not(this).prop('checked', false);
    });

    $("#modalDocumentLog").modal();
}
