// DocumentControlDashboard. Server values come from documentControlDashboardConfig in the view.
// Rincian teknis kolom Acceptance (rasio, jumlah department/divisi, hint
// belum di-Send, klik untuk detail popup) cuma untuk QMS/document-control
// staff - sama seperti P4DMaintenance/Index (request Hendra 2026-08-15).
var gIsQms = documentControlDashboardConfig.canDocumentControlAccess;

function computeGridHeight() {
    var gridTop = $("#datagrid").offset().top;
    var footerHeight = $("#divFooter").outerHeight(true) || 0;

    return Math.max(300, $(window).height() - gridTop - footerHeight - 70);
}

// ---------- KPI cards (Total / Effective / Need Review / Pending Acknowledgement) ----------
function updateHeaderStats(rows) {
    rows = rows || [];
    var total = rows.length;
    var effective = 0, needReview = 0, pendingAck = 0;
    var today = moment().startOf("day");
    var in30Days = moment().startOf("day").add(30, "days");

    rows.forEach(function (r) {
        if (r.DOC_STATUS == "5") effective++;

        if (r.NEXT_REVIEW_DATE) {
            var reviewDate = moment(r.NEXT_REVIEW_DATE).startOf("day");
            if (!reviewDate.isBefore(today) && !reviewDate.isAfter(in30Days)) needReview++;
        }

        if (r.ACK_TOTAL != null && r.ACK_DONE != null && r.ACK_DONE < r.ACK_TOTAL) pendingAck++;
    });

    $("#dcdTotal").text(total);
    $("#dcdEffective").text(effective);
    $("#dcdNeedReview").text(needReview);
    $("#dcdPendingAck").text(pendingAck);

    // surfaces the pending-acknowledgement count on the folders toggle too,
    // so it stays useful (not just a bare arrow) even when the sidebar - which
    // now also holds Register Overview - is collapsed.
    dcdLastPendingAck = pendingAck;
    refreshToggleBadge();
}

// ---------- Folders / Register Overview sidebar toggles - each independent
// (restored 2026-08-29: "keduanya bisa show hide" separately, Register Overview
// defaults hidden like before) - top-level (not inside $(document).ready) so
// updateHeaderStats() above can reach dcdLastPendingAck too ----------
var dcdLastPendingAck = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    $("#dpToggleBadge").text(dcdLastPendingAck).toggle(collapsed && dcdLastPendingAck > 0);
}

function setFoldersPanelCollapsed(collapsed) {
    var $row = $("#dpLayoutRow");

    $row.toggleClass("folders-collapsed", collapsed);
    // tombol hide ada di header panel Folders; tombol toolbar cuma untuk show
    // (request Hendra 2026-11-05)
    $("#btn_toggleFolders").toggleClass("dcd-hidden", !collapsed);

    // width-only change (grid height is unaffected), but DevExtreme needs a nudge
    // to reflow column widths into the space freed/reclaimed by the transition.
    setTimeout(function () {
        $("#datagrid").dxDataGrid("instance").updateDimensions();
    }, 230);
}

function setStatsPanelCollapsed(collapsed) {
    var $row = $("#dpLayoutRow");
    var $icon = $("#btn_toggleStats i");

    $row.toggleClass("stats-collapsed", collapsed);
    $icon.toggleClass("fa-angle-double-right", !collapsed).toggleClass("fa-angle-double-left", collapsed);
    $("#btn_toggleStats").attr("title", collapsed ? "Show Register Overview" : "Hide Register Overview");
    refreshToggleBadge();

    setTimeout(function () {
        $("#datagrid").dxDataGrid("instance").updateDimensions();
    }, 230);
}

var dcdCategoryPillClasses = ["dms-pill-info", "dms-pill-success", "dms-pill-warning", "dms-pill-primary", "dms-pill-default"];
function dcdCategoryPillClass(code) {
    var sum = 0;
    for (var i = 0; i < (code || "").length; i++) sum += code.charCodeAt(i);
    return dcdCategoryPillClasses[sum % dcdCategoryPillClasses.length];
}

function dcdOpenDocument(row) {
    onViewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
}

// ---------- per-row "more actions" kebab menu (request Hendra 2026-08-15:
// gabungkan View + Download jadi satu kebab, bukan 2 tombol terpisah) ----------
var $dcdRowMenu = null;

