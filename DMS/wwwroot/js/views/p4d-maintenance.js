// P4DMaintenance. Server values come from p4dMaintenanceConfig in the view.
var SESSION_DEPARTMENT_ID_LOGIN = p4dMaintenanceConfig.sessionDepartmentId;

var gCanReceive = p4dMaintenanceConfig.canReceive;
var gCanUnReceive = p4dMaintenanceConfig.canUnReceive;
var gCanEdit = p4dMaintenanceConfig.canEdit;
var gCanSend = p4dMaintenanceConfig.canSend;
var gCanDistribution = p4dMaintenanceConfig.canDistribution;
var gCanReject = p4dMaintenanceConfig.canReject;
var gCanDelete = p4dMaintenanceConfig.canDelete;
// QMS/document-control staff vs regular user, terlepas dari izin aksi spesifik
// (Receive/Send/dst) - dipakai untuk sembunyikan rincian teknis distribusi
// (rasio accept, jumlah department/divisi) dari user biasa yang cuma lihat
// dokumen departemennya sendiri (request Hendra 2026-08-14).
var gIsQms = p4dMaintenanceConfig.canDocumentControlAccess;

// ---------- "My Action": dokumen yang menunggu tindakan QMS user login, ditandai
// seperti "My Approval" di Document Preparation (request Hendra 2026-11-05).
// P4D tidak punya approver per orang - siapa pun di tim QMS boleh memproses,
// jadi "milik saya" = status yang bisa ditindaklanjuti dengan izin user ini. ----------
var dpMyApprovalOnly = false;

function getMyAction(row) {
    if (row == null) return null;
    if (gCanReceive && row.STATUS == 0) {
        return { label: "To Receive", hint: "Waiting to be received", run: onReceivePrepare };
    }
    if (gCanReceive && row.STATUS == 1) {
        return { label: "To Accept", hint: "Received, waiting to be accepted", run: onAcceptPrepare };
    }
    if (gCanSend && (row.STATUS == 2 || row.STATUS == 6) && (row.PENDING_DISTRIBUTION_COUNT || 0) > 0) {
        return { label: "To Send", hint: row.PENDING_DISTRIBUTION_COUNT + " department(s) not sent yet", run: onSendDistributionAllPrepare };
    }
    if (gCanDistribution && row.STATUS == 2) {
        return { label: "To Distribute", hint: "Accepted, distribution targets not set yet", run: onDistributionPrepare };
    }
    return null;
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

function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);
    // Delete cuma untuk Draft/Rejected - Checking (1) SENGAJA tidak boleh,
    // sebab QMS sudah mulai Receive/review; department harus Un-Receive dulu
    // (balik ke Draft) baru bisa Delete, supaya log aktivitas & distribusi
    // tidak hilang permanen tanpa jejak di tengah proses QMS (request Hendra
    // 2026-08-14).
    $("#btn_delete").prop("disabled", !(selected && (row.STATUS == 0 || row.STATUS == 3) && row.DEPARTMENT_ID == SESSION_DEPARTMENT_ID_LOGIN));
    $("#btn_receive").prop("disabled", !(selected && row.STATUS == 0));
    $("#btn_accept").prop("disabled", !(selected && row.STATUS == 1));
    $("#btn_unreceive").prop("disabled", !(selected && row.STATUS == 2));
    $("#btn_reject").prop("disabled", !(selected && row.STATUS == 0));
    $("#btn_distribution").prop("disabled", !(selected && row.STATUS != 0 && row.STATUS != 3 && row.STATUS != 4));
    $("#btn_send").prop("disabled", !(selected && (row.STATUS == 2 || row.STATUS == 6)));
}

