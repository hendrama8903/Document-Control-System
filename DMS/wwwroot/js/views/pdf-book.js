// Book-style document viewer (request Hendra 2026-11-18), used by Views/Preview/PDFBookPreview.cshtml
// inside the popup of My Documents. Book mode shows two pages side by side (page 1 alone on the
// right like a cover) and turns pages in 3D; single mode shows one page with a slide. Landscape
// documents and narrow screens start in single mode.
// Pages are rendered with PDF.js to <canvas>, so the browser's PDF toolbar (print/download) is
// never shown - a deterrent, not a guarantee (same as PDFPreview.cshtml).
(function () {
    var $ = function (id) { return document.getElementById(id); };
    var stage = $("bk-stage"), book = $("bk-book");
    var slotL = book.querySelector(".bk-left"), slotR = book.querySelector(".bk-right");
    var leaf = book.querySelector(".bk-leaf");
    var faceFront = leaf.querySelector(".bk-front"), faceBack = leaf.querySelector(".bk-back");
    var pageInput = $("bk-page-input");

    var pdfDoc = null, n = 0;
    var baseW = 595, baseH = 842;          // page 1 at scale 1 (as rotated), used to fit the screen
    var natW = 595, natH = 842;            // page 1 at scale 1, not rotated by the viewer
    var rotation = 0;                      // viewer rotation on top of the PDF's own: 0/90/180/270 (2026-11-18)
    var mode = "spread";                   // "spread" (book) | "single"
    var zoom = 1;                          // 1 = fit to screen
    var scale = 1;                         // fit * zoom
    var spread = 0, page = 1;              // position in book / single mode
    var cache = {};                        // page number -> Promise<canvas> at the current scale
    var generation = 0;                    // bumps when the scale changes; late renders are dropped
    var busy = false;
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    var MIN_ZOOM = 0.5, MAX_ZOOM = 3, ZOOM_STEP = 0.15, MAX_CANVAS = 4096;

    // ------------------------------------------------------------------ geometry
    function pagesOf(s) {
        if (s === 0) return { l: null, r: 1 };
        var l = 2 * s, r = 2 * s + 1;
        return { l: l <= n ? l : null, r: r <= n ? r : null };
    }
    function spreadCount() { return 1 + Math.floor(n / 2); }
    function spreadOfPage(p) { return p <= 1 ? 0 : Math.floor(p / 2); }
    function firstVisiblePage() {
        if (mode === "single") return page;
        var p = pagesOf(spread);
        return p.l || p.r;
    }

    function computeScale() {
        var cs = getComputedStyle(stage);
        var w = stage.clientWidth - parseFloat(cs.paddingLeft) - parseFloat(cs.paddingRight);
        var h = stage.clientHeight - parseFloat(cs.paddingTop) - parseFloat(cs.paddingBottom);
        var across = mode === "spread" ? 2 : 1;
        var fit = Math.max(0.1, Math.min(w / (baseW * across), h / baseH));
        scale = fit * zoom;
    }

    function resetCache() {
        cache = {};
        generation++;
    }

    // ------------------------------------------------------------------ rendering
    function getCanvas(p) {
        if (!p) return Promise.resolve(null);
        if (cache[p]) return cache[p];
        var gen = generation;
        cache[p] = pdfDoc.getPage(p).then(function (pg) {
            // the PDF's own page rotation plus the one chosen in the viewer
            var vp = pg.getViewport({ scale: scale, rotation: ((pg.rotate || 0) + rotation) % 360 });
            // sharp on HiDPI screens, but never past the canvas size browsers can draw
            var out = window.devicePixelRatio || 1;
            var biggest = Math.max(vp.width, vp.height);
            if (biggest * out > MAX_CANVAS) out = MAX_CANVAS / biggest;

            var canvas = document.createElement("canvas");
            canvas.width = Math.floor(vp.width * out);
            canvas.height = Math.floor(vp.height * out);
            canvas.style.width = Math.floor(vp.width) + "px";
            canvas.style.height = Math.floor(vp.height) + "px";
            var transform = out !== 1 ? [out, 0, 0, out, 0, 0] : null;
            return pg.render({ canvasContext: canvas.getContext("2d"), viewport: vp, transform: transform }).promise
                .then(function () { return gen === generation ? canvas : null; });
        });
        return cache[p];
    }

    // keep only the pages around the current position, so long documents do not fill memory
    function prune() {
        var keep = {};
        var centre = firstVisiblePage();
        for (var p = centre - 6; p <= centre + 7; p++) keep[p] = true;
        Object.keys(cache).forEach(function (k) { if (!keep[k]) delete cache[k]; });
    }

    function preload() {
        prune();
        if (mode === "spread") {
            [spread - 1, spread + 1].forEach(function (s) {
                if (s < 0 || s >= spreadCount()) return;
                var p = pagesOf(s);
                getCanvas(p.l); getCanvas(p.r);
            });
        } else {
            if (page > 1) getCanvas(page - 1);
            if (page < n) getCanvas(page + 1);
        }
    }

    function place(slot, canvas) {
        slot.innerHTML = "";
        if (canvas) {
            slot.appendChild(canvas);
            slot.classList.remove("blank");
            slot.style.width = slot.style.height = "";
        } else {
            // an empty half keeps the book's shape (left of the cover, right of the last page)
            slot.classList.add("blank");
            slot.style.width = Math.floor(baseW * scale) + "px";
            slot.style.height = Math.floor(baseH * scale) + "px";
        }
    }

    function show() {
        book.classList.toggle("spread", mode === "spread");
        book.classList.toggle("single", mode === "single");
        book.classList.toggle("zoomed", zoom > 1.001);
        var gen = generation;
        var p = mode === "spread" ? pagesOf(spread) : { l: null, r: page };
        return Promise.all([getCanvas(p.l), getCanvas(p.r)]).then(function (c) {
            if (gen !== generation) return;
            place(slotL, c[0]);
            place(slotR, c[1]);
            book.classList.add("ready");
            updateUI();
            preload();
        });
    }

    // ------------------------------------------------------------------ turning
    function turnSpread(dir) {
        var target = spread + dir;
        if (target < 0 || target >= spreadCount()) return;
        busy = true;
        var nw = pagesOf(target);
        var gen = generation;
        Promise.all([getCanvas(nw.l), getCanvas(nw.r)]).then(function (c) {
            if (gen !== generation) { busy = false; return; }
            var newL = c[0], newR = c[1];
            spread = target;

            if (reduceMotion) {
                place(slotL, newL); place(slotR, newR);
                finishTurn();
                return;
            }

            // The leaf lies on the page being turned: its front is that page, its back is the
            // page that lands on the other side. The page under it already shows the new one.
            var moving = (dir > 0 ? slotR : slotL).querySelector("canvas");
            var landing = dir > 0 ? newL : newR;
            var w = moving ? moving.offsetWidth : Math.floor(baseW * scale);
            var h = moving ? moving.offsetHeight : Math.floor(baseH * scale);

            faceFront.innerHTML = ""; faceBack.innerHTML = "";
            if (moving) faceFront.appendChild(moving);
            if (landing) faceBack.appendChild(landing);
            leaf.style.width = w + "px";
            leaf.style.height = h + "px";
            leaf.style.left = (dir > 0 ? slotL.offsetWidth : 0) + "px";
            leaf.style.transformOrigin = dir > 0 ? "left center" : "right center";
            leaf.style.transition = "none";
            leaf.style.transform = "rotateY(0deg)";
            leaf.classList.add("turning");

            if (dir > 0) place(slotR, newR); else place(slotL, newL);

            void leaf.offsetWidth;          // start the transition from 0deg
            leaf.style.transition = "";
            leaf.style.transform = dir > 0 ? "rotateY(-180deg)" : "rotateY(180deg)";

            var done = false;
            var end = function () {
                if (done) return;
                done = true;
                if (dir > 0) place(slotL, landing); else place(slotR, landing);
                faceFront.innerHTML = ""; faceBack.innerHTML = "";
                leaf.classList.remove("turning");
                leaf.style.transition = "none";
                leaf.style.transform = "";
                finishTurn();
            };
            leaf.addEventListener("transitionend", end, { once: true });
            setTimeout(end, 900);           // in case transitionend never fires
        });
    }

    function turnSingle(dir) {
        var target = page + dir;
        if (target < 1 || target > n) return;
        busy = true;
        var gen = generation;
        getCanvas(target).then(function (canvas) {
            if (gen !== generation) { busy = false; return; }
            page = target;
            place(slotR, canvas);
            if (!reduceMotion) {
                var cls = dir > 0 ? "bk-in-next" : "bk-in-prev";
                slotR.classList.remove("bk-in-next", "bk-in-prev");
                void slotR.offsetWidth;
                slotR.classList.add(cls);
                setTimeout(function () { slotR.classList.remove(cls); }, 360);
            }
            finishTurn();
        });
    }

    function finishTurn() {
        busy = false;
        updateUI();
        preload();
        stage.scrollTop = 0;
    }

    function go(dir) {
        if (busy || !pdfDoc) return;
        if (mode === "spread") turnSpread(dir); else turnSingle(dir);
    }

    function goToPage(p) {
        if (busy || !pdfDoc || isNaN(p)) return;
        p = Math.max(1, Math.min(n, p));
        if (mode === "spread") {
            var s = spreadOfPage(p);
            if (s === spread) return updateUI();
            if (Math.abs(s - spread) === 1) return turnSpread(s - spread);
            spread = s;
        } else {
            if (p === page) return updateUI();
            if (Math.abs(p - page) === 1) return turnSingle(p - page);
            page = p;
        }
        show();
    }

    // ------------------------------------------------------------------ zoom / mode
    function rerender() {
        computeScale();
        resetCache();
        show();
    }

    function setZoom(z) {
        zoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, z));
        rerender();
    }

    // Rotate every page by 90 degrees (dir 1 = clockwise). Only the view turns; the file is not
    // changed and the next opening starts upright again.
    function rotate(dir) {
        if (busy || !pdfDoc) return;
        rotation = (rotation + (dir > 0 ? 90 : 270)) % 360;
        var sideways = rotation % 180 !== 0;
        baseW = sideways ? natH : natW;
        baseH = sideways ? natW : natH;
        rerender();
    }

    function setMode(m) {
        if (m === mode || busy) return;
        var first = firstVisiblePage();
        mode = m;
        if (mode === "single") page = first; else spread = spreadOfPage(first);
        zoom = 1;
        rerender();
    }

    // ------------------------------------------------------------------ toolbar state
    function updateUI() {
        var atStart, atEnd;
        if (mode === "spread") {
            var p = pagesOf(spread);
            pageInput.value = p.l && p.r ? p.l + "-" + p.r : String(p.l || p.r);
            atStart = spread === 0;
            atEnd = spread >= spreadCount() - 1;
        } else {
            pageInput.value = String(page);
            atStart = page <= 1;
            atEnd = page >= n;
        }
        $("bk-page-total").textContent = "/ " + n;
        $("bk-first").disabled = $("bk-prev").disabled = $("bk-side-prev").disabled = atStart;
        $("bk-last").disabled = $("bk-next").disabled = $("bk-side-next").disabled = atEnd;
        $("bk-zoom-label").textContent = Math.abs(zoom - 1) < 0.001 ? "Fit" : Math.round(zoom * 100) + "%";
        $("bk-zoom-out").disabled = zoom <= MIN_ZOOM + 0.001;
        $("bk-zoom-in").disabled = zoom >= MAX_ZOOM - 0.001;
        var modeBtn = $("bk-mode");
        modeBtn.classList.toggle("active", mode === "spread");
        modeBtn.title = mode === "spread" ? "Single page (B)" : "Book view (B)";
        modeBtn.disabled = n < 2;
        $("bk-rotate-right").classList.toggle("active", rotation !== 0);
        $("bk-rotate-right").title = "Rotate right (R)" + (rotation ? " - now " + rotation + "°" : "");
    }

    // ------------------------------------------------------------------ events
    $("bk-prev").addEventListener("click", function () { go(-1); });
    $("bk-next").addEventListener("click", function () { go(1); });
    $("bk-side-prev").addEventListener("click", function () { go(-1); });
    $("bk-side-next").addEventListener("click", function () { go(1); });
    $("bk-first").addEventListener("click", function () { goToPage(1); });
    $("bk-last").addEventListener("click", function () { goToPage(n); });
    $("bk-zoom-in").addEventListener("click", function () { setZoom(zoom + ZOOM_STEP); });
    $("bk-zoom-out").addEventListener("click", function () { setZoom(zoom - ZOOM_STEP); });
    $("bk-zoom-label").addEventListener("click", function () { setZoom(1); });
    $("bk-mode").addEventListener("click", function () { setMode(mode === "spread" ? "single" : "spread"); });
    $("bk-fullscreen").addEventListener("click", toggleFullscreen);
    $("bk-rotate-right").addEventListener("click", function () { rotate(1); });
    $("bk-rotate-left").addEventListener("click", function () { rotate(-1); });

    // click the right page to go forward, the left page to go back (not while zoomed in)
    slotR.addEventListener("click", function () { if (zoom <= 1.001 && !slotR.classList.contains("blank")) go(1); });
    slotL.addEventListener("click", function () { if (zoom <= 1.001 && !slotL.classList.contains("blank")) go(-1); });

    pageInput.addEventListener("focus", function () { pageInput.select(); });
    pageInput.addEventListener("keydown", function (e) {
        if (e.key === "Enter") {
            e.preventDefault();
            goToPage(parseInt(pageInput.value, 10));
            pageInput.blur();
        } else if (e.key === "Escape") {
            updateUI();
            pageInput.blur();
        }
        e.stopPropagation();
    });
    pageInput.addEventListener("blur", updateUI);

    document.addEventListener("keydown", function (e) {
        var k = e.key ? e.key.toLowerCase() : "";
        if ((e.ctrlKey || e.metaKey) && (k === "p" || k === "s")) { e.preventDefault(); e.stopPropagation(); return; }
        if (e.ctrlKey || e.metaKey || e.altKey) return;
        switch (e.key) {
            case "ArrowRight": case "PageDown": case " ": e.preventDefault(); go(1); break;
            case "ArrowLeft": case "PageUp": e.preventDefault(); go(-1); break;
            case "Home": e.preventDefault(); goToPage(1); break;
            case "End": e.preventDefault(); goToPage(n); break;
            case "+": case "=": setZoom(zoom + ZOOM_STEP); break;
            case "-": setZoom(zoom - ZOOM_STEP); break;
            case "0": setZoom(1); break;
            case "b": case "B": setMode(mode === "spread" ? "single" : "spread"); break;
            case "f": case "F": toggleFullscreen(); break;
            case "r": rotate(1); break;
            case "R": rotate(e.shiftKey ? -1 : 1); break;
            case "Escape":
                // the popup in the parent page closes on Esc too (focus is inside this frame)
                if (!document.fullscreenElement && window.parent !== window)
                    window.parent.postMessage({ type: "dms-docviewer-close" }, window.location.origin);
                break;
        }
    });

    // swipe on touch screens
    var touchX = null;
    stage.addEventListener("touchstart", function (e) { touchX = e.touches.length === 1 && zoom <= 1.001 ? e.touches[0].clientX : null; }, { passive: true });
    stage.addEventListener("touchend", function (e) {
        if (touchX === null) return;
        var dx = e.changedTouches[0].clientX - touchX;
        if (Math.abs(dx) > 50) go(dx < 0 ? 1 : -1);
        touchX = null;
    });

    function toggleFullscreen() {
        if (document.fullscreenElement) document.exitFullscreen();
        else if (document.documentElement.requestFullscreen) document.documentElement.requestFullscreen().catch(function () { });
    }
    document.addEventListener("fullscreenchange", function () {
        $("bk-fullscreen").innerHTML = document.fullscreenElement ? '<i class="fa fa-compress"></i>' : '<i class="fa fa-expand"></i>';
    });

    var resizeTimer = null;
    window.addEventListener("resize", function () {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(function () { if (pdfDoc && !busy) rerender(); }, 150);
    });

    // ------------------------------------------------------------------ deterrents
    document.addEventListener("contextmenu", function (e) { e.preventDefault(); });
    document.addEventListener("dragstart", function (e) { e.preventDefault(); });
    window.addEventListener("beforeprint", function () { $("bk-blocked").style.display = "flex"; });
    window.addEventListener("afterprint", function () { $("bk-blocked").style.display = "none"; });

    // ------------------------------------------------------------------ load
    fetch(BOOK_PDF_URL, { credentials: "same-origin" })
        .then(function (resp) {
            if (!resp.ok) throw new Error("HTTP " + resp.status);
            return resp.arrayBuffer();
        })
        .then(function (buf) { return pdfjsLib.getDocument({ data: buf }).promise; })
        .then(function (doc) {
            pdfDoc = doc;
            n = doc.numPages;
            return doc.getPage(1);
        })
        .then(function (first) {
            var vp = first.getViewport({ scale: 1 });
            natW = baseW = vp.width;
            natH = baseH = vp.height;
            // landscape pages (A3 SPR/SIPOCOR...), one-page documents and narrow screens read
            // better one page at a time
            mode = n < 2 || baseW > baseH || stage.clientWidth < 760 ? "single" : "spread";
            computeScale();
            return show();
        })
        .then(function () {
            $("bk-loading").style.display = "none";
            window.focus();
        })
        .catch(function () {
            $("bk-loading").innerHTML = '<i class="fa fa-exclamation-triangle" style="font-size:28px;color:#F79009"></i><span>The document could not be loaded. Close the viewer and try again.</span>';
        });
})();