function dcdBuildRowActionMenu() {
    if ($dcdRowMenu) return $dcdRowMenu;

    $dcdRowMenu = $('<ul class="dropdown-menu dp-dropdown-menu" role="menu"></ul>')
        .css({ position: "absolute", display: "none", "z-index": 2000, "min-width": "160px" })
        .appendTo("body");

    $(document).on("click", function () {
        $dcdRowMenu.hide();
    });

    return $dcdRowMenu;
}

function dcdMenuItem(cssClass, icon, label, onClick) {
    var $li = $('<li><a href="#" class="' + cssClass + '"><i class="fa ' + icon + '"></i> ' + label + '</a></li>');
    $li.find("a").on("click", function (e) {
        e.preventDefault();
        $dcdRowMenu.hide();
        onClick();
    });
    return $li;
}

function dcdOpenRowActionMenu($btn, row) {
    var $menu = dcdBuildRowActionMenu();
    $menu.empty();

    $menu.append(dcdMenuItem("dp-menu-preview", "fa-eye", "View", function () {
        dcdOpenDocument(row);
    }));

    $menu.append(dcdMenuItem("dp-menu-download", "fa-download", "Download", function () {
        onDownloadAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
    }));

    // Download PDF with a stamp chosen by purpose (request Hendra 2026-11-11). Role
    // function DOCUMENTCONTROLDASHBOARD-EXPORTPDF; Approved or Published only (the server
    // checks both again).
    if (documentControlDashboardConfig.canExportPdf && (row.DOC_STATUS == "1" || row.DOC_STATUS == "5")) {
        $menu.append(dcdMenuItem("dp-menu-download", "fa-file-pdf-o", "Download PDF", function () {
            dcdExportPdf(row);
        }));
    }

    // Copy Register: controlled copies of every revision and their withdrawal.
    if (documentControlDashboardConfig.canExportPdf) {
        $menu.append(dcdMenuItem("dp-menu-history", "fa-list-ol", "Copy Register", function () {
            // After this click has finished bubbling, or the outside-click handler closes it again.
            setTimeout(function () { dcdOpenCopyRegister(row); }, 0);
        }));
    }

    // Reuse total popup History yang sudah ada & teruji di Document
    // Preparation (_DocumentHistoryPopup.cshtml, dirender dari Index.cshtml
    // dashboard ini) - onHistoryPrepare() otomatis baca row.DOCUMENT_CODE
    // dari getSelectedRowData() (request Hendra 2026-08-16).
    $menu.append(dcdMenuItem("dp-menu-history", "fa-history", "History", function () {
        $("#datagrid").dxDataGrid("instance").selectRows([row.DOCUMENT_TRANSACTION_ID], false);
        onHistoryPrepare();
    }));

    $menu.append(dcdMenuItem("dp-menu-move", "fa-folder-o", "Move to Folder", function () {
        dcdMoveDocumentsToFolder([row.DOCUMENT_TRANSACTION_ID], $btn);
    }));

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

// Purpose decides the stamp: MASTER (archive), CONTROLLED COPY (numbered per revision,
// recipient required) or UNCONTROLLED COPY (reference / audit / external).
function dcdExportPdf(row) {
    $.MessageBox({
        title: "Download PDF",
        message: confirmText(row.DOCUMENT_CODE || row.DOCUMENT_NAME || "") + " (Rev. " + (row.REVISION != null ? row.REVISION : 0) + ")",
        input: {
            purpose: {
                type: "select",
                label: "Purpose",
                title: "Choose a purpose",
                options: {
                    "UNCONTROLLED": "Reference / audit / external - UNCONTROLLED COPY",
                    "CONTROLLED": "Distribution - CONTROLLED COPY (numbered)",
                    "MASTER": "Archive - MASTER"
                },
                defaultValue: "UNCONTROLLED"
            },
            recipient: {
                type: "text",
                label: "Recipient (required for Controlled Copy)",
                title: "Department or person",
                maxlength: 200
            },
            notes: {
                type: "text",
                label: "Notes",
                title: "Optional",
                maxlength: 500
            }
        },
        buttonDone: "Download",
        buttonFail: "Cancel",
        customClass: "msg-confirm",
        filterDone: function (data) {
            if (!data.purpose) return "Choose a purpose.";
            if (data.purpose === "CONTROLLED" && !$.trim(data.recipient)) return "Enter the recipient of the controlled copy.";
        }
    }).done(function (data) {
        var $form = $('<form method="post" target="_blank"></form>').attr("action", documentControlDashboardConfig.urls.exportPdf);
        $('<input type="hidden" name="documentTransactionId">').val(row.DOCUMENT_TRANSACTION_ID).appendTo($form);
        $('<input type="hidden" name="purpose">').val(data.purpose).appendTo($form);
        $('<input type="hidden" name="recipient">').val($.trim(data.recipient || "")).appendTo($form);
        $('<input type="hidden" name="notes">').val($.trim(data.notes || "")).appendTo($form);
        $form.appendTo("body").trigger("submit").remove();
    });
}

// ---------- folder breadcrumb/title + grid filter (request Hendra 2026-08-28) ----------
// Called from _FolderTree.cshtml's dcdSelectFolder(). The grid already loads every
// document once (loadMode:"raw"/length:100000) and relies on DevExtreme's own
// client-side filtering elsewhere (see the DOCUMENT_CODE deep-link filter in
// onContentReady below) - folder selection reuses that same mechanism, no extra
// server round-trip.
function dcdApplyFolderFilter(folderId) {
    var grid = $("#datagrid").dxDataGrid("instance");
    // NOTE: columnOption(field, "filterValue", value) silently does nothing for a
    // hidden (visible:false) column in this DevExtreme version - verified directly.
    // grid.filter(expr) works regardless of column visibility and still combines
    // (AND) with any other active column filters - filter(null) clears just this
    // criterion, leaving other filters (e.g. the DOCUMENT_CODE deep-link one below,
    // or user header filters) untouched.
    grid.filter(folderId === null ? null : ["FOLDER_ID", "=", folderId]);

    // Breadcrumb only shown for a nested folder (depth > 1) - the title below
    // already names the current folder, so a 1-item breadcrumb would just repeat
    // it (request Hendra 2026-08-29: "tulisan breadcrumb ada 2, buat satu aja").
    var $crumb = $("#dcdCrumb").empty();
    if (folderId === null) {
        $("#dcdFolderTitle").text("All Documents");
    } else {
        var path = dcdGetFolderPath(folderId);
        if (path.length > 1) {
            path.forEach(function (folder, i) {
                if (i > 0) { $("<span>").addClass("dcd-crumb-sep").text("›").appendTo($crumb); }
                $("<span>").addClass("dcd-crumb-item").text(folder.FOLDER_NAME).on("click", function () { dcdSelectFolder(folder.FOLDER_ID); }).appendTo($crumb);
            });
        }
        $("#dcdFolderTitle").text(path.length > 0 ? path[path.length - 1].FOLDER_NAME : "");
    }
}

// ---------- "Move to Folder" (bulk toolbar button + per-row kebab) ----------
function dcdMoveDocumentsToFolder(documentTransactionIds, $anchor) {
    dcdOpenFolderPicker($anchor, function (folderId) {
        $.ajax({
            type: "POST",
            url: documentControlDashboardConfig.urls.moveDocumentsToFolder,
            traditional: true,
            data: { documentTransactionIds: documentTransactionIds, folderId: folderId },
            success: function (result) {
                if (result.status == true) {
                    showSuccessMesgGrowl(result.message);
                    loadFolderTree();
                    $("#datagrid").dxDataGrid("instance").refresh();
                } else {
                    showErrorMesgGrowl(result.message);
                }
            },
            error: function (result) { showErrorMesgGrowl(result); }
        });
    });
}

$(document).ready(function () {
    $("#btn_moveToFolder").on("click", function () {
        var selected = $("#datagrid").dxDataGrid("instance").getSelectedRowKeys();
        if (selected.length === 0) { return; }
        dcdMoveDocumentsToFolder(selected, $(this));
    });

    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentControlDashboardConfig.urls.search,
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
            allowSelectAll: true
        },
        onSelectionChanged: function (e) {
            $("#btn_moveToFolder").toggle(e.selectedRowKeys.length > 0);
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
                dataField: "DOCUMENT_CODE",
                caption: "Document No",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    $("<span>").addClass("dcd-doc-link").text(options.data.DOCUMENT_CODE)
                        .on("click", function () { dcdOpenDocument(options.data); })
                        .appendTo(container);
                }
            },
            { dataField: "DOCUMENT_NAME", caption: "Document Name" },
            {
                dataField: "CATEGORY_CODE",
                caption: "Category",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    if (!row.CATEGORY_CODE) return;

                    $("<span>")
                        .addClass("dms-pill " + dcdCategoryPillClass(row.CATEGORY_CODE))
                        .attr("title", row.CATEGORY_NAME || "")
                        .text(row.CATEGORY_CODE)
                        .appendTo(container);
                }
            },
            {
                dataField: "REVISION",
                caption: "Revision",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.REVISION;
                    $("<span>").addClass("dms-pill dms-pill-info").text("Rev. " + (value != null ? value : 0)).appendTo(container);
                }
            },
            {
                dataField: "DOC_STATUS_VAL",
                caption: "Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = "dms-pill-default";
                    var icon = "fa-question-circle";

                    switch (row.DOC_STATUS) {
                        case "0": pillClass = "dms-pill-info"; icon = "fa-clock-o"; break;
                        case "1": pillClass = "dms-pill-success"; icon = "fa-check-circle"; break;
                        case "2": pillClass = "dms-pill-danger"; icon = "fa-times-circle"; break;
                        case "3": pillClass = "dms-pill-default"; icon = "fa-trash-o"; break;
                        case "4": pillClass = "dms-pill-danger"; icon = "fa-ban"; break;
                        case "5": pillClass = "dms-pill-success"; icon = "fa-check-circle"; break;
                    }

                    // Tampilan khusus grid ini saja - "Published" tanpa "(Effective)",
                    // master data TB_M_SYSTEM tidak diubah supaya tempat lain (mis.
                    // UserDashboard) tetap seperti biasa (request Hendra 2026-08-15).
                    var statusText = (row.DOC_STATUS_VAL || "-").replace(" (Effective)", "");
                    $("<span>").addClass("dms-pill " + pillClass).html('<i class="fa ' + icon + '"></i> ' + statusText).appendTo(container);
                }
            },
            {
                caption: "Acceptance",
                alignment: "center",
                calculateCellValue: function (row) {
                    return (row.ACK_DONE != null ? row.ACK_DONE : 0) + "/" + (row.ACK_TOTAL != null ? row.ACK_TOTAL : 0);
                },
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var total = row.ACK_TOTAL != null ? row.ACK_TOTAL : 0;
                    var done = row.ACK_DONE != null ? row.ACK_DONE : 0;
                    var pending = row.PENDING_DISTRIBUTION_COUNT || 0;
                    var divCount = row.DIVISION_COUNT || 0;
                    var complete = total > 0 && done >= total;

                    // User biasa (bukan QMS) cuma lihat label polos, sama seperti
                    // P4DMaintenance/Index - rincian teknis di bawah ini QMS-only.
                    if (!gIsQms) {
                        $("<span>")
                            .css({ fontWeight: 600, color: total === 0 ? "#8A94A6" : (complete ? "#1E9E63" : "#B36B00") })
                            .text(total === 0 ? "Not Distributed" : (complete ? "Accepted" : "Waiting"))
                            .appendTo(container);
                        return;
                    }

                    // Ikon panah kecil di kanan sel menandakan sel ini bisa diklik
                    // untuk membuka panel detail Acceptance (sidebar), biar tidak
                    // cuma mengandalkan cursor:pointer yang gampang tidak disadari
                    // user (request Hendra 2026-08-15).
                    container.css({ display: "flex", alignItems: "center", justifyContent: "center", gap: "6px", cursor: "pointer" });

                    var $wrap = $("<div>").css({ lineHeight: 1.3 });

                    if (total === 0) {
                        // Belum pernah di-"Send Distribution" ke department manapun -
                        // beda dari "Pending" (sudah dikirim, belum semua acknowledge),
                        // dan beda lagi dari "sudah dicentang tapi belum di-Send".
                        if (pending > 0) {
                            $("<span>")
                                .css({ fontWeight: 600, color: "#B36B00" })
                                .html('<i class="fa fa-exclamation-circle"></i> ' + pending + ' Not Sent Yet')
                                .appendTo($wrap);
                        } else {
                            $("<span>").css({ fontWeight: 600, color: "#8A94A6" }).text("Not Distributed").appendTo($wrap);
                        }
                    } else {
                        var waitingLabel = "Waiting (" + done + "/" + total + ")";
                        $("<span>")
                            .css({ fontWeight: 700, color: complete ? "#1E9E63" : "#B36B00" })
                            .text(complete ? "Accepted" : waitingLabel)
                            .appendTo($wrap);

                        $("<span>")
                            .css({ display: "block", fontSize: "10.5px", color: "#8A94A6", marginTop: "2px" })
                            .text(total + " Department" + (total !== 1 ? "s" : "") + " / " + divCount + " Division" + (divCount !== 1 ? "s" : ""))
                            .appendTo($wrap);

                        if (pending > 0) {
                            $("<span>")
                                .css({ display: "block", fontSize: "10.5px", color: "#B36B00", fontWeight: 600, marginTop: "2px" })
                                .html('<i class="fa fa-exclamation-circle"></i> ' + pending + ' Not Sent Yet')
                                .appendTo($wrap);
                        }
                    }

                    // Klik untuk lihat rincian per department (QMS-only) - lihat
                    // _AcceptanceDetailPopup.cshtml (request Hendra 2026-08-15).
                    container.on("click", function (e) {
                        e.stopPropagation();
                        dcdOpenAcceptanceDetail(row);
                    });

                    $wrap.appendTo(container);
                    $("<i>").addClass("fa fa-chevron-circle-right dcd-acc-chevron").attr("title", "Click for detail").appendTo(container);
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Effective Date",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD MMM YYYY") : "");
                }
            },
            {
                dataField: "OWNER_FULL_NAME",
                caption: "Document Owner",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    $("<span>").text(row.OWNER_FULL_NAME || "-").appendTo(container);
                    if (row.DEPARTMENT_NAME) {
                        $("<span>").addClass("dcd-owner-dept").text(row.DEPARTMENT_NAME).appendTo(container);
                    }
                }
            },
            {
                dataField: "DEPARTMENT_CODE",
                caption: "Department",
                calculateDisplayValue: function (row) {
                    return row.DEPARTMENT_CODE != null ? row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME : "";
                }
            },
            {
                dataField: "CLASSIFIED_VAL",
                caption: "Classification",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    if (!row.CLASSIFIED_VAL) return;

                    var pillClass = row.CLASSIFIED_VAL.toUpperCase().indexOf("RAHASIA") >= 0 ? "dms-pill-danger" : "dms-pill-default";
                    $("<span>").addClass("dms-pill " + pillClass).text(row.CLASSIFIED_VAL).appendTo(container);
                }
            },
            {
                caption: "Last Updated",
                alignment: "right",
                calculateCellValue: function (row) {
                    return row.CHANGED_DT || row.CREATED_DT;
                },
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT || options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD MMM YYYY HH:mm") : "");
                }
            },
            { dataField: "FOLDER_ID", dataType: "number", caption: "Folder", visible: false, allowHeaderFiltering: false },
            { dataField: "FOLDER_NAME", caption: "Folder Name", visible: false },
            { dataField: "LOCATION", caption: "Location", visible: false },
            { dataField: "REFERENCE_NO", caption: "Reference No", visible: false },
            {
                dataField: "STATUS_VAL",
                caption: "Registration Status",
                visible: false,
                calculateCellValue: function (row) {
                    return row.STATUS == 0 ? "Requested" : row.STATUS_VAL;
                }
            },
            { dataField: "ITEM_CHANGED", caption: "Item Changed", visible: false },
            { dataField: "REASON", caption: "Reason", visible: false },
            { dataField: "CREATED_BY", caption: "Created By", visible: false },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY HH:mm") : "");
                }
            },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
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
                            dcdOpenRowActionMenu($(this), row);
                        })
                        .appendTo(container);
                }
            }
        ],
        onContentReady: function (e) {
            //Deep-link lama (mis. redirect setelah login dari /DocumentControlDashboard/Index?DOCUMENT_CODE=...) -
            //terapkan sebagai filter kolom Document No sekali saja saat grid pertama kali siap.
            var params = new URLSearchParams(window.location.search);
            var documentCode = params.get("DOCUMENT_CODE");
            if (documentCode && !e.component.columnOption("DOCUMENT_CODE", "filterValue")) {
                e.component.columnOption("DOCUMENT_CODE", "filterValue", documentCode);
            }

            var count = e.component.totalCount();
            $("#dcdFolderSubtitle").text(count >= 0 ? (count + " document" + (count !== 1 ? "s" : "")) : "");
        }
    }));

    bindGridToolbar("columns filters refresh resize");

    // ---------- toggle the Folders sidebar (collapses to the left, grid
    // reflows into the freed width) ----------
    $("#btn_toggleFolders").on("click", function () {
        setFoldersPanelCollapsed(!$("#dpLayoutRow").hasClass("folders-collapsed"));
    });

    // ---------- toggle the docked Register Overview panel (independent of
    // Folders - defaults hidden) ----------
    $("#btn_toggleStats").on("click", function () {
        setStatsPanelCollapsed(!$("#dpLayoutRow").hasClass("stats-collapsed"));
    });

    $("#btn_closeStatsPanel").on("click", function () {
        setStatsPanelCollapsed(true);
    });

    $("#btn_export").on("click", function () {
        window.location.href = documentControlDashboardConfig.urls.downloadExcel;
    });

    // ---------- toolbar: reset ----------
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        grid.clearFilter();
        grid.searchByText("");
        grid.clearSelection();
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

