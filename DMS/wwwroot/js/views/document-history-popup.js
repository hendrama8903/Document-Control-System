// DocumentHistoryPopup. Server values come from documentHistoryPopupConfig in the view.
var gHistoryDocumentCode = null;

$(document).ready(function () {
    $("#btn_history").on("click", function () {
        onHistoryPrepare();
    });

    // No key: revisions brought in by Legacy Import have no DOCUMENT_TRANSACTION_ID.
    var historyGridStore = new DevExpress.data.CustomStore({
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            if (!gHistoryDocumentCode) {
                d.resolve([]);
                return d.promise();
            }

            $.ajax({
                type: "POST",
                url: documentHistoryPopupConfig.urls.searchHistory,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false,
                    DOCUMENT_CODE: gHistoryDocumentCode
                }
            }).done(function (result) {
                if (!result || typeof result === "string") {
                    console.error("Document history:", result);
                    d.resolve([]);
                    return;
                }
                d.resolve((result.data || []).filter(function (r) { return r != null; }));
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagridHistory").dxDataGrid({
        dataSource: historyGridStore,
        showBorders: true,
        // Fixed column widths so everything fits the popup without a horizontal scroll.
        // Document No is already in the popup title, so it is not repeated per row.
        columnAutoWidth: false,
        allowColumnResizing: true,
        columnResizingMode: "widget",
        wordWrapEnabled: true,
        sorting: { mode: "none" },
        noDataText: "No previous revision for this document.",
        height: 400,
        paging: { pageSize: 10 },
        pager: {
            visible: true,
            displayMode: "compact",
            showPageSizeSelector: true,
            allowedPageSizes: [10, 25, 50],
            showInfo: true,
            showNavigationButtons: true
        },
        columns: [
            {
                caption: "No.",
                alignment: "center",
                width: 50,
                cellTemplate: function (container, options) {
                    var grid = options.component;
                    container.text(grid.pageIndex() * grid.pageSize() + options.rowIndex + 1);
                }
            },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name", minWidth: 180 },
            { dataField: "REVISION", caption: "Revision", alignment: "center", width: 80 },
            {
                dataField: "STATUS_DISPLAY",
                caption: "Status",
                alignment: "center",
                width: 170,
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var labelClass = "label label-danger";

                    if (row.STATUS == 0) {
                        labelClass = "label label-info";
                    } else if (row.STATUS == 1) {
                        labelClass = "label label-success";
                    } else if (row.STATUS == 4) {
                        labelClass = "label label-default";
                    } else if (row.STATUS == 5) {
                        labelClass = "label label-primary";
                    }

                    $("<span>").addClass(labelClass).text(row.STATUS_DISPLAY).appendTo(container);

                    // Revisions brought in by Legacy Import from the QMS master list: only the
                    // revision date was recorded there, the file itself is usually gone.
                    if (isMigrationRecordWithoutFile(row)) {
                        $("<div>").addClass("dh-migration-note")
                            .attr("title", "Recorded from the master list during migration. The master list holds only the date of this revision, not its file.")
                            .html('<i class="fa fa-info-circle"></i> From master list, no file')
                            .appendTo(container);
                    }
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Document Date",
                alignment: "center",
                width: 115,
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "CREATED_BY", caption: "Created By", width: 110 },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "center",
                width: 140,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value ? moment(value).format("DD-MM-YYYY HH:mm") : "");
                }
            },
            {
                caption: "Action",
                alignment: "center",
                width: 70,
                allowFiltering: false,
                cellTemplate: function (container, options) {
                    var historyRow = options.data;
                    var hasFile = !!historyRow.FILE_PATH;

                    var btn = $("<button>")
                        .attr({
                            type: "button",
                            title: hasFile ? "View"
                                : isMigrationRecordWithoutFile(historyRow) ? "No file: this revision was recorded from the master list during migration"
                                : "No file recorded for this revision"
                        })
                        .addClass("btn btn-sm adm-act-btn")
                        .addClass(hasFile ? "btn-primary" : "btn-default disabled")
                        .html('<i class="fa fa-file-pdf-o"></i>')
                        .appendTo(container);

                    if (hasFile) {
                        btn.on("click", function () {
                            onViewAttachment(historyRow.FILE_PATH, historyRow.DOCUMENT_TRANSACTION_ID, historyRow);
                        });
                    }
                }
            }
        ]
    });
});

function isMigrationRecordWithoutFile(row) {
    return row.SOURCE === "MIGRATION" && !row.FILE_PATH;
}

function onHistoryPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gHistoryDocumentCode = row.DOCUMENT_CODE;
    $("#modal-title-history").text("Document History - " + row.DOCUMENT_CODE);
    $("#datagridHistory").dxDataGrid("instance").refresh();

    $("#modalDocumentHistory").modal();
}
