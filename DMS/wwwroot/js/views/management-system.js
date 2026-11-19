// Master Data > Management System (IMS tahap 1, 2026-11-19).
// Server values come from managementSystemConfig in Views/ManagementSystem/Index.cshtml.
var msCfg = managementSystemConfig;
var msEditing = null;   // the row being edited; null = new

$(document).ready(function () {
    var store = new DevExpress.data.CustomStore({
        key: "Code",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();
            $.ajax({ type: "POST", url: msCfg.urls.search, dataType: "json" })
                .done(function (result) {
                    if (!result || result.status !== true) {
                        showErrorMesgGrowl((result && result.message) || "The list could not be loaded.");
                        d.resolve([]);
                        return;
                    }
                    d.resolve(result.data || []);
                })
                .fail(function () { d.reject("Data loading error"); });
            return d.promise();
        }
    });

    var people = function (c, list) {
        if (!list || !list.length) { $("<span class='ms-muted'>").text("Everyone").appendTo(c); return; }
        list.forEach(function (p) { $("<span class='ms-person'>").attr("title", p.username).text(p.name || p.username).appendTo(c); });
    };

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: store,
        height: computeGridHeight(),
        wordWrapEnabled: true,
        noDataText: "No management system.",
        columns: [
            { dataField: "Code", caption: "Code", width: 90, cellTemplate: function (c, o) { $("<span class='ms-code'>").text(o.value).appendTo(c); } },
            {
                dataField: "Name", caption: "Name", minWidth: 220,
                cellTemplate: function (c, o) {
                    c.text(o.value || "");
                    if (o.data.Standard) $("<div class='ms-muted'>").text(o.data.Standard).appendTo(c);
                }
            },
            {
                dataField: "NumberCode", caption: "Number Code", width: 170,
                cellTemplate: function (c, o) {
                    if (o.value) $("<span class='ms-num'>").text("…/PRO/" + o.value + "/001").appendTo(c);
                    else $("<span class='ms-muted'>").text("QMS pattern").appendTo(c);
                }
            },
            { caption: "Creators", minWidth: 180, allowSorting: false, cellTemplate: function (c, o) { people(c, o.data.Creators); } },
            {
                caption: "Document Control", minWidth: 180, allowSorting: false,
                cellTemplate: function (c, o) {
                    if (!o.data.Controllers || !o.data.Controllers.length) $("<span class='ms-muted'>").text("-").appendTo(c);
                    else people(c, o.data.Controllers);
                }
            },
            { dataField: "Documents", caption: "Documents", width: 100, alignment: "center" },
            {
                dataField: "Active", caption: "Status", width: 110, alignment: "center",
                cellTemplate: function (c, o) { $("<span class='ms-state'>").addClass(o.value ? "on" : "off").text(o.value ? "Active" : "Archived").appendTo(c); }
            },
            {
                caption: "", width: 70, alignment: "center", visible: msCfg.canEdit, allowSorting: false,
                cellTemplate: function (c, o) {
                    $("<button type='button' class='dp-icon-btn' title='Edit'><i class='fa fa-pencil'></i></button>")
                        .on("click", function () { msOpen(o.data); }).appendTo(c);
                }
            }
        ]
    }));

    $("#btn_refresh").on("click", function () { $("#datagrid").dxDataGrid("instance").refresh(); });
    $("#btn_add").on("click", function () { msOpen(null); });
    $("#btn_save").on("click", msSave);
    $("#msCode, #msNumberCode").on("input", function () { this.value = this.value.toUpperCase().replace(/[^A-Z0-9]/g, ""); });

    $.getJSON(msCfg.urls.users).done(function (result) {
        var options = ((result && result.data) || []).map(function (u) { return { id: u.username, text: (u.name || u.username) + " (" + u.username + ")" }; });
        ["#msCreators", "#msControllers"].forEach(function (id) {
            $(id).select2({ data: options, placeholder: "Choose users", dropdownParent: $("#modalMsForm"), width: "100%" });
        });
    });
});

function msOpen(row) {
    msEditing = row;
    $("#msFormTitle").text(row ? "Edit " + row.Code : "New Management System");
    $("#msCode").val(row ? row.Code : "").prop("disabled", !!row);
    $("#msName").val(row ? row.Name : "");
    $("#msStandard").val(row ? (row.Standard || "") : "");
    $("#msNumberCode").val(row ? (row.NumberCode || "") : "").prop("disabled", !!(row && row.Documents > 0));
    $("#msNumberLock").toggle(!!(row && row.Documents > 0));
    $("#msSortNo").val(row ? row.SortNo : 0);
    $("#msActive").prop("checked", row ? row.Active : true);
    $("#msCreators").val(row ? row.Creators.map(function (p) { return p.username; }) : []).trigger("change");
    $("#msControllers").val(row ? row.Controllers.map(function (p) { return p.username; }) : []).trigger("change");
    $("#modalMsForm").modal("show");
}

function msSave() {
    var data = {
        IsNew: !msEditing,
        Code: $("#msCode").val(),
        Name: $("#msName").val(),
        Standard: $("#msStandard").val(),
        NumberCode: $("#msNumberCode").val(),
        SortNo: parseInt($("#msSortNo").val(), 10) || 0,
        Active: $("#msActive").is(":checked"),
        Creators: $("#msCreators").val() || [],
        Controllers: $("#msControllers").val() || []
    };
    if (!data.Code || !data.Name) { showErrorMesgGrowl("Code and Name are required."); return; }
    var $btn = $("#btn_save").prop("disabled", true);
    $.ajax({ type: "POST", url: msCfg.urls.save, data: data, dataType: "json" })
        .done(function (result) {
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                $("#modalMsForm").modal("hide");
                $("#datagrid").dxDataGrid("instance").refresh();
            } else {
                showErrorMesgGrowl((result && result.message) || "Could not save.");
            }
        })
        .fail(function () { showErrorMesgGrowl("Could not save."); })
        .always(function () { $btn.prop("disabled", false); });
}