var adpGridData = [];

function dcdOpenAcceptanceDetail(row) {
    $("#adpDocumentCode").text(row.DOCUMENT_CODE || "-");
    $("#acceptancePanel").addClass("open");

    $.ajax({
        type: "POST",
        url: documentControlDashboardConfig.urls.searchDistributionDetail,
        data: { documentTransactionId: row.DOCUMENT_TRANSACTION_ID },
        success: function (result) {
            adpGridData = (result.status && result.data) ? result.data : [];

            if ($("#acceptanceDetailGrid").data("dxDataGrid")) {
                $("#acceptanceDetailGrid").dxDataGrid("instance").option("dataSource", adpGridData);
            } else {
                buildAcceptanceDetailGrid();
            }
        },
        error: function () {
            showErrorMesgGrowl("Failed to load acceptance detail");
        }
    });
}

function buildAcceptanceDetailGrid() {
    $("#acceptanceDetailGrid").dxDataGrid({
        dataSource: adpGridData,
        showBorders: true,
        columnAutoWidth: true,
        wordWrapEnabled: false,
        height: "calc(100vh - 110px)",
        searchPanel: { visible: true, width: "100%", placeholder: "Search department..." },
        groupPanel: { visible: false },
        grouping: { autoExpandAll: true },
        paging: { pageSize: 20 },
        pager: { visible: true, displayMode: "compact", showInfo: true },
        noDataText: "No distribution target for this document yet.",
        columns: [
            {
                dataField: "DIVISION_NAME",
                caption: "Division",
                groupIndex: 0
            },
            {
                caption: "Department",
                calculateDisplayValue: function (d) { return (d.DEPARTMENT_CODE || "") + " - " + (d.DEPARTMENT_NAME || ""); }
            },
            {
                dataField: "STATUS",
                caption: "Status",
                alignment: "center",
                width: 130,
                cellTemplate: function (container, options) {
                    var d = options.data;
                    var pillClass = "adp-pill-default";
                    var pillText = "Not Sent";

                    if (d.STATUS == 1) {
                        if (d.ACCEPTED_FLAG == 1) {
                            pillClass = "adp-pill-success";
                            pillText = "Accepted";
                        } else {
                            pillClass = "adp-pill-warning";
                            pillText = "Waiting";
                        }
                    }

                    $("<span>").addClass("adp-pill " + pillClass).text(pillText).appendTo(container);
                }
            }
        ]
    });
}