// ---------- docked "Document Status" panel toggle - top-level (not inside
// $(document).ready) so updateHeaderStats() below can reach dpLastPending too ----------
var dpLastPending = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    $("#dpToggleBadge").text(dpLastPending).toggle(collapsed && dpLastPending > 0);
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
    $("#btn_receive").prop("disabled", true);
    $("#btn_accept").prop("disabled", true);
    $("#btn_unreceive").prop("disabled", true);
    $("#btn_reject").prop("disabled", true);
    $("#btn_distribution").prop("disabled", true);
    $("#btn_send").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_CTRL_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: p4dMaintenanceConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                updateHeaderStats(rows);
                // dokumen yang menunggu tindakan user login ditaruh paling atas
                var mine = rows.filter(function (r) { return getMyAction(r) != null; });
                var others = rows.filter(function (r) { return getMyAction(r) == null; });
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
        filterRow: { visible: false, applyFilter: "auto" },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
        },
        onRowPrepared: function (e) {
            if (e.rowType === "data" && getMyAction(e.data) != null) {
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
            {
                dataField: "DOCUMENT_CODE",
                caption: "Document No",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    $("<span>").addClass("dms-link").text(options.data.DOCUMENT_CODE).appendTo(container);
                }
            },
            { dataField: "DOCUMENT_NAME", caption: "Document Name" },
            { dataField: "REVISION", caption: "Revision", alignment: "center" },
            {
                dataField: "STATUS_VAL",
                caption: "Status",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = "dms-pill dms-pill-danger";
                    var icon = "fa-times-circle";

                    if (row.STATUS == 0) {
                        pillClass = "dms-pill dms-pill-info";
                        icon = "fa-clock-o";
                    } else if (row.STATUS == 1) {
                        pillClass = "dms-pill dms-pill-warning";
                        icon = "fa-hourglass-half";
                    } else if (row.STATUS == 6) {
                        // Partially Sent - beda visual dari Checking (1) meski dulu
                        // sama-sama amber, supaya jelas ini soal distribusi bukan
                        // registrasi (request Hendra 2026-08-14: user tidak bisa
                        // bedakan status "sudah didistribusi" dari tampilan grid).
                        pillClass = "dms-pill dms-pill-primary";
                        icon = "fa-share-alt";
                    } else if (row.STATUS == 5) {
                        pillClass = "dms-pill dms-pill-primary";
                        icon = "fa-paper-plane";
                    } else if (row.STATUS == 2) {
                        pillClass = "dms-pill dms-pill-success";
                        icon = "fa-check-circle";
                    }

                    // Menunggu tindakan user login: pill oranye + lonceng berisi aksi
                    // yang dibutuhkan, klik langsung buka aksinya (request Hendra 2026-11-05)
                    var myAction = getMyAction(row);
                    if (myAction != null) {
                        $("<span>").addClass("dms-pill dp-pill-my-approval")
                            .attr("title", row.STATUS_VAL + " - " + myAction.hint + " (click to process)")
                            .html('<i class="fa fa-bell"></i> ' + myAction.label)
                            .on("click", function (e) {
                                e.stopPropagation();
                                selectRowThen(row, myAction.run);
                            })
                            .appendTo(container);
                        return;
                    }

                    $("<span>").addClass(pillClass).html('<i class="fa ' + icon + '"></i> ' + row.STATUS_VAL).appendTo(container);
                }
            },
            {
                dataField: "ACK_DONE",
                caption: "Acceptance",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    // Only meaningful once the document has actually been sent
                    // (STATUS 5 = Sent, 6 = Partially Sent) - before that there is
                    // nothing to accept yet (request Hendra 2026-08-13: P4D should
                    // show "Waiting" until every target department clicks Accepted
                    // in UserDashboard, then "Accepted" once all of them have).
                    // Shown as "Not Distributed" (not just "-") so it directly
                    // answers "has this document been sent out yet or not" at a
                    // glance (request Hendra 2026-08-14).
                    var total = row.ACK_TOTAL || 0;
                    var done = row.ACK_DONE || 0;
                    var pending = row.PENDING_DISTRIBUTION_COUNT || 0;

                    if ((row.STATUS != 5 && row.STATUS != 6) || total === 0) {
                        // Beda antara "belum ada target sama sekali" vs "sudah ada
                        // department yang dicentang di popup Distribution tapi belum
                        // di-Send" - supaya QMS tidak perlu buka popup satu-satu untuk
                        // tahu mana yang kelupaan di-Send (request Hendra 2026-08-14).
                        // Badge "Not Sent Yet" cuma untuk yang punya akses Send (gCanSend)
                        // - user tanpa akses itu tidak bisa menindaklanjuti, jadi cuma bikin
                        // bingung/kesan sistem nyangkut kalau ditampilkan ke mereka.
                        if (pending > 0 && gCanSend) {
                            $("<span>")
                                .css({ color: "#B36B00", fontWeight: 600 })
                                .html('<i class="fa fa-exclamation-circle"></i> ' + pending + ' Not Sent Yet')
                                .appendTo(container);
                        } else {
                            $("<span>").css({ color: "#98A2B3", fontWeight: 600 }).text("Not Distributed").appendTo(container);
                        }
                        return;
                    }

                    var accepted = done >= total;
                    var divCount = row.DIVISION_COUNT || 0;

                    // User biasa (bukan QMS/document-control staff) cuma lihat label polos
                    // Accepted/Waiting, sama seperti UserDashboard - rincian teknis (rasio,
                    // jumlah department/divisi, hint "belum di-Send") disembunyikan karena
                    // itu urusan operasional QMS, bukan info yang relevan/bisa ditindak
                    // lanjuti oleh user biasa (request Hendra 2026-08-15).
                    if (!gIsQms) {
                        $("<span>")
                            .css({ color: accepted ? "#1E9E63" : "#C62828", fontWeight: 600 })
                            .html('<i class="fa ' + (accepted ? "fa-check-circle" : "fa-hourglass-half") + '"></i> ' + (accepted ? "Accepted" : "Waiting"))
                            .appendTo(container);
                        return;
                    }

                    // Ikon panah kecil di kanan sel menandakan sel ini bisa diklik
                    // untuk membuka panel detail Acceptance (sidebar) - samakan
                    // dengan DocumentControlDashboard (request Hendra 2026-08-15).
                    container.css({ display: "flex", alignItems: "center", justifyContent: "center", gap: "6px", cursor: "pointer" });

                    var $wrap = $("<div>").css({ lineHeight: 1.3 });

                    // Label "Waiting" polos menyesatkan kalau SEBAGIAN department sudah
                    // Accepted (mis. 1 dari 2) - kelihatan seolah belum ada satupun yang
                    // accept. Tambahkan rasio done/total di sini khusus saat belum lengkap,
                    // supaya progres separuh jalan tetap kelihatan (request Hendra
                    // 2026-08-14). "Accepted" sendiri dibiarkan polos (sudah jelas 100%).
                    var waitingLabel = "Waiting" + (total > 0 ? " (" + done + "/" + total + ")" : "");
                    $("<span>")
                        .css({ color: accepted ? "#1E9E63" : "#C62828", fontWeight: 600 })
                        .html('<i class="fa ' + (accepted ? "fa-check-circle" : "fa-hourglass-half") + '"></i> ' + (accepted ? "Accepted" : waitingLabel))
                        .appendTo($wrap);

                    // Target distribusi: berapa department (dan divisi) yang dipilih
                    // saat Send Distribution (request Hendra 2026-08-14).
                    $("<span>")
                        .css({ display: "block", fontSize: "10.5px", color: "#98A2B3", marginTop: "2px" })
                        .text(total + " Department" + (total !== 1 ? "s" : "") + " / " + divCount + " Division" + (divCount !== 1 ? "s" : ""))
                        .appendTo($wrap);

                    // Ada department yang ditambahkan BELAKANGAN (setelah Send pertama,
                    // makanya status sudah Partially Sent) tapi belum di-Send lagi - jangan
                    // sampai hilang dari pandangan cuma karena statusnya sudah "ada yang
                    // terkirim" (request Hendra 2026-08-14, kasus "nambah 1 department lagi
                    // lalu lupa Send").
                    if (pending > 0 && gCanSend) {
                        $("<span>")
                            .css({ display: "block", fontSize: "10.5px", color: "#B36B00", fontWeight: 600, marginTop: "2px" })
                            .html('<i class="fa fa-exclamation-circle"></i> ' + pending + ' Not Sent Yet')
                            .appendTo($wrap);
                    }

                    // Klik untuk lihat rincian per department (QMS-only) - samakan
                    // format dengan DocumentControlDashboard, lihat
                    // _AcceptanceDetailPopup.cshtml (request Hendra 2026-08-15).
                    container.on("click", function (e) {
                        e.stopPropagation();
                        dcdOpenAcceptanceDetail(row);
                    });

                    $wrap.appendTo(container);
                    $("<i>").addClass("fa fa-chevron-circle-right")
                        .css({ flex: "none", fontSize: "11px", color: "#B0B8C4" })
                        .attr("title", "Click for detail")
                        .appendTo(container);
                }
            },
            {
                dataField: "DIVISION_NAME",
                caption: "Division",
                width: 220,
                cellTemplate: function (container, options) {
                    container.text(options.data.DIVISION + " - " + options.data.DIVISION_NAME);
                }
            },
            {
                dataField: "DEPARTMENT_NAME",
                caption: "Department",
                width: 260,
                cellTemplate: function (container, options) {
                    container.text(options.data.DEPARTMENT_CODE + " - " + options.data.DEPARTMENT_NAME);
                }
            },
            { dataField: "DOCUMENT_TYPE", caption: "Document Type" },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Date",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "LOCATION", caption: "Location" },
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
                    container.text(value != null ? moment(value).format("DD-MM-YYYY hh:mm:ss") : "");
                }
            },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
            {
                // kolom bantu filter tombol "My Action" - tidak ditampilkan
                name: "MY_APPROVAL",
                visible: false,
                showInColumnChooser: false,
                calculateCellValue: function (row) {
                    return getMyAction(row) != null ? "Y" : "N";
                }
            },
            {
                dataField: "CHANGED_DT",
                caption: "Changed Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY hh:mm:ss") : "");
                }
            },
            {
                caption: "Action",
                alignment: "center",
                width: 100,
                allowHiding: false,
                allowFiltering: false,
                fixed: true,
                fixedPosition: "right",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    container.css("white-space", "nowrap");

                    // Tombol mata (Preview) dihapus - fungsinya sudah ada di dalam
                    // kebab menu ("Preview"), jadi ini duplikat (request Hendra
                    // 2026-08-15).
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

    // ---------- toolbar: My Action (tampilkan hanya dokumen yang menunggu tindakan saya) ----------
    $("#btn_myApproval").on("click", function () {
        setMyApprovalOnly(!dpMyApprovalOnly);
    });

    // link dari Dashboard: ?myAction=1
    if (new URLSearchParams(window.location.search).get("myAction") === "1") {
        setMyApprovalOnly(true);
    }

});

