// Accept Registration with stamp positions (Stamp Layout stage 2, Hendra 2026-11-11), and
// Change Stamp Position after Accept with a reason (stage 3). Opened from onAcceptPrepare /
// the "Stamp Position" row menu in p4d-maintenance.js for users with P4D-STAMPLAYOUT. Draws the
// real document pages (PDF.js) with the stamps where the PDF will get them; the position maths
// is in stamp-layout-core.js. Config: p4dMaintenanceConfig (stampLimits, urls).
var gSp = {
    ctrlId: null,
    docNo: null,
    mode: "category",       // "category" = follow the category layout, "custom" = this revision only
    categoryMap: {},
    customMap: {},
    pageMap: {},            // "TYPE#page" -> { x, y, w, o }: this page only (custom mode, stage 4)
    aspect: {},
    pdf: null,
    pages: [],              // index = page number: { w, h } in points
    current: 1,
    stamp: "MASTER",
    scale: 1,
    drag: null,
    renderTask: null,
    loadToken: 0
};

function spLimits() { return p4dMaintenanceConfig.stampLimits; }
function spEditable() { return gSp.mode === "custom"; }
function spPageSize(n) { return gSp.pages[n || gSp.current] || { w: 595.28, h: 841.89 }; }
function spPageKey(type, n) { return type + "#" + n; }

// Own position of a stamp on page n (custom mode only), or null when it follows all pages.
function spOwn(type, n) {
    if (!spEditable()) return null;
    var own = gSp.pageMap[spPageKey(type, n)], size = spPageSize(n);
    return own && own.o === StampCore.orientationOf(size.w, size.h) ? own : null;
}

// Positions used on page n: the category or this document's set, with the page's own positions.
function spMapFor(n) {
    var base = spEditable() ? gSp.customMap : gSp.categoryMap;
    var map = null;
    $.each(StampCore.TYPES, function (_, type) {
        var own = spOwn(type, n);
        if (!own) return;
        map = map || $.extend({}, base);
        map[StampCore.key(type, own.o)] = { x: own.x, y: own.y, w: own.w };
    });
    return map || base;
}

function spPlace(type, n) {
    n = n || gSp.current;
    var size = spPageSize(n);
    return StampCore.place(spLimits(), spMapFor(n), gSp.aspect, type, size.w, size.h);
}

// Pages where a stamp has its own position.
function spOwnPages(type) {
    var list = [];
    $.each(gSp.pageMap, function (k) {
        var parts = k.split("#");
        if (parts[0] === type && spOwn(type, Number(parts[1]))) list.push(Number(parts[1]));
    });
    return list.sort(function (a, b) { return a - b; });
}

function spPageList(positions) {
    var map = {};
    $.each(positions || [], function (_, p) {
        if (p.PAGE_NO == null) return;
        map[spPageKey(p.STAMP_TYPE, p.PAGE_NO)] = { x: Number(p.POS_X), y: Number(p.POS_Y), w: Number(p.WIDTH_PCT), o: p.ORIENTATION };
    });
    return map;
}