// ---------- Copy Register (request Hendra 2026-11-11) ----------
var crpRow = null;
var crpData = [];

function dcdOpenCopyRegister(row) {
    crpRow = row;
    $("#acceptancePanel").removeClass("open");
    $("#crpDocumentCode").text((row.DOCUMENT_CODE || "-") + (row.DOCUMENT_NAME ? " · " + row.DOCUMENT_NAME : ""));
    $("#crpSummary").empty();
    $("#crpBody").html('<div class="crp-empty"><i class="fa fa-spinner fa-spin"></i> Loading...</div>');
    $("#copyRegisterPanel").addClass("open");
    crpLoad();
}

function crpLoad() {
    $.ajax({
        type: "GET",
        url: documentControlDashboardConfig.urls.searchCopyRegister,
        data: { documentTransactionId: crpRow.DOCUMENT_TRANSACTION_ID },
        cache: false,
        success: function (result) {
            if (!result.status) {
                $("#crpBody").html('<div class="crp-empty"></div>').find(".crp-empty").text(result.message || "Failed to load the copy register");
                return;
            }
            crpData = result.data || [];
            crpRender();
        },
        error: function () {
            $("#crpBody").html('<div class="crp-empty">Failed to load the copy register</div>');
        }
    });
}

function crpFormatDate(value) {
    return value ? moment(value).format("DD MMM YYYY HH:mm") : "-";
}

