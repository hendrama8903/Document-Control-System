// StampLayout. Server values come from stampLayoutConfig in the view; the position maths is in
// stamp-layout-core.js (shared with the stamp dialog in Document Registration).
var SL_PAGE = { P: { w: 595.28, h: 841.89 }, L: { w: 841.89, h: 595.28 } };

var gSl = {
    scopeId: null,          // null = global default
    orientation: "P",
    stamp: "MASTER",
    positions: {},          // "TYPE|O" -> { x, y, w } in percent
    saved: {},
    isCustom: true,         // what is shown is this scope's own layout (editable)
    savedIsCustom: true,
    dirty: false,
    aspect: {},             // image height / width per stamp type
    drag: null
};

function slPage() { return SL_PAGE[gSl.orientation]; }

function slPlace(type) {
    var page = slPage();
    return StampCore.place(stampLayoutConfig, gSl.positions, gSl.aspect, type, page.w, page.h);
}

function slSetPt(type, x, y, w) {
    var page = slPage();
    StampCore.setPt(stampLayoutConfig, gSl.positions, gSl.aspect, type, page.w, page.h, x, y, w);
}

function slEditable() { return stampLayoutConfig.canEdit && gSl.isCustom; }

function slMarkDirty() {
    gSl.dirty = true;
    slRender();
}

function slRender() {
    if (!gSl.positions[StampCore.key(gSl.stamp, gSl.orientation)]) return; // not loaded yet
    var o = gSl.orientation, page = slPage();
    var $page = $("#slPage").toggleClass("landscape", o === "L");
    var scale = $page.innerWidth() / page.w;
    var safe = stampLayoutConfig.safeMarginPt * scale;

    $page.find(".sl-safe").css({ left: safe, top: safe, right: safe, bottom: safe });

    $page.find(".sl-box").each(function () {
        var type = $(this).data("stamp");
        var r = slPlace(type);
        $(this).css({ left: r.x * scale, top: r.y * scale, width: r.w * scale })
            .toggleClass("active", type === gSl.stamp)
            .toggleClass("editable", slEditable());
        $(this).find(".sl-note").css("height", (stampLayoutConfig.noteHeightPt - 3) * scale);
    });

    var cur = slPlace(gSl.stamp);
    var editable = slEditable();
    $("#slPanelTitle").text(StampCore.NAMES[gSl.stamp] + " · " + (o === "P" ? "Portrait" : "Landscape"));
    $("#slWidth").val(Math.round(cur.w / StampCore.MM)).prop("disabled", !editable);
    $("#slWidthText").text(Math.round(cur.w / StampCore.MM) + " mm");
    if (!$("#slX").is(":focus")) $("#slX").val(Math.round(cur.x / StampCore.MM));
    if (!$("#slY").is(":focus")) $("#slY").val(Math.round(cur.y / StampCore.MM));
    $("#slX, #slY, #slGrid9 button").prop("disabled", !editable);

    var isGlobal = gSl.scopeId === null;
    $("#slSave, #slRevert").toggle(editable || gSl.dirty);
    $("#slSave").prop("disabled", !gSl.dirty);
    $("#slRevert").prop("disabled", !gSl.dirty);
    $("#slCustomize").toggle(!isGlobal && !gSl.isCustom);
    $("#slRestore").toggle(isGlobal);
    $("#slUseGlobal").toggle(!isGlobal && gSl.savedIsCustom);

    var $banner = $("#slBanner").removeClass("visible info");
    if (isGlobal) {
        $banner.addClass("visible info").text("The global layout applies to every document category that has no layout of its own. "
            + "Positions are fixed on each document when it is accepted in Document Registration.");
    } else if (!gSl.isCustom) {
        $banner.addClass("visible").text("This category uses the global layout." +
            (stampLayoutConfig.canEdit ? " Click Customize for This Category to give it its own positions." : ""));
    } else if (!gSl.savedIsCustom) {
        $banner.addClass("visible info").text("New layout for this category. Save to apply it; until then the global layout is used.");
    } else {
        $banner.addClass("visible info").text("This category has its own layout. Use Global Layout removes it.");
    }
}

