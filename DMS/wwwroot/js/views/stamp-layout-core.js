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
    // Only CONTROLLED COPY has a note line under it (copy number, recipient); UNCONTROLLED COPY
    // is the image alone since 2026-11-12.
    function hasNote(type) { return type === "CONTROLLED"; }
    function clamp(v, min, max) { return max < min ? min : Math.max(min, Math.min(max, v)); }
    function orientationOf(w, h) { return w > h ? "L" : "P"; }

    // Positions for every page only; page positions (PAGE_NO set) are kept apart by the caller.
    function toMap(list) {
        var map = {};
        $.each(list || [], function (_, p) {
            if (p.PAGE_NO != null) return;
            map[key(p.STAMP_TYPE, p.ORIENTATION)] = { x: Number(p.POS_X), y: Number(p.POS_Y), w: Number(p.WIDTH_PCT), r: Number(p.ROTATION || 0) };
        });
        return map;
    }

    function toList(map) {
        var list = [];
        $.each(TYPES, function (_, type) {
            $.each(["P", "L"], function (_, o) {
                var p = map[key(type, o)];
                list.push({ STAMP_TYPE: type, ORIENTATION: o, POS_X: +p.x.toFixed(3), POS_Y: +p.y.toFixed(3), WIDTH_PCT: +p.w.toFixed(3), ROTATION: +(p.r || 0).toFixed(2) });
            });
        });
        return list;
    }

    // Rectangle in points on a page of pageW x pageH, clamped like the server does
    // (StampLayoutService.Layout.Place). r = rotation in degrees clockwise around the centre of
    // stamp + note; the rotated box stays inside the margin. halfW/halfH = half its size.
    function place(limits, positions, aspect, type, pageW, pageH) {
        var p = positions[key(type, orientationOf(pageW, pageH))];
        var safe = limits.safeMarginPt;
        var usable = Math.max(1, pageW - 2 * safe);
        var w = clamp(p.w / 100 * pageW, Math.min(limits.minWidthPt, usable), Math.min(limits.maxWidthPt, usable));
        var h = w * (aspect[type] || 0.25);
        var block = h + (hasNote(type) ? limits.noteHeightPt : 0);
        var rot = p.r || 0, a = rot * Math.PI / 180;
        var cos = Math.abs(Math.cos(a)), sin = Math.abs(Math.sin(a));
        var halfW = (w * cos + block * sin) / 2, halfH = (w * sin + block * cos) / 2;
        var cx = clamp(p.x / 100 * pageW + w / 2, safe + halfW, pageW - safe - halfW);
        var cy = clamp(p.y / 100 * pageH + block / 2, safe + halfH, pageH - safe - halfH);
        return { x: cx - w / 2, y: cy - block / 2, w: w, h: h, block: block, r: rot, halfW: halfW, halfH: halfH };
    }

    // Stores a rectangle in points as percentages of the page, clamped first. r (degrees)
    // keeps the current rotation when left out.
    function setPt(limits, positions, aspect, type, pageW, pageH, x, y, w, r) {
        var k = key(type, orientationOf(pageW, pageH));
        var rot = r === undefined ? ((positions[k] && positions[k].r) || 0) : normAngle(r);
        positions[k] = { x: x / pageW * 100, y: y / pageH * 100, w: w / pageW * 100, r: rot };
        var p = place(limits, positions, aspect, type, pageW, pageH);
        positions[k] = { x: p.x / pageW * 100, y: p.y / pageH * 100, w: p.w / pageW * 100, r: rot };
    }

    // -180..180, whole degrees.
    function normAngle(r) {
        r = Math.round(Number(r) || 0) % 360;
        if (r > 180) r -= 360;
        if (r < -180) r += 360;
        return r;
    }

    // CSS of a stamp box on screen (scale = screen px per point), turned around the centre of
    // stamp + note like the PDF.
    function boxCss(rect, scale) {
        return {
            left: rect.x * scale, top: rect.y * scale, width: rect.w * scale,
            transform: rect.r ? "rotate(" + rect.r + "deg)" : "",
            "transform-origin": (rect.w / 2 * scale) + "px " + (rect.block / 2 * scale) + "px"
        };
    }

    // Rotate handle on every stamp box (shown on the selected one when it can be edited).
    function addRotateHandles($page) {
        $page.find(".sl-box").each(function () {
            if (!$(this).children(".sl-rot").length)
                $(this).append('<span class="sl-rot" title="Drag to rotate (hold Shift for 15° steps)"><i class="fa fa-repeat"></i></span>');
        });
    }

    // Angle (degrees clockwise, 0 = handle straight up) from the box centre to the pointer.
    // Snaps to 0 / 90 / 180 / -90 within 4°, or to 15° steps with Shift.
    function angleFromPointer(box, e) {
        var b = box.getBoundingClientRect();
        var deg = Math.atan2(e.clientX - (b.left + b.width / 2), (b.top + b.height / 2) - e.clientY) * 180 / Math.PI;
        if (e.shiftKey) return normAngle(Math.round(deg / 15) * 15);
        var right = Math.round(deg / 90) * 90;
        return normAngle(Math.abs(deg - right) <= 4 ? right : deg);
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
        toMap: toMap, toList: toList, place: place, setPt: setPt, loadAspects: loadAspects,
        normAngle: normAngle, boxCss: boxCss, addRotateHandles: addRotateHandles, angleFromPointer: angleFromPointer
    };
})();
