// LogMonitoring. Server values come from logMonitoringConfig in the view.
// status codes per sp_StartLog/sp_WriteLog: 0=Start, 1=Process, 2=Finish, 3=Finish with error, 4=Abnormal
function alStatusCategory(processStatus) {
    var s = String(processStatus);
    if (s == "2") { return "SUCCESS"; }
    if (s == "3" || s == "4") { return "FAILED"; }
    return "RUNNING";
}

function alStatusLabel(category) {
    if (category == "SUCCESS") { return "Success"; }
    if (category == "FAILED") { return "Failed"; }
    return "Running";
}

function alStatusDotClass(category) {
    if (category == "SUCCESS") { return "success"; }
    if (category == "FAILED") { return "failed"; }
    return "running";
}

// FUNCTION is the real, factual field from the log - Action is a short, derived
// label for readability only (not a separate field in the database).
function alDeriveAction(func) {
    var f = (func || "").toLowerCase();
    if (f.indexOf("delete") > -1) { return { label: "Delete", color: "red" }; }
    if (f.indexOf("reject") > -1) { return { label: "Reject", color: "red" }; }
    if (f.indexOf("approve") > -1) { return { label: "Approve", color: "green" }; }
    if (f.indexOf("validate") > -1) { return { label: "Login", color: "blue" }; }
    if (f.indexOf("togglestatus") > -1 || f.indexOf("update") > -1) { return { label: "Update", color: "amber" }; }
    if (f.indexOf("insert") > -1) { return { label: "Create", color: "green" }; }
    if (f.indexOf("send") > -1) { return { label: "Send", color: "purple" }; }
    return { label: func || "-", color: "gray" };
}

function alFormatDuration(startDt, endDt) {
    if (!startDt || !endDt) { return ""; }
    var ms = moment(endDt).diff(moment(startDt));
    if (ms < 1000) { return ms + " ms"; }
    return (ms / 1000).toFixed(2) + " s";
}

$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: logMonitoringConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    START_DT: gStartDate,
                    END_DT: gEndDate
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                alOnDataLoaded(data);
                d.resolve(alApplyClientFilters(data));
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid({
        dataSource: gridStore,
        showBorders: true,
        columnAutoWidth: true,
        allowColumnResizing: true,
        columnResizingMode: "widget",
        wordWrapEnabled: false,
        sorting: { mode: "none" },
        noDataText: "Data not found.",
        height: computeGridHeight(),
        filterRow: { visible: false },
        headerFilter: { visible: false },
        toolbar: { visible: false },
        paging: { pageSize: 10 },
        pager: {
            visible: true,
            displayMode: "compact",
            showPageSizeSelector: true,
            allowedPageSizes: [10, 25, 50, 100],
            showInfo: true,
            showNavigationButtons: true
        },
        columnChooser: {
            enabled: true,
            mode: "select"
        },
        columnFixing: { enabled: true },
        columns: [
            {
                caption: "No",
                width: 50,
                alignment: "center",
                allowHiding: false,
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var pageIndex = options.component.pageIndex();
                    var pageSize = options.component.pageSize();
                    container.text(pageIndex * pageSize + options.row.rowIndex + 1);
                }
            },
            {
                dataField: "PROCESS_ID",
                caption: "Process ID",
                alignment: "center",
                width: 100
            },
            {
                dataField: "START_DT",
                caption: "Time",
                alignment: "center",
                cellTemplate: function (container, options) {
                    var value = options.data.START_DT;
                    container.text(value != null ? moment(value).format("DD MMM YYYY HH:mm:ss") : "");
                }
            },
            {
                caption: "Relative Time",
                calculateCellValue: function (row) {
                    return row.START_DT != null ? moment(row.START_DT).fromNow() : "";
                }
            },
            { dataField: "CREATED_BY", caption: "User" },
            { dataField: "MODULE", caption: "Module" },
            { dataField: "FUNCTION", caption: "Function" },
            {
                caption: "Action",
                calculateCellValue: function (row) {
                    return alDeriveAction(row.FUNCTION).label;
                },
                cellTemplate: function (container, options) {
                    var action = alDeriveAction(options.data.FUNCTION);
                    $("<span>").addClass("al-pill " + action.color).text(action.label).appendTo(container);
                }
            },
            {
                caption: "Status",
                calculateCellValue: function (row) {
                    return alStatusLabel(alStatusCategory(row.PROCESS_STATUS));
                },
                cellTemplate: function (container, options) {
                    var category = alStatusCategory(options.data.PROCESS_STATUS);
                    var $cell = $("<div>").addClass("al-status-cell");
                    $("<span>").addClass("al-status-dot " + alStatusDotClass(category)).appendTo($cell);
                    $("<span>").text(alStatusLabel(category)).appendTo($cell);
                    $cell.appendTo(container);
                }
            },
            {
                caption: "Duration",
                alignment: "right",
                calculateCellValue: function (row) {
                    return alFormatDuration(row.START_DT, row.END_DT);
                }
            },
            {
                caption: "Detail",
                alignment: "center",
                width: 70,
                allowHiding: false,
                fixed: true,
                fixedPosition: "right",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    $("<button>")
                        .attr({ type: "button", title: "Detail" })
                        .addClass("al-detail-btn")
                        .html('<i class="fa fa-eye"></i>')
                        .on("click", function () {
                            onDetail(row.PROCESS_ID);
                        })
                        .appendTo(container);
                }
            }
        ]
    });

    bindGridToolbar("columns resize");

    $("#btn_refresh").on("click", function () {
        $("#datagrid").dxDataGrid("instance").refresh();
    });

    $("#filterModule, #filterUser, #filterStatus").on("change", function () {
        $("#datagrid").dxDataGrid("instance").refresh();
    });

});

