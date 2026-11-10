// UserDashboard. Server values come from userDashboardConfig in the view.
function canAcknowledge(row) {
    return row != null && row.ACK_ELIGIBLE == 1 && (row.STATUS == 2 || row.STATUS == 5) && row.PUBLISH_FLAG != 1;
}

function updateActionButtons() {
    var row = getSelectedRowData();

    $("#btn_acknowledge").prop("disabled", !canAcknowledge(row));
}

function computeGridHeight() {
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

// ---------- folder breadcrumb/title + grid filter (ported from Document
// Control Dashboard, request Hendra 2026-08-29) ----------
// Called from the shared _FolderTree.cshtml's dcdSelectFolder(). The grid
// already loads every document once (loadMode:"raw"/length:100000) - folder
// selection filters that same loaded set client-side, no extra round-trip.
function dcdApplyFolderFilter(folderId) {
    var grid = $("#datagrid").dxDataGrid("instance");
    grid.filter(folderId === null ? null : ["FOLDER_ID", "=", folderId]);

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

// ---------- "Move to Folder" (toolbar button + per-row kebab). Selection on
// this grid is capped at one row (see onSelectionChanged below), so this only
// ever moves a single document - unlike Document Control Dashboard's bulk
// version. No explicit grid `key` is configured here (pre-existing), so this
// reads DOCUMENT_TRANSACTION_ID off the row data directly rather than via
// getSelectedRowKeys(). ----------
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
    $("#btn_acknowledge").prop("disabled", true);

    $("#btn_moveToFolder").on("click", function () {
        var row = getSelectedRowData();
        if (row == null) { return; }
        dcdMoveDocumentToFolder(row.DOCUMENT_TRANSACTION_ID, $(this));
    });

    var gridStore = new DevExpress.data.CustomStore({
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

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
            $("#btn_moveToFolder").toggle(e.selectedRowKeys.length > 0);
        },
        onContentReady: function (e) {
            var count = e.component.totalCount();
            $("#dcdFolderSubtitle").text(count >= 0 ? (count + " document" + (count !== 1 ? "s" : "")) : "");
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
        var eligible = canAcknowledge(row);

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

        if (userDashboardConfig.canAcknowledge) {
            $menu.append('<li class="divider"></li>');
            $menu.append(
                $('<li>').append(
                    $('<button type="button" class="dp-menu-approve">')
                        .prop("disabled", !eligible)
                        .html('<i class="fa fa-share-square-o"></i> Accepted')
                        .on("click", function () {
                            if (!eligible) return;
                            gridInstance.selectRows([row.DOCUMENT_CTRL_ID], false);
                            $udRowMenu.hide();
                            confirmAcknowledge();
                        })
                )
            );
        }

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

$(document).ready(function () {
    $("#btn_acknowledge").on("click", function () {
        confirmAcknowledge();
    });
});

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

function confirmAcknowledge() {
    showConfirmMesg("Are you sure you want to accept this document?", insertLog, "Confirmation");
}

function insertLog() {
    popUpProgressShow();

    var row = getSelectedRowData();
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