function crpRender() {
    var showWithdrawn = $("#crpShowWithdrawn").is(":checked");
    var total = crpData.length;
    var circulating = crpData.filter(function (d) { return !d.WITHDRAWN_DT; }).length;
    var obsoleteCirculating = crpData.filter(function (d) { return !d.WITHDRAWN_DT && d.IS_OBSOLETE; }).length;

    var $summary = $("#crpSummary").empty();
    [
        { value: total, label: "Copies issued" },
        { value: circulating, label: "In circulation" },
        { value: obsoleteCirculating, label: "Obsolete, not withdrawn", alert: obsoleteCirculating > 0 }
    ].forEach(function (s) {
        $('<div class="crp-stat"></div>').toggleClass("crp-stat-alert", !!s.alert)
            .append($('<div class="crp-stat-value"></div>').text(s.value))
            .append($('<div class="crp-stat-label"></div>').text(s.label))
            .appendTo($summary);
    });

    var $body = $("#crpBody").empty();
    if (total === 0) {
        $body.html('<div class="crp-empty">No controlled copy has been issued for this document yet.<br/>Use Download PDF &rarr; Distribution to issue one.</div>');
        return;
    }

    // Group by revision, newest first (the server already sorts).
    var revisions = [];
    crpData.forEach(function (d) {
        var key = d.REVISION != null ? d.REVISION : 0;
        var group = revisions.filter(function (g) { return g.revision === key; })[0];
        if (!group) { group = { revision: key, obsolete: d.IS_OBSOLETE, items: [] }; revisions.push(group); }
        group.items.push(d);
    });

    revisions.forEach(function (g) {
        var visible = g.items.filter(function (d) { return showWithdrawn || !d.WITHDRAWN_DT; });
        var open = g.items.filter(function (d) { return !d.WITHDRAWN_DT; }).length;

        var $rev = $('<div class="crp-rev"></div>').text("Rev. " + g.revision).appendTo($body);
        $('<span class="crp-pill"></span>')
            .addClass(g.obsolete ? "crp-pill-default" : "crp-pill-success")
            .text(g.obsolete ? "Obsolete" : "Current")
            .appendTo($rev);

        if (g.obsolete && open > 0) {
            $('<div class="crp-rev-warning"></div>')
                .html('<i class="fa fa-exclamation-triangle"></i> ')
                .append(document.createTextNode(open + " cop" + (open > 1 ? "ies" : "y") + " of this obsolete revision still in circulation. Withdraw and replace them with the current revision."))
                .appendTo($body);
        }

        if (visible.length === 0) {
            $('<div class="crp-muted" style="font-size:12px;margin-bottom:8px;"></div>').text("All copies withdrawn.").appendTo($body);
        }

        visible.forEach(function (d) { crpCard(d).appendTo($body); });
    });
}

