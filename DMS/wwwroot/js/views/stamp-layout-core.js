// Shared by Master Data > Stamp Layout and the stamp dialog on Accept in Document Registration.
// Positions are kept like the server stores them: top-left corner and width in percent of the
// page. StampCore.place repeats StampLayoutService.Layout.Place, so what is drawn here is
// where the PDF stamps land.
// limits = { safeMarginPt, minWidthPt, maxWidthPt, noteHeightPt } from StampLayoutService.
var StampCore = (function () {
    var MM = 2.83465;
    var TYPES = ["MASTER", "CONTROLLED", "UNCONTROLLED", "OBSOLETE"];
    var NAMES = { MASTER: "Master", CONTROLLED: "Controlled Copy", UNCONTROLLED: "Uncontrolled Copy", OBSOLETE: "Obsolete" };

    function key(type, orientation) { return type + "|" + orientation; }
    function clone(map) { return JSON.parse(JSON.stringify(map)); }
    function hasNote(type) { return type === "CONTROLLED" || type === "UNCONTROLLED"; }
    function clamp(v, min, max) { return max < min ? min : Math.max(min, Math.min(max, v)); }
    function orientationOf(w, h) { return w > h ? "L" : "P"; }

    // Positions for every page only; page positions (PAGE_NO set) are kept apart by the caller.
    function toMap(list) {
        var map = {};
        $.each(list || [], function (_, p) {
            if (p.PAGE_NO != null) return;
            map[key(p.STAMP_TYPE, p.ORIENTATION)] = { x: Number(p.POS_X), y: Number(p.POS_Y), w: Number(p.WIDTH_PCT) };
        });
        return map;
    }

    function toList(map) {
        var list = [];
        $.each(TYPES, function (_, type) {
            $.each(["P", "L"], function (_, o) {
                var p = map[key(type, o)];
                list.push({ STAMP_TYPE: type, ORIENTATION: o, POS_X: +p.x.toFixed(3), POS_Y: +p.y.toFixed(3), WIDTH_PCT: +p.w.toFixed(3) });
            });
        });
        return list;
    }

    // Rectangle in points on a page of pageW x pageH, clamped like the server does.
    function place(limits, positions, aspect, type, pageW, pageH) {
        var p = positions[key(type, orientationOf(pageW, pageH))];
        var safe = limits.safeMarginPt;
        var usable = Math.max(1, pageW - 2 * safe);
        var w = clamp(p.w / 100 * pageW, Math.min(limits.minWidthPt, usable), Math.min(limits.maxWidthPt, usable));
        var h = w * (aspect[type] || 0.25);
        var block = h + (hasNote(type) ? limits.noteHeightPt : 0);
        var x = clamp(p.x / 100 * pageW, safe, pageW - safe - w);
        var y = clamp(p.y / 100 * pageH, safe, pageH - safe - block);
        return { x: x, y: y, w: w, h: h, block: block };
    }

    // Stores a rectangle in points as percentages of the page, clamped first.
    function setPt(limits, positions, aspect, type, pageW, pageH, x, y, w) {
        var k = key(type, orientationOf(pageW, pageH));
        positions[k] = { x: x / pageW * 100, y: y / pageH * 100, w: w / pageW * 100 };
        var r = place(limits, positions, aspect, type, pageW, pageH);
        positions[k] = { x: r.x / pageW * 100, y: r.y / pageH * 100, w: r.w / pageW * 100 };
    }

    // Top-left corner for one of the nine quick positions (h, v = 0 left/top .. 2 right/bottom).
    function quick(limits, rect, pageW, pageH, h, v) {
        var safe = limits.safeMarginPt;
        return {
            x: h === 0 ? safe : h === 1 ? (pageW - rect.w) / 2 : pageW - safe - rect.w,
            y: v === 0 ? safe : v === 1 ? (pageH - rect.block) / 2 : pageH - safe - rect.block
        };
    }

    // Image height / width of each stamp image found under $root (.sl-box[data-stamp] img),
    // then done() once all are known.
    function loadAspects($root, aspect, done) {
        var $imgs = $root.find(".sl-box img"), pending = $imgs.length;
        if (!pending) { done(); return; }
        $imgs.each(function () {
            var img = this, type = $(img).closest(".sl-box").data("stamp");
            var finish = function () {
                if (img.naturalWidth) aspect[type] = img.naturalHeight / img.naturalWidth;
                if (--pending === 0) done();
            };
            if (img.complete) setTimeout(finish, 0); else $(img).one("load error", finish);
        });
    }

    return {
        MM: MM, TYPES: TYPES, NAMES: NAMES,
        key: key, clone: clone, hasNote: hasNote, clamp: clamp, orientationOf: orientationOf,
        toMap: toMap, toList: toList, place: place, setPt: setPt, quick: quick, loadAspects: loadAspects
    };
})();
