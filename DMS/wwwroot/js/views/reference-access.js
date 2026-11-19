// Administration > Reference Access (request Hendra 2026-11-18, tahap 1).
// Server values come from referenceAccessConfig in Views/ReferenceAccess/Index.cshtml.
var raCfg = referenceAccessConfig;
var raRows = [];
var raState = "";
var raOptions = { users: [], departments: [], categories: [] };
var raUserItems = [];   // select2 items for the reader (a person, 2026-11-18)

$(document).ready(function () {
    loadOptions();

    var store = new DevExpress.data.CustomStore({
        key: "id",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();
            $.ajax({ type: "POST", url: raCfg.urls.search, dataType: "json" })
                .done(function (result) {
                    if (!result || result.status !== true) {
                        showErrorMesgGrowl((result && result.message) || "The list could not be loaded.");
                        d.resolve([]);
                        return;
                    }
                    raRows = result.data || [];
                    updateTabCounts();
                    d.resolve(raRows);
                })
                .fail(function () { d.reject("Data loading error"); });
            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: store,
        height: computeGridHeight(),
        wordWrapEnabled: true,
        noDataText: "No reference access yet.",
        columns: [
            {
                caption: "No", width: 50, alignment: "center", allowFiltering: false, allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    c.text(o.component.pageIndex() * o.component.pageSize() + o.row.rowIndex + 1);
                }
            },
            {
                dataField: "viewer", caption: "Reader", minWidth: 190,
                cellTemplate: function (c, o) {
                    c.text(o.value);
                    var sub = (o.data.viewerDepartments || "No department") + (o.data.viewerActive ? "" : " · inactive user");
                    $("<span class='ra-sub'>").toggleClass("ra-ends-soon", !o.data.viewerActive).text(sub).appendTo(c);
                }
            },
            { dataField: "owner", caption: "Owner Department", minWidth: 170 },
            { dataField: "category", caption: "Category", minWidth: 140 },
            {
                caption: "Valid Period", minWidth: 170, allowHeaderFiltering: false,
                calculateCellValue: function (r) { return r.validFrom + " " + r.validTo; },
                cellTemplate: function (c, o) {
                    var r = o.data;
                    c.text(moment(r.validFrom).format("DD MMM YYYY") + " - " + moment(r.validTo).format("DD MMM YYYY"));
                    if (r.state === "Active") {
                        var days = moment(r.validTo).startOf("day").diff(moment().startOf("day"), "days");
                        $("<span>").addClass("ra-sub" + (days <= 7 ? " ra-ends-soon" : ""))
                            .text(days === 0 ? "Ends today" : days + " day(s) left").appendTo(c);
                    }
                }
            },
            {
                dataField: "state", caption: "Status", width: 120, alignment: "center",
                cellTemplate: function (c, o) {
                    $("<span>").addClass("ra-state " + o.value).text(o.value).appendTo(c);
                    if (o.value === "Revoked" && o.data.revokedBy) {
                        $("<span class='ra-sub'>").text("by " + o.data.revokedBy).appendTo(c);
                    }
                }
            },
            {
                dataField: "documentCount", caption: "Documents", width: 100, alignment: "center", allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    $("<span class='ra-link' title='Documents the department can read through this access'>")
                        .text(o.value).on("click", function () { showDocuments(o.data); }).appendTo(c);
                }
            },
            { dataField: "reason", caption: "Reason", minWidth: 220, allowHeaderFiltering: false },
            { dataField: "createdBy", caption: "Created By", visible: false },
            {
                dataField: "createdDt", caption: "Created Date", visible: false, allowHeaderFiltering: false,
                cellTemplate: function (c, o) { c.text(o.value ? moment(o.value).format("DD-MM-YYYY HH:mm") : ""); }
            },
            {
                caption: "Action", width: 70, alignment: "center", allowFiltering: false, allowHeaderFiltering: false,
                fixed: true, fixedPosition: "right",
                cellTemplate: function (c, o) {
                    $("<button>").attr({ type: "button", title: "More actions" }).addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) { e.stopPropagation(); openRowMenu($(this), o.data); })
                        .appendTo(c);
                }
            }
        ]
    }));

    bindGridToolbar("refresh resize");

    $("#raTabs").on("click", ".ra-tab", function () {
        raState = $(this).data("state");
        $("#raTabs .ra-tab").removeClass("active");
        $(this).addClass("active");
        var grid = $("#datagrid").dxDataGrid("instance");
        if (raState) grid.filter(["state", "=", raState]); else grid.clearFilter("dataSource");
    });

    $("#btn_add").on("click", function () { openForm(null); });
    $("#btn_save").on("click", saveForm);
    $("#btn_revoke").on("click", doRevoke);

    $(".ra-quick").on("click", "button", function () {
        var from = $("#raFrom").val() ? moment($("#raFrom").val()) : moment();
        if (!$("#raFrom").val()) $("#raFrom").val(from.format("YYYY-MM-DD"));
        $("#raTo").val(from.clone().add($(this).data("months"), "months").subtract(1, "day").format("YYYY-MM-DD"));
        updatePreview();
    });
    $("#raViewer, #raOwner, #raCategory").on("change", updatePreview);
    $("#raFrom, #raTo").on("change", updatePreview);
});