function crpCard(d) {
    var withdrawn = !!d.WITHDRAWN_DT;
    var $card = $('<div class="crp-card"></div>').toggleClass("crp-withdrawn", withdrawn);

    var $top = $('<div class="crp-card-top"></div>').appendTo($card);
    $('<span class="crp-copy-no"></span>').text("Copy No. " + ("0" + d.COPY_NO).slice(-2)).appendTo($top);
    $('<span class="crp-pill"></span>')
        .addClass(withdrawn ? "crp-pill-default" : (d.IS_OBSOLETE ? "crp-pill-danger" : "crp-pill-success"))
        .text(withdrawn ? "Withdrawn" : "In circulation")
        .appendTo($top);
    if (!withdrawn) {
        $('<button type="button" class="crp-withdraw-btn"><i class="fa fa-undo"></i> Withdraw</button>')
            .on("click", function () { crpWithdraw(d); })
            .appendTo($top);
    }

    $('<div class="crp-line"></div>').append("<b>To:</b> ").append(document.createTextNode(d.RECIPIENT || "-")).appendTo($card);
    $('<div class="crp-line crp-muted"></div>').text("Issued by " + (d.CREATED_BY || "-") + " on " + crpFormatDate(d.CREATED_DT)).appendTo($card);
    if (d.NOTES) $('<div class="crp-line crp-muted"></div>').text("Note: " + d.NOTES).appendTo($card);
    if (withdrawn) {
        $('<div class="crp-line crp-muted"></div>')
            .text("Withdrawn by " + (d.WITHDRAWN_BY || "-") + " on " + crpFormatDate(d.WITHDRAWN_DT) + (d.WITHDRAW_NOTE ? " · " + d.WITHDRAW_NOTE : ""))
            .appendTo($card);
    }
    return $card;
}

