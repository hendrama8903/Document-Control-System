// DocumentHistoryPopup. Server values come from documentHistoryPopupConfig in the view.
var gHistoryDocumentCode = null;

$(document).ready(function () {
    $("#btn_history").on("click", function () {
        onHistoryPrepare();
    });

    var historyGridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
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
                d.resolve(result.data == "" ? [] : result.data);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagridHistory").dxDataGrid({
        dataSource: historyGridStore,
        showBorders: true,
        columnAutoWidth: true,
        allowColumnResizing: true,
        columnResizingMode: "widget",
        wordWrapEnabled: false,
        sorting: { mode: "none" },
        noDataText: "No previous revision for this document.",
        height: 400,
        filterRow: { visible: true, applyFilter: "auto" },
        headerFilter: { visible: true },
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
            { dataField: "DOCUMENT_CODE", caption: "Document No" },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name" },
            { dataField: "REVISION", caption: "Revision", alignment: "center", width: 80 },
            {
                dataField: "STATUS_DISPLAY",
                caption: "Status",
                alignment: "center",
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
                }
            },
            {
                dataField: "DOCUMENT_DATE",
                caption: "Document Date",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "CREATED_BY", caption: "Created By" },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "center",
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
                        .attr({ type: "button", title: hasFile ? "View" : "No file recorded for this revision" })
                        .addClass("btn btn-sm adm-act-btn")
                        .addClass(hasFile ? "btn-primary" : "btn-default disabled")
                        .html('<i class="fa fa-file-pdf-o"></i>')
                        .appendTo(container);

                    if (hasFile) {
                        btn.on("click", function () {
                            onViewAttachment(historyRow.FILE_PATH, historyRow.DOCUMENT_TRANSACTION_ID);
                        });
                    }
                }
            }
        ]
    });
});

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
