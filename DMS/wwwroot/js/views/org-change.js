// Master Data > Organization Change (tahap 1, 2026-11-19).
// Server values come from orgChangeConfig in Views/OrgChange/Index.cshtml.
var ocCfg = orgChangeConfig;
var ocOptions = { divisions: [], departments: [], sections: [], types: [] };
var ocEditId = null;
var ocActionId = null;

var OC_HINTS = {
    RENAME: "One old code becomes one new code (or the same code with a new name). The new code must already be in the master.",
    MERGE: "Two or more old units become one new unit. Add a row for every old code; all rows have the same new code.",
    SPLIT: "One old unit is divided. Add a row for every new unit and mark the default owner of the documents; each department decides later which documents go where.",
    MOVE: "The unit keeps its code and moves to another division (department) or department (section). Choose the new parent.",
    DOWNGRADE: "A department becomes a section of another department. Choose the new section (add it in Section Master first).",
    NEW: "A unit that did not exist before. Add it in the master first, then record it here with its HR reference.",
    CLOSE: "A unit that is dissolved without a successor. Its documents are not made obsolete automatically: the department decides."
};

$(document).ready(function () {
    loadOptions();

    var store = new DevExpress.data.CustomStore({
        key: "Id",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();
            $.ajax({ type: "POST", url: ocCfg.urls.search, data: { includeCancelled: $("#ocShowCancelled").is(":checked") }, dataType: "json" })
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

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: store,
        height: computeGridHeight(),
        wordWrapEnabled: true,
        noDataText: "No organization change recorded yet.",
        masterDetail: { enabled: true, template: renderImpactDetail },
        columns: [
            {
                dataField: "EffectiveDate", caption: "Effective Date", width: 120, alignment: "center", allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    if (o.value) c.text(moment(o.value).format("DD MMM YYYY"));
                    else $("<span class='oc-missing'>").text("Not filled").appendTo(c);
                }
            },
            {
                dataField: "TypeLabel", caption: "Type", minWidth: 170,
                cellTemplate: function (c, o) {
                    c.text(o.value);
                    $("<span class='oc-sub'>").text(o.data.LevelLabel).appendTo(c);
                }
            },
            {
                caption: "Old → New", minWidth: 240, allowHeaderFiltering: false,
                calculateCellValue: function (r) { return ocSummary(r); },
                cellTemplate: function (c, o) { renderMaps(c, o.data); }
            },
            {
                dataField: "HrReference", caption: "HR Reference", minWidth: 150,
                cellTemplate: function (c, o) {
                    c.text(o.value || "");
                    if (!o.value) $("<span class='oc-missing'>").text("Not filled").appendTo(c);
                    if (o.data.HasAttachment) {
                        $("<a class='oc-file' target='_blank'>").attr("href", ocCfg.urls.attachment + "?id=" + o.data.Id)
                            .attr("title", o.data.AttachmentName || "").html('<i class="fa fa-paperclip"></i> Memo').appendTo(c);
                    }
                }
            },
            {
                caption: "Old Codes", minWidth: 200, allowHeaderFiltering: false,
                cellTemplate: function (c, o) {
                    (o.data.Impacts || []).forEach(function (i) { $("<div>").append(impactChip(i)).appendTo(c); });
                }
            },
            { dataField: "Description", caption: "Description", minWidth: 200, allowHeaderFiltering: false },
            {
                dataField: "Status", caption: "Status", width: 100, alignment: "center",
                calculateCellValue: function (r) { return r.Status === 1 ? "Valid" : "Cancelled"; },
                cellTemplate: function (c, o) {
                    $("<span>").addClass("oc-state " + o.value).text(o.value).appendTo(c);
                    if (o.data.Status !== 1 && o.data.CancelReason) $("<span class='oc-sub'>").text(o.data.CancelReason).appendTo(c);
                }
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
    $("#ocShowCancelled").on("change", onSearch);

    $("#btn_add").on("click", function () { openForm(null); });
    $("#btn_save").on("click", saveForm);
    $("#btn_addRow").on("click", function () { addRow({}); applyTypeLayout(); });
    $("#ocType, #ocLevel").on("change", function () { onTypeOrLevelChange(); });
    $("#ocMap").on("click", ".oc-del", function () { $(this).closest("tr").remove(); applyTypeLayout(); });
    $("#ocMap").on("change input", ".oc-old", function () { onOldCodeChange($(this).closest("tr")); });
    $("#btn_cancelChange").on("click", doCancel);
    $("#btn_deactivate").on("click", doDeactivate);
    $("#btn_transfer").on("click", doTransfer);
    $("#ocTransferBody").on("change", "input[type=checkbox], select", updateTransferCount);
});

function onSearch() { $("#datagrid").dxDataGrid("instance").refresh(); }

function loadOptions() {
    $.getJSON(ocCfg.urls.options).done(function (result) {
        if (!result || result.status !== true) return;
        ocOptions = result;
        var $t = $("#ocType").empty();
        result.types.forEach(function (t) { $("<option>").val(t.id).text(t.name).appendTo($t); });
    });
}

// ---------- list rendering ----------
function ocSummary(r) {
    var olds = [], news = [];
    (r.Maps || []).forEach(function (m) {
        if (m.OldCode && olds.indexOf(m.OldCode) < 0) olds.push(m.OldCode);
        if (m.NewCode && news.indexOf(m.NewCode) < 0) news.push(m.NewCode);
    });
    return (olds.join(", ") || "(new)") + " → " + (news.join(", ") || "(closed)");
}

function unitText(code, name, parent, showParent) {
    return (showParent && parent ? parent + " / " : "") + code + (name ? " - " + name : "");
}

function renderMaps(c, r) {
    var seenOld = {}, seenNew = {}, $olds = $("<div class='oc-side'>"), $news = $("<div class='oc-side'>");
    var parentOld = r.Type === "MOVE" || r.Level === "SECTION";
    (r.Maps || []).forEach(function (m) {
        if (m.OldCode && !seenOld[m.OldCode + m.OldParent]) {
            seenOld[m.OldCode + m.OldParent] = 1;
            $("<div>").text(unitText(m.OldCode, m.OldName, m.OldParent, parentOld)).appendTo($olds);
        }
        if (m.NewCode && !seenNew[m.NewCode + m.NewParent]) {
            seenNew[m.NewCode + m.NewParent] = 1;
            var $n = $("<div>").text(unitText(m.NewCode, m.NewName, m.NewParent, r.Type === "MOVE" || (m.NewLevel || r.Level) === "SECTION"));
            if (r.Type === "SPLIT" && m.IsOwner) $("<span class='oc-owner'>").text("owner").appendTo($n);
            $n.appendTo($news);
        }
    });
    if (!$olds.children().length) $("<div class='oc-sub'>").text("(new unit)").appendTo($olds);
    if (!$news.children().length) $("<div class='oc-sub'>").text("(closed)").appendTo($news);
    $("<div class='oc-maps'>").append($olds, $("<div class='oc-arrow'>").html('<i class="fa fa-long-arrow-right"></i>'), $news).appendTo(c);
}

function impactState(i) {
    if (i.CodeKept) return { cls: "kept", text: "code kept" };
    if (i.MasterState === "NotInMaster") return { cls: "gone", text: "not in master" };
    if (i.MasterState === "Inactive") return { cls: "inactive", text: "inactive" + (i.ValidTo && moment(i.ValidTo).year() < 9999 ? " until " + moment(i.ValidTo).format("DD MMM YYYY") : "") };
    return { cls: i.Blocked ? "busy" : "active", text: i.Blocked ? "active · in use" : "active · free" };
}

function impactChip(i) {
    var s = impactState(i);
    return $("<span>").addClass("oc-chip " + s.cls).text(i.Code + " · " + s.text);
}

function renderImpactDetail(container, info) {
    var r = info.data;
    if (!r.Impacts || !r.Impacts.length) {
        $("<div class='oc-detail-empty'>").text("No old code in this change.").appendTo(container);
        return;
    }
    var $t = $("<table class='oc-impact'>").appendTo(container);
    $("<thead>").append($("<tr>").append(
        ["Old Code", "Master", "Documents Owned", "In Approval", "Numbered With Code", "Copies Held", "Users", "Active Sub-units"].map(function (h) { return $("<th>").text(h); })
    )).appendTo($t);
    var $b = $("<tbody>").appendTo($t);
    r.Impacts.forEach(function (i) {
        var s = impactState(i);
        $("<tr>").append(
            $("<td>").text((i.Parent && (r.Level === "SECTION") ? i.Parent + " / " : "") + i.Code),
            $("<td>").append($("<span>").addClass("oc-chip " + s.cls).text(s.text)),
            numCell(i.DocumentsOwned, true), numCell(i.DocumentsInApproval, true), numCell(i.DocumentsNumbered, false),
            numCell(i.CopiesHeld, true), numCell(i.Users, true), numCell(i.ActiveChildren, true)
        ).appendTo($b);
    });
    $("<div class='oc-detail-note'>").text("Documents numbered with the old code keep their number (QMS). The other counts must be zero before the old code can be deactivated; moving them is tahap 2.").appendTo(container);
}

function numCell(n, blocks) {
    return $("<td class='oc-num'>").text(n).toggleClass("oc-num-busy", blocks && n > 0);
}

// ---------- row menu ----------
function openRowMenu($btn, row) {
    var $menu = buildRowActionMenu().empty();
    var valid = row.Status === 1;
    if (valid && ocCfg.canEdit) $menu.append(menuItem("", "fa-pencil", "Edit", function () { openForm(row); }));
    if (valid && ocCfg.canTransfer) $menu.append(menuItem("", "fa-exchange", "Transfer to New Unit", function () { openTransfer(row); }));
    if (valid && ocCfg.canDeactivate) $menu.append(menuItem("", "fa-power-off", "Deactivate Old Codes", function () { openDeactivate(row); }));
    if (row.HasAttachment) $menu.append(menuItem("", "fa-paperclip", "Open HR Memo", function () { window.open(ocCfg.urls.attachment + "?id=" + row.Id, "_blank"); }));
    $menu.append(menuItem("", "fa-history", "History", function () { showHistory(row); }));
    $menu.append(menuItem("", "fa-list-alt", "Move Log", function () { showMoves(row); }));
    if (valid && ocCfg.canCancel) $menu.append(menuItem("dp-menu-delete", "fa-ban", "Cancel Change", function () { openCancel(row); }));
    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() }).show();
}

// ---------- form ----------
function unitsOf(level, activeOnly) {
    var list = level === "DIVISION" ? ocOptions.divisions : level === "SECTION" ? ocOptions.sections : ocOptions.departments;
    return (list || []).filter(function (u) { return !activeOnly || u.active; });
}

// a section is written "DEPT/SEC" in the pickers: section codes repeat across departments
function unitKey(level, u) { return level === "SECTION" ? (u.parent || "") + "/" + u.code : u.code; }
function splitKey(level, key) {
    key = $.trim(key || "").toUpperCase();
    if (level !== "SECTION" || key.indexOf("/") < 0) return { code: key || null, parent: null };
    var p = key.split("/");
    return { parent: p[0] || null, code: p.slice(1).join("/") || null };
}
function newLevel() { return $("#ocType").val() === "DOWNGRADE" ? "SECTION" : $("#ocLevel").val(); }

function fillNewSelect($sel, value) {
    var lvl = newLevel();
    $sel.empty().append($("<option>").val("").text("- choose -"));
    unitsOf(lvl, true).forEach(function (u) {
        $("<option>").val(unitKey(lvl, u)).text(unitKey(lvl, u) + " - " + (u.name || "")).appendTo($sel);
    });
    if (value) {
        if (!$sel.find("option").filter(function () { return this.value === value; }).length)
            $("<option>").val(value).text(value + " (not active)").appendTo($sel);
        $sel.val(value);
    }
}

function fillParentSelect($sel, value) {
    var parentLevel = $("#ocLevel").val() === "SECTION" ? "DEPARTMENT" : "DIVISION";
    $sel.empty().append($("<option>").val("").text("- choose -"));
    unitsOf(parentLevel, true).forEach(function (u) { $("<option>").val(u.code).text(u.code + " - " + (u.name || "")).appendTo($sel); });
    if (value) $sel.val(value);
}

function addRow(m) {
    var $tr = $("<tr>");
    var oldKey = m.OldCode ? ($("#ocLevel").val() === "SECTION" ? (m.OldParent || "") + "/" + m.OldCode : m.OldCode) : "";
    var newKey = m.NewCode ? (newLevel() === "SECTION" ? (m.NewParent || "") + "/" + m.NewCode : m.NewCode) : "";
    $("<td class='oc-col-old'>").append($("<input type='text' class='form-control oc-old' list='ocOldList' maxlength='60'>").val(oldKey)).appendTo($tr);
    $("<td class='oc-col-oldname'>").append($("<input type='text' class='form-control oc-oldname' maxlength='255'>").val(m.OldName || "")).appendTo($tr);
    $("<td class='oc-col-arrow'>").html('<i class="fa fa-long-arrow-right"></i>').appendTo($tr);
    var $new = $("<select class='form-control oc-new'>");
    fillNewSelect($new, newKey);
    $("<td class='oc-col-new'>").append($new).appendTo($tr);
    var $parent = $("<select class='form-control oc-parent'>");
    fillParentSelect($parent, m.NewParent);
    $("<td class='oc-col-parent'>").append($parent).appendTo($tr);
    $("<td class='oc-col-owner'>").append($("<input type='radio' name='ocOwner' class='oc-ownerradio'>").prop("checked", !!m.IsOwner)).appendTo($tr);
    $("<td class='oc-col-del'>").append($("<button type='button' class='oc-del' title='Remove row'>").html('<i class="fa fa-trash-o"></i>')).appendTo($tr);
    $tr.appendTo("#ocMap tbody");
    onOldCodeChange($tr);
}

// The name of an old code still in the master is taken from there; otherwise the user types it.
function onOldCodeChange($tr) {
    var lvl = $("#ocLevel").val(), k = splitKey(lvl, $tr.find(".oc-old").val());
    var found = unitsOf(lvl, false).filter(function (u) {
        return u.code === k.code && (lvl !== "SECTION" || !k.parent || u.parent === k.parent);
    })[0];
    $tr.find(".oc-oldname").prop("readonly", !!found).attr("placeholder", found ? "" : "Name (not in master)");
    if (found) $tr.find(".oc-oldname").val(found.name || "");
    if ($("#ocType").val() === "MOVE") {          // a move keeps the code
        var $new = $tr.find(".oc-new");
        if (k.code && !$new.find("option").filter(function () { return this.value === $tr.find(".oc-old").val().toUpperCase(); }).length)
            $("<option>").val($tr.find(".oc-old").val().toUpperCase()).text($tr.find(".oc-old").val().toUpperCase()).appendTo($new);
        $new.val($.trim($tr.find(".oc-old").val()).toUpperCase());
    }
}

function onTypeOrLevelChange() {
    var lvl = $("#ocLevel").val();
    if ($("#ocType").val() === "DOWNGRADE" && lvl !== "DEPARTMENT") $("#ocLevel").val("DEPARTMENT");
    var $dl = $("#ocOldList").empty();
    unitsOf($("#ocLevel").val(), false).forEach(function (u) {
        $("<option>").val(unitKey($("#ocLevel").val(), u)).text((u.name || "") + (u.active ? "" : " (inactive)")).appendTo($dl);
    });
    $("#ocMap tbody tr").each(function () {
        var $sel = $(this).find(".oc-new"), keep = $sel.val();
        fillNewSelect($sel, keep);
        fillParentSelect($(this).find(".oc-parent"), $(this).find(".oc-parent").val());
        onOldCodeChange($(this));
    });
    applyTypeLayout();
}

function applyTypeLayout() {
    var t = $("#ocType").val();
    $("#ocTypeHint").text(OC_HINTS[t] || "");
    var $m = $("#ocMap");
    $m.toggleClass("hide-old", t === "NEW").toggleClass("hide-new", t === "CLOSE")
      .toggleClass("show-parent", t === "MOVE").toggleClass("show-owner", t === "SPLIT").toggleClass("lock-new", t === "MOVE");
    $m.find(".oc-new").prop("disabled", t === "MOVE");
    var rows = $m.find("tbody tr").length;
    $("#btn_addRow").toggle(!(t === "RENAME" || t === "DOWNGRADE") || rows === 0);
    if (rows === 0) addRow({});
}

function defaultRows(t) {
    $("#ocMap tbody").empty();
    var n = t === "MERGE" || t === "SPLIT" ? 2 : 1;
    for (var i = 0; i < n; i++) addRow({ IsOwner: t === "SPLIT" && i === 0 });
}

function openForm(row) {
    ocEditId = row ? row.Id : null;
    $("#ocFormTitle").text(row ? "Edit Organization Change" : "New Organization Change");
    $("#ocType").val(row ? row.Type : "RENAME");
    $("#ocLevel").val(row ? row.Level : "DEPARTMENT");
    $("#ocEffective").val(row && row.EffectiveDate ? moment(row.EffectiveDate).format("YYYY-MM-DD") : "");
    $("#ocHr").val(row ? row.HrReference || "" : "");
    $("#ocDesc").val(row ? row.Description || "" : "");
    $("#ocFile").val("");
    var $cur = $("#ocCurrentFile").empty();
    if (row && row.HasAttachment) {
        $cur.append($("<a target='_blank'>").attr("href", ocCfg.urls.attachment + "?id=" + row.Id).html('<i class="fa fa-paperclip"></i> ').append(document.createTextNode(row.AttachmentName || "Memo")))
            .append($("<label class='oc-check'>").append($("<input type='checkbox' id='ocRemoveFile'>"), " Remove"));
    }
    $("#ocMap tbody").empty();
    onTypeOrLevelChange();
    $("#ocMap tbody").empty();
    if (row) (row.Maps || []).forEach(function (m) { addRow(m); });
    else defaultRows("RENAME");
    applyTypeLayout();
    $("#ocType").off("change.rows").on("change.rows", function () { defaultRows($(this).val()); applyTypeLayout(); });
    $("#modalOcForm").modal("show");
}

function readMaps() {
    var t = $("#ocType").val(), lvl = $("#ocLevel").val(), nl = newLevel(), maps = [];
    $("#ocMap tbody tr").each(function () {
        var $tr = $(this), o = splitKey(lvl, $tr.find(".oc-old").val()), n = splitKey(nl, $tr.find(".oc-new").val());
        if (t === "NEW") o = { code: null, parent: null };
        if (t === "CLOSE") n = { code: null, parent: null };
        if (t === "MOVE") n = { code: o.code, parent: null };
        maps.push({
            OldCode: o.code, OldParent: o.parent, OldName: $.trim($tr.find(".oc-oldname").val()) || null,
            NewCode: n.code, NewParent: t === "MOVE" ? ($tr.find(".oc-parent").val() || null) : n.parent,
            IsOwner: t === "SPLIT" && $tr.find(".oc-ownerradio").is(":checked")
        });
    });
    return maps.filter(function (m) { return m.OldCode || m.NewCode; });
}

function saveForm() {
    var missing = [];
    if (!$("#ocEffective").val()) missing.push("Effective Date");
    if (!$.trim($("#ocHr").val())) missing.push("HR Reference");
    var maps = readMaps();
    if (!maps.length) missing.push("Old → New Codes");
    if (missing.length) { showErrorMesgGrowl("Fill in: " + missing.join(", ") + "."); return; }

    var fd = new FormData();
    if (ocEditId != null) fd.append("id", ocEditId);
    fd.append("changeType", $("#ocType").val());
    fd.append("orgLevel", $("#ocLevel").val());
    fd.append("effectiveDate", $("#ocEffective").val());
    fd.append("hrReference", $.trim($("#ocHr").val()));
    fd.append("description", $.trim($("#ocDesc").val()));
    fd.append("mapsJson", JSON.stringify(maps));
    fd.append("removeAttachment", $("#ocRemoveFile").is(":checked") ? "true" : "false");
    var file = $("#ocFile")[0].files[0];
    if (file) fd.append("attachment", file);

    popUpProgressShow();
    $.ajax({ type: "POST", url: ocCfg.urls.save, data: fd, processData: false, contentType: false, dataType: "json" })
        .done(function (result) {
            popUpProgressHide();
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                $("#modalOcForm").modal("hide");
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

// ---------- cancel ----------
function openCancel(row) {
    ocActionId = row.Id;
    $("#ocCancelSub").text(row.TypeLabel + ": " + ocSummary(row));
    $("#ocCancelReason").val("");
    $("#modalOcCancel").modal("show");
}

function doCancel() {
    var reason = $.trim($("#ocCancelReason").val());
    if (!reason) { showErrorMesgGrowl("Write why this change is cancelled."); return; }
    postAction(ocCfg.urls.cancel, { id: ocActionId, reason: reason }, "#modalOcCancel");
}

// ---------- deactivate ----------
function openDeactivate(row) {
    ocActionId = row.Id;
    $("#ocDeactivateSub").text(row.TypeLabel + ": " + ocSummary(row));
    var $b = $("#ocDeactivateBody").empty(), canGo = true, any = false;
    if (!row.EffectiveDate) {
        $("<p class='oc-note oc-note-warn'>").text("Fill in the effective date first (Edit): the old codes stay valid until the day before it.").appendTo($b);
        canGo = false;
    } else {
        $("<p class='oc-note'>").text("The old codes are deactivated in the master, valid until "
            + moment(row.EffectiveDate).subtract(1, "day").format("DD MMM YYYY")
            + ". They are not deleted: old documents and reports keep showing them, and they can be activated again in the master.").appendTo($b);
    }
    var $ul = $("<ul class='oc-deact-list'>").appendTo($b);
    (row.Impacts || []).forEach(function (i) {
        var s = impactState(i), $li = $("<li>").append(impactChip(i));
        if (!i.CodeKept && i.MasterState === "Active") {
            any = true;
            if (i.Blocked) {
                canGo = false;
                $("<div class='oc-sub oc-blocked'>").text("Still in use: " + describeImpact(i) + ". Move these to the new unit first.").appendTo($li);
            } else {
                $("<div class='oc-sub'>").text("Will be deactivated.").appendTo($li);
            }
        } else {
            $("<div class='oc-sub'>").text(s.cls === "kept" ? "The code stays in use (no change)." : "Nothing to do.").appendTo($li);
        }
        $li.appendTo($ul);
    });
    if (!any) { $("<p class='oc-note'>").text("No old code of this change is still active in the master.").appendTo($b); canGo = false; }
    $("#btn_deactivate").prop("disabled", !canGo);
    $("#modalOcDeactivate").modal("show");
}

function describeImpact(i) {
    var p = [];
    if (i.DocumentsOwned) p.push(i.DocumentsOwned + " document(s) owned");
    if (i.DocumentsInApproval) p.push(i.DocumentsInApproval + " in approval");
    if (i.CopiesHeld) p.push(i.CopiesHeld + " copy record(s)");
    if (i.Users) p.push(i.Users + " user(s)");
    if (i.ActiveChildren) p.push(i.ActiveChildren + " active sub-unit(s)");
    return p.join(", ");
}

function doDeactivate() {
    postAction(ocCfg.urls.deactivate, { id: ocActionId }, "#modalOcDeactivate");
}

function postAction(url, data, modal) {
    popUpProgressShow();
    $.ajax({ type: "POST", url: url, data: data, dataType: "json" })
        .done(function (result) {
            popUpProgressHide();
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                $(modal).modal("hide");
                onSearch();
            } else {
                showErrorMesgGrowl((result && result.message) || "The action failed.");
            }
        })
        .fail(function (xhr) {
            popUpProgressHide();
            showErrorMesgGrowl("The action failed (HTTP " + xhr.status + "). Refresh the page and try again.");
        });
}

// ---------- history ----------
function showHistory(row) {
    $("#ocHistorySub").text(row.TypeLabel + ": " + ocSummary(row));
    var $grid = $("#ocHistoryGrid");
    if ($grid.data("dxDataGrid")) $grid.dxDataGrid("dispose");
    $grid.empty();
    $("#modalOcHistory").modal("show");
    $.getJSON(ocCfg.urls.history, { id: row.Id }).done(function (result) {
        if (!result || result.status !== true) { showErrorMesgGrowl((result && result.message) || "The history could not be loaded."); return; }
        $grid.dxDataGrid({
            dataSource: result.data, showBorders: true, columnAutoWidth: true, wordWrapEnabled: true,
            noDataText: "No history.", paging: { pageSize: 15 }, pager: { visible: "auto", showInfo: true }, sorting: { mode: "none" },
            columns: [
                { dataField: "at", caption: "Time", width: 140, cellTemplate: function (c, o) { c.text(moment(o.value).format("DD-MM-YYYY HH:mm")); } },
                { dataField: "action", caption: "Action", width: 100 },
                { dataField: "by", caption: "By", width: 150 },
                { dataField: "detail", caption: "Detail" }
            ]
        });
    });
}

// ---------- transfer to the new unit (tahap 2) ----------
var ocTransferPlan = null;

function openTransfer(row) {
    ocActionId = row.Id;
    $("#ocTransferSub").text(row.TypeLabel + ": " + ocSummary(row));
    $("#ocTransferBody").html('<div class="oc-detail-empty">Loading…</div>');
    $("#ocTransferCount").text("");
    $("#btn_transfer").prop("disabled", true);
    $("#modalOcTransfer").modal("show");
    loadTransferPlan(row.Id);
}

// after a move the list is built again: moved items are gone, what is left stays
function loadTransferPlan(id) {
    $.getJSON(ocCfg.urls.transferPlan, { id: id }).done(function (result) {
        if (!result || result.status !== true) {
            $("#ocTransferBody").empty().append($("<p class='oc-note oc-note-warn'>").text((result && result.message) || "The list could not be loaded."));
            return;
        }
        ocTransferPlan = result.plan;
        renderTransfer(result.plan);
    });
}

function renderTransfer(plan) {
    var $b = $("#ocTransferBody").empty();
    if (!plan.Supported) {
        $("<p class='oc-note oc-note-warn'>").text(plan.Message).appendTo($b);
        return;
    }
    $("<p class='oc-note'>").text("Tick what moves to the new unit and press Move Selected. Document numbers do not change. "
        + "Every moved row is kept with its old and new value (Move Log). Moving can be done in several steps.").appendTo($b);
    if (plan.BeforeEffective) $("<p class='oc-note oc-note-warn'>").text("The effective date is still ahead. Moving now makes the new unit the owner before that date.").appendTo($b);

    var movable = plan.Groups.filter(function (g) { return !g.InfoOnly; });
    if (!movable.length) $("<p class='oc-note'>").text("Nothing is left to move for this change. The old codes can be deactivated if no user is left on them.").appendTo($b);

    plan.Groups.forEach(function (g) {
        var $sec = $("<div class='oc-tr-group'>").attr("data-kind", g.Kind).appendTo($b);
        var $head = $("<div class='oc-tr-head'>").appendTo($sec);
        if (!g.InfoOnly) $("<label class='oc-tr-all'>").append($("<input type='checkbox' class='oc-tr-checkall' checked>"), " ").appendTo($head);
        $("<b>").text(g.Title + " (" + g.Items.length + ")").appendTo($head);
        if (g.InfoOnly) $("<span class='oc-tr-info'>").text("information only").appendTo($head);
        $("<div class='oc-sub'>").text(g.Hint || "").appendTo($sec);

        var $t = $("<table class='oc-impact oc-tr-table'>").appendTo($sec), $tb = $("<tbody>").appendTo($t);
        g.Items.forEach(function (it) {
            var $tr = $("<tr>").attr({ "data-kind": it.Kind, "data-key": it.Key }).appendTo($tb);
            if (!g.InfoOnly) $("<td class='oc-tr-check'>").append($("<input type='checkbox' class='oc-tr-item' checked>")).appendTo($tr);
            $("<td>").append(document.createTextNode(it.Label), it.Detail ? $("<span class='oc-sub'>").text(it.Detail) : null).appendTo($tr);
            $("<td class='oc-tr-old'>").text(it.OldUnit || "").appendTo($tr);
            if (!g.InfoOnly) {
                var $td = $("<td class='oc-tr-target'>").appendTo($tr);
                if (plan.ChooseTarget) {
                    var $sel = $("<select class='form-control oc-tr-select'>").appendTo($td);
                    $("<option>").val("").text("- choose -").appendTo($sel);
                    plan.Targets.forEach(function (t) { $("<option>").val(t.Key).text(t.Label).appendTo($sel); });
                    $sel.val(it.DefaultTarget || "");
                } else {
                    var t0 = plan.Targets.filter(function (t) { return t.Key === it.DefaultTarget; })[0] || plan.Targets[0];
                    $td.append($("<i class='fa fa-long-arrow-right'>"), " ", document.createTextNode(t0 ? t0.Label : "-"));
                }
            }
        });
    });

    $b.find(".oc-tr-checkall").on("change", function () {
        $(this).closest(".oc-tr-group").find(".oc-tr-item").prop("checked", this.checked);
        updateTransferCount();
    });
    updateTransferCount();
}

function transferSelections() {
    var list = [];
    $("#ocTransferBody tr[data-key]").each(function () {
        var $tr = $(this);
        if (!$tr.find(".oc-tr-item").is(":checked")) return;
        list.push({ Kind: $tr.data("kind"), Key: String($tr.data("key")), Target: $tr.find(".oc-tr-select").val() || null });
    });
    return list;
}

function updateTransferCount() {
    var sel = transferSelections();
    var missing = sel.filter(function (s) { return ocTransferPlan && ocTransferPlan.ChooseTarget && !s.Target; }).length;
    $("#ocTransferCount").text(sel.length ? sel.length + " selected" + (missing ? " · " + missing + " without a target" : "") : "");
    $("#btn_transfer").prop("disabled", !sel.length || missing > 0);
}

function doTransfer() {
    var sel = transferSelections();
    if (!sel.length) return;
    popUpProgressShow();
    $.ajax({ type: "POST", url: ocCfg.urls.transfer, data: { id: ocActionId, selectionsJson: JSON.stringify(sel) }, dataType: "json" })
        .done(function (result) {
            popUpProgressHide();
            if (result && result.status === true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                loadTransferPlan(ocActionId);
            } else {
                showErrorMesgGrowl((result && result.message) || "Moving failed.");
            }
        })
        .fail(function (xhr) {
            popUpProgressHide();
            showErrorMesgGrowl("Moving failed (HTTP " + xhr.status + "). Refresh the page and try again.");
        });
}

// ---------- move log ----------
function showMoves(row) {
    $("#ocHistorySub").text("Move Log · " + row.TypeLabel + ": " + ocSummary(row));
    var $grid = $("#ocHistoryGrid");
    if ($grid.data("dxDataGrid")) $grid.dxDataGrid("dispose");
    $grid.empty();
    $("#modalOcHistory").modal("show");
    $.getJSON(ocCfg.urls.moves, { id: row.Id }).done(function (result) {
        if (!result || result.status !== true) { showErrorMesgGrowl((result && result.message) || "The move log could not be loaded."); return; }
        $grid.dxDataGrid({
            dataSource: result.data, showBorders: true, columnAutoWidth: true, wordWrapEnabled: true,
            noDataText: "Nothing moved yet.", paging: { pageSize: 15 }, pager: { visible: "auto", showInfo: true },
            searchPanel: { visible: result.data.length > 15, width: 220 }, sorting: { mode: "none" },
            columns: [
                { dataField: "batch", caption: "Batch", width: 60, alignment: "center" },
                { dataField: "at", caption: "Time", width: 130, cellTemplate: function (c, o) { c.text(moment(o.value).format("DD-MM-YYYY HH:mm")); } },
                { dataField: "kind", caption: "Kind", width: 90 },
                { dataField: "label", caption: "Item" },
                { dataField: "oldValue", caption: "Before" },
                { dataField: "newValue", caption: "After" },
                { dataField: "by", caption: "By", width: 130 }
            ]
        });
    });
}