// ---------- header stats (Pending / Registered / Rejected / Today) ----------
function updateHeaderStats(rows) {
    rows = rows || [];
    var pending = 0, registered = 0, rejected = 0, todayRegistered = 0;
    var today = moment().format("YYYY-MM-DD");

    rows.forEach(function (r) {
        if (r.STATUS == 0 || r.STATUS == 1) pending++;
        if (r.STATUS == 2) {
            registered++;
            if (r.CHANGED_DT && moment(r.CHANGED_DT).format("YYYY-MM-DD") == today) {
                todayRegistered++;
            }
        }
        if (r.STATUS == 3) rejected++;
    });

    $("#dpPending").text(pending);
    $("#dpRegistered").text(registered);
    $("#dpRejected").text(rejected);
    $("#dpTodayRegistered").text(todayRegistered);

    // surfaces the pending count on the collapsed toggle too, so it
    // stays useful (not just a bare arrow) even when the panel is closed
    dpLastPending = pending;
    refreshToggleBadge();
}

// ---------- per-row "more actions" kebab menu ----------
var $dpRowMenu = null;

function buildRowActionMenu() {
    if ($dpRowMenu) return $dpRowMenu;

    $dpRowMenu = $('<ul class="dropdown-menu dp-dropdown-menu" role="menu"></ul>')
        .css({ position: "absolute", display: "none", "z-index": 2000, "min-width": "190px" })
        .appendTo("body");

    $(document).on("click", function () {
        $dpRowMenu.hide();
    });

    return $dpRowMenu;
}

function menuItem(cssClass, icon, label, enabled, onClick) {
    var $li = $('<li><button type="button" class="' + cssClass + '"' + (enabled ? "" : " disabled") + '><i class="fa ' + icon + '"></i> ' + label + '</button></li>');
    if (enabled) {
        $li.find("button").on("click", function (e) {
            e.preventDefault();
            $dpRowMenu.hide();
            onClick();
        });
    }
    return $li;
}