function slLoad(scopeId) {
    $.ajax({
        type: "GET",
        url: stampLayoutConfig.urls.get,
        data: scopeId === null ? {} : { documentId: scopeId },
        dataType: "json",
        success: function (result) {
            if (result.status != true) {
                showErrorMesgGrowl(result.message);
                return;
            }
            gSl.scopeId = scopeId;
            gSl.positions = StampCore.toMap(result.positions);
            gSl.saved = StampCore.clone(gSl.positions);
            gSl.isCustom = gSl.savedIsCustom = result.isCustom === true;
            gSl.dirty = false;
            $(".sl-scope").removeClass("active")
                .filter(function () { return String($(this).data("id")) === (scopeId === null ? "" : String(scopeId)); })
                .addClass("active");
            slRender();
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}

function slConfirmDiscard(next) {
    if (!gSl.dirty) {
        next();
        return;
    }
    showConfirmMesg("Discard the unsaved stamp positions?", next, "Unsaved Changes", "Discard", "Cancel");
}

function slSetBadge(scopeId, custom) {
    $(".sl-scope").filter(function () { return String($(this).data("id")) === String(scopeId); })
        .find(".sl-badge").toggleClass("custom", custom).text(custom ? "Custom" : "Global");
}

function slSave() {
    var scopeId = gSl.scopeId;
    // Accepted documents keep the positions fixed on Accept (Stamp Layout stage 3).
    var message = (scopeId === null
        ? "Save the global layout? It applies to every category without its own layout."
        : "Save the layout for this category?")
        + " Documents already accepted in Document Registration keep their stamp positions; change those there with Stamp Position.";
    showConfirmMesg(message, function () {
        popUpProgressShow();
        $.ajax({
            type: "POST",
            url: stampLayoutConfig.urls.save,
            dataType: "json",
            data: { documentId: scopeId, positions: JSON.stringify(StampCore.toList(gSl.positions)) },
            success: function (result) {
                popUpProgressHide();
                if (result.status != true) {
                    showErrorMesgGrowl(result.message);
                    return;
                }
                gSl.saved = StampCore.clone(gSl.positions);
                gSl.savedIsCustom = true;
                gSl.dirty = false;
                if (scopeId !== null) slSetBadge(scopeId, true);
                slRender();
                showSuccessMesgGrowl(result.message);
            },
            error: function (result) {
                popUpProgressHide();
                showErrorMesgGrowl(result);
            }
        });
    }, "Confirmation", "Save", "Cancel");
}

function slUseGlobal() {
    var scopeId = gSl.scopeId;
    showConfirmMesg("Remove this category's own layout? Its documents will use the global layout again.", function () {
        popUpProgressShow();
        $.ajax({
            type: "POST",
            url: stampLayoutConfig.urls.useGlobal,
            dataType: "json",
            data: { documentId: scopeId },
            success: function (result) {
                popUpProgressHide();
                if (result.status != true) {
                    showErrorMesgGrowl(result.message);
                    return;
                }
                slSetBadge(scopeId, false);
                gSl.dirty = false;
                slLoad(scopeId);
                showSuccessMesgGrowl(result.message);
            },
            error: function (result) {
                popUpProgressHide();
                showErrorMesgGrowl(result);
            }
        });
    }, "Use Global Layout", "Remove", "Cancel", undefined, "msg-danger");
}

function slPreview() {
    var $form = $('<form method="post" target="_blank"></form>').attr("action", stampLayoutConfig.urls.preview);
    $('<input type="hidden" name="positions">').val(JSON.stringify(StampCore.toList(gSl.positions))).appendTo($form);
    $form.appendTo("body").trigger("submit").remove();
}

function slSelectStamp(type) {
    gSl.stamp = type;
    $(".sl-stamp-btn").removeClass("active").filter("[data-stamp='" + type + "']").addClass("active");
    slRender();
}

function slBindDrag() {
    $("#slPage").on("pointerdown", ".sl-box", function (e) {
        var type = $(this).data("stamp");
        if (type !== gSl.stamp) {
            slSelectStamp(type);
            return;
        }
        if (!slEditable()) return;
        var r = slPlace(type);
        gSl.drag = { startX: e.clientX, startY: e.clientY, x: r.x, y: r.y, w: r.w };
        this.setPointerCapture(e.pointerId);
        e.preventDefault();
    }).on("pointermove", ".sl-box", function (e) {
        if (!gSl.drag) return;
        var scale = $("#slPage").innerWidth() / slPage().w;
        var d = gSl.drag;
        slSetPt(gSl.stamp, d.x + (e.clientX - d.startX) / scale, d.y + (e.clientY - d.startY) / scale, d.w);
        gSl.dirty = true;
        slRender();
    }).on("pointerup pointercancel", ".sl-box", function () {
        gSl.drag = null;
    });
}

$(document).ready(function () {
    // Grid buttons draw their own little stamp marker at their position.
    $("#slGrid9 button").each(function () {
        this.style.setProperty("--h", $(this).data("h"));
        this.style.setProperty("--v", $(this).data("v"));
    });

    StampCore.loadAspects($("#slPage"), gSl.aspect, slRender);
    slBindDrag();
    slLoad(null);

    $(".sl-scope").on("click", function () {
        var raw = $(this).data("id");
        var scopeId = raw === "" || raw === undefined ? null : Number(raw);
        if (scopeId === gSl.scopeId) return;
        slConfirmDiscard(function () { slLoad(scopeId); });
    });

    $("#slScopeSearch").on("input", function () {
        var q = $.trim($(this).val()).toLowerCase();
        $("#slScopeList .sl-scope").each(function () {
            $(this).toggle(!q || String($(this).data("search")).indexOf(q) >= 0);
        });
    });

    $(".sl-seg-btn").on("click", function () {
        gSl.orientation = $(this).data("orientation");
        $(".sl-seg-btn").removeClass("active");
        $(this).addClass("active");
        slRender();
    });

    $(".sl-stamp-btn").on("click", function () {
        slSelectStamp($(this).data("stamp"));
    });

    $("#slGrid9 button").on("click", function () {
        var page = slPage(), r = slPlace(gSl.stamp);
        var q = StampCore.quick(stampLayoutConfig, r, page.w, page.h, $(this).data("h"), $(this).data("v"));
        slSetPt(gSl.stamp, q.x, q.y, r.w);
        slMarkDirty();
    });

    $("#slWidth").on("input", function () {
        var r = slPlace(gSl.stamp);
        var w = Number($(this).val()) * StampCore.MM;
        // Grow or shrink around the stamp's centre.
        slSetPt(gSl.stamp, r.x + (r.w - w) / 2, r.y, w);
        slMarkDirty();
    });

    $("#slX, #slY").on("change", function () {
        var r = slPlace(gSl.stamp);
        var x = $("#slX").val() === "" ? r.x : Number($("#slX").val()) * StampCore.MM;
        var y = $("#slY").val() === "" ? r.y : Number($("#slY").val()) * StampCore.MM;
        slSetPt(gSl.stamp, x, y, r.w);
        $(this).blur();
        slMarkDirty();
    });

    $("#slSave").on("click", slSave);
    $("#slRevert").on("click", function () {
        gSl.positions = StampCore.clone(gSl.saved);
        gSl.isCustom = gSl.savedIsCustom;
        gSl.dirty = false;
        slRender();
    });
    $("#slCustomize").on("click", function () {
        gSl.isCustom = true;
        slMarkDirty();
    });
    $("#slRestore").on("click", function () {
        showConfirmMesg("Put every stamp back to the system default position (Master top left, others bottom centre)? Nothing is saved until you click Save.", function () {
            gSl.positions = StampCore.toMap(stampLayoutConfig.builtInDefaults);
            slMarkDirty();
        }, "Restore System Default", "Restore", "Cancel");
    });
    $("#slUseGlobal").on("click", slUseGlobal);
    $("#slPreview").on("click", slPreview);

    $(window).on("resize", slRender);
    $(window).on("beforeunload", function () {
        if (gSl.dirty) return "You have unsaved stamp positions.";
    });
});