var gScreenMode = null;
var gId = null;
var gStartDate = null;
var gEndDate = null;
var gAllLogs = [];

$(document).ready(function () {
    var today = moment().format("YYYY-MM-DD");
    gStartDate = today;
    gEndDate = today;

    $('#searchDate').daterangepicker({
        startDate: moment(),
        endDate: moment(),
        autoUpdateInput: true,
        locale: { cancelLabel: 'Clear' }
    });
    $('#searchDate').val(moment().format('DD-MM-YYYY') + ' - ' + moment().format('DD-MM-YYYY'));

    $('#searchDate').on('apply.daterangepicker', function (ev, picker) {
        gStartDate = picker.startDate.format("YYYY-MM-DD");
        gEndDate = picker.endDate.format("YYYY-MM-DD");
        $("#datagrid").dxDataGrid("instance").refresh();
    });

    $('#searchDate').on('cancel.daterangepicker', function (ev, picker) {
        gStartDate = null;
        gEndDate = null;
        $(this).val('');
        $("#datagrid").dxDataGrid("instance").refresh();
    });

    $("#btn_export").on("click", function () {
        var params = $.param({
            startDt: gStartDate,
            endDt: gEndDate,
            module: $("#filterModule").val() || null,
            createdBy: $("#filterUser").val() || null
        });
        window.location.href = logMonitoringConfig.urls.downloadExcel + "?" + params;
    });

    bindGridToolbar("stats");

});

// called once per grid load, with the full (date-range-only-filtered) result set -
// drives the Module/User dropdown options and the KPI cards, both independent of
// whatever Module/User/Status the user currently has selected.
function alOnDataLoaded(data) {
    gAllLogs = data;
    alPopulateFilterOptions(data);
    alRenderStats(alApplyClientFilters(data));
}

function alPopulateFilterOptions(data) {
    var modules = Array.from(new Set(data.map(function (r) { return r.MODULE; }).filter(Boolean))).sort();
    var users = Array.from(new Set(data.map(function (r) { return r.CREATED_BY; }).filter(Boolean))).sort();

    alFillSelect($("#filterModule"), modules, "All Module");
    alFillSelect($("#filterUser"), users, "All User");
}

function alFillSelect($select, values, allLabel) {
    var current = $select.val();
    $select.empty();
    $select.append($("<option>").val("").text(allLabel));
    values.forEach(function (v) {
        $select.append($("<option>").val(v).text(v));
    });
    if (values.indexOf(current) > -1) {
        $select.val(current);
    }
}