function selectRowThen(row, action) {
    var grid = $("#datagrid").dxDataGrid("instance");
    grid.selectRows([row.DOCUMENT_CTRL_ID], false);
    action();
}

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();

    $menu.append(menuItem("dp-menu-preview", "fa-eye", "Preview", true, function () {
        onViewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
    }));

    if (gCanReceive) {
        $menu.append(menuItem("dp-menu-approve", "fa-check-circle", "Receive", row.STATUS == 0, function () {
            selectRowThen(row, onReceivePrepare);
        }));
    }

    if (gCanReceive) {
        $menu.append(menuItem("dp-menu-accept", "fa-check-square-o", "Accept", row.STATUS == 1, function () {
            selectRowThen(row, onAcceptPrepare);
        }));
    }

    if (gCanUnReceive) {
        $menu.append(menuItem("dp-menu-unreceive", "fa-undo", "Un-Receive", row.STATUS == 2, function () {
            selectRowThen(row, onUnReceivePrepare);
        }));
    }

    // Stamp Layout stage 3: change the fixed stamp positions after Accept (reason required).
    if (p4dMaintenanceConfig.canAdjustStamp && typeof spOpen === "function") {
        $menu.append(menuItem("dp-menu-stamp", "fa-certificate", "Stamp Position", (row.STATUS == 2 || row.STATUS == 5 || row.STATUS == 6), function () {
            selectRowThen(row, function () { spOpen(row.DOCUMENT_CTRL_ID, row.DOCUMENT_CODE); });
        }));
    }

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-location", "fa-map-marker", "Update Location", true, function () {
            selectRowThen(row, onEditPrepare);
        }));
    }

    if (gCanSend) {
        $menu.append(menuItem("dp-menu-send", "fa-paper-plane-o", "Send", (row.STATUS == 2 || row.STATUS == 6), function () {
            selectRowThen(row, onSendDistributionAllPrepare);
        }));
    }

    if (gCanDistribution) {
        $menu.append(menuItem("dp-menu-distribution", "fa-share-alt", "Distribution", (row.STATUS != 0 && row.STATUS != 3 && row.STATUS != 4), function () {
            selectRowThen(row, onDistributionPrepare);
        }));
    }

    if (gCanReject) {
        $menu.append(menuItem("dp-menu-reject", "fa-ban", "Reject", row.STATUS == 0, function () {
            selectRowThen(row, onRejectPrepare);
        }));
    }

    $menu.append(menuItem("dp-menu-history", "fa-history", "Revision History", true, function () {
        onclickHistory(row.DOCUMENT_CODE);
    }));

    $menu.append(menuItem("dp-menu-history", "fa-file-text", "Activity Log", true, function () {
        onclickLog(row.DOCUMENT_TRANSACTION_ID);
    }));

    if (gCanDelete) {
        var deletable = (row.STATUS == 0 || row.STATUS == 3) && row.DEPARTMENT_ID == SESSION_DEPARTMENT_ID_LOGIN;
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", deletable, function () {
            selectRowThen(row, onDeletePrepare);
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
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

var adpGridData = [];

function dcdOpenAcceptanceDetail(row) {
    $("#adpDocumentCode").text(row.DOCUMENT_CODE || "-");
    $("#acceptancePanel").addClass("open");

    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.searchDistributionDetail,
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

var gScreenMode = null;
var gId = null;
var gDocumentCtrlId = null;
var gDocumentTransactionId = null;
var gStatus = null;
var gDepartmentId = null;
var gDocumentDistributionId = null;
var gDocumentNo = null;
var gIsApproved = null;
var gFilePath = null;
var DepartmentCollection = [];
var selectedDistributionIds = [];

$(document).ready(function () {
    setDataSelect2("addEditDocumentNo", p4dMaintenanceConfig.urls.documentMaintenanceGetDocumentCodeLoginBased, "- Choose -");

    $("#btn_add").on("click", function () {
        onAddPrepare();
    });

    $("#btn_edit").on("click", function () {
        onEditPrepare();
    });

    $("#btn_delete").on("click", function () {
        onDeletePrepare();
    });

    $("#btn_receive").on("click", function () {
        onReceivePrepare();
    });

    $("#btn_accept").on("click", function () {
        onAcceptPrepare();
    });

    $("#btn_unreceive").on("click", function () {
        onUnReceivePrepare();
    });

    $("#btn_reject").on("click", function () {
        onRejectPrepare();
    });

    $("#btn_save").on("click", function () {
        confirmAddEditData();
    });

    $("#btn_okApproveReject").on("click", function () {
        receiveRejectData();
    });

    //$("#btn_saveEdit").on("click", function () {
    //    confirmAddEditData();
    //});

    $("#btn_cancel").on("click", function () {
        reset();
    });

    //$("#btn_edit").on("click", function () {
    //    onEditPrepare();
    //});

    $("#btn_download").on("click", function () {
        onDownload();
    });

    $("#btn_distribution").on("click", function () {
        onDistributionPrepare();
    });

    $("#btn_send").on("click", function () {
        onSendDistributionAllPrepare();
    });

    $("#btn_addDistribution").on("click", function () {
        onAddDistributionPrepare();
    });

    $("#btn_editDistribution").on("click", function () {
        resetDistribution();
        onEditDistributionPrepare();
    });

    $("#btn_saveDistribution").on("click", function () {
        confirmAddEditDistributionData();
    });

    $("#btn_deleteDistribution").on("click", function () {
        onDeleteDistributionPrepare();
    });

    $("#btn_cancelDistribution").on("click", function () {
        resetDistribution();
    });
});

function onclickClose() {
    $('#datagridDistribution').DataTable({
        "bDestroy": true
    });
}

function onclickDistribution(documentTransactionId) {
    gDocumentTransactionId = documentTransactionId

    $('#modalDistribution-title').html("Document Distribution");
    $('#distributionDocumentId').val(gDocumentTransactionId);

    $("#btn_addDistribution").hide();
    $("#btn_deleteDistribution").hide();

    openDistributionModal();
}

function onDistributionPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDocumentTransactionId = row.DOCUMENT_TRANSACTION_ID;
    gStatus = row.STATUS;
    gDepartmentId = row.DEPARTMENT_ID;

    $('#modalDistribution-title').html("Document Distribution");
    $('#distributionDocumentId').val(gDocumentTransactionId);

    $("#btn_addDistribution").show();
    $("#btn_deleteDistribution").show();

    openDistributionModal();
}

function openDistributionModal() {
    setButtonState("#btn_addDistribution");

    $("#datagridDistribution").dataTable().fnDestroy();
    $('#datagridDistribution').DataTable({
        "ordering": false,
        "oLanguage": { "sEmptyTable": "Data not found." },
        "dom": "<t><r><'row mt-10'<'col-lg-2'i><'col-lg-1'><'col-lg-5'l><'col-lg-4'p>>",
        "processing": true,
        "serverSide": true,
        "ajax": {
            "url": p4dMaintenanceConfig.urls.searchDocumentApproval,
            "type": "POST",
            "datatype": "json",
            "data": function (d) {
                d.DOCUMENT_TRANSACTION_ID = $("#distributionDocumentId").val()
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
                "className": "text-left",
            },
            {
                "targets": [4],
                "className": "text-right",
            }
            ,
            {
                "targets": [5],
                "className": "text-center",
            }
        ],
        "columns": [
            {
                "render": function (data, type, row) {
                    return '<input class="grid-checkbox grid-checkbox-dist form-check-input" type="checkbox" name="chkRowDistribution" data-id="' + row["DISTRIBUTION_ID"] + '" data-status="' + row["STATUS"] + '" style="cursor: pointer;" > ';
                }
            },
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
                    if (row["STATUS"] != 1) {
                        return '<span class="label label-danger">' + row["STATUS_DISPLAY"] + '</span>';
                    }

                    // Sent, but only shows "Accepted" once this department has
                    // clicked Accepted in UserDashboard (request Hendra 2026-08-13).
                    if (row["ACCEPTED_FLAG"] == 1) {
                        return '<span class="label label-success">Accepted</span>';
                    }
                    return '<span class="label label-warning">Waiting Acceptance</span>';
                }
            },
        ],
        "fnDrawCallback": function (oSettings) {
            $('#datagridDistribution tbody > tr').removeClass('selected');
            $('.grid-checkbox-dist').not(this).prop('checked', false);

            //setGridButtonState("#btn_editDistribution");
            setGridButtonState("#btn_deleteDistribution");

            $("th").removeClass('sorting_asc');
        }
    });

    $("#datagridDistribution").on("click", "tbody tr", function (e) {
        if (event.target.type !== 'checkbox') {
            $(':checkbox', this).trigger('click');
        }
    });

    $("#datagridDistribution").on('change', "input[type='checkbox']", function (e) {
        // $('#datagridDistribution tbody > tr').removeClass('selected');
        // $('.grid-checkbox-dist').not(this).prop('checked', false);

        $(this).is(":checked") ? $(this).closest('tr').addClass('selected') : $(this).closest('tr').removeClass('selected');

        //setGridButtonState("#btn_editDistribution");
        setGridButtonState("#btn_deleteDistribution");
    });

    $("#modalDistribution").modal();
}

function onDownload() {
    download();
}

function download() {
    var link = p4dMaintenanceConfig.urls.downloadRaw;
    window.location.href = link;
}

function setButtonState(id) {
    $(id).prop("disabled",
        ((gStatus != 4 && gStatus != 3)
        )
            ? false : true);
}