function loadOptions() {
    $.getJSON(raCfg.urls.options).done(function (result) {
        if (!result || result.status !== true) {
            showErrorMesgGrowl((result && result.message) || "The departments could not be loaded.");
            return;
        }
        raOptions = result;
        var departments = result.departments.map(function (d) {
            return { id: d.id, text: d.code + " - " + d.name + (d.division ? "  (" + d.division + ")" : "") };
        });
        var categories = result.categories.map(function (c) {
            return { id: c.id, text: c.code + " - " + c.name + (c.level != null ? "  (Level " + c.level + ")" : "") };
        });
        raUserItems = result.users.map(function (u) {
            return { id: u.id, text: (u.name || u.id) + " (" + u.id + ")" + (u.departments ? "  - " + u.departments : "") };
        });
        initReaderSelect();
        initSelect("#raOwner", departments, "Choose the owner department", false);
        initSelect("#raCategory", categories, "All categories", true);
    });
}

function initSelect(selector, data, placeholder, allowClear) {
    $(selector).empty().append("<option></option>").select2({
        data: data,
        placeholder: placeholder,
        allowClear: allowClear,
        width: "100%",
        dropdownParent: $("#modalRaForm")
    });
}

// Reader: one person per access, a single-choice combo like Owner Department (Hendra 2026-11-18).
function initReaderSelect() {
    initSelect("#raViewer", raUserItems, "Choose the reader", false);
}

function updateTabCounts() {
    $("#raTabs [data-count]").each(function () {
        var state = $(this).data("count");
        $(this).text(state ? raRows.filter(function (r) { return r.state === state; }).length : raRows.length);
    });
}

function openRowMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();
    var open = row.state !== "Revoked";

    if (raCfg.canEdit && open) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () { openForm(row); }));
    }
    $menu.append(menuItem("dp-menu-edit", "fa-files-o", "View Documents", function () { showDocuments(row); }));
    $menu.append(menuItem("dp-menu-edit", "fa-history", "History", function () { showHistory(row); }));
    if (raCfg.canRevoke && open) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-ban", "Revoke", function () { openRevoke(row); }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() }).show();
}

// ---------- Add / Edit ----------
var raEditId = null;

function openForm(row) {
    raEditId = row ? row.id : null;
    $("#raFormTitle").text(row ? "Edit Reference Access" : "New Reference Access");
    $("#raFormSub").text(row ? "Change the reader, owner, category or period" : "Let people read another department's documents");
    $("#raViewer").val(row ? row.viewerUsername : null).trigger("change.select2");
    $("#raOwner").val(row ? String(row.ownerDepartmentId) : null).trigger("change.select2");
    $("#raCategory").val(row && row.documentId != null ? String(row.documentId) : null).trigger("change.select2");
    $("#raFrom").val(row ? row.validFrom : moment().format("YYYY-MM-DD"));
    $("#raTo").val(row ? row.validTo : "");
    $("#raReason").val(row ? row.reason : "");
    updatePreview();
    $("#modalRaForm").modal("show");
}

function textOf(selector) {
    var data = $(selector).select2("data");
    return data && data.length && data[0].id ? data[0].text.split("  (")[0] : null;
}

// What the grant will do, in one sentence, so the user checks it before saving.
function updatePreview() {
    var readers = ($("#raViewer").select2("data") || []).filter(function (d) { return d.id; })
        .map(function (d) { return d.text.split("  - ")[0]; });
    var owner = textOf("#raOwner"), category = textOf("#raCategory");
    var from = $("#raFrom").val(), to = $("#raTo").val();
    var $p = $("#raPreview");
    if (!readers.length || !owner || !from || !to) { $p.removeClass("show").text(""); return; }
    var who = readers.length <= 3 ? readers.join(", ") : readers.slice(0, 3).join(", ") + " and " + (readers.length - 3) + " more";
    $p.addClass("show").text(who + " can read the approved, non-confidential documents of " + owner
        + (category ? " in category " + category : " (all categories)")
        + " from " + moment(from).format("DD MMM YYYY") + " to " + moment(to).format("DD MMM YYYY")
        + ". View only: no download or print.");
}

