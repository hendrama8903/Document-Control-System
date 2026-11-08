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
            { dataField: "DOCUMENT_CODE", caption: "Document Code", fixed: true, fixedPosition: "left" },
            { dataField: "DOCUMENT_TRANSACTION_NAME", caption: "Document Name" },
            { dataField: "DOCUMENT_CATEGORY", caption: "Category" },
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
            { dataField: "REVISION", caption: "Revision", alignment: "center", width: 80 },
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
                caption: "Effective Date",
                alignment: "center",
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
                        .attr({ type: "button", title: "View" })
                        .addClass("btn btn-primary btn-sm adm-act-btn")
                        .html('<i class="fa fa-file-pdf-o"></i>')
                        .on("click", function () {
                            viewAttachment(row.FILE_PATH, row.DOCUMENT_TRANSACTION_ID);
                        })
                        .appendTo(container);
                }
            }
        ]
    }));

    bindGridToolbar("columns resize");

    $("#btn_export").on("click", function () {
        window.location.href = documentMasterlistConfig.urls.downloadExcel;
    });

});