function setGridButtonState(id) {
    // Distribusi yang sudah Sent (STATUS=1) tidak boleh dihapus (lihat
    // sp_P4DMaintenance_DeleteDistribution) - kalau salah satu baris yang
    // dicentang sudah Sent, tombol Delete langsung nonaktif di sini juga,
    // tidak perlu tunggu error dari server (request Hendra 2026-08-15).
    var hasSentSelected = false;
    $("[name='chkRowDistribution']:checked").each(function () {
        if ($(this).data("status") == 1) hasSentSelected = true;
    });

    $(id).prop("disabled",
        ($("[name='chkRowDistribution']:checked").length >= 1
            && (gStatus != 4 && gStatus != 3)
            && !hasSentSelected
        )
            ? false : true);
}

function onSearchDistribution() {
    $("#datagridDistribution").DataTable().ajax.reload();
    $("#btn_editDistribution").prop("disabled", true);
    $("#btn_deleteDistribution").prop("disabled", true);
}

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New Registration");
    $('#addEditDocumentNo').val("").trigger("change");;
    $('#addEditDocumentId').val("");
    $('#addEditDocumentName').val("");
    $('#addEditRevision').val("");
    $('#addEditDivision').val("");
    $('#addEditDepartment').val("");

    $('#addEditDocumentNo').prop('disabled', false);
    $('#addEditDocumentName').prop('disabled', true);
    $('#addEditDivision').prop('disabled', true);
    $('#addEditDepartment').prop('disabled', true);

    gFilePath = null;
    $('#divLocation').hide();
    $('#divRevision').show();

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditDocumentNo').focus();
    });

}

function onAddDistributionPrepare() {
    gScreenMode = 'ADD';
    $('#modalAddEditDistribution-title').html("New Distribution");

    getDivision();

    $("#modalAddEditDistribution").modal();
}

function getDivision() {
    var row = getSelectedRowData();
    if (row != null) {
        gDocumentTransactionId = row.DOCUMENT_TRANSACTION_ID;
    }

    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.departmentMasterSearchByDistribution,
        async: false,
        data: {
            DOCUMENT_TRANSACTION_ID: gDocumentTransactionId
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;

                DepartmentCollection = [];

                $.each(result.data, function (index, item) {
                    if (item.STATUS_EXIST == true) {
                        var dtl = new Object();

                        dtl.DOCUMENT_TRANSACTION_ID = parseInt(gDocumentTransactionId, 10);
                        dtl.DEPARTMENT_ID = String(item.DEPARTMENT_ID);
                        DepartmentCollection.push(dtl);
                    }
                });

                $("#divisionGrid").dataTable().fnDestroy();
                $('#divisionGrid').DataTable({
                    "ordering": false,
                    "oLanguage": { "sEmptyTable": "Data not found." },
                    "scrollCollapse": false,
                    //"lengthMenu": [5, 10, 25, 50], // Set the available page lengths
                    //"pageLength": 5, // Set the initial page length
                    //"info": false,
                    //"scrollY": '200px',
                    //"scrollX": true,
                    //"processing": true,
                    "data": data,
                    "columnDefs": [
                        {
                            "targets": [0],
                            "className": "text-center",
                        },
                        {
                            "targets": [1],
                            "className": "text-center",
                        }
                    ],
                    "columns": [
                        {
                            "render": function (data, type, row, meta) {
                                let num = meta.row + meta.settings._iDisplayStart + 1;
                                if (row.STATUS_EXIST == true) {
                                    return '<input class="grid-checkbox-poup grid-checkbox-body-popup form-check-input" type="checkbox" name="chkRowPopup" id="chkRow_' + num + '" data-id="' + row["DEPARTMENT_ID"] + '" style = "cursor: pointer;" checked> '
                                        + '<input type="hidden" id="rowDepartmentId_' + num + '" value="' + row["DEPARTMENT_ID"] + '">';
                                } else {
                                    return '<input class="grid-checkbox-poup grid-checkbox-body-popup form-check-input" type="checkbox" name="chkRowPopup" id="chkRow_' + num + '" data-id="' + row["DEPARTMENT_ID"] + '" style = "cursor: pointer;" > '
                                        + '<input type="hidden" id="rowDepartmentId_' + num + '" value="' + row["DEPARTMENT_ID"] + '">';
                                }
                            }
                        },
                        {
                            render: function (data, type, row, meta) {
                                return meta.row + meta.settings._iDisplayStart + 1;
                            }
                        },
                        { "data": "DIVISION", "name": "DIVISION" },
                        { "data": "DEPARTMENT_CODE_NAME", "name": "DEPARTMENT_CODE_NAME" }
                    ],
                    rowCallback: function (row, data) {
                        $.each(DepartmentCollection, function (index, item) {
                            if (item.DEPARTMENT_ID == data.DEPARTMENT_ID) {
                                $(row).addClass('selected');
                            }
                        });
                    }
                });

                $('#modalAddEdit').on('shown.bs.modal', function () {
                    $("#divisionGrid").DataTable().columns.adjust().draw();
                });

                $("#divisionGrid").off("click", "tbody tr").on("click", "tbody tr", function (e) {
                    if (event.target.type !== 'checkbox') {
                        $(':checkbox', this).trigger('click');
                    }
                });

                $("#divisionGrid").on('change', "input[type='checkbox']", function (e) {
                    var selectedDepartmentId = $(this).attr('data-id');

                    if ($(this).is(":checked")) {
                        $(this).closest('tr').addClass('selected')
                        if (selectedDepartmentId != null && selectedDepartmentId != "") {

                            const doesExist = DepartmentCollection.some(obj => obj.DEPARTMENT_ID == selectedDepartmentId);

                            if (!doesExist) {
                                var dtl = new Object();

                                dtl.DOCUMENT_TRANSACTION_ID = parseInt(gDocumentTransactionId, 10);
                                dtl.DEPARTMENT_ID = selectedDepartmentId;
                                DepartmentCollection.push(dtl);
                            }
                        }
                    } else {
                        $(this).closest('tr').removeClass('selected')
                        if (selectedDepartmentId != null && selectedDepartmentId != "") {
                            selectedDepartmentId = $(this).attr('data-id');

                            DepartmentCollection = DepartmentCollection.filter(obj => obj.DEPARTMENT_ID !== selectedDepartmentId);
                        }
                    }
                });

                $("#checkallDivision").click(function () {
                    $("#divisionGrid input[type='checkbox']").prop('checked', $("#checkallDivision").is(":checked"));
                    $("#divisionGrid input[type='checkbox']").trigger('change');

                    if (!$(this).is(":checked")) {
                        DepartmentCollection = [];
                    }
                });

                popUpProgressHide();
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

$("#addEditDocumentNo").change(function () {
    if ($('#addEditDocumentNo').val() !== null || $('#addEditDocumentNo').val() !== 'null') {
        $.ajax({
            type: "POST",
            url: p4dMaintenanceConfig.urls.getDataByDocumentNo,
            async: false,
            data: {
                DOCUMENT_TRANSACTION_ID: $("#addEditDocumentNo").val()
            },
            success: function (result) {
                if (result.status == true) {
                    var data = result.data;
                    $("#addEditDocumentName").val(data.DOCUMENT_TRANSACTION_NAME);
                    $("#addEditRevision").val(data.REVISION);
                    $("#addEditDivision").val(data.DIVISION + " - " + data.DIVISION_NAME);
                    $("#addEditDepartment").val(data.DEPARTMENT_CODE + " - " + data.DEPARTMENT_NAME);
                } else {
                    showErrorMesgGrowl(result.message);
                }
            },
            error: function (result) {
                //popUpProgressHide();
                showErrorMesgGrowl(result);
            }
        });
    } else {
        s
        $('#addEditDocumentId').val("");
        $('#addEditDocumentName').val("");
        $('#addEditRevision').val("");
        $('#addEditDivision').val("");
        $('#addEditDepartment').val("");
    }
});

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

function confirmAddEditDistributionData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditDistributionData, "Confirmation");
}

