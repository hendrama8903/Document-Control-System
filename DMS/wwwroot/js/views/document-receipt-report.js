// DocumentReceiptReport. Server values come from documentReceiptReportConfig in the view.
// 2026-11-19: columns follow form QCD/FR-QMS-00/006 (Tanda Terima Dokumen) and the
// Print (PDF) / Export (Excel) use the same form layout as the Master List.

// The running number at the end of the document code: QCD/SOP-PQA-00/002 -> 2
function drSeqNumber(code) {
    var m = /(\d+)$/.exec(code || "");
    return m ? parseInt(m[1], 10) : 999999999;
}

// Rows to print: the ticked ones, otherwise every row the current filters show
function drRowsToPrint() {
    var grid = $("#datagrid").dxDataGrid("instance");
    var selected = grid.getSelectedRowsData();
    if (selected.length > 0) return $.Deferred().resolve(selected).promise();
    var source = new DevExpress.data.DataSource({
        store: grid.getDataSource().store(),
        filter: grid.getCombinedFilter(true),
        paginate: false
    });
    return source.load();
}

// POST the ids in a hidden form: a long selection does not fit in a URL
function drSubmit(url, rows, newTab) {
    var $form = $("<form>").attr({ method: "POST", action: url, target: newTab ? "_blank" : "_self" }).hide();
    rows.forEach(function (row) {
        $("<input>").attr({ type: "hidden", name: "publishHistoryIds" }).val(row.PUBLISH_HISTORY_ID).appendTo($form);
    });
    $form.appendTo("body").trigger("submit").remove();
}

function drPrint(url, newTab) {
    drRowsToPrint().done(function (rows) {
        if (!rows || rows.length === 0) {
            DevExpress.ui.notify("No receipt to print for the current filter.", "warning", 2500);
            return;
        }
        drSubmit(url, rows, newTab);
    });
}

function updateActionButtons() {
    var n = getSelectedRowsData().length;
    $("#btn_print").attr("title", n > 0 ? "Print the " + n + " selected receipt(s)" : "Print every receipt in the list");
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
        filterRow: { visible: false, applyFilter: "auto" },
        // default order = the printed form: per receiving department, smallest document number first
        sorting: { mode: "multiple" },
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
                allowSorting: false,
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
                caption: "No. Dokumen",
                fixed: true,
                fixedPosition: "left",
                sortIndex: 1,
                sortOrder: "asc",
                calculateSortValue: function (row) {
                    return ("000000000" + drSeqNumber(row.DOCUMENT_CODE)).slice(-9) + "|" + (row.DOCUMENT_CODE || "");
                }
            },
            { dataField: "DOCUMENT_NAME", caption: "Nama Dokumen" },
            { dataField: "REVISION", caption: "No. Revisi", alignment: "center", width: 90 },
            {
                caption: "Diterima Oleh",
                columns: [
                    {
                        dataField: "RECEIVED_DATE",
                        caption: "Tanggal",
                        dataType: "date",
                        alignment: "center",
                        format: "dd-MMM-yy"
                    },
                    {
                        caption: "Div. / Dept.",
                        name: "department",
                        sortIndex: 0,
                        sortOrder: "asc",
                        calculateCellValue: function (row) {
                            return [row.DIVISION, row.DEPARTMENT_CODE].filter(Boolean).join(" / ");
                        },
                        cellTemplate: function (container, options) {
                            var row = options.data;
                            container.text(options.value || "").attr("title", [row.DIVISION_NAME, row.DEPARTMENT_NAME].filter(Boolean).join(" - "));
                        }
                    },
                    { dataField: "RECEIVED_BY_NAME", caption: "Nama" }
                ]
            },
            {
                caption: "Penarikan Dokumen Lama",
                columns: [
                    {
                        caption: "Ya",
                        width: 60,
                        alignment: "center",
                        allowSorting: false,
                        calculateCellValue: function (row) { return (row.REVISION || 0) > 0 ? "✓" : ""; }
                    },
                    {
                        caption: "Tidak",
                        width: 60,
                        alignment: "center",
                        allowSorting: false,
                        calculateCellValue: function (row) { return (row.REVISION || 0) > 0 ? "" : "✓"; }
                    }
                ]
            }
        ]
    }));

    bindGridToolbar("columns filters reset refresh resize");
    updateActionButtons();

    $("#btn_print").on("click", function () {
        drPrint(documentReceiptReportConfig.urls.printReceipt, true);
    });

    $("#btn_export").on("click", function () {
        drPrint(documentReceiptReportConfig.urls.downloadExcel, false);
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});
