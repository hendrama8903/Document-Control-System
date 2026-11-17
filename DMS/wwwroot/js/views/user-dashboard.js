// UserDashboard. Server values come from userDashboardConfig in the view.
function canAcknowledge(row) {
    return row != null && row.ACK_ELIGIBLE == 1 && (row.STATUS == 2 || row.STATUS == 5) && row.PUBLISH_FLAG != 1;
}

// Documents the login user can accept now (the "Waiting for Acceptance" folder).
function udWaitingRows() {
    return userDashboardConfig.canAcknowledge ? udAllRows.filter(canAcknowledge) : [];
}

function computeGridHeight() {
    // Phone: as tall as the rows (see grid-common.js, 2026-11-16)
    if (typeof dmsIsPhone === "function" && dmsIsPhone()) return "auto";
    var gridTop = $("#datagrid").offset().top;
    var footerHeight = $("#divFooter").outerHeight(true) || 0;

    return Math.max(300, $(window).height() - gridTop - footerHeight - 70);
}

// ---------- header stats (Total / Acknowledged / Not Acknowledged / Obsolete) ----------
function updateHeaderStats(rows) {
    rows = rows || [];
    var total = rows.length;
    var acknowledged = 0, notAcknowledged = 0, obsolete = 0;

    rows.forEach(function (r) {
        if (r.PUBLISH_FLAG == 1) acknowledged++; else notAcknowledged++;

        if (r.DOC_STATUS == "4") obsolete++;
    });

    $("#udTotal").text(total);
    $("#udAcknowledged").text(acknowledged);
    $("#udNotAcknowledged").text(notAcknowledged);
    $("#udObsolete").text(obsolete);

    // surfaces the "waiting acceptance" count on the collapsed toggle
    // too, so it stays useful (not just a bare arrow) even when the
    // panel is closed
    udLastNotAcknowledged = notAcknowledged;
    refreshToggleBadge();
}

// ---------- docked "Document Status" panel toggle - top-level (not
// inside $(document).ready) so updateHeaderStats() above can reach
// udLastNotAcknowledged too ----------
var udLastNotAcknowledged = 0;

