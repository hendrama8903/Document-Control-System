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

    var $all = $("<div>").addClass("dcd-folder-row").toggleClass("selected", gSelectedFolderId === null);
    $("<span>").addClass("dcd-folder-chevron").appendTo($all); // spacer, no children
    $("<i>").addClass("fa fa-folder-open-o dcd-folder-icon").appendTo($all);
    $("<span>").addClass("dcd-folder-name").text("All Documents").appendTo($all);
    $all.on("click", function () { dcdSelectFolder(null); });
    $tree.append($all);

    if (gFolderAddingUnder === null) {
        dcdAppendFolderInput($tree, null);
    }

    dcdRenderFolderLevel($tree, null, 0);

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
            $("<i>").addClass("fa fa-folder-o dcd-folder-icon").appendTo($row);
            $("<span>").addClass("dcd-folder-name").text(folder.FOLDER_NAME).appendTo($row);
            $("<span>").addClass("dcd-folder-count").text(folder.DOCUMENT_COUNT || 0).appendTo($row);
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

function dcdOpenFolderPicker($anchor, onPick) {
    $(".dcd-folder-picker").remove();

    var $picker = $("<div>").addClass("dcd-folder-picker");
    $("<div>").addClass("dcd-folder-picker-item").text("All Documents (unassigned)").on("click", function () {
        $picker.remove(); onPick(null);
    }).appendTo($picker);

    dcdFlattenFolders(null, 0, []).forEach(function (entry) {
        $("<div>").addClass("dcd-folder-picker-item").css("padding-left", (10 + entry.depth * 14) + "px")
            .text(entry.folder.FOLDER_NAME)
            .on("click", function () { $picker.remove(); onPick(entry.folder.FOLDER_ID); })
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