function crpWithdraw(d) {
    $.MessageBox({
        title: "Withdraw Copy",
        message: "Mark copy no. " + d.COPY_NO + " (Rev. " + (d.REVISION != null ? d.REVISION : 0) + ", to " + confirmText(d.RECIPIENT || "-") + ") as withdrawn? Do this once the copy has been returned or destroyed.",
        input: {
            note: { type: "text", label: "Note", title: "Optional, e.g. returned, destroyed, replaced by Rev. 1", maxlength: 500 }
        },
        buttonDone: "Withdraw",
        buttonFail: "Cancel",
        customClass: "msg-warning"
    }).done(function (data) {
        $.ajax({
            type: "POST",
            url: documentControlDashboardConfig.urls.withdrawCopy,
            data: { exportId: d.EXPORT_ID, note: $.trim((data && data.note) || "") },
            success: function (result) {
                if (result.status) {
                    showSuccessMesgGrowl(result.message);
                } else {
                    showErrorMesgGrowl(result.message);
                }
                crpLoad();
            },
            error: function () {
                showErrorMesgGrowl("Failed to withdraw the copy");
            }
        });
    });
}

$(document).ready(function () {
    $("#btn_closeCopyRegisterPanel").on("click", function () {
        $("#copyRegisterPanel").removeClass("open");
    });

    $("#crpShowWithdrawn").on("change", crpRender);

    // Outside click closes the panel, except clicks in the Withdraw dialog.
    $(document).on("click", function (e) {
        if ($("#copyRegisterPanel").hasClass("open")
            && !$(e.target).closest("#copyRegisterPanel, .messagebox, .messagebox_overlay").length) {
            $("#copyRegisterPanel").removeClass("open");
        }
    });
});