function saveForm() {
    var readers = $("#raViewer").val();
    readers = $.isArray(readers) ? readers : (readers ? [readers] : []);
    var data = {
        id: raEditId,
        viewerUsernames: readers,
        ownerDepartmentId: $("#raOwner").val(),
        documentId: $("#raCategory").val() || null,
        validFrom: $("#raFrom").val(),
        validTo: $("#raTo").val(),
        reason: $.trim($("#raReason").val())
    };
    var missing = [];
    if (!readers.length) missing.push("Reader");
    if (!data.ownerDepartmentId) missing.push("Owner Department");
    if (!data.validFrom || !data.validTo) missing.push("Valid Period");
    if (!data.reason) missing.push("Reason");
    if (missing.length) { showErrorMesgGrowl("Fill in: " + missing.join(", ") + "."); return; }
    if (data.validTo < data.validFrom) { showErrorMesgGrowl("Valid To must be on or after Valid From."); return; }
    if (data.id == null) delete data.id;
    if (data.documentId == null) delete data.documentId;

    popUpProgressShow();
    // traditional: viewerUsernames=a&viewerUsernames=b, bound to string[] by MVC
    $.ajax({ type: "POST", url: raCfg.urls.save, data: data, dataType: "json", traditional: true })
        .done(function (result) {
            popUpProgressHide();
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                $("#modalRaForm").modal("hide");
                onSearch();
            } else {
                showErrorMesgGrowl((result && result.message) || "Saving failed.");
            }
        })
        .fail(function (xhr) {
            popUpProgressHide();
            showErrorMesgGrowl("Saving failed (HTTP " + xhr.status + "). Refresh the page and try again.");
        });
}

// ---------- Revoke ----------
var raRevokeId = null;

function openRevoke(row) {
    raRevokeId = row.id;
    $("#raRevokeSub").text(row.viewer + " reading " + row.owner + " (" + row.category + ")");
    $("#raRevokeReason").val("");
    $("#modalRaRevoke").modal("show");
}

function doRevoke() {
    var reason = $.trim($("#raRevokeReason").val());
    if (!reason) { showErrorMesgGrowl("Write why the access is revoked."); return; }
    popUpProgressShow();
    $.ajax({ type: "POST", url: raCfg.urls.revoke, data: { id: raRevokeId, reason: reason }, dataType: "json" })
        .done(function (result) {
            popUpProgressHide();
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                $("#modalRaRevoke").modal("hide");
                onSearch();
            } else {
                showErrorMesgGrowl((result && result.message) || "Revoking failed.");
            }
        })
        .fail(function (xhr) {
            popUpProgressHide();
            showErrorMesgGrowl("Revoking failed (HTTP " + xhr.status + "). Refresh the page and try again.");
        });
}

// ---------- History / Documents popup ----------
function showInfo(icon, title, sub, url, id, columns, noData) {
    $("#raInfoIcon").attr("class", "fa " + icon);
    $("#raInfoTitle").text(title);
    $("#raInfoSub").text(sub);
    var $grid = $("#raInfoGrid");
    if ($grid.data("dxDataGrid")) $grid.dxDataGrid("dispose");
    $grid.empty();
    $("#modalRaInfo").modal("show");

    $.getJSON(url, { id: id }).done(function (result) {
        if (!result || result.status !== true) {
            showErrorMesgGrowl((result && result.message) || "The data could not be loaded.");
            return;
        }
        $grid.dxDataGrid({
            dataSource: result.data,
            showBorders: true,
            columnAutoWidth: true,
            wordWrapEnabled: true,
            noDataText: noData,
            paging: { pageSize: 15 },
            pager: { visible: "auto", showInfo: true },
            searchPanel: { visible: result.data.length > 15, width: 220 },
            sorting: { mode: "none" },
            columns: columns
        });
    });
}

function showHistory(row) {
    showInfo("fa-history", "History", row.viewer + " reading " + row.owner, raCfg.urls.history, row.id, [
        { dataField: "at", caption: "Time", width: 140, cellTemplate: function (c, o) { c.text(moment(o.value).format("DD-MM-YYYY HH:mm")); } },
        { dataField: "action", caption: "Action", width: 80 },
        { dataField: "by", caption: "By", width: 150 },
        { dataField: "detail", caption: "Detail" }
    ], "No history.");
}

function showDocuments(row) {
    var sub = row.owner + " documents" + (row.category === "All categories" ? "" : " - " + row.category)
        + (row.state === "Active" ? ", readable by " + row.viewer + " now" : " (" + row.state.toLowerCase() + ": not readable now)");
    showInfo("fa-files-o", "Documents", sub, raCfg.urls.documents, row.id, [
        { dataField: "code", caption: "Document No.", width: 190 },
        { dataField: "title", caption: "Title" },
        { dataField: "revision", caption: "Rev", width: 55, alignment: "center" },
        { dataField: "category", caption: "Category", width: 160 },
        { dataField: "date", caption: "Date", width: 100, cellTemplate: function (c, o) { c.text(o.value ? moment(o.value).format("DD-MM-YYYY") : ""); } }
    ], "No approved, non-confidential documents yet.");
}
