// DocumentControlDashboard. Server values come from documentControlDashboardConfig in the view.
// Rincian teknis kolom Acceptance (rasio, jumlah department/divisi, hint
// belum di-Send, klik untuk detail popup) cuma untuk QMS/document-control
// staff - sama seperti P4DMaintenance/Index (request Hendra 2026-08-15).
var gIsQms = documentControlDashboardConfig.canDocumentControlAccess;

function computeGridHeight() {
    // Phone: as tall as the rows (see grid-common.js, 2026-11-16)
    if (typeof dmsIsPhone === "function" && dmsIsPhone()) return "auto";
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

    dcdUpdateGridActionCounts(rows);
}

// ---------- Needs Action (request Hendra 2026-11-15) ----------
// What Document Control still has to do, as chips above the toolbar. A chip with a
// predicate filters the register; a chip with a link opens the menu where the work is done
// (its count comes from GetActionItems). Counts of the predicate chips come from the grid
// rows, so they always match what the filter then shows.
var gActionFilter = null; // key of the chip that filters the grid now
var gActionCounts = {};
var gActionLinks = { registration: false, printRequest: false };

function dcdReviewDue(row) {
    if (!row.NEXT_REVIEW_DATE) return false;
    // due within 30 days, or already overdue
    return !moment(row.NEXT_REVIEW_DATE).startOf("day").isAfter(moment().startOf("day").add(30, "days"));
}

var dcdActionDefs = [
    { key: "registration", icon: "fa-inbox", label: "Registrations to Process", link: "documentRegistration", allow: "registration",
      hint: "Document Registration requested or being checked" },
    { key: "notSent", icon: "fa-paper-plane-o", label: "Distributions Not Sent", link: "documentRegistration", allow: "registration",
      hint: "Accepted registrations with a department ticked but not sent yet" },
    { key: "ack", icon: "fa-users", label: "Waiting Acknowledgment",
      hint: "Documents a department has not acknowledged yet",
      predicate: function (row) { return row.ACK_TOTAL != null && row.ACK_DONE != null && row.ACK_DONE < row.ACK_TOTAL; } },
    { key: "obsolete", icon: "fa-undo", label: "Obsolete Copies to Withdraw",
      hint: "Documents with a controlled copy of an old revision still in circulation",
      predicate: function (row) { return (row.OPEN_OBSOLETE_COPIES || 0) > 0; } },
    { key: "print", icon: "fa-print", label: "Print Requests to Approve", link: "printRequest", allow: "printRequest",
      hint: "Print Requests waiting for approval" },
    { key: "review", icon: "fa-clock-o", label: "Review Due",
      hint: "Documents whose next review is within 30 days or overdue", predicate: dcdReviewDue }
];

function dcdActionDef(key) {
    return dcdActionDefs.filter(function (d) { return d.key === key; })[0];
}

function dcdUpdateGridActionCounts(rows) {
    dcdActionDefs.forEach(function (def) {
        if (def.predicate) { gActionCounts[def.key] = rows.filter(def.predicate).length; }
    });
    dcdRenderActionChips();
}

function dcdLoadActionItems() {
    $.ajax({
        type: "GET",
        url: documentControlDashboardConfig.urls.getActionItems,
        cache: false,
        success: function (result) {
            if (!result.status) return;
            gActionCounts.registration = result.data.REGISTRATION_WAITING;
            gActionCounts.notSent = result.data.DISTRIBUTION_NOT_SENT;
            gActionCounts.print = result.data.PRINT_REQUEST_WAITING;
            gActionLinks = { registration: !!result.canRegistration, printRequest: !!result.canPrintRequest };
            dcdRenderActionChips();
        }
    });
}