function refreshToggleBadge() {
    var collapsed = $("#dpLayoutRow").hasClass("stats-collapsed");
    $("#dpToggleBadge").text(udLastNotAcknowledged).toggle(collapsed && udLastNotAcknowledged > 0);
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

// ---------- Folders sidebar toggle (request Hendra 2026-08-29: folder feature
// ported from Document Control Dashboard) - independent of the Document Status
// toggle above, defaults shown. ----------
function setFoldersPanelCollapsed(collapsed) {
    var $row = $("#dpLayoutRow");

    $row.toggleClass("folders-collapsed", collapsed);
    // tombol hide ada di header panel Folders; tombol toolbar cuma untuk show
    // (request Hendra 2026-11-05)
    $("#btn_toggleFolders").toggleClass("dcd-hidden", !collapsed);

    setTimeout(function () {
        $("#datagrid").dxDataGrid("instance").updateDimensions();
    }, 230);
}

var udCategoryPillClasses = ["dms-pill-info", "dms-pill-success", "dms-pill-warning", "dms-pill-primary", "dms-pill-default"];
function udCategoryPillClass(code) {
    var sum = 0;
    for (var i = 0; i < (code || "").length; i++) sum += code.charCodeAt(i);
    return udCategoryPillClasses[sum % udCategoryPillClasses.length];
}

// ---------- File Explorer view (Hendra 2026-11-13) ----------
// Locations: all | waiting (Waiting for Acceptance) | mine (My Folders) | folder (a personal
// folder, TB_M_DOCUMENT_FOLDER_PERSONAL). The Library tree (Level -> Category) was dropped on
// 2026-11-14 at Hendra's request; the Category column filter covers that.
// The grid loads every document once; opening a location filters that cached set in the
// store (not grid.filter, so Reset only clears the column filters and keeps the location).
var udAllRows = [];
var udUseCache = false;
var udLocation = { type: "all" };
var udTreeExpanded = { mine: true };

function udRowsIn(loc) {
    switch (loc.type) {
        case "mine": return udAllRows.filter(function (r) { return r.FOLDER_ID != null; });
        case "folder": return udAllRows.filter(function (r) { return r.FOLDER_ID === loc.id; });
        case "waiting": return udWaitingRows();
        default: return udAllRows;
    }
}

// Subfolders of a location: [{label, icon, loc, info}]
function udSubfolders(loc) {
    var items = [];
    var count = function (l) { return udRowsIn(l).length; };

    // All Documents lists no subfolders: My Folders is already in the tree.
    if (loc.type === "mine" || loc.type === "folder") {
        dcdGetFolderChildren(loc.type === "mine" ? null : loc.id).forEach(function (f) {
            items.push({ label: f.FOLDER_NAME, icon: "fa-folder", loc: { type: "folder", id: f.FOLDER_ID } });
        });
    }
    items.forEach(function (it) { it.info = count(it.loc); });
    return items;
}

// Path root -> location, each {label, loc}
function udPath(loc) {
    var path = [{ label: "All Documents", loc: { type: "all" } }];
    if (loc.type === "waiting") { path.push({ label: "Waiting for Acceptance", loc: loc }); }
    if (loc.type === "mine" || loc.type === "folder") {
        path.push({ label: "My Folders", loc: { type: "mine" } });
        if (loc.type === "folder") {
            dcdGetFolderPath(loc.id).forEach(function (f) {
                path.push({ label: f.FOLDER_NAME, loc: { type: "folder", id: f.FOLDER_ID } });
            });
        }
    }
    return path;
}

function udOpen(loc) {
    if (loc.type === "folder" && !dcdGetFolderById(loc.id)) { loc = { type: "mine" }; }
    udLocation = loc;

    // keep the tree in step: folder highlight + expand the branch that holds the location
    gSelectedFolderId = loc.type === "folder" ? loc.id : (loc.type === "all" ? null : "UD");
    if (loc.type === "mine" || loc.type === "folder") { udTreeExpanded.mine = true; }
    if (loc.type === "folder") {
        dcdGetFolderPath(loc.id).forEach(function (f) { if (f.FOLDER_ID !== loc.id) gExpandedFolders.add(f.FOLDER_ID); });
    }
    renderFolderTree(); // also redraws the address bar and the folder list

    var grid = $("#datagrid").data("dxDataGrid") ? $("#datagrid").dxDataGrid("instance") : null;
    if (grid) {
        udUseCache = true;
        grid.clearSelection();
        grid.refresh();
    }
}

function udRenderHeader() {
    var path = udPath(udLocation);
    // the path is shown only inside a folder; at the top levels the title says it all
    var $crumb = $("#dcdCrumb").empty().toggle(udLocation.type === "folder");
    path.forEach(function (p, i) {
        if (i > 0) { $("<span>").addClass("dcd-crumb-sep").text("›").appendTo($crumb); }
        var last = i === path.length - 1;
        $("<span>").addClass(last ? "ud-crumb-current" : "dcd-crumb-item").text(p.label).attr("title", p.title || p.label)
            .on("click", function () { if (!last) udOpen(p.loc); })
            .appendTo($crumb);
    });
    var current = path[path.length - 1];
    $("#dcdFolderTitle").text(current.title || current.label);
}

function udRenderFolderList() {
    var items = udSubfolders(udLocation);
    var $list = $("#udFolderList").empty().toggle(items.length > 0);

    items.forEach(function (it) {
        var $row = $("<div>").addClass("ud-folder-item").attr({ tabindex: 0, title: "Double-click to open" });
        $("<i>").addClass("fa " + it.icon + " ud-folder-item-icon").appendTo($row);
        $("<span>").addClass("ud-folder-item-name").text(it.label).appendTo($row);
        $("<span>").addClass("ud-folder-item-type").text("Folder").appendTo($row);
        $("<span>").addClass("ud-folder-item-count").text(it.info + " document" + (it.info !== 1 ? "s" : "")).appendTo($row);

        $row.on("click", function () {
            $list.find(".ud-folder-item").removeClass("selected");
            $row.addClass("selected");
        });
        $row.on("dblclick", function () { udOpen(it.loc); });
        $row.on("keydown", function (e) { if (e.key === "Enter") udOpen(it.loc); });
        $list.append($row);
    });

    setTimeout(function () {
        var grid = $("#datagrid").data("dxDataGrid") ? $("#datagrid").dxDataGrid("instance") : null;
        if (grid) grid.option("height", computeGridHeight());
    }, 0);
}

// Top of the shared folder tree (hook in folder-tree.js renderFolderTree). Returns the depth
// for My Folders' folders, or -1 when My Folders is collapsed.
function folderTreeRenderTop($tree) {
    function treeRow(label, icon, depth, selected, onClick, opts) {
        opts = opts || {};
        var $row = $("<div>").addClass("dcd-folder-row").toggleClass("selected", selected)
            .toggleClass("ud-tree-section", !!opts.section)
            .css("padding-left", (8 + depth * 18) + "px");
        var $chev = $("<span>").addClass("dcd-folder-chevron fa fa-chevron-right")
            .toggleClass("expanded", !!opts.expanded)
            .css("visibility", opts.onToggle ? "visible" : "hidden").appendTo($row);
        if (opts.onToggle) { $chev.on("click", function (e) { e.stopPropagation(); opts.onToggle(); }); }
        $("<i>").addClass("fa " + icon + " dcd-folder-icon").appendTo($row);
        $("<span>").addClass("dcd-folder-name").text(label).attr("title", opts.title || label).appendTo($row);
        if (opts.count != null) { $("<span>").addClass("dcd-folder-count").addClass(opts.countClass || "").text(opts.count).appendTo($row); }
        $row.on("click", function (e) {
            if ($(e.target).closest(".dp-act-kebab, .dp-dropdown-menu").length > 0) { return; }
            onClick();
        });
        $tree.append($row);
    }

    var loc = udLocation;

    // ---- inbox: documents waiting for this user's acceptance (only for users who may accept) ----
    if (userDashboardConfig.canAcknowledge) {
        var waiting = udWaitingRows().length;
        treeRow("Waiting for Acceptance", "fa-inbox", 0, loc.type === "waiting", function () { udOpen({ type: "waiting" }); },
            { count: waiting, countClass: waiting > 0 ? "ud-count-alert" : null });
    }

    treeRow("All Documents", "fa-folder-open-o", 0, loc.type === "all", function () { udOpen({ type: "all" }); },
        { count: udAllRows.length });

    // ---- My Folders: the personal folders, rendered by folder-tree.js under this row ----
    treeRow("My Folders", "fa-star-o", 0, loc.type === "mine", function () { udOpen({ type: "mine" }); }, {
        section: true, expanded: udTreeExpanded.mine,
        onToggle: function () { udTreeExpanded.mine = !udTreeExpanded.mine; renderFolderTree(); }
    });

    // the tree re-renders after a folder is created, renamed or deleted: keep the address
    // bar and the folder list in step with it
    udRenderHeader();
    udRenderFolderList();
    return udTreeExpanded.mine ? 1 : -1;
}

// Called by folder-tree.js dcdSelectFolder (tree rows, folder delete): null = All Documents.
function dcdApplyFolderFilter(folderId) {
    udOpen(folderId === null ? { type: "all" } : { type: "folder", id: folderId });
}

// ---------- "Move to Folder" (row menu). Moves one document - unlike Document
// Control Dashboard's bulk version. ----------
function dcdMoveDocumentToFolder(documentTransactionId, $anchor) {
    dcdOpenFolderPicker($anchor, function (folderId) {
        $.ajax({
            type: "POST",
            url: userDashboardConfig.urls.moveDocumentsToFolder,
            traditional: true,
            data: { documentTransactionIds: [documentTransactionId], folderId: folderId },
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
    // folder-tree.js shows the new-folder input first; make sure My Folders is open to show it
    $("#btn_addRootFolder").on("click", function () {
        if (!udTreeExpanded.mine) { udTreeExpanded.mine = true; renderFolderTree(); }
    });

    var gridStore = new DevExpress.data.CustomStore({
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            // Opening a location re-filters the documents already loaded; Refresh fetches again.
            if (udUseCache) {
                udUseCache = false;
                return d.resolve(udRowsIn(udLocation)).promise();
            }

            $.ajax({
                type: "POST",
                url: userDashboardConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                updateHeaderStats(rows);
                udAllRows = rows;
                renderFolderTree(); // counts in the tree and the folder list follow the new data
                d.resolve(udRowsIn(udLocation));
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
        // no checkbox column: Accept is a button in the Acceptance column, the rest is in the
        // row menu; a click only highlights the row
        selection: { mode: "single" },
        onContentReady: function (e) {
            var count = e.component.totalCount();
            $("#dcdFolderSubtitle").text(count >= 0 ? (count + " document" + (count !== 1 ? "s" : "")) : "");
        },
        // double-click a document opens it, like a file in File Explorer
        onRowDblClick: function (e) {
            if (e.rowType === "data" && e.data.STATUS != 0) {
                onViewAttachment(e.data.FILE_PATH, e.data.DOCUMENT_TRANSACTION_ID);
            }
        },
        columns: [
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
                    // file-type icon like File Explorer
                    $("<i>").addClass("fa dms-file-icon " + dmsFileIconClass(options.data.FILE_PATH)).appendTo(container);
                    $("<span>").text(options.value || "").appendTo(container);
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
                        .addClass("dms-pill " + udCategoryPillClass(row.CATEGORY_CODE))
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

                    $("<span>").addClass("dms-pill " + pillClass).html('<i class="fa ' + icon + '"></i> ' + (row.DOC_STATUS_VAL || "-")).appendTo(container);
                }
            },
            {
                dataField: "PUBLISH_FLAG",
                caption: "Acceptance",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var acknowledged = row.PUBLISH_FLAG == 1;

                    // waiting for this user: the Accept button itself, right on the document
                    if (userDashboardConfig.canAcknowledge && canAcknowledge(row)) {
                        $("<button>").attr({ type: "button", title: "Confirm that your department has received this document" })
                            .addClass("ud-accept-btn")
                            .html('<i class="fa fa-check"></i> Accept')
                            .on("click", function (e) {
                                e.stopPropagation();
                                confirmAcknowledge(row);
                            })
                            .on("dblclick", function (e) { e.stopPropagation(); })
                            .appendTo(container);
                        return;
                    }

                    $("<span>")
                        .css({ color: acknowledged ? "#1E9E63" : "#C62828", fontWeight: 600 })
                        .html('<i class="fa ' + (acknowledged ? "fa-check-circle" : "fa-hourglass-half") + '"></i> ' + (acknowledged ? "Accepted" : "Waiting"))
                        .appendTo(container);

                    if (acknowledged && row.ACKNOWLEDGE_DT) {
                        $("<span>").addClass("ud-ack-dt").text(moment(row.ACKNOWLEDGE_DT).format("DD MMM YYYY")).appendTo(container);
                    }
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Date",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD MMM YYYY") : "");
                }
            },
            {
                dataField: "NEXT_REVIEW_DATE",
                caption: "Next Review",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.NEXT_REVIEW_DATE;
                    if (value == null) { container.text(""); return; }

                    var reviewDate = moment(value).startOf("day");
                    var overdue = reviewDate.isBefore(moment().startOf("day"));

                    $("<span>")
                        .css({ color: overdue ? "#C62828" : "#B36B00", fontWeight: 600 })
                        .text(reviewDate.format("DD MMM YYYY"))
                        .appendTo(container);
                }
            },
            {
                dataField: "DEPARTMENT_CODE",
                caption: "Department",
                calculateDisplayValue: function (row) {
                    return row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME;
                }
            },
            {
                caption: "Division",
                visible: false,
                calculateCellValue: function (row) {
                    return row.DIVISION + " - " + row.DIVISION_NAME;
                }
            },
            { dataField: "CLASSIFIED_VAL", caption: "Classified", visible: false },
            {
                dataField: "STATUS_VAL",
                caption: "Registration Status",
                visible: false,
                calculateCellValue: function (row) {
                    return row.STATUS == 0 ? "Requested" : row.STATUS_VAL;
                }
            },
            { dataField: "FOLDER_ID", dataType: "number", caption: "Folder", visible: false, allowHeaderFiltering: false },
            { dataField: "FOLDER_NAME", caption: "Folder Name", visible: false },
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

                    if (row.STATUS == 0) {
                        return;
                    }

                    container.css("white-space", "nowrap");

                    // Tombol mata (View) dihapus - dipindah ke dalam kebab menu supaya
                    // tidak duplikat (request Hendra 2026-08-15).
                    $("<button>")
                        .attr({ type: "button", title: "More actions" })
                        .addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) {
                            e.stopPropagation();
                            openRowMenu($(this), row, options.component);
                        })
                        .appendTo(container);
                }
            }
        ]
    }));

    // ---------- per-row "more actions" kebab menu ----------
    var $udRowMenu = null;

    function buildRowMenu() {
        if ($udRowMenu) return $udRowMenu;

        $udRowMenu = $('<ul class="dropdown-menu dp-dropdown-menu" role="menu"></ul>')
            .css({ position: "absolute", display: "none", "z-index": 2000, "min-width": "180px" })
            .appendTo("body");

        $(document).on("click", function () {
            $udRowMenu.hide();
        });

        return $udRowMenu;
    }

    function openRowMenu($btn, row, gridInstance) {
        var $menu = buildRowMenu();

        // Accept is not here: it is the button in the Acceptance column (Hendra 2026-11-14)
        $menu.empty();
        $menu.append(
            $('<li>').append(
                $('<button type="button" class="dp-menu-preview">')
                    .html('<i class="fa fa-eye"></i> View')
                    .on("click", function () {
                        $udRowMenu.hide();
                        onViewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
                    })
            )
        );
        $menu.append(
            $('<li>').append(
                $('<button type="button" class="dp-menu-download">')
                    .html('<i class="fa fa-download"></i> Download')
                    .on("click", function () {
                        $udRowMenu.hide();
                        onDownloadAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
                    })
            )
        );

        $menu.append('<li class="divider"></li>');
        $menu.append(
            $('<li>').append(
                $('<button type="button">')
                    .html('<i class="fa fa-folder-o"></i> Move to Folder')
                    .on("click", function () {
                        $udRowMenu.hide();
                        dcdMoveDocumentToFolder(row.DOCUMENT_TRANSACTION_ID, $btn);
                    })
            )
        );

        // use getBoundingClientRect (+ scroll offset) rather than jQuery's .offset() -
        // DevExtreme's fixed/frozen columns are transform-positioned, which throws off
        // offset()'s offsetParent-chain math but not the actual rendered rect.
        var rect = $btn[0].getBoundingClientRect();
        var top = rect.bottom + window.scrollY + 4;
        var left = rect.right + window.scrollX - $menu.outerWidth();
        $menu.css({ top: top, left: left }).show();
    }

    bindGridToolbar("columns filters reset refresh resize");

    // ---------- toggle the Folders sidebar (collapses to the left, grid
    // reflows into the freed width) ----------
    $("#btn_toggleFolders").on("click", function () {
        setFoldersPanelCollapsed(!$("#dpLayoutRow").hasClass("folders-collapsed"));
    });

    // ---------- toggle the docked "Document Status" panel (collapses to
    // the right, grid reflows into the freed width via flex) ----------
    $("#btn_toggleStats").on("click", function () {
        setStatsPanelCollapsed(!$("#dpLayoutRow").hasClass("stats-collapsed"));
    });

    $("#btn_closeStatsPanel").on("click", function () {
        setStatsPanelCollapsed(true);
    });

    $("#btn_export").on("click", function () {
        window.location.href = userDashboardConfig.urls.downloadExcel;
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

//RECOMMIT DINA
var gScreenMode = null;
var gId = null;
var gDocumentId = null;
var gDocumentTransactionId = null;
var gDocumentCtrlId = null;
var gDepartmentId = null;
var gDocumentNo = null;
var gIsApproved = null;
var gFilePath = null;

function maxLengthCheck(object) {
    if (object.value.length > object.maxLength)
        object.value = object.value.slice(0, object.maxLength)
}

function onclickDownload(element) {
    //alert(element.getAttribute('data-doc'));

    if (element.getAttribute('data-doc') !== "null" || element.getAttribute('data-doc') !== null || element.getAttribute('data-doc') !== "") {
        //var data = new FormData();
        //data.append("FILE_PATH", element.getAttribute('data-doc'));

        var link = userDashboardConfig.urls.downloadDocument;
        link = link.replace("-1", element.getAttribute('data-doc'));
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an uploaded document");
    }
    //data: {
    //    DOCUMENT_NO: $("#addEditDocumentNo").val()
    //},
}

function onDownloadAttachment(attachment, documentId) {
    gAttachmentPath = attachment;
    gDocumentId = documentId;
    downloadAttachment();
}

function downloadAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = userDashboardConfig.urls.downloadAttachment;
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

function viewAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = userDashboardConfig.urls.viewAttachment;
        link = link.replace("-1", gAttachmentPath);
        link = link.replace("-2", gDocumentId);
        link = link.replace("-3", "3");
        window.open(link, '_blank');
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

// The row comes from the Accept button in its Acceptance cell (the grid has no key, so it is
// passed along instead of being selected).
var udAcceptRow = null;

function confirmAcknowledge(row) {
    udAcceptRow = row;
    showConfirmMesg("Are you sure you want to accept " + confirmText(row.DOCUMENT_CODE) + "?", insertLog, "Confirmation");
}

function insertLog() {
    popUpProgressShow();

    var row = udAcceptRow;
    if (row == null) {
        popUpProgressHide();
        return;
    }

    gDocumentTransactionId = row.DOCUMENT_TRANSACTION_ID;
    gDocumentCtrlId = row.DOCUMENT_CTRL_ID;
    gDepartmentId = row.DEPARTMENT_ID;

    $.ajax({
        type: "POST",
        url: userDashboardConfig.urls.acknowledgeDocument,
        dataType: 'json',
        traditional: true,
        data: {
            DOCUMENT_TRANSACTION_ID: gDocumentTransactionId,
            DOCUMENT_CTRL_ID: gDocumentCtrlId,
            DEPARTMENT_ID: gDepartmentId,
        },
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
