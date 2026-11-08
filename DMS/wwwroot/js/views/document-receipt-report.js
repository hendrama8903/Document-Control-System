// DocumentReceiptReport. Server values come from documentReceiptReportConfig in the view.
function updateActionButtons() {
    $("#btn_print").prop("disabled", getSelectedRowsData().length < 1);
}

$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        key: "PUBLISH_HISTORY_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentReceiptReportConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                d.resolve(result.data == "" ? [] : result.data);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        filterRow: { visible: true, applyFilter: "auto" },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: true
        },
        onSelectionChanged: function () {
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
            { dataField: "DOCUMENT_NAME", caption: "Document Name" },
            { dataField: "REVISION", caption: "Revision", alignment: "center", width: 90 },
            {
                caption: "Department",
                calculateCellValue: function (row) {
                    return row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME;
                }
            },
            {
                dataField: "RECEIVED_DATE",
                caption: "Received Date",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.RECEIVED_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "RECEIVED_BY_NAME", caption: "Received By" }
        ]
    }));

    bindGridToolbar("columns resize");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

$(document).ready(function () {
    $("#btn_print").on("click", function () {
        onPrintPrepare();
    });
});

function onPrintPrepare() {
    var rows = getSelectedRowsData();
    if (rows.length < 1) {
        return;
    }

    var query = rows.map(function (row) {
        return "publishHistoryIds=" + encodeURIComponent(row.PUBLISH_HISTORY_ID);
    }).join("&");

    window.open(documentReceiptReportConfig.urls.printReceipt + "?" + query, "_blank");
}