function dcdRenderActionChips() {
    var $chips = $("#dcdActionChips").empty();
    dcdActionDefs.forEach(function (def) {
        // a chip that leads to a menu the user cannot open is left out
        if (def.link && !gActionLinks[def.allow]) return;

        var count = gActionCounts[def.key];
        var hasWork = count > 0;
        var $chip = $('<button type="button" class="dcd-action-chip"></button>')
            .toggleClass("has-work", hasWork)
            .toggleClass("active", gActionFilter === def.key)
            .attr("title", def.hint + (def.link ? " - opens that menu" : " - click to filter the list"))
            .append($('<i class="fa"></i>').addClass(def.icon))
            .append($('<b></b>').text(count != null ? count : "-"))
            .append($('<span></span>').text(def.label));
        if (def.link) { $chip.append('<i class="fa fa-external-link dcd-action-chip-go"></i>'); }

        $chip.on("click", function () {
            if (def.link) {
                window.location.href = documentControlDashboardConfig.urls[def.link];
                return;
            }
            gActionFilter = gActionFilter === def.key ? null : def.key;
            dcdRenderActionChips();
            dcdApplyGridFilter();
        });
        $chips.append($chip);
    });

    // Total on the Needs Action tab and on the button that opens the panel, and the active
    // filter next to the folder title (the panel may be closed).
    gActionTotal = 0;
    dcdActionDefs.forEach(function (def) {
        if (def.link && !gActionLinks[def.allow]) return;
        gActionTotal += gActionCounts[def.key] || 0;
    });
    $("#dcdActionBadge").text(gActionTotal).toggle(gActionTotal > 0);
    refreshToggleBadge();

    var active = gActionFilter ? dcdActionDef(gActionFilter) : null;
    $("#dcdActionActiveLabel").text(active ? active.label : "");
    $("#dcdActionActive").toggle(!!active);
    $("#dcdFolderTitleRow").toggle(!!active);
}

var gActionTotal = 0;
var gStatsTabChosen = false; // the user picked a tab: stop choosing one for them

function dcdShowStatsTab(tab) {
    $("#dcdStatsTabs .dcd-stats-tab").each(function () { $(this).toggleClass("active", $(this).data("tab") === tab); });
    $("#dcdStatsRow").toggle(tab === "overview");
    $("#dcdActionChips").toggle(tab === "action");
}

$(document).ready(function () {
    $("#dcdStatsTabs").on("click", ".dcd-stats-tab", function () {
        gStatsTabChosen = true;
        dcdShowStatsTab($(this).data("tab"));
    });

    $("#btn_clearActionFilter").on("click", function () {
        gActionFilter = null;
        dcdRenderActionChips();
        dcdApplyGridFilter();
    });
});

// ---------- one place that filters the grid: open folder / binder AND Needs Action chip ----------
// grid.filter(expr) replaces the previous expression, so the two criteria are combined here
// instead of each caller setting its own (column header filters and search stay separate).
var gBinderDocCodes = null; // document codes with a copy in the open controlled copy binder

function dcdApplyGridFilter() {
    var grid = $("#datagrid").dxDataGrid("instance");
    var parts = [];

    var folder = gSelectedFolderId != null ? dcdGetFolderById(gSelectedFolderId) : null;
    if (folder && folder.FOLDER_TYPE === "COPY") {
        // a controlled copy binder holds copies: list the documents it has a copy of
        parts.push([function (row) { return !!gBinderDocCodes && gBinderDocCodes[row.DOCUMENT_CODE] === true; }, "=", true]);
    } else if (gSelectedFolderId != null) {
        parts.push(["FOLDER_ID", "=", gSelectedFolderId]);
    }

    var action = gActionFilter ? dcdActionDef(gActionFilter) : null;
    if (action && action.predicate) { parts.push([action.predicate, "=", true]); }

    grid.filter(parts.length === 0 ? null : (parts.length === 1 ? parts[0] : [parts[0], "and", parts[1]]));
}