function alApplyClientFilters(data) {
    var module = $("#filterModule").val();
    var user = $("#filterUser").val();
    var status = $("#filterStatus").val();

    return data.filter(function (row) {
        if (module && row.MODULE != module) { return false; }
        if (user && row.CREATED_BY != user) { return false; }
        if (status && alStatusCategory(row.PROCESS_STATUS) != status) { return false; }
        return true;
    });
}

function alRenderStats(filtered) {
    var total = filtered.length;
    var success = filtered.filter(function (r) { return alStatusCategory(r.PROCESS_STATUS) == "SUCCESS"; }).length;
    var failed = filtered.filter(function (r) { return alStatusCategory(r.PROCESS_STATUS) == "FAILED"; }).length;
    var running = total - success - failed;

    var todayStr = moment().format("YYYY-MM-DD");
    var todayRows = filtered.filter(function (r) { return r.START_DT != null && moment(r.START_DT).format("YYYY-MM-DD") == todayStr; });
    var activeUsersToday = new Set(todayRows.map(function (r) { return r.CREATED_BY; }).filter(Boolean));

    var pct = function (n) { return total > 0 ? ((n / total) * 100).toFixed(1) + "% of total" : "-"; };

    $("#alStatTotal").text(total);
    $("#alStatSuccess").text(success);
    $("#alStatSuccessPct").text(pct(success));
    $("#alStatFailed").text(failed);
    $("#alStatFailedPct").text(pct(failed));
    $("#alStatRunning").text(running);
    $("#alStatRunningPct").text(pct(running));
    $("#alStatToday").text(todayRows.length);
    $("#alStatActiveUser").text(activeUsersToday.size);
}

function onDetail(processID) {
    $.ajax({
        type: "POST",
        url: logMonitoringConfig.urls.getByKey,
        async: false,
        data: {
            PROCESS_ID: processID
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;
                var category = alStatusCategory(data.PROCESS_STATUS);
                $('#txtProcessID').html('<b>: </b>' + data.PROCESS_ID);
                $('#txtModuleName').html('<b>: </b>' + data.MODULE);
                $('#txtStatus').html('<b>: </b>' + alStatusLabel(category));
                $('#txtStartDate').html('<b>: </b>' + moment(data.START_DT).format("DD-MM-YYYY HH:mm:ss"));
                $('#txtFunction').html('<b>: </b>' + data.FUNCTION);
                $('#txtUsername').html('<b>: </b>' + data.CREATED_BY);
                $('#txtStartEnd').html('<b>: </b>' + moment(data.END_DT).format("DD-MM-YYYY HH:mm:ss"));

                $("#modalLogDetail").modal();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });

    $.ajax({
        type: "POST",
        url: logMonitoringConfig.urls.searchDetail,
        async: false,
        data: {
            PROCESS_ID: processID
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;

                $("#logDetailGrid").dataTable().fnDestroy();
                $('#logDetailGrid').DataTable({
                    "ordering": false,
                    "oLanguage": { "sEmptyTable": "Data not found." },
                    "scrollCollapse": false,
                    "paging": false,
                    "info": false,
                    "searching": false,
                    "data": data,
                    "columns": [
                        {
                            data: 'SEQ_NO'
                        },
                        {
                            render: function (data, type, row) {
                                if (row["CREATED_DT"] != null) {
                                    return moment(row["START_DT"]).format("DD-MM-YYYY HH:mm:ss");
                                } else {
                                    return "";
                                }
                            }
                        },
                        {
                            data: 'LOCATION'
                        },
                        {
                            data: 'MESSAGE_TYPE'
                        },
                        {
                            data: 'MESSAGE_CONTENT'
                        }
                    ],
                    "columnDefs": [
                        { "width": "15%", "targets": 4 },
                        {
                            "targets": [0],
                            "className": "text-center",
                        },
                        {
                            "targets": [1],
                            "className": "text-center",
                        },
                        {
                            "targets": [3],
                            "className": "text-center",
                        },
                    ]
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}