$(document).ready(function () {
    $("#btn_closeAcceptancePanel").on("click", function () {
        $("#acceptancePanel").removeClass("open");
    });

    // Klik di luar panel (di area grid utama) menutup panel - klik pada sel
    // Acceptance sendiri sudah stopPropagation() jadi tidak akan langsung
    // menutup ulang panel yang baru dibuka.
    $(document).on("click", function (e) {
        if ($("#acceptancePanel").hasClass("open") && !$(e.target).closest("#acceptancePanel").length) {
            $("#acceptancePanel").removeClass("open");
        }
    });
});

var gDocumentId = null;
var gDocumentTransactionId = null;
var gFilePath = null;

function onViewAttachment(attachment, documentId) {
    gFilePath = attachment;
    gDocumentTransactionId = documentId;
    viewAttachment();
}

function viewAttachment() {
    if (gFilePath !== "null" && gFilePath !== null && gFilePath !== "") {
        var link = documentControlDashboardConfig.urls.viewAttachment;
        link = link.replace("-1", gFilePath);
        link = link.replace("-2", gDocumentTransactionId);
        link = link.replace("-3", "2");
        window.open(link, '_blank');
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function onDownloadAttachment(attachment, documentId) {
    gFilePath = attachment;
    gDocumentTransactionId = documentId;
    downloadAttachment();
}

function downloadAttachment() {
    if (gFilePath !== "null" && gFilePath !== null && gFilePath !== "") {
        var link = documentControlDashboardConfig.urls.downloadAttachment;
        link = link.replace("-1", gFilePath);
        link = link.replace("-2", gDocumentTransactionId);
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}