// ---------- Folders / Register Overview sidebar toggles - each independent
// (restored 2026-08-29: "keduanya bisa show hide" separately, Register Overview
// defaults hidden like before) - top-level (not inside $(document).ready) so
// updateHeaderStats() above can reach dcdLastPendingAck too ----------
var dcdLastPendingAck = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    // 2026-11-15: the badge counts everything under Needs Action, not only pending acknowledgment
    $("#dpToggleBadge").text(gActionTotal).toggle(collapsed && gActionTotal > 0);
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
    // Opens on Needs Action while something is waiting, until the user picks a tab himself.
    if (!collapsed && !gStatsTabChosen) { dcdShowStatsTab(gActionTotal > 0 ? "action" : "overview"); }

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
    onViewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID, row);
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

    if (dcdFoldersEnabled()) {
        $menu.append(dcdMenuItem("dp-menu-move", "fa-folder-o", "Move to Folder", function () {
            dcdMoveDocumentsToFolder([row.DOCUMENT_TRANSACTION_ID], $btn);
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

// Purpose decides the stamp: MASTER (archive), CONTROLLED COPY (numbered per revision,
// recipient required) or UNCONTROLLED COPY (reference / audit / external).
function dcdExportPdf(row) {
    // Controlled copy binders (2026-11-15): a controlled copy can be filed in one right away,
    // its holder department is then the recipient unless another one is typed. Offered only
    // once a controlled copy binder exists. Opened from inside such a binder, the dialog
    // starts on a controlled copy for that binder.
    var copyBinders = gAllFolders.filter(function (f) { return f.FOLDER_TYPE === "COPY"; });
    var openBinder = gSelectedFolderId != null ? dcdGetFolderById(gSelectedFolderId) : null;
    if (openBinder && openBinder.FOLDER_TYPE !== "COPY") { openBinder = null; }

    var inputs = {
        purpose: {
            type: "select",
            label: "Purpose",
            title: "Choose a purpose",
            options: {
                "UNCONTROLLED": "Reference / audit / external - UNCONTROLLED COPY",
                "CONTROLLED": "Distribution - CONTROLLED COPY (numbered)",
                "MASTER": "Archive - MASTER"
            },
            defaultValue: openBinder ? "CONTROLLED" : "UNCONTROLLED"
        }
    };
    if (copyBinders.length > 0) {
        var binderOptions = { "none": "Not filed in a binder" };
        copyBinders.forEach(function (f) {
            binderOptions["b" + f.FOLDER_ID] = confirmText((f.FOLDER_CODE || f.FOLDER_NAME) + " - " + (f.DEPARTMENT_CODE || "") + (f.FOLDER_CODE ? " (" + f.FOLDER_NAME + ")" : ""));
        });
        inputs.binder = {
            type: "select",
            label: "Binder (Controlled Copy only)",
            title: "Choose a binder",
            options: binderOptions,
            defaultValue: openBinder ? "b" + openBinder.FOLDER_ID : "none"
        };
    }
    inputs.recipient = {
        type: "text",
        label: copyBinders.length > 0 ? "Recipient (Controlled Copy; empty = holder of the binder)" : "Recipient (required for Controlled Copy)",
        title: "Department or person",
        maxlength: 200
    };
    inputs.notes = {
        type: "text",
        label: "Notes",
        title: "Optional",
        maxlength: 500
    };

    function chosenBinder(data) {
        return data.purpose === "CONTROLLED" && data.binder && data.binder !== "none" ? String(data.binder).substring(1) : "";
    }

    $.MessageBox({
        title: "Download PDF",
        message: confirmText(row.DOCUMENT_CODE || row.DOCUMENT_NAME || "") + " (Rev. " + (row.REVISION != null ? row.REVISION : 0) + ")",
        input: inputs,
        buttonDone: "Download",
        buttonFail: "Cancel",
        customClass: "msg-confirm",
        filterDone: function (data) {
            if (!data.purpose) return "Choose a purpose.";
            if (data.purpose === "CONTROLLED" && !$.trim(data.recipient) && !chosenBinder(data)) {
                return copyBinders.length > 0
                    ? "Choose the binder the controlled copy goes into, or enter its recipient."
                    : "Enter the recipient of the controlled copy.";
            }
        }
    }).done(function (data) {
        var $form = $('<form method="post" target="_blank"></form>').attr("action", documentControlDashboardConfig.urls.exportPdf);
        $('<input type="hidden" name="documentTransactionId">').val(row.DOCUMENT_TRANSACTION_ID).appendTo($form);
        $('<input type="hidden" name="purpose">').val(data.purpose).appendTo($form);
        $('<input type="hidden" name="recipient">').val($.trim(data.recipient || "")).appendTo($form);
        $('<input type="hidden" name="notes">').val($.trim(data.notes || "")).appendTo($form);
        if (chosenBinder(data)) { $('<input type="hidden" name="binderId">').val(chosenBinder(data)).appendTo($form); }
        $form.appendTo("body").trigger("submit").remove();

        // The copy is numbered while the PDF is built in the other tab: refresh the binder
        // counts and the register shortly after.
        if (data.purpose === "CONTROLLED") {
            setTimeout(function () {
                loadFolderTree(function () { dcdRefreshOpenBinder(); });
            }, 4000);
        }
    });
}

// ---------- folder breadcrumb/title + grid filter (request Hendra 2026-08-28) ----------
// Called from _FolderTree.cshtml's dcdSelectFolder(). The grid already loads every
// document once (loadMode:"raw"/length:100000) and relies on DevExtreme's own
// client-side filtering elsewhere (see the DOCUMENT_CODE deep-link filter in
// onContentReady below) - folder selection reuses that same mechanism, no extra
// server round-trip.
function dcdApplyFolderFilter(folderId) {
    // NOTE: columnOption(field, "filterValue", value) silently does nothing for a
    // hidden (visible:false) column in this DevExtreme version - verified directly.
    // grid.filter(expr) works regardless of column visibility and still combines
    // (AND) with any other active column filters - filter(null) clears just this
    // criterion, leaving other filters (e.g. the DOCUMENT_CODE deep-link one below,
    // or user header filters) untouched.
    // 2026-11-15: the expression itself is built in dcdApplyGridFilter() (folder / binder
    // AND Needs Action chip). A controlled copy binder first loads which documents it has
    // a copy of; until then the list is empty rather than wrong.
    gBinderDocCodes = null;
    dcdApplyGridFilter();
    dcdRenderBinderHeader();
    dcdRefreshOpenBinder();

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

// ---------- Binders (bantex, request Hendra 2026-11-15) ----------
// Bar under the folder title: what kind of binder the open folder is, its label code,
// holder and location, with Binder Index and Binder Details. See folder-tree.js for the
// tree side and SQL/DocumentBinder_Migration.sql for the data.
function dcdOpenFolder() {
    return gSelectedFolderId != null ? dcdGetFolderById(gSelectedFolderId) : null;
}

function dcdRenderBinderHeader() {
    var folder = dcdOpenFolder();
    var $bar = $("#dcdBinderBar");
    if (!folder) { $bar.hide(); return; }

    var $meta = $("#dcdBinderMeta").empty();
    var isCopy = folder.FOLDER_TYPE === "COPY";

    $('<span class="dms-pill"></span>')
        .addClass(isCopy ? "dms-pill-primary" : (folder.FOLDER_TYPE === "MASTER" ? "dms-pill-success" : "dms-pill-default"))
        .html('<i class="fa ' + dcdFolderIconClass(folder) + '"></i> ')
        .append(document.createTextNode(dcdBinderTypeLabel(folder)))
        .appendTo($meta);

    function item(label, value) {
        if (!value) return;
        $('<span class="dcd-binder-item"></span>').append($("<b></b>").text(label)).append(document.createTextNode(" " + value)).appendTo($meta);
    }
    item("Code", folder.FOLDER_CODE);
    item("Holder", dcdBinderHolder(folder));
    item("Location", folder.LOCATION);
    if (isCopy) {
        item("Copies", String(folder.COPY_COUNT || 0));
        if (folder.OBSOLETE_COPY_COUNT > 0) {
            $('<span class="dcd-binder-alert"></span>')
                .html('<i class="fa fa-exclamation-triangle"></i> ')
                .append(document.createTextNode(folder.OBSOLETE_COPY_COUNT + " obsolete cop" + (folder.OBSOLETE_COPY_COUNT > 1 ? "ies" : "y") + " to withdraw"))
                .appendTo($meta);
        }
    }
    if (!folder.FOLDER_TYPE) {
        $('<span class="dcd-binder-hint"></span>').text("Is this a physical binder? Set its type, code and holder in Binder Details.").appendTo($meta);
    }

    // the copies of a controlled copy binder follow the Copy Register permission
    $("#btn_binderIndex").toggle(!!folder.FOLDER_TYPE && (!isCopy || documentControlDashboardConfig.canExportPdf));
    $bar.css("display", "flex");
}

// Reloads what the open controlled copy binder holds: the grid then lists the documents
// it has a copy of, and an open Binder Index panel is redrawn.
function dcdRefreshOpenBinder() {
    var folder = dcdOpenFolder();
    dcdRenderBinderHeader();
    if (!folder || folder.FOLDER_TYPE !== "COPY" || !documentControlDashboardConfig.canExportPdf) {
        if (folder && $("#binderPanel").hasClass("open")) { bdpLoad(folder); }
        return;
    }
    $.ajax({
        type: "GET",
        url: documentControlDashboardConfig.urls.getBinderContents,
        data: { folderId: folder.FOLDER_ID },
        cache: false,
        success: function (result) {
            if (gSelectedFolderId !== folder.FOLDER_ID) return; // another folder was opened meanwhile
            var codes = {};
            ((result.status && result.data) || []).forEach(function (d) { codes[d.DOCUMENT_CODE] = true; });
            gBinderDocCodes = codes;
            dcdApplyGridFilter();
            if ($("#binderPanel").hasClass("open")) { bdpRender(folder, result); }
        }
    });
}

// ---------- Binder Index side panel ----------
var bdpFolder = null;

function dcdOpenBinderIndex(folder) {
    bdpFolder = folder;
    $("#acceptancePanel, #copyRegisterPanel").removeClass("open");
    $("#bdpTitle").text((folder.FOLDER_CODE ? folder.FOLDER_CODE + " · " : "") + folder.FOLDER_NAME);
    $("#bdpSummary").empty();
    $("#bdpBody").html('<div class="crp-empty"><i class="fa fa-spinner fa-spin"></i> Loading...</div>');
    $("#binderPanel").addClass("open");
    bdpLoad(folder);
}

function bdpLoad(folder) {
    $.ajax({
        type: "GET",
        url: documentControlDashboardConfig.urls.getBinderContents,
        data: { folderId: folder.FOLDER_ID },
        cache: false,
        success: function (result) { bdpRender(folder, result); },
        error: function () { $("#bdpBody").html('<div class="crp-empty">Failed to load the binder</div>'); }
    });
}

function bdpRender(folder, result) {
    bdpFolder = folder;
    var $body = $("#bdpBody").empty();
    var $summary = $("#bdpSummary").empty();
    if (!result.status) {
        $('<div class="crp-empty"></div>').text(result.message || "Failed to load the binder").appendTo($body);
        return;
    }

    var items = result.data || [];
    var isCopy = folder.FOLDER_TYPE === "COPY";
    var obsolete = items.filter(function (d) { return d.IS_OBSOLETE; }).length;

    var stats = [{ value: items.length, label: isCopy ? "Copies in the binder" : "Documents in the binder" }];
    if (isCopy) {
        stats.push({ value: items.length - obsolete, label: "Current revision" });
        stats.push({ value: obsolete, label: "Obsolete, to withdraw", alert: obsolete > 0 });
    }
    stats.forEach(function (s) {
        $('<div class="crp-stat"></div>').toggleClass("crp-stat-alert", !!s.alert)
            .append($('<div class="crp-stat-value"></div>').text(s.value))
            .append($('<div class="crp-stat-label"></div>').text(s.label))
            .appendTo($summary);
    });

    if (items.length === 0) {
        $body.html(isCopy
            ? '<div class="crp-empty">No controlled copy is filed in this binder yet.<br/>Use Download PDF &rarr; Distribution and choose this binder, or File in Binder in a Copy Register.</div>'
            : '<div class="crp-empty">No document is assigned to this binder yet.<br/>Tick documents in the list and use Move to Folder.</div>');
        return;
    }

    if (obsolete > 0) {
        $('<div class="crp-rev-warning"></div>')
            .html('<i class="fa fa-exclamation-triangle"></i> ')
            .append(document.createTextNode(obsolete + " cop" + (obsolete > 1 ? "ies" : "y") + " of a superseded revision still in this binder. Replace with the current revision, then mark the old copy as withdrawn."))
            .appendTo($body);
    }

    // obsolete copies first: they are the work to do
    items.slice().sort(function (a, b) { return (b.IS_OBSOLETE ? 1 : 0) - (a.IS_OBSOLETE ? 1 : 0); })
        .forEach(function (d) { bdpCard(d, isCopy).appendTo($body); });
}

function bdpCard(d, isCopy) {
    var $card = $('<div class="crp-card"></div>');
    var $top = $('<div class="crp-card-top"></div>').appendTo($card);
    $('<span class="crp-copy-no"></span>').text(d.DOCUMENT_CODE || "-").appendTo($top);

    if (isCopy) {
        $('<span class="crp-pill"></span>')
            .addClass(d.IS_OBSOLETE ? "crp-pill-danger" : "crp-pill-success")
            .text(d.IS_OBSOLETE ? "Obsolete" : "Current")
            .appendTo($top);
        $('<button type="button" class="crp-withdraw-btn"><i class="fa fa-undo"></i> Withdraw</button>')
            .on("click", function () { crpWithdraw(d, bdpAfterChange); })
            .appendTo($top);
    }

    if (d.DOCUMENT_NAME) { $('<div class="crp-line"></div>').text(d.DOCUMENT_NAME).appendTo($card); }

    var rev = "Rev. " + (d.REVISION != null ? d.REVISION : 0);
    if (isCopy) {
        $('<div class="crp-line"></div>').append($("<b></b>").text(rev + " · Copy No. " + ("0" + d.COPY_NO).slice(-2))).appendTo($card);
        if (d.IS_OBSOLETE) {
            $('<div class="crp-line crp-obsolete-note"></div>')
                .text(d.CURRENT_REVISION != null ? "Replace with Rev. " + d.CURRENT_REVISION : "The document is no longer effective: withdraw this copy")
                .appendTo($card);
        }
        $('<div class="crp-line crp-muted"></div>').text("To " + (d.RECIPIENT || "-") + " · issued " + crpFormatDate(d.ISSUED_DT)).appendTo($card);
        $('<button type="button" class="crp-link-btn"><i class="fa fa-exchange"></i> Move to Another Binder</button>')
            .on("click", function (e) { e.stopPropagation(); dcdFileCopyInBinder(d, $(this), bdpAfterChange); })
            .appendTo($card);
    } else {
        $('<div class="crp-line crp-muted"></div>').text(rev + (d.ISSUED_DT ? " · effective " + moment(d.ISSUED_DT).format("DD MMM YYYY") : "")).appendTo($card);
    }
    return $card;
}

// after a copy was withdrawn or moved: binder counts in the tree, then the open binder
function bdpAfterChange() {
    loadFolderTree(function () {
        var folder = bdpFolder ? dcdGetFolderById(bdpFolder.FOLDER_ID) : null;
        if (folder && dcdOpenFolder() !== folder) { bdpLoad(folder); }
        dcdRefreshOpenBinder();
    });
    $("#datagrid").dxDataGrid("instance").refresh();
}

// Pick a controlled copy binder for one copy (Copy Register and Binder Index).
function dcdFileCopyInBinder(copy, $anchor, afterDone) {
    dcdOpenFolderPicker($anchor, function (folderId) {
        $.ajax({
            type: "POST",
            url: documentControlDashboardConfig.urls.assignCopyToBinder,
            data: { exportId: copy.EXPORT_ID, folderId: folderId },
            success: function (result) {
                if (result.status) { showSuccessMesgGrowl(result.message); } else { showErrorMesgGrowl(result.message); }
                afterDone();
            },
            error: function () { showErrorMesgGrowl("Failed to file the copy"); }
        });
    }, true);
}

$(document).ready(function () {
    $("#btn_binderIndex").on("click", function (e) {
        e.stopPropagation(); // or the outside-click handler below closes the panel again
        var folder = dcdOpenFolder();
        if (folder) { dcdOpenBinderIndex(folder); }
    });

    $("#btn_binderDetails").on("click", function () {
        var folder = dcdOpenFolder();
        if (folder) { dcdOpenBinderDetails(folder); }
    });

    $("#btn_printBinderIndex").on("click", function () {
        if (bdpFolder) { window.open(documentControlDashboardConfig.urls.binderIndex + "?folderId=" + bdpFolder.FOLDER_ID, "_blank"); }
    });

    $("#btn_closeBinderPanel").on("click", function () {
        $("#binderPanel").removeClass("open");
    });

    // Outside click closes the panel, except clicks in its dialogs and the binder picker.
    $(document).on("click", function (e) {
        if ($("#binderPanel").hasClass("open")
            && !$(e.target).closest("#binderPanel, .messagebox, .messagebox_overlay, .dcd-folder-picker").length) {
            $("#binderPanel").removeClass("open");
        }
    });

    dcdRenderActionChips();
    dcdLoadActionItems();
});

var dcdDeepLinkApplied = false;

// Folders (sidebar, Move to Folder, row checkboxes) are hidden for now (Hendra 2026-11-18).
// One switch: the "folders-hidden" class on #dpLayoutRow in Index.cshtml.
function dcdFoldersEnabled() {
    return !$("#dpLayoutRow").hasClass("folders-hidden");
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

    $("#btn_clearSelection").on("click", function () {
        $("#datagrid").dxDataGrid("instance").clearSelection();
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
                dcdLoadActionItems(); // Refresh also brings the counts from the other menus up to date
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
        // The checkboxes only serve Move to Folder; while folders are hidden a click just
        // highlights the row (History still selects its row through selectRows).
        selection: dcdFoldersEnabled()
            ? { mode: "multiple", showCheckBoxesMode: "always", allowSelectAll: true }
            : { mode: "single" },
        // bulk bar: "3 selected · Move to Folder · x", only while documents are ticked
        onSelectionChanged: function (e) {
            var n = e.selectedRowKeys.length;
            $("#dcdSelectionCount").text(n + " selected");
            $("#dcdSelectionBar").toggle(n > 0 && dcdFoldersEnabled());
        },
        // double-click a document opens it, like a file in File Explorer
        onRowDblClick: function (e) {
            // the Document No link already opens on a single click
            if (e.rowType === "data" && !$(e.event.target).closest(".dcd-doc-link, .dp-act-kebab").length) { dcdOpenDocument(e.data); }
        },
        columns: [
            { type: "selection", width: 40, fixed: true, fixedPosition: "left", visible: dcdFoldersEnabled() },
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
                    $("<i>").addClass("fa dms-file-icon " + dmsFileIconClass(options.data.FILE_PATH)).appendTo(container);
                    $("<span>").addClass("dcd-doc-link").text(options.data.DOCUMENT_CODE)
                        .on("click", function () { dcdOpenDocument(options.data); })
                        .appendTo(container);
                    // copies of an old revision still in circulation: opens the Copy Register
                    var openObsolete = options.data.OPEN_OBSOLETE_COPIES || 0;
                    if (openObsolete > 0 && documentControlDashboardConfig.canExportPdf) {
                        $("<i>").addClass("fa fa-exclamation-triangle dcd-obsolete-flag")
                            .attr("title", openObsolete + " obsolete cop" + (openObsolete > 1 ? "ies" : "y") + " still in circulation - click to open the Copy Register")
                            .on("click", function (e) { e.stopPropagation(); dcdOpenCopyRegister(options.data); })
                            .appendTo(container);
                    }
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
                // owning division (snapshot on the document), next to Department (Hendra 2026-11-18)
                dataField: "DIVISION",
                caption: "Division",
                calculateDisplayValue: function (row) {
                    if (!row.DIVISION) return "";
                    return row.DIVISION_NAME ? row.DIVISION + " - " + row.DIVISION_NAME : row.DIVISION;
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
            // Only once: before, it was applied again after every Reset (2026-11-14).
            var params = new URLSearchParams(window.location.search);
            var documentCode = params.get("DOCUMENT_CODE");
            if (documentCode && !dcdDeepLinkApplied) {
                dcdDeepLinkApplied = true;
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
    // Clears column filters, search and selection, but keeps the open folder: clearFilter()
    // also drops the folder filter, so it is put back (2026-11-14; before, the tree still
    // showed the folder while the grid listed every document).
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        grid.clearFilter();
        grid.searchByText("");
        grid.clearSelection();
        // the Needs Action chip is a filter too, so Reset clears it (2026-11-15)
        gActionFilter = null;
        dcdRenderActionChips();
        dcdApplyGridFilter();
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
        // fit the panel width: long department names wrap instead of pushing Status out of
        // view (Hendra 2026-11-18, the Status column was cut off)
        columnAutoWidth: false,
        wordWrapEnabled: true,
        height: "calc(100vh - 110px)",
        searchPanel: { visible: true, width: 300, placeholder: "Search department..." },
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
                width: 104,
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
            .on("click", function () { crpWithdraw(d, crpAfterChange); })
            .appendTo($top);
    }

    $('<div class="crp-line"></div>').append("<b>To:</b> ").append(document.createTextNode(d.RECIPIENT || "-")).appendTo($card);
    // Binder the copy is filed in (2026-11-15)
    if (d.FOLDER_ID || !withdrawn) {
        var binder = d.FOLDER_ID ? (d.BINDER_CODE || d.BINDER_NAME || "-") + (d.BINDER_CODE && d.BINDER_NAME ? " · " + d.BINDER_NAME : "") : "Not filed in a binder";
        var $binder = $('<div class="crp-line"></div>').append("<b>Binder:</b> ").append(document.createTextNode(binder)).appendTo($card);
        if (!withdrawn) {
            $('<button type="button" class="crp-link-btn"><i class="fa fa-book"></i> ' + (d.FOLDER_ID ? "Move" : "File in Binder") + '</button>')
                .on("click", function (e) { e.stopPropagation(); dcdFileCopyInBinder(d, $(this), crpAfterChange); })
                .appendTo($binder);
        }
    }
    $('<div class="crp-line crp-muted"></div>').text("Issued by " + (d.CREATED_BY || "-") + " on " + crpFormatDate(d.CREATED_DT)).appendTo($card);
    if (d.NOTES) $('<div class="crp-line crp-muted"></div>').text("Note: " + d.NOTES).appendTo($card);
    if (withdrawn) {
        $('<div class="crp-line crp-muted"></div>')
            .text("Withdrawn by " + (d.WITHDRAWN_BY || "-") + " on " + crpFormatDate(d.WITHDRAWN_DT) + (d.WITHDRAW_NOTE ? " · " + d.WITHDRAW_NOTE : ""))
            .appendTo($card);
    }
    return $card;
}

// after a copy was withdrawn or filed: the register, the binder counts and the obsolete
// flags of the document list
function crpAfterChange() {
    crpLoad();
    loadFolderTree(function () { dcdRefreshOpenBinder(); });
    $("#datagrid").dxDataGrid("instance").refresh();
}

// afterDone: what to reload - the Copy Register or the Binder Index, whichever asked.
function crpWithdraw(d, afterDone) {
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
                afterDone();
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

    // Outside click closes the panel, except clicks in the Withdraw dialog and the binder picker.
    $(document).on("click", function (e) {
        if ($("#copyRegisterPanel").hasClass("open")
            && !$(e.target).closest("#copyRegisterPanel, .messagebox, .messagebox_overlay, .dcd-folder-picker").length) {
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

function onViewAttachment(attachment, documentId, row) {
    gFilePath = attachment;
    gDocumentTransactionId = documentId;
    // in-page book viewer, the same in every menu (2026-11-18)
    dmsViewDocument(documentControlDashboardConfig.urls.viewAttachment, attachment, documentId, "2", row);
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
