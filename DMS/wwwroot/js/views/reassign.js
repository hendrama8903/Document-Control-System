// Reassign. Server values come from reassignConfig in the view.
function updateActionButtons() {
    var selected = getSelectedRowData() != null;

    $("#btn_reassign").prop("disabled", !selected);
}

function rsRenderStats(data) {
    var divisions = {}, departments = {}, roles = {};

    data.forEach(function (r) {
        if (r.DIVISION) divisions[r.DIVISION] = true;
        if (r.DEPARTMENT_CODE) departments[r.DEPARTMENT_CODE] = true;
        if (r.ROLE_NAME) roles[r.ROLE_NAME] = true;
    });

    $("#rsStatTotal").text(data.length);
    $("#rsStatDivisions").text(Object.keys(divisions).length);
    $("#rsStatDepartments").text(Object.keys(departments).length);
    $("#rsStatRoles").text(Object.keys(roles).length);
}

$(document).ready(function () {
    $("#btn_reassign").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "USERNAME",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: reassignConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                rsRenderStats(data);
                d.resolve(data);
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
        noDataText: "No data.",
        height: computeGridHeight(),
        filterRow: { visible: false, applyFilter: "auto" },
        headerFilter: { visible: true },
        toolbar: { visible: false },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
        },
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
        onSelectionChanged: function (e) {
            if (e.selectedRowKeys.length > 1) {
                e.component.selectRows(e.currentSelectedRowKeys, false);
                return;
            }
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
            { dataField: "USERNAME", caption: "Username", fixed: true, fixedPosition: "left" },
            { dataField: "REG_NO", caption: "Reg No" },
            { dataField: "FULL_NAME", caption: "Full Name" },
            { dataField: "EMAIL", caption: "Email" },
            { dataField: "PHONE", caption: "Phone" },
            { dataField: "ROLE_NAME", caption: "Role" },
            { dataField: "POSITION_NAME", caption: "Position" },
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
            {
                caption: "Section",
                calculateCellValue: function (row) {
                    return row.SECTION_CODE != null ? row.SECTION_CODE + " - " + row.SECTION_NAME : "";
                }
            }
        ]
    });

    bindGridToolbar("columns filters reset refresh resize");

});

var gId = null;

$(document).ready(function () {
    $("#btn_reassign").on("click", function () {
        onReassignPrepare();
    });

    $("#btn_save_reassign").on("click", function () {
        confirmReassignData();
    });

    $("#checkAllReassignTask").on("change", function () {
        $("#reassignDetailGrid tbody input[type='checkbox']").prop("checked", $(this).is(":checked"));
    });

    bindGridToolbar("stats");

});

function onReassignPrepare() {
    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gId = row.USERNAME;

    $('#reassignOldApprover').val(row.USERNAME + (row.FULL_NAME ? " - " + row.FULL_NAME : ""));
    $('#addEditNewApprover').val("").trigger("change");
    $('#addEditReassignReason').val("");
    $('#checkAllReassignTask').prop('checked', true);

    setDataSelect2("addEditNewApprover", reassignConfig.urls.getUserName, "- Choose -");

    $.ajax({
        type: "POST",
        url: reassignConfig.urls.getPendingApprovals,
        async: false,
        data: {
            username: gId
        },
        success: function (result) {
            if (result.status == true) {
                var tbody = $("#reassignDetailGrid tbody");
                if (tbody.length == 0) {
                    tbody = $("<tbody>").appendTo("#reassignDetailGrid");
                }
                tbody.empty();

                if (result.data == null || result.data.length == 0) {
                    $("#spanNoPendingTask").show();
                    $("#btn_save_reassign").prop("disabled", true);
                } else {
                    $("#spanNoPendingTask").hide();
                    $("#btn_save_reassign").prop("disabled", false);

                    $.each(result.data, function (index, task) {
                        var submittedDate = task.APPROVAL_DATE ? moment(task.APPROVAL_DATE).format('DD-MM-YYYY HH:mm') : "";
                        var documentCode = task.DOCUMENT_CODE ? task.DOCUMENT_CODE : "-";
                        var documentName = task.DOCUMENT_TRANSACTION_NAME ? task.DOCUMENT_TRANSACTION_NAME : "-";
                        var label = task.LABEL ? task.LABEL : ("Step " + task.WORKFLOW_SEQ);

                        var row = $("<tr>");
                        row.append(
                            $("<td>").addClass("text-center").append(
                                $("<input>").attr("type", "checkbox")
                                    .prop("checked", true)
                                    .attr("data-approval-id", task.APPROVAL_ID)
                                    .attr("data-workflow-seq", task.WORKFLOW_SEQ)
                            )
                        );
                        row.append($("<td>").text(documentCode));
                        row.append($("<td>").text(documentName));
                        row.append($("<td>").text(submittedDate));
                        row.append($("<td>").text(label));

                        tbody.append(row);
                    });
                }
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });

    $("#modalReassign").modal();
}

function confirmReassignData() {
    var newApprover = $('#addEditNewApprover').val();
    if (newApprover == null || newApprover == "") {
        showErrorMesgGrowl("New Approver must be selected");
        return;
    }

    var checkedTasks = $("#reassignDetailGrid tbody input[type='checkbox']:checked");
    if (checkedTasks.length == 0) {
        showErrorMesgGrowl("Select at least one pending approval task to reassign");
        return;
    }

    showConfirmMesg("Are you sure you want to reassign " + checkedTasks.length + " approval task(s) to " + $('#addEditNewApprover').select2('data')[0].text + "?", reassignData, "Confirmation");
}

function reassignData() {
    popUpProgressShow();

    var oldApprover = gId;
    var newApprover = $('#addEditNewApprover').val();
    var reason = $('#addEditReassignReason').val();
    var checkedTasks = $("#reassignDetailGrid tbody input[type='checkbox']:checked");

    var tasks = [];
    checkedTasks.each(function () {
        tasks.push({
            approvalId: $(this).data("approval-id"),
            workflowSeq: $(this).data("workflow-seq")
        });
    });

    var successCount = 0;
    var errorMessages = [];

    function reassignNext(index) {
        if (index >= tasks.length) {
            popUpProgressHide();

            if (successCount > 0) {
                showSuccessMesgGrowl(successCount + " approval task(s) successfully reassigned.");
                onSearch();
                $("#modalReassign").modal('hide');
            }

            if (errorMessages.length > 0) {
                showErrorMesgGrowl(errorMessages.join("<br/>"));
            }

            return;
        }

        $.ajax({
            type: "POST",
            url: reassignConfig.urls.reassignApprover,
            data: {
                approvalId: tasks[index].approvalId,
                workflowSeq: tasks[index].workflowSeq,
                oldApprover: oldApprover,
                newApprover: newApprover,
                reason: reason
            },
            success: function (result) {
                if (result.status == true) {
                    successCount++;
                } else {
                    errorMessages.push(result.message);
                }
                reassignNext(index + 1);
            },
            error: function (result) {
                errorMessages.push("Unexpected error while reassigning task #" + (index + 1));
                reassignNext(index + 1);
            }
        });
    }

    reassignNext(0);
}