function spOpen(ctrlId, docNo) {
    $.ajax({
        type: "GET",
        url: p4dMaintenanceConfig.urls.stampLayoutInfo,
        data: { documentCtrlId: ctrlId },
        dataType: "json",
        success: function (result) {
            if (result.status != true) {
                showErrorMesgGrowl(result.message);
                return;
            }
            gSp.ctrlId = ctrlId;
            gSp.docNo = docNo;
            gSp.dialogMode = result.mode; // "accept" or "change" (after Accept, reason required)
            gSp.categoryMap = StampCore.toMap(result.categoryPositions);
            gSp.customMap = result.documentPositions ? StampCore.toMap(result.documentPositions) : StampCore.clone(gSp.categoryMap);
            gSp.pageMap = spPageList(result.documentPositions);
            // Change: start from the positions fixed on the document. Accept: an inherited
            // custom set (from the previous revision) stays selected, otherwise the category.
            gSp.mode = result.documentPositions && (result.mode === "change" || result.documentSource === "CUSTOM") ? "custom" : "category";
            gSp.stamp = "MASTER";
            gSp.current = 1;
            gSp.pages = [];
            gSp.pdf = null;

            var isChange = result.mode === "change";
            var categoryText = result.categoryName + (result.categoryHasOwn ? "" : " · global");
            $("#spTitle").text(isChange ? "Change Stamp Position" : "Accept Registration");
            $("#spAccept").html(isChange ? '<i class="fa fa-save"></i> Save Changes' : '<i class="fa fa-check"></i> Accept');
            $("#spSubtitle").text(docNo + " · Rev." + ("0" + result.revision).slice(-2));
            $("#spIntro").text(isChange
                ? "Copies already downloaded or printed keep their stamps; the new positions apply to copies made from now on."
                : "Accepting makes the status Accepted so the document can be distributed, and fixes these stamp positions on it.");
            $("#spCategoryName").text(categoryText);
            $("#spCustomHint").text(isChange ? "Positions fixed on this document" : "This revision only");
            $("#spReasonField").toggleClass("visible", isChange);
            $("#spReason").val("");

            var $banner = $("#spBanner").removeClass("visible info");
            if (!isChange && result.inheritedFrom) {
                $banner.addClass("visible info").text("Stamp positions taken from the previous revision (" + result.inheritedFrom.replace("From ", "")
                    + "). Check that they still fit this revision.");
            }
            spRenderHistory(result.history || []);
            $("input[name='spMode'][value='" + gSp.mode + "']").prop("checked", true);
            $("input[name='spMode'][value='custom']").prop("disabled", false);
            $(".sp-stamp-btn").removeClass("active").filter("[data-stamp='MASTER']").addClass("active");
            $("#spStampNote").removeClass("warn").text(SP_STAMP_WHEN.MASTER);
            $("#spThumbs").empty();
            $("#spMessage").text("Loading document…").show();
            $("#spPage").hide();

            $("#modalStampAccept").modal("show");
            spLoadPdf(result.sourceUrl);
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}

function spLoadPdf(url) {
    var token = ++gSp.loadToken;
    pdfjsLib.GlobalWorkerOptions.workerSrc = p4dMaintenanceConfig.urls.pdfWorker;
    pdfjsLib.getDocument({ url: url, withCredentials: true }).promise.then(function (pdf) {
        if (token !== gSp.loadToken) return;
        gSp.pdf = pdf;
        var sizes = [];
        for (var i = 1; i <= pdf.numPages; i++) sizes.push(pdf.getPage(i));
        return Promise.all(sizes).then(function (pages) {
            if (token !== gSp.loadToken) return;
            $.each(pages, function (_, page) {
                var vp = page.getViewport({ scale: 1 });
                gSp.pages[page.pageNumber] = { w: vp.width, h: vp.height };
            });
            // Page positions beyond the last page can never apply; drop them.
            $.each(Object.keys(gSp.pageMap), function (_, k) {
                if (Number(k.split("#")[1]) > pdf.numPages) delete gSp.pageMap[k];
            });
            spBuildThumbs(pages, token);
            $("#spMessage").hide();
            $("#spPage").show();
            spShowPage(1);
        });
    }).catch(function () {
        if (token !== gSp.loadToken) return;
        gSp.mode = "category";
        $("input[name='spMode'][value='category']").prop("checked", true);
        $("input[name='spMode'][value='custom']").prop("disabled", true);
        $("#spMessage").text("The document PDF could not be loaded, so the stamps cannot be adjusted here. You can still accept with the category layout.").show();
        $("#spPage").hide();
        spRender();
    });
}

function spBuildThumbs(pages, token) {
    var $thumbs = $("#spThumbs").empty();
    var thumbWidth = 76;
    var queue = Promise.resolve();
    $.each(pages, function (_, page) {
        var n = page.pageNumber, size = gSp.pages[n];
        var $thumb = $('<button type="button" class="sp-thumb"></button>').attr("data-page", n).attr("title", "Page " + n);
        var canvas = document.createElement("canvas");
        var scale = thumbWidth / size.w;
        var vp = page.getViewport({ scale: scale * (window.devicePixelRatio || 1) });
        canvas.width = vp.width;
        canvas.height = vp.height;
        $(canvas).css({ width: thumbWidth, height: size.h * scale });
        $thumb.append(canvas)
            .append($('<div class="sp-thumb-box"><img alt="" draggable="false" /></div>'))
            .append($('<span class="sp-thumb-no"></span>').text(n));
        $thumbs.append($thumb);
        // One page at a time keeps long documents responsive; stops when the dialog closes.
        queue = queue.then(function () {
            if (token !== gSp.loadToken) return;
            return page.render({ canvasContext: canvas.getContext("2d"), viewport: vp }).promise.catch(function () { });
        });
    });
    if (token === gSp.loadToken) spRender();
}

function spShowPage(n) {
    if (!gSp.pdf || !gSp.pages[n]) return;
    gSp.current = n;
    var size = gSp.pages[n];
    var $wrap = $(".sp-page-wrap");
    // Fills the space left in the dialog (sized to the window), so the dialog never scrolls.
    var wrapH = $wrap.innerHeight();
    var maxW = Math.max(240, $wrap.innerWidth() - 4);
    var maxH = window.innerWidth > 1000 && wrapH > 100 ? wrapH - 4 : 560;
    gSp.scale = Math.min(maxW / size.w, maxH / size.h);
    $("#spPage").css({ width: size.w * gSp.scale, height: size.h * gSp.scale });

    var canvas = document.getElementById("spCanvas");
    gSp.pdf.getPage(n).then(function (page) {
        if (gSp.current !== n) return;
        if (gSp.renderTask) gSp.renderTask.cancel();
        var ratio = window.devicePixelRatio || 1;
        var vp = page.getViewport({ scale: gSp.scale * ratio });
        canvas.width = vp.width;
        canvas.height = vp.height;
        gSp.renderTask = page.render({ canvasContext: canvas.getContext("2d"), viewport: vp });
        gSp.renderTask.promise.catch(function () { });
    });
    spRender();
}

function spRender() {
    var editable = spEditable();
    var size = spPageSize(), scale = gSp.scale;
    var safe = spLimits().safeMarginPt * scale;
    var $page = $("#spPage");

    $page.find(".sl-safe").css({ left: safe, top: safe, right: safe, bottom: safe });
    $page.find(".sl-box").each(function () {
        var type = $(this).data("stamp");
        var r = spPlace(type);
        $(this).css({ left: r.x * scale, top: r.y * scale, width: r.w * scale })
            .toggleClass("active", type === gSp.stamp)
            .toggleClass("editable", editable);
        $(this).find(".sl-note").css("height", (spLimits().noteHeightPt - 3) * scale);
    });

    // Every page thumbnail shows the selected stamp where it lands on that page.
    var src = $page.find(".sl-box[data-stamp='" + gSp.stamp + "'] img").attr("src");
    $("#spThumbs .sp-thumb").each(function () {
        var n = Number($(this).data("page")), p = gSp.pages[n];
        if (!p) return;
        var s = $(this).find("canvas").width() / p.w;
        var r = spPlace(gSp.stamp, n);
        $(this).toggleClass("active", n === gSp.current)
            .toggleClass("own", !!spOwn(gSp.stamp, n))
            .attr("title", "Page " + n + (spOwn(gSp.stamp, n) ? " · own position" : ""));
        $(this).find(".sp-thumb-box").css({ left: r.x * s, top: r.y * s, width: r.w * s })
            .find("img").attr("src", src);
    });

    var cur = spPlace(gSp.stamp);
    var own = !!spOwn(gSp.stamp, gSp.current);
    var orientation = StampCore.orientationOf(size.w, size.h) === "L" ? "Landscape" : "Portrait";
    $("#spPanelTitle").text(StampCore.NAMES[gSp.stamp] + " · " + orientation);
    $("#spPageLabel").text(gSp.pdf ? "Page " + gSp.current + " of " + gSp.pdf.numPages + " · " + orientation
        + (own ? " · own position for this page" : "") : "");
    $("#spWidth").val(Math.round(cur.w / StampCore.MM)).prop("disabled", !editable);
    $("#spWidthText").text(Math.round(cur.w / StampCore.MM) + " mm");
    if (!$("#spX").is(":focus")) $("#spX").val(Math.round(cur.x / StampCore.MM));
    if (!$("#spY").is(":focus")) $("#spY").val(Math.round(cur.y / StampCore.MM));
    $("#spX, #spY, #spGrid9 button").prop("disabled", !editable);
    $(".sp-panel").toggleClass("readonly", !editable);

    // Only this page (stage 4): checked while the page has its own position for this stamp.
    $("#spPageOnly").prop("checked", own).prop("disabled", !editable || !gSp.pdf);
    $("#spPageOnlyText").text("Only page " + gSp.current);
    $("#spPageReset").toggle(own);
    var pages = spOwnPages(gSp.stamp);
    $("#spPageOwn").text(pages.length ? "Own position on page " + pages.join(", ") + "." : "").toggle(pages.length > 0);
}

// Moves the selected stamp: on the current page only when it has its own position there,
// otherwise on every page of the same orientation.
function spSetPt(x, y, w) {
    var size = spPageSize(), n = gSp.current;
    var own = spOwn(gSp.stamp, n);
    if (own) {
        var map = $.extend({}, spMapFor(n));
        StampCore.setPt(spLimits(), map, gSp.aspect, gSp.stamp, size.w, size.h, x, y, w);
        gSp.pageMap[spPageKey(gSp.stamp, n)] = $.extend({ o: own.o }, map[StampCore.key(gSp.stamp, own.o)]);
    } else {
        StampCore.setPt(spLimits(), gSp.customMap, gSp.aspect, gSp.stamp, size.w, size.h, x, y, w);
    }
    spRender();
}

// Gives the current page its own position (starting where the stamp is now), or removes it.
function spSetPageOnly(on) {
    var n = gSp.current, size = spPageSize(n), k = spPageKey(gSp.stamp, n);
    if (on) {
        var r = spPlace(gSp.stamp, n);
        gSp.pageMap[k] = { x: r.x / size.w * 100, y: r.y / size.h * 100, w: r.w / size.w * 100, o: StampCore.orientationOf(size.w, size.h) };
    } else {
        delete gSp.pageMap[k];
    }
    spRender();
}

// The document's positions to save: every page, then the pages with their own position.
function spPositionList() {
    var list = StampCore.toList(gSp.customMap);
    $.each(gSp.pageMap, function (k, p) {
        var parts = k.split("#"), n = Number(parts[1]);
        if (!gSp.pages[n] || !spOwn(parts[0], n)) return;
        list.push({ STAMP_TYPE: parts[0], ORIENTATION: p.o, PAGE_NO: n, POS_X: +p.x.toFixed(3), POS_Y: +p.y.toFixed(3), WIDTH_PCT: +p.w.toFixed(3) });
    });
    return list;
}

// When each copy gets its stamp (ResolveStamps and the PDF builders in DocumentMaintenanceController):
// a copy carries exactly one stamp, so the user only checks where it lands on that copy.
var SP_STAMP_WHEN = {
    MASTER: "On the original Document Control opens in the QMS menus, from Accept on.",
    CONTROLLED: "On copies viewed or printed by the departments it is sent to, with the copy number.",
    UNCONTROLLED: "On copies downloaded by users other than the creator and Document Control.",
    OBSOLETE: "On every copy once a new revision replaces this one. Set it now: it cannot be changed later."
};

function spSelectStamp(type) {
    gSp.stamp = type;
    $(".sp-stamp-btn").removeClass("active").filter("[data-stamp='" + type + "']").addClass("active");
    $("#spStampNote").toggleClass("warn", type === "OBSOLETE").text(SP_STAMP_WHEN[type] || "");
    spRender();
}

var SP_ACTIONS = { ACCEPT: "Accepted", CHANGE: "Changed", INHERIT: "Inherited" };
var SP_SOURCES = { CATEGORY: "category layout", CUSTOM: "own positions" };

function spRenderHistory(items) {
    var $list = $("#spHistory").empty();
    if (!items.length) {
        $list.append($('<li class="sp-history-empty"></li>').text("No changes yet."));
        return;
    }
    $.each(items, function (_, h) {
        var $li = $("<li></li>");
        $li.append($("<b></b>").text((SP_ACTIONS[h.ACTION] || h.ACTION) + " · " + (SP_SOURCES[h.SOURCE] || h.SOURCE)));
        $li.append(document.createTextNode(" · Rev." + ("0" + h.REVISION).slice(-2) + " · " + (h.CREATED_BY || "-") + " · " + h.CREATED_DT));
        if (h.REASON) $li.append($("<i></i>").text(h.REASON));
        $list.append($li);
    });
}

function spAccept() {
    var isChange = gSp.dialogMode === "change";
    var reason = $.trim($("#spReason").val());
    if (isChange && reason.length < 5) {
        showErrorMesgGrowl("Enter the reason for changing the stamp position (at least 5 characters).");
        $("#spReason").focus();
        return;
    }

    var stamp = {
        customStamp: gSp.mode === "custom",
        stampPositions: gSp.mode === "custom" ? JSON.stringify(spPositionList()) : ""
    };
    var data = isChange
        ? $.extend({ documentCtrlId: gSp.ctrlId, reason: reason }, stamp)
        : $.extend({ DOCUMENT_CTRL_ID: gSp.ctrlId }, stamp);

    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: isChange ? p4dMaintenanceConfig.urls.changeStampLayout : p4dMaintenanceConfig.urls.accept,
        dataType: "json",
        data: data,
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                $("#modalStampAccept").modal("hide");
                showSuccessMesgGrowl(result.message);
                onSearch();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

$(document).ready(function () {
    $("#spGrid9 button").each(function () {
        this.style.setProperty("--h", $(this).data("h"));
        this.style.setProperty("--v", $(this).data("v"));
    });
    StampCore.loadAspects($("#spPage"), gSp.aspect, function () { });

    $("input[name='spMode']").on("change", function () {
        gSp.mode = $(this).val();
        spRender();
    });

    $(".sp-stamp-btn").on("click", function () { spSelectStamp($(this).data("stamp")); });

    $("#spThumbs").on("click", ".sp-thumb", function () { spShowPage(Number($(this).data("page"))); });

    $("#spPage").on("pointerdown", ".sl-box", function (e) {
        var type = $(this).data("stamp");
        if (type !== gSp.stamp) {
            spSelectStamp(type);
            return;
        }
        if (!spEditable()) return;
        var r = spPlace(type);
        gSp.drag = { startX: e.clientX, startY: e.clientY, x: r.x, y: r.y, w: r.w };
        this.setPointerCapture(e.pointerId);
        e.preventDefault();
    }).on("pointermove", ".sl-box", function (e) {
        var d = gSp.drag;
        if (!d) return;
        spSetPt(d.x + (e.clientX - d.startX) / gSp.scale, d.y + (e.clientY - d.startY) / gSp.scale, d.w);
    }).on("pointerup pointercancel", ".sl-box", function () {
        gSp.drag = null;
    });

    $("#spGrid9 button").on("click", function () {
        var size = spPageSize(), r = spPlace(gSp.stamp);
        var q = StampCore.quick(spLimits(), r, size.w, size.h, $(this).data("h"), $(this).data("v"));
        spSetPt(q.x, q.y, r.w);
    });

    $("#spWidth").on("input", function () {
        var r = spPlace(gSp.stamp), w = Number($(this).val()) * StampCore.MM;
        spSetPt(r.x + (r.w - w) / 2, r.y, w);
    });

    $("#spX, #spY").on("change", function () {
        var r = spPlace(gSp.stamp);
        var x = $("#spX").val() === "" ? r.x : Number($("#spX").val()) * StampCore.MM;
        var y = $("#spY").val() === "" ? r.y : Number($("#spY").val()) * StampCore.MM;
        $(this).blur();
        spSetPt(x, y, r.w);
    });

    $("#spPageOnly").on("change", function () { spSetPageOnly(this.checked); });
    $("#spPageReset").on("click", function () { spSetPageOnly(false); });

    $("#spAccept").on("click", spAccept);

    $("#modalStampAccept").on("hidden.bs.modal", function () {
        gSp.loadToken++;
        if (gSp.renderTask) gSp.renderTask.cancel();
        if (gSp.pdf) gSp.pdf.destroy();
        gSp.pdf = null;
    }).on("shown.bs.modal", function () {
        if (gSp.pdf) spShowPage(gSp.current);
    });

    $(window).on("resize", function () {
        if ($("#modalStampAccept").hasClass("in") && gSp.pdf) spShowPage(gSp.current);
    });
});
