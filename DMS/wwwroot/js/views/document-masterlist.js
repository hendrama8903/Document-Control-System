// DocumentMasterlist. Server values come from documentMasterlistConfig in the view.
function viewAttachment(filePath, documentTransactionId) {
    if (filePath == null) {
        showErrorMesgGrowl("This data does not have an attachment");
        return;
    }

    var link = documentMasterlistConfig.urls.viewAttachment;
    link = link.replace("-1", filePath);
    link = link.replace("-2", documentTransactionId);
    link = link.replace("-3", "1");
    window.open(link, '_blank');
}

// ---------- summary panel (2026-11-16) ----------
function mlRenderStats(rows) {
    var count = function (status) { return rows.filter(function (r) { return r.STATUS == status; }).length; };
    $("#mlStatPublished").text(count(5));
    $("#mlStatApproved").text(count(1));
    $("#mlStatValid").text(count(5) + count(1));
    $("#mlStatObsolete").text(count(4));
}

var mlCategoryPills = ["dms-pill-info", "dms-pill-success", "dms-pill-warning", "dms-pill-primary", "dms-pill-default"];
function mlCategoryPillClass(code) {
    var sum = 0;
    for (var i = 0; i < (code || "").length; i++) sum += code.charCodeAt(i);
    return mlCategoryPills[sum % mlCategoryPills.length];
}

// Default view: valid documents only (Approved, Published). Reset brings it back.
var ML_DEFAULT_STATUS = [1, 5];

$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentMasterlistConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var rows = result.data == "" ? [] : result.data;
                mlRenderStats(rows);
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
                caption: "Document Code",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    $("<i>").addClass("fa dms-file-icon " + dmsFileIconClass(row.FILE_PATH)).appendTo(container);
                    $("<span>").addClass("ml-doc-link").text(row.DOCUMENT_CODE || "")
                        .on("click", function () { viewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID); })
                        .appendTo(container);
                }
            },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name" },
            {
                dataField: "DOCUMENT_CATEGORY",
                caption: "Category",
                alignment: "center",
                cellTemplate: function (container, options) {
                    if (!options.value) return;
                    $("<span>").addClass("dms-pill " + mlCategoryPillClass(options.value)).text(options.value).appendTo(container);
                }
            },
            {
                caption: "Division",
                calculateCellValue: function (row) {
                    return row.DIVISION != null ? row.DIVISION + " - " + row.DIVISION_NAME : "";
                }
            },
            {
                caption: "Department",
                calculateCellValue: function (row) {
                    return row.DEPARTMENT_CODE != null ? row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME : "";
                }
            },
            { dataField: "CLASSIFIED_VAL", caption: "Classified" },
            {
                dataField: "REVISION",
                caption: "Revision",
                alignment: "center",
                width: 90,
                cellTemplate: function (container, options) {
                    $("<span>").addClass("dms-pill dms-pill-info").text("Rev. " + (options.value != null ? options.value : 0)).appendTo(container);
                }
            },
            {
                dataField: "STATUS",
                caption: "Status",
                alignment: "center",
                // Default view = "current, valid" documents: Approved and Published (Effective).
                // Filter row renders as a multi-select (anyof) so users can still narrow/widen it.
                filterValues: [1, 5],
                selectedFilterOperation: "anyof",
                lookup: {
                    dataSource: [
                        { value: 0, text: "Waiting Approval" },
                        { value: 1, text: "Approved" },
                        { value: 2, text: "Rejected" },
                        { value: 4, text: "Obsolete" },
                        { value: 5, text: "Published (Effective)" }
                    ],
                    valueExpr: "value",
                    displayExpr: "text"
                },
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = "dms-pill-danger";

                    if (row.STATUS == 0) {
                        pillClass = "dms-pill-info";
                    } else if (row.STATUS == 1) {
                        pillClass = "dms-pill-success";
                    } else if (row.STATUS == 4) {
                        pillClass = "dms-pill-default";
                    } else if (row.STATUS == 5) {
                        pillClass = "dms-pill-primary";
                    }

                    $("<span>").addClass("dms-pill " + pillClass).append($("<span>").addClass("dms-dot")).append(document.createTextNode(row.STATUS_DISPLAY || "")).appendTo(container);
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Effective Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                caption: "Last Change",
                calculateCellValue: function (row) {
                    return [row.ITEM_CHANGED, row.REASON].filter(Boolean).join(" - ");
                }
            },
            { dataField: "DOCUMENT_CREATOR", caption: "Owner" },
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

                    if (row.FILE_PATH == null) {
                        return;
                    }

                    $("<button>")
                        .attr({ type: "button", title: "More actions" })
                        .addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) {
                            e.stopPropagation();
                            var $menu = buildRowActionMenu();
                            $menu.empty().append(menuItem("dp-menu-preview", "fa-eye", "View", function () {
                                viewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
                            }));
                            var offset = $(this).offset();
                            $menu.css({ top: offset.top + $(this).outerHeight() + 4, left: offset.left - $menu.outerWidth() + $(this).outerWidth() }).show();
                        })
                        .appendTo(container);
                }
            }
        ]
    }));

    bindGridToolbar("columns filters refresh resize stats");

    // Reset clears the column filters but keeps the default view (valid documents)
    $("#btn_resetFilters").on("click", function () {
        var grid = $("#datagrid").dxDataGrid("instance");
        grid.beginUpdate();
        grid.clearFilter();
        grid.clearSelection();
        grid.columnOption("STATUS", "filterValues", ML_DEFAULT_STATUS);
        grid.endUpdate();
    });

    // Reset clears every filter; bring back the default "current, valid" status filter
    $("#btn_resetFilters").on("click", function () {
        $("#datagrid").dxDataGrid("instance").columnOption("STATUS", "filterValues", [1, 5]);
    });

    $("#btn_export").on("click", function () {
        window.location.href = documentMasterlistConfig.urls.downloadExcel;
    });

});
