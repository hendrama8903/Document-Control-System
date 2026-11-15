// DocumentReport. Server values come from documentReportConfig in the view.
$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        key: "DOCUMENT_TRANSACTION_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: documentReportConfig.urls.search,
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
            {
                dataField: "DOCUMENT_DATE",
                caption: "Document Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.DOCUMENT_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                dataField: "NEXT_REVIEW_DATE",
                caption: "Next Review Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.NEXT_REVIEW_DATE;

                    if (value == null) {
                        container.text("");
                        return;
                    }

                    var reviewDate = moment(value).startOf("day");
                    var today = moment().startOf("day");
                    var daysLeft = reviewDate.diff(today, "days");
                    var pillClass = "dms-pill-default";

                    if (daysLeft < 0) {
                        pillClass = "dms-pill-danger";
                    } else if (daysLeft <= 30) {
                        pillClass = "dms-pill-warning";
                    }

                    $("<span>").addClass("dms-pill " + pillClass).text(reviewDate.format("DD-MM-YYYY")).appendTo(container);
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
            { dataField: "REVISION", caption: "Revision", alignment: "center" },
            {
                dataField: "PUBLISH_FLAG",
                caption: "Published",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var pillClass = row.PUBLISH_FLAG == 1 ? "dms-pill-success" : "dms-pill-warning";
                    var text = row.PUBLISH_FLAG == 1 ? "Yes" : "No";

                    $("<span>").addClass("dms-pill " + pillClass).append($("<span>").addClass("dms-dot")).append(document.createTextNode(text)).appendTo(container);
                }
            },
            { dataField: "DOCUMENT_CREATOR", caption: "Creator" },
            {
                dataField: "DISTRIBUTION_DATE",
                caption: "Distribution Date",
                alignment: "right",
                cellTemplate: function (container, options) {
                    var value = options.data.DISTRIBUTION_DATE;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            }
        ]
    }));

    bindGridToolbar("columns filters reset refresh resize");

    $("#btn_export").on("click", function () {
        window.location.href = documentReportConfig.urls.downloadExcel;
    });

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});