function addEditData() {
    popUpProgressShow();

    var documentId = $('#addEditDocumentId').val();
    var documentNo = $('#addEditDocumentNo').val();
    var documentName = $('#addEditDocumentName').val();
    var division = $('#addEditDivision').val();
    var department = $('#addEditDepartment').val();
    //var documentId = $('#addEditDocumentId').val();
    var location = $('#addEditLocation').val();

    var data = new FormData();
    data.append("screenMode", gScreenMode);

    var isValid = "N";

    if (gScreenMode == "ADD") {
        if (documentNo == null || documentNo == "null" || documentNo == "") {
            showErrorMesgGrowl("ERROR: Document No should not be blank");
            popUpProgressHide();
            isValid = "N";
        } else {
            isValid = "Y";

            if (gDocumentCtrlId == null || gDocumentCtrlId == "null") {
                data.append("DOCUMENT_CTRL_ID", "");
            } else {
                data.append("DOCUMENT_CTRL_ID", gDocumentCtrlId);
            }

            if (documentNo == null || documentNo == "null") {
                data.append("DOCUMENT_CODE", "");
            } else {
                data.append("DOCUMENT_TRANSACTION_ID", $('#addEditDocumentNo').val());
            }
            if (documentName == null || documentName == "null") {
                data.append("DOCUMENT_NAME", "");
            } else {
                data.append("DOCUMENT_NAME", $('#addEditDocumentName').val());
            }
            if (division == null || division == "null") {
                data.append("DIVISION", "");
            } else {
                data.append("DIVISION", $('#addEditDivision').val());
            }
            if (department == null || department == "null") {
                data.append("DEPARTMENT", "");
            } else {
                data.append("DEPARTMENT", $('#addEditDepartment').val());
            }
        }
    } else if (gScreenMode == "EDIT") {
        if (location == null || location == "null" || location == "") {
            showErrorMesgGrowl("ERROR: Location should not be blank");
            popUpProgressHide();
            isValid = "N";
        } else {
            isValid = "Y";

            if (gDocumentCtrlId == null || gDocumentCtrlId == "null") {
                data.append("DOCUMENT_CTRL_ID", "");
            } else {
                data.append("DOCUMENT_CTRL_ID", gDocumentCtrlId);
            }

            if (location == null || location == "null") {
                data.append("LOCATION", "");
            } else {
                data.append("LOCATION", $('#addEditLocation').val());
            }
        }
    }

    if (isValid == "Y") {
        $.ajax({
            type: "POST",
            url: p4dMaintenanceConfig.urls.addEdit,
            contentType: false,
            processData: false,
            data: data,
            success: function (result) {
                //popUpProgressHide();
                if (result.status == true) {
                    showSuccessMesgGrowl(result.message);
                    onSearch();
                    popUpProgressHide();
                    $("#modalAddEdit").modal('hide');

                    //if (gScreenMode == "ADD") {
                    //    $("#modalAddEdit").modal('hide');
                    //} else if (gScreenMode == "EDIT") { $("#modalEdit").modal('hide'); }
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
}

function addEditDistributionData() {
    popUpProgressShow();

    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.addEditDistribution,
        contentType: 'application/json',
        data: JSON.stringify(DepartmentCollection),
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearchDistribution();
                onSearch();
                $("#modalAddEditDistribution").modal('hide');
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

$(document).on('change', '#addEditDocumentUpload', function (e) {
    ex = e;
});

function reset() {
    $('#addEditDocumentNo').val("");
    $('#addEditDocumentId').val("");
    $('#addEditDocumentName').val("");
    $('#addEditDivision').val("");
    $('#addEditDepartment').val("");
    $('#addEditDocumentNo').text("");
    $('#addEditDocumentId').text("");
    $('#addEditDocumentName').text("");
    $('#addEditDivision').text("");
    $('#addEditDepartment').text("");

    gFilePath = null;
}

function onDeletePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;

    showDangerConfirm(deleteMessage("Document Registration", row.DOCUMENT_CODE), deleteData, "Delete");
}

function onDeleteDistributionPrepare() {
    selectedDistributionIds = [];

    $("input[name='chkRowDistribution']").each(function () {
        if ($(this).prop('checked')) {
            // Store the data-id and data-status of checked checkboxes in arrays
            selectedDistributionIds.push($(this).closest('tr').find(".grid-checkbox-dist:checked").attr('data-id'));
        }
    });

    showDangerConfirm("Delete " + selectedDistributionIds.length + " selected distribution(s)? This cannot be undone.", deleteDistributionData, "Delete");
}

function onSendDistributionAllPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentTransactionId = row.DOCUMENT_TRANSACTION_ID;

    showConfirmMesg("Are you sure you want to send the record?", sendDistributionAllData, "Confirmation");
}

function deleteData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_CTRL_ID: gDocumentCtrlId
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

function deleteDistributionData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.deleteDistributionMultiple,
        dataType: 'json',
        traditional: true,
        data: {
            distributionIds: selectedDistributionIds
        },
        success: function (result) {
            //popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearchDistribution();
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

function sendDistributionAllData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.sendDistributionAll,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_TRANSACTION_ID: gDocumentTransactionId
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

function onReceivePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDocumentNo = row.DOCUMENT_CODE;
    //showConfirmMesg("Are you sure you want to receive the record?", receiveRejectData, "Confirmation");
    gIsApproved = 'Y';
    $('#addEditRemarks').val('');
    $("#modalApproveReject").modal();
    $('#modalApproveReject-title').html("Receive Registration");
    $('#labelConfirmation').html("Are you sure you want to receive the registration for Document No " + gDocumentNo + "? "
        + "(First step - status will become Checking. QMS still needs to Accept it afterwards before it can be distributed.)");
    $('#divRemarks').hide();
    $('#modalApproveReject').on('shown.bs.modal', function () {
    });
}

function onAcceptPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDocumentNo = row.DOCUMENT_CODE;

    // With P4D-STAMPLAYOUT, Accept also asks where the stamps go (p4d-stamp-layout.js).
    if (p4dMaintenanceConfig.canAdjustStamp && typeof spOpen === "function") {
        spOpen(gDocumentCtrlId, gDocumentNo);
        return;
    }

    showConfirmMesg("Are you sure you want to accept Document No " + gDocumentNo
        + "? (Second step - status will become Accepted, and the document can then be distributed.)",
        acceptData, "Confirmation");
}

function acceptData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.accept,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_CTRL_ID: gDocumentCtrlId
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
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onRejectPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDocumentNo = row.DOCUMENT_CODE;
    //showConfirmMesg("Are you sure you want to receive the record?", receiveRejectData, "Confirmation");
    gIsApproved = 'N';
    $('#addEditRemarks').val('');
    $("#modalApproveReject").modal();
    $('#modalApproveReject-title').html("Reject Document");
    $('#labelConfirmation').html("Are you sure want to reject Document No " + gDocumentNo + "?");
    $('#divRemarks').show();
    $('#modalApproveReject').on('shown.bs.modal', function () {
    });
}

function onUnReceivePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDocumentNo = row.DOCUMENT_CODE;

    showWarningConfirm("Are you sure you want to un-receive Document No " + confirmText(gDocumentNo)
        + "? This will revert the registration back to Draft. Only possible if no distribution has been assigned yet.",
        unReceiveData, "Un-receive");
}

function unReceiveData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.unReceive,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_CTRL_ID: gDocumentCtrlId
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

function receiveRejectData() {
    popUpProgressHide();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.approveReject,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_CTRL_ID: gDocumentCtrlId,
            DOCUMENT_CODE: gDocumentNo,
            IS_APPROVED: gIsApproved,
            REMARKS: $('#addEditRemarks').val()
        },
        success: function (result) {
            //popUpProgressHide();
            $("#modalApproveReject").modal('hide');
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
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;

    var status = row.STATUS;

    if (status == "DELETED") {
        showErrorMesgGrowl("ERROR: Edit is not allowed for Document with status DELETED");
    } else {
        $.ajax({
            type: "POST",
            url: p4dMaintenanceConfig.urls.getByKey,
            async: false,
            data: {
                DOCUMENT_CTRL_ID: gDocumentCtrlId
            },
            success: function (result) {
                if (result.status == true) {
                    var data = result.data;
                    //showErrorMesgGrowl(data.STATUS);
                    gScreenMode = 'EDIT';
                    $('#modalEdit-title').html("Edit Registration");
                    $('#addEditDocumentId').val(data.DOCUMENT_CTRL_ID);
                    $("#addEditDocumentNo").select2("trigger", "select", { data: { id: data.DOCUMENT_CODE, text: data.DOCUMENT_CODE } });
                    $('#addEditDocumentName').val(data.DOCUMENT_NAME);
                    $('#addEditRevision').val(data.REVISION);

                    $('#divLocation').show();
                    $('#addEditLocation').val(data.LOCATION);

                    $('#divDivision').hide();
                    $('#divDepartment').hide();

                    $('#addEditDocumentId').prop('disabled', true);
                    $('#addEditDocumentNo').prop('disabled', true);
                    $('#addEditDocumentName').prop('disabled', true);
                    $('#addEditLocation').prop('disabled', false);

                    $("#modalAddEdit").modal();
                    $('#modalAddEdit').on('shown.bs.modal', function () {
                        $('#addEditLocation').focus();
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

}

function onDownloadAttachment(attachment, documentId) {
    gAttachmentPath = attachment;
    gDocumentCtrlId = documentId;
    downloadAttachment();
}

function downloadAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = p4dMaintenanceConfig.urls.downloadAttachment;
        link = link.replace("-1", gAttachmentPath);
        link = link.replace("-2", gDocumentCtrlId);
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function onViewAttachment(attachment, documentId) {
    gAttachmentPath = attachment;
    gDocumentCtrlId = documentId;
    viewAttachment();
}

function viewAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = p4dMaintenanceConfig.urls.viewAttachment;
        link = link.replace("-1", gAttachmentPath);
        link = link.replace("-2", gDocumentCtrlId);
        link = link.replace("-3", "2");
        window.open(link, '_blank');
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function onPrintAttachment(attachment, documentId, approvalId) {
    gAttachmentPath = attachment;
    gDocumentCtrlId = documentId;
    gApprovalId = approvalId;
    printAttachment();
}

function printAttachment() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.documentMaintenancePrintAttachment,
        data: {
            FILE_PATH: gAttachmentPath,
            DOCUMENT_TRANSACTION_ID: gDocumentCtrlId,
            type: "2"
        },
        success: function (result) {
            if (result.status == true) {
                popUpProgressHide();
                printJS({ printable: result.data, type: 'pdf', showModal: true });
            } else {
                popUpProgressHide();
                console.log(result);
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onclickLog(documentId) {
    countTotalLog(documentId);
    openLogModal(documentId)
}

function countTotalLog(documentId) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.documentMaintenanceCountTotalLog,
        data: {
            DOCUMENT_TRANSACTION_ID: documentId
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
            "url": p4dMaintenanceConfig.urls.documentMaintenanceSearchDocumentLog,
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

function onclickHistory(documentCode) {
    setModalHeader(documentCode);
    openHistoryModal(documentCode)
}

function setModalHeader(documentCode) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: p4dMaintenanceConfig.urls.documentMaintenanceGetByKey,
        data: {
            DOCUMENT_CODE: documentCode
        },
        success: function (result) {
            console.log(result);
            if (result.status == true) {
                popUpProgressHide();
                $('#txtDocumentCode').html(result.data.DOCUMENT_CODE);
                $('#txtLastRevision').html(result.data.REVISION);
                $('#txtDivision').html(result.data.DIVISION + " - " + result.data.DIVISION_NAME);
                $('#txtDepartment').html(result.data.DEPARTMENT_CODE + " - " + result.data.DEPARTMENT_NAME);
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

function openHistoryModal(documentCode) {
    $('#modalDocumentHistory-title').html("Revision History");

    $("#datagridDocumentHistory").dataTable().fnDestroy();
    $('#datagridDocumentHistory').DataTable({
        "ordering": false,
        "oLanguage": { "sEmptyTable": "Data not found." },
        "dom": "<t><r><'row mt-10'<'col-lg-2'i><'col-lg-1'><'col-lg-5'l><'col-lg-4'p>>",
        "scrollY": '200px',
        "scrollCollapse": false,
        "scrollX": true,
        "processing": true,
        "serverSide": true,
        "ajax": {
            "url": p4dMaintenanceConfig.urls.documentMaintenanceSearchHistory,
            "type": "POST",
            "datatype": "json",
            "data": function (d) {
                d.DOCUMENT_CODE = documentCode
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
            }
            ,
            {
                "targets": [2],
                "className": "text-center",
            }
            ,
            {
                "targets": [3],
                "className": "text-center",
            }
            ,
            {
                "targets": [4],
                "className": "text-left",
            },
            {
                "targets": [5],
                "className": "text-left",
            }
            ,
            {
                "targets": [6],
                "className": "text-left",
            }
            ,

            {
                "targets": [7],
                "className": "text-left",
            }
            ,
            {
                "targets": [8],
                "className": "text-left",
            }
            ,
            {
                "targets": [9],
                "className": "text-center",
            }
            ,
            {
                "targets": [10],
                "className": "text-center",
            }
            ,
            {
                "targets": [11],
                "className": "text-left",
            }
            ,
            {
                "targets": [12],
                "className": "text-left",
            }
            ,
            {
                "targets": [13],
                "className": "text-left",
            }
            ,
            {
                "targets": [14],
                "className": "text-left",
            }
            ,
            {
                "targets": [15],
                "className": "text-left",
            }
            ,
            {
                "targets": [16],
                "className": "text-right",
            },
            {
                "targets": [17],
                "className": "text-left",
            },
            {
                "targets": [18],
                "className": "text-right",
            }
        ],
        "columns": [
            {
                render: function (data, type, row, meta) {
                    return meta.row + meta.settings._iDisplayStart + 1;
                }
            },
            {
                "render": function (data, type, row) {
                    return '<div class="btn-group">' +
                        ' <button type = "button" class="btn btn-primary dropdown-toggle" data-toggle="dropdown" aria-expanded="false" > Action ' +
                        '<span class="fa fa-caret-down"> </span></button> ' +
                        '<ul class="dropdown-menu"> ' +
                        '<li><a onClick="onViewAttachment(\'' + row["FILE_PATH"] + '\', \'' + row["DOCUMENT_TRANSACTION_ID"] + '\')"> View </a></li> ' +
                        '<li><a onClick="onPrintAttachment(\'' + row["FILE_PATH"] + '\', \'' + row["DOCUMENT_TRANSACTION_ID"] + '\', \'' + row["APPROVAL_ID"] + '\')"> Print </a></li>' +
                        //'<li><a id="downloadFile" onClick="onDownloadAttachment(\'' + row["FILE_PATH"] + '\', \'' + row["DOCUMENT_ID"] + '\')"> Download </a></li>' +
                        '</ul>' +
                        '</div>';
                }
            },
            {
                "render": function (data, type, row) {
                    return '<button class="btn btn-primary" type="button" name="btnDistribution" data-id="' + row["DOCUMENT_TRANSACTION_ID"] + '"  data-no="' + row["DOCUMENT_CODE"] + '" onclick="onclickDistribution(\'' + row["DOCUMENT_TRANSACTION_ID"] + '\')"> <i class="fa fa-file-o"></i></button>';
                }
            },
            {
                "render": function (data, type, row) {
                    return '<button class="btn btn-primary" type="button" onclick="onclickLog(\'' + row["DOCUMENT_TRANSACTION_ID"] + '\')"> <i class="fa fa-history"></i></button>';
                }
            },
            { "data": "DOCUMENT_CODE", "name": "DOCUMENT_CODE" },
            { "data": "DOCUMENT_TRANSACTION_NAME", "name": "DOCUMENT_TRANSACTION_NAME" },
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
            { "data": "CLASSIFIED_VAL", "name": "CLASSIFIED_VAL" },
            {
                render: function (data, type, row) {
                    if (row["STATUS"] == 0) {
                        return '<span class="label label-info">' + row["STATUS_DISPLAY"] + '</span>';
                    } else if (row["STATUS"] == 1) {
                        return '<span class="label label-success">' + row["STATUS_DISPLAY"] + '</span>';
                    } else {
                        return '<span class="label label-danger">' + row["STATUS_DISPLAY"] + '</span>';
                    }
                }
            },
            { "data": "REVISION", "name": "REVISION" },
            { "data": "ITEM_CHANGED", "name": "ITEM_CHANGED" },
            { "data": "REASON", "name": "REASON" },
            { "data": "REFERENCE_NO", "name": "REFERENCE_NO" },
            { "data": "SOURCE", "name": "SOURCE" },
            { "data": "CREATED_BY", "name": "CREATED_BY" },
            {
                render: function (data, type, row) {
                    if (row["CREATED_DT"] != null) {
                        return moment(row["CREATED_DT"]).format("DD-MM-YYYY hh:mm:ss");
                    } else {
                        return "";
                    }
                }
            },
            { "data": "CHANGED_BY", "name": "CHANGED_BY" },
            {
                render: function (data, type, row) {
                    if (row["CHANGED_DT"] != null) {
                        return moment(row["CHANGED_DT"]).format("DD-MM-YYYY hh:mm:ss");
                    } else {
                        return "";
                    }
                }
            },
        ],
        "fnDrawCallback": function (oSettings) {
            $('#datagridDocumentHistory tbody > tr').removeClass('selected');
            $('.grid-checkbox').not(this).prop('checked', false);

            $("th").removeClass('sorting_asc');

            toggleColumnWithId("datagridDocumentHistory");

            resizeGrid();
        }
    });

    $("#datagridDocumentHistory").on("click", "tbody tr", function (e) {
        if (event.target.type !== 'checkbox') {
            $(':checkbox', this).trigger('click');
        }
    });

    $("#datagridDocumentHistory").on('change', "input[type='checkbox']", function (e) {
        $('#datagridDocumentHistory tbody > tr').removeClass('selected');
        $('.grid-checkbox').not(this).prop('checked', false);
    });

    $("#modalDocumentHistory").modal();
}
