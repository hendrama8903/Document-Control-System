// FolderTree. Server values come from folderTreeConfig in the view.
var gAllFolders = [];
var gExpandedFolders = new Set();
var gSelectedFolderId = null; // null = "All Documents"
var gFolderCanManage = true; // folders are org structure, not sensitive - anyone reaching this menu can manage them
var gFolderAddingUnder = undefined; // folder id (or null for root) currently showing the inline "new folder" input
var gFolderRenamingId = undefined; // folder id currently showing the inline rename input

function dcdGetFolderChildren(parentId) {
    return gAllFolders.filter(function (f) { return (f.PARENT_ID || null) === (parentId || null); });
}

function dcdGetFolderById(folderId) {
    return gAllFolders.find(function (f) { return f.FOLDER_ID === folderId; });
}

// Ancestor chain root -> selected, for the breadcrumb.
function dcdGetFolderPath(folderId) {
    var path = [];
    var cur = dcdGetFolderById(folderId);
    while (cur) {
        path.unshift(cur);
        cur = cur.PARENT_ID ? dcdGetFolderById(cur.PARENT_ID) : null;
    }
    return path;
}

function loadFolderTree(afterLoad) {
    $.ajax({
        type: "POST",
        url: folderTreeConfig.urls.getFolderTree,
        success: function (result) {
            if (result.status == true) {
                gAllFolders = result.data;
                $("#dcdTotalFolders").text(gAllFolders.length);
                renderFolderTree();
                if (typeof afterLoad === "function") { afterLoad(); }
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) { showErrorMesgGrowl(result); }
    });
}

function renderFolderTree() {
    var $tree = $("#dcdFolderTree").empty();
    var rootDepth = 0;

    if (typeof folderTreeRenderTop === "function") {
        // Page-specific top of the tree (UserDashboard: All Documents, Library, My Folders).
        // Returns the depth for the folders of this tree, or -1 when they are collapsed.
        rootDepth = folderTreeRenderTop($tree);
        if (rootDepth < 0) { return; }
    } else {
        var $all = $("<div>").addClass("dcd-folder-row").toggleClass("selected", gSelectedFolderId === null);
        $("<span>").addClass("dcd-folder-chevron").appendTo($all); // spacer, no children
        $("<i>").addClass("fa fa-folder-open-o dcd-folder-icon").appendTo($all);
        $("<span>").addClass("dcd-folder-name").text("All Documents").appendTo($all);
        $all.on("click", function () { dcdSelectFolder(null); });
        $tree.append($all);
    }

    if (gFolderAddingUnder === null) {
        dcdAppendFolderInput($tree, null, rootDepth);
    }

    dcdRenderFolderLevel($tree, null, rootDepth);

    if (gAllFolders.length === 0 && gFolderAddingUnder !== null) {
        $("<div>").addClass("mf-empty-state").text("No folders yet - click + to create one.").appendTo($tree);
    }
}

function dcdRenderFolderLevel($container, parentId, depth) {
    dcdGetFolderChildren(parentId).forEach(function (folder) {
        var children = dcdGetFolderChildren(folder.FOLDER_ID);
        var hasChildren = children.length > 0;
        var expanded = gExpandedFolders.has(folder.FOLDER_ID);

        if (gFolderRenamingId === folder.FOLDER_ID) {
            var $editRow = $("<div>").addClass("dcd-folder-input-row").css("padding-left", (8 + depth * 18) + "px");
            var $input = $("<input>").attr("type", "text").val(folder.FOLDER_NAME).appendTo($editRow);
            dcdWireFolderInput($input, function (name) { dcdConfirmRenameFolder(folder.FOLDER_ID, name); }, function () { gFolderRenamingId = undefined; renderFolderTree(); });
            $container.append($editRow);
            setTimeout(function () { $input.focus().select(); }, 0);
        } else {
            var $row = $("<div>").addClass("dcd-folder-row").toggleClass("selected", gSelectedFolderId === folder.FOLDER_ID).css("padding-left", (8 + depth * 18) + "px");
            var $chevron = $("<span>").addClass("dcd-folder-chevron fa fa-chevron-right").toggleClass("expanded", expanded).css("visibility", hasChildren ? "visible" : "hidden").appendTo($row);
            $chevron.on("click", function (e) { e.stopPropagation(); dcdToggleFolderExpand(folder.FOLDER_ID); });
            // A binder shows its own icon, its label code, and for a controlled copy binder the
            // copies in circulation instead of documents - red while an obsolete copy is in it.
            var isCopyBinder = folder.FOLDER_TYPE === "COPY";
            var obsoleteCopies = isCopyBinder ? (folder.OBSOLETE_COPY_COUNT || 0) : 0;
            $row.attr("title", dcdFolderTooltip(folder));
            $("<i>").addClass("fa " + dcdFolderIconClass(folder) + " dcd-folder-icon").appendTo($row);
            var $name = $("<span>").addClass("dcd-folder-name").appendTo($row);
            if (folder.FOLDER_CODE && folder.FOLDER_CODE !== folder.FOLDER_NAME) {
                $("<span>").addClass("dcd-folder-code").text(folder.FOLDER_CODE).appendTo($name);
            }
            $name.append(document.createTextNode(folder.FOLDER_NAME));
            $("<span>").addClass("dcd-folder-count").toggleClass("dcd-folder-count-alert", obsoleteCopies > 0)
                .text(isCopyBinder ? (folder.COPY_COUNT || 0) : (folder.DOCUMENT_COUNT || 0)).appendTo($row);
            if (gFolderCanManage) { dcdAppendFolderKebab($row, folder); }

            $row.on("click", function (e) {
                if ($(e.target).closest(".dp-act-kebab, .dp-dropdown-menu").length > 0) { return; }
                dcdSelectFolder(folder.FOLDER_ID);
            });

            $container.append($row);
        }

        if (expanded) {
            if (gFolderAddingUnder === folder.FOLDER_ID) {
                dcdAppendFolderInput($container, folder.FOLDER_ID, depth + 1);
            }
            dcdRenderFolderLevel($container, folder.FOLDER_ID, depth + 1);
        }
    });
}

function dcdAppendFolderInput($container, parentId, depth) {
    var $row = $("<div>").addClass("dcd-folder-input-row").css("padding-left", (8 + (depth || 0) * 18) + "px");
    var $input = $("<input>").attr("type", "text").attr("placeholder", "Folder name").appendTo($row);
    dcdWireFolderInput($input, function (name) { dcdConfirmCreateFolder(parentId, name); }, function () { gFolderAddingUnder = undefined; renderFolderTree(); });
    $container.append($row);
    setTimeout(function () { $input.focus(); }, 0);
}

// "settled" guards against a double-submit: confirming via Enter re-renders the
// tree (async, once the save completes), which removes this <input> from the DOM -
// and browsers fire a native blur when a focused element is removed, which would
// otherwise re-trigger the deferred blur-confirm below with the same value.
function dcdWireFolderInput($input, onConfirm, onCancel) {
    var settled = false;
    function confirmOnce(val) { if (settled) { return; } settled = true; onConfirm(val); }
    function cancelOnce() { if (settled) { return; } settled = true; onCancel(); }

    $input.on("keydown", function (e) {
        if (e.key === "Enter") {
            var val = $input.val().trim();
            if (val) { confirmOnce(val); } else { cancelOnce(); }
        } else if (e.key === "Escape") {
            cancelOnce();
        }
    });
    $input.on("blur", function () {
        setTimeout(function () {
            if (settled) { return; }
            var val = $input.val().trim();
            if (val) { confirmOnce(val); } else { cancelOnce(); }
        }, 120);
    });
}

function dcdToggleFolderExpand(folderId) {
    if (gExpandedFolders.has(folderId)) { gExpandedFolders.delete(folderId); } else { gExpandedFolders.add(folderId); }
    renderFolderTree();
}

function dcdSelectFolder(folderId) {
    gSelectedFolderId = folderId;
    renderFolderTree();
    if (typeof dcdApplyFolderFilter === "function") { dcdApplyFolderFilter(folderId); }
}

function dcdAppendFolderKebab($row, folder) {
    var $btn = $("<button>").attr("type", "button").addClass("dp-act-kebab").html('<i class="fa fa-ellipsis-v"></i>')
        .on("click", function (e) {
            e.stopPropagation();
            var already = $btn.data("menu-open");
            dcdCloseFolderMenu();
            if (already) { $btn.data("menu-open", false); return; }
            dcdOpenFolderMenu($btn, folder);
            $btn.data("menu-open", true);
        });
    $row.append($btn);
}

function dcdCloseFolderMenu() { $(".dcd-folder-dropdown-menu").remove(); }

// ---------- Binders (bantex), Document Control only (request Hendra 2026-11-15) ----------
// A folder can be a physical binder: a master binder kept by Document Control (holds
// documents) or a controlled copy binder kept by a department (holds numbered copies).
// See SQL/DocumentBinder_Migration.sql. My Documents has no saveBinder url, so its
// personal folders never show any of this.
function dcdBinderTypeLabel(folder) {
    if (folder.FOLDER_TYPE === "COPY") { return "Controlled Copy Binder"; }
    if (folder.FOLDER_TYPE === "MASTER") { return "Master Binder"; }
    return "Folder";
}

function dcdFolderIconClass(folder) {
    if (folder.FOLDER_TYPE === "COPY") { return "fa-book"; }
    if (folder.FOLDER_TYPE === "MASTER") { return "fa-archive"; }
    return "fa-folder-o";
}

function dcdBinderHolder(folder) {
    return folder.DEPARTMENT_CODE ? folder.DEPARTMENT_CODE + " - " + (folder.DEPARTMENT_NAME || "") : "";
}

function dcdFolderTooltip(folder) {
    if (!folder.FOLDER_TYPE) { return folder.FOLDER_NAME; }
    var lines = [dcdBinderTypeLabel(folder) + (folder.FOLDER_CODE ? " " + folder.FOLDER_CODE : "")];
    if (dcdBinderHolder(folder)) { lines.push("Holder: " + dcdBinderHolder(folder)); }
    if (folder.LOCATION) { lines.push("Location: " + folder.LOCATION); }
    if (folder.FOLDER_TYPE === "COPY" && folder.OBSOLETE_COPY_COUNT > 0) {
        lines.push(folder.OBSOLETE_COPY_COUNT + " obsolete cop" + (folder.OBSOLETE_COPY_COUNT > 1 ? "ies" : "y") + " to withdraw");
    }
    return lines.join("\n");
}

var gBinderDepartments = null; // loaded once, on the first Binder Details

function dcdOpenBinderDetails(folder) {
    if (gBinderDepartments) { dcdShowBinderDetails(folder); return; }
    $.ajax({
        type: "GET",
        url: folderTreeConfig.urls.getBinderDepartments,
        cache: false,
        success: function (result) {
            if (result.status == true) {
                gBinderDepartments = result.data || [];
                dcdShowBinderDetails(folder);
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function () { showErrorMesgGrowl("Failed to load the departments"); }
    });
}

// The popup (#modalBinder, _BinderDetailsPopup.cshtml) replaced the generic message box
// (2026-11-15): type as three cards, the binder fields only for a binder, errors under the
// field they belong to.
var gBinderEditing = null;

function dcdBinderType() {
    return $("#bdfTypes .bdf-type.selected").data("type") || "";
}

function dcdSetBinderType(type) {
    $("#bdfTypes .bdf-type").each(function () {
        $(this).toggleClass("selected", ($(this).data("type") || "") === (type || ""));
    });
    // a plain folder has no label, holder or location
    $("#bdfBinderFields").toggle(!!type);
    // the holder is required only for a binder kept by a department
    $("#bdfHolderRequired").toggle(type === "COPY");
    dcdClearBinderErrors();
}

function dcdClearBinderErrors() {
    $("#modalBinder .bdf-error").removeClass("show").text("");
    $("#modalBinder .bdf-invalid").removeClass("bdf-invalid");
}

function dcdBinderError(errorId, inputId, message) {
    $("#" + errorId).text(message).addClass("show");
    if (inputId) { $("#" + inputId).addClass("bdf-invalid").trigger("focus"); }
}

function dcdShowBinderDetails(folder) {
    gBinderEditing = folder;

    // departments grouped by division, in the order the server sent them
    var $department = $("#bdfDepartment").empty().append($("<option>").val("").text("Choose a department"));
    var $group = null, division = null;
    gBinderDepartments.forEach(function (d) {
        if (d.DIVISION !== division) {
            division = d.DIVISION;
            $group = $("<optgroup>").attr("label", division || "-").appendTo($department);
        }
        $("<option>").val(d.DEPARTMENT_ID).text(d.DEPARTMENT_CODE + " - " + d.DEPARTMENT_NAME).appendTo($group);
    });

    $("#bdfFolderName").text(folder.FOLDER_NAME);
    $("#bdfCode").val(folder.FOLDER_CODE || "");
    $department.val(folder.DEPARTMENT_ID || "");
    $("#bdfLocation").val(folder.LOCATION || "");
    $("#bdfRemarks").val(folder.REMARKS || "");
    dcdSetBinderType(folder.FOLDER_TYPE || "");
    $("#btn_saveBinder").prop("disabled", false);
    $("#modalBinder").modal("show");
}

function dcdSaveBinderDetails() {
    var folder = gBinderEditing;
    if (!folder) { return; }
    var type = dcdBinderType();
    var code = $.trim($("#bdfCode").val());
    var departmentId = $("#bdfDepartment").val();

    dcdClearBinderErrors();
    if (type && !code) { dcdBinderError("bdfCodeError", "bdfCode", "Enter the binder code written on its label."); return; }
    if (type === "COPY" && !departmentId) { dcdBinderError("bdfDepartmentError", "bdfDepartment", "Choose the department that holds this binder."); return; }

    $("#btn_saveBinder").prop("disabled", true);
    $.ajax({
        type: "POST",
        url: folderTreeConfig.urls.saveBinder,
        // a plain folder keeps no binder data
        data: {
            folderId: folder.FOLDER_ID,
            folderType: type,
            folderCode: type ? code : "",
            departmentId: type ? departmentId : "",
            location: type ? $.trim($("#bdfLocation").val()) : "",
            remarks: $.trim($("#bdfRemarks").val())
        },
        success: function (result) {
            $("#btn_saveBinder").prop("disabled", false);
            if (result.status == true) {
                $("#modalBinder").modal("hide");
                showSuccessMesgGrowl(result.message);
                // the open folder may have changed type: refresh its header and grid filter
                loadFolderTree(function () { if (gSelectedFolderId === folder.FOLDER_ID) { dcdSelectFolder(folder.FOLDER_ID); } });
            } else if (/code is already used/i.test(result.message || "")) {
                dcdBinderError("bdfCodeError", "bdfCode", result.message);
            } else {
                // e.g. the folder still holds documents / copies of the other kind
                dcdBinderError("bdfFormError", null, result.message || "Failed to save the binder details");
            }
        },
        error: function () {
            $("#btn_saveBinder").prop("disabled", false);
            dcdBinderError("bdfFormError", null, "Failed to save the binder details");
        }
    });
}

$(document).ready(function () {
    $("#bdfTypes").on("click", ".bdf-type", function () { dcdSetBinderType($(this).data("type") || ""); });
    $("#btn_saveBinder").on("click", dcdSaveBinderDetails);
    $("#modalBinder").on("keydown", "input", function (e) { if (e.key === "Enter") { dcdSaveBinderDetails(); } });
    // first field of the form once the popup is open
    $("#modalBinder").on("shown.bs.modal", function () { if (dcdBinderType()) { $("#bdfCode").trigger("focus"); } });
});

function dcdOpenFolderMenu($btn, folder) {
    var $menu = $("<ul>").addClass("dropdown-menu dp-dropdown-menu dcd-folder-dropdown-menu")
        .css({ position: "fixed", "z-index": 2000, "min-width": "170px", background: "#fff", margin: 0, "list-style": "none" });

    $("<li>").append($("<button>").attr("type", "button").html('<i class="fa fa-plus"></i> Add Subfolder').on("click", function (e) {
        e.stopPropagation(); dcdCloseFolderMenu();
        gExpandedFolders.add(folder.FOLDER_ID);
        gFolderAddingUnder = folder.FOLDER_ID;
        renderFolderTree();
    })).appendTo($menu);

    $("<li>").append($("<button>").attr("type", "button").html('<i class="fa fa-pencil"></i> Rename').on("click", function (e) {
        e.stopPropagation(); dcdCloseFolderMenu();
        gFolderRenamingId = folder.FOLDER_ID;
        renderFolderTree();
    })).appendTo($menu);

    if (folderTreeConfig.urls.saveBinder) {
        $("<li>").append($("<button>").attr("type", "button").html('<i class="fa fa-book"></i> Binder Details').on("click", function (e) {
            e.stopPropagation(); dcdCloseFolderMenu();
            dcdOpenBinderDetails(folder);
        })).appendTo($menu);
    }

    $("<li>").append($("<button>").attr("type", "button").html('<i class="fa fa-trash-o"></i> Delete').on("click", function (e) {
        e.stopPropagation(); dcdCloseFolderMenu();
        showDangerConfirm(deleteMessage("folder", folder.FOLDER_NAME), function () { dcdConfirmDeleteFolder(folder.FOLDER_ID); }, "Delete");
    })).appendTo($menu);

    $menu.appendTo("body");
    var offset = $btn.offset();
    // Bootstrap's base .dropdown-menu class defaults to display:none - without
    // this explicit .show(), the menu is created/positioned correctly but stays
    // invisible (bug found 2026-08-29: kebab click did nothing visible).
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() }).show();
}

$(document).on("click", function () { dcdCloseFolderMenu(); });

function dcdConfirmCreateFolder(parentId, folderName) {
    $.ajax({
        type: "POST",
        url: folderTreeConfig.urls.createFolder,
        data: { folderName: folderName, parentId: parentId },
        success: function (result) {
            gFolderAddingUnder = undefined;
            if (result.status == true) {
                showSuccessMesgGrowl("Folder created");
                if (parentId != null) { gExpandedFolders.add(parentId); }
                loadFolderTree();
            } else {
                showErrorMesgGrowl(result.message);
                renderFolderTree();
            }
        },
        error: function (result) { showErrorMesgGrowl(result); renderFolderTree(); }
    });
}

function dcdConfirmRenameFolder(folderId, folderName) {
    $.ajax({
        type: "POST",
        url: folderTreeConfig.urls.renameFolder,
        data: { folderId: folderId, folderName: folderName },
        success: function (result) {
            gFolderRenamingId = undefined;
            if (result.status == true) {
                showSuccessMesgGrowl("Folder renamed");
                loadFolderTree();
            } else {
                showErrorMesgGrowl(result.message);
                renderFolderTree();
            }
        },
        error: function (result) { showErrorMesgGrowl(result); renderFolderTree(); }
    });
}

function dcdConfirmDeleteFolder(folderId) {
    $.ajax({
        type: "POST",
        url: folderTreeConfig.urls.deleteFolder,
        data: { folderId: folderId },
        success: function (result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                if (gSelectedFolderId === folderId) { dcdSelectFolder(null); }
                loadFolderTree();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) { showErrorMesgGrowl(result); }
    });
}

// ---------- flat indented picker, reused by _GridView.cshtml's "Move to Folder" ----------
function dcdFlattenFolders(parentId, depth, out) {
    dcdGetFolderChildren(parentId).forEach(function (folder) {
        out.push({ folder: folder, depth: depth });
        dcdFlattenFolders(folder.FOLDER_ID, depth + 1, out);
    });
    return out;
}

// copyBinders = true: pick a controlled copy binder for a copy (flat list, with holder);
// otherwise pick a folder / master binder for documents - a controlled copy binder holds
// copies, not documents, so it is left out there (2026-11-15).
function dcdOpenFolderPicker($anchor, onPick, copyBinders) {
    $(".dcd-folder-picker").remove();

    var $picker = $("<div>").addClass("dcd-folder-picker");
    $("<div>").addClass("dcd-folder-picker-item").text(copyBinders ? "Not in a binder" : "All Documents (unassigned)").on("click", function () {
        $picker.remove(); onPick(null);
    }).appendTo($picker);

    var entries = dcdFlattenFolders(null, 0, []).filter(function (entry) {
        return (entry.folder.FOLDER_TYPE === "COPY") === !!copyBinders;
    });
    if (copyBinders && entries.length === 0) {
        $("<div>").addClass("dcd-folder-picker-empty").text("No controlled copy binder yet. Open Binder Details of a folder to make one.").appendTo($picker);
    }
    entries.forEach(function (entry) {
        var folder = entry.folder;
        var label = copyBinders
            ? (folder.FOLDER_CODE || folder.FOLDER_NAME) + (folder.DEPARTMENT_CODE ? " · " + folder.DEPARTMENT_CODE : "")
            : folder.FOLDER_NAME;
        $("<div>").addClass("dcd-folder-picker-item").css("padding-left", (10 + (copyBinders ? 0 : entry.depth) * 14) + "px")
            .text(label)
            .on("click", function () { $picker.remove(); onPick(folder.FOLDER_ID); })
            .appendTo($picker);
    });

    $picker.appendTo("body");
    var offset = $anchor.offset();
    $picker.css({ top: offset.top + $anchor.outerHeight() + 4, left: offset.left });

    setTimeout(function () {
        $(document).one("click.dcdFolderPicker", function () { $picker.remove(); });
    }, 0);
}

$("#btn_addRootFolder").on("click", function () {
    gFolderAddingUnder = null;
    renderFolderTree();
});

// setFoldersPanelCollapsed ada di _GridView masing-masing halaman
// (DocumentControlDashboard & UserDashboard)
$("#btn_hideFolders").on("click", function () {
    setFoldersPanelCollapsed(true);
});

$(document).ready(function () {
    loadFolderTree();
});
