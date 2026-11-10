// Verify Document (2026-11-10). Reads the QR of a DCS signature box in the browser (jsQR) from an
// uploaded image or PDF, a webcam, or a pasted link / code, then opens /Verify/Signature/<code>.
// Only the 32-character code is taken from the QR, so a QR printed with another server address
// (e.g. localhost) is still checked against this DCS.
(function () {
    var TOKEN = /(?:Verify\/Signature\/)?([0-9a-fA-F]{32})(?![0-9a-fA-F])/;
    var MAX_PDF_PAGES = 20;

    var status = document.getElementById("vdStatus");
    var found = document.getElementById("vdFound");
    var foundList = document.getElementById("vdFoundList");
    var canvas = document.getElementById("vdCanvas");
    var ctx = canvas.getContext("2d", { willReadFrequently: true });

    if (window.pdfjsLib) pdfjsLib.GlobalWorkerOptions.workerSrc = verifyConfig.pdfWorker;

    function tokenOf(text) {
        var m = TOKEN.exec(text || "");
        return m ? m[1].toLowerCase() : null;
    }

    function openToken(token) {
        window.location.href = verifyConfig.signatureUrl + token;
    }

    function showStatus(kind, html) {
        found.style.display = "none";
        status.className = "vd-status vd-status-" + kind;
        status.innerHTML = html;
        status.style.display = "";
    }

    function hideStatus() { status.style.display = "none"; }

    // ---------- tabs ----------
    document.querySelectorAll(".vd-tab").forEach(function (tab) {
        tab.addEventListener("click", function () {
            document.querySelectorAll(".vd-tab").forEach(function (t) { t.classList.toggle("active", t === tab); });
            var name = tab.getAttribute("data-tab");
            document.getElementById("vdPaneUpload").classList.toggle("active", name === "upload");
            document.getElementById("vdPaneCamera").classList.toggle("active", name === "camera");
            document.getElementById("vdPaneCode").classList.toggle("active", name === "code");
            if (name !== "camera") stopCamera();
            if (name === "code") document.getElementById("vdCode").focus();
            hideStatus();
            found.style.display = "none";
        });
    });

    // ---------- paste link or code ----------
    function verifyCode() {
        var token = tokenOf(document.getElementById("vdCode").value);
        if (token) openToken(token);
        else showStatus("bad", '<i class="fa fa-times-circle"></i> This is not a DCS signature link or code. Paste the full link from the QR code, or its 32-character code.');
    }
    document.getElementById("vdVerifyCode").addEventListener("click", verifyCode);
    document.getElementById("vdCode").addEventListener("keydown", function (e) {
        if (e.key === "Enter") { e.preventDefault(); verifyCode(); }
    });

    // ---------- scanning ----------
    // Read the codes in one area of the canvas. A code found is painted over and the area read
    // again, because jsQR returns one code per call.
    function scanArea(x, y, w, h, tokens) {
        x = Math.max(0, Math.round(x)); y = Math.max(0, Math.round(y));
        w = Math.min(canvas.width - x, Math.round(w)); h = Math.min(canvas.height - y, Math.round(h));
        if (w < 20 || h < 20) return;
        for (var i = 0; i < 6; i++) {
            var code = jsQR(ctx.getImageData(x, y, w, h).data, w, h, { inversionAttempts: "dontInvert" });
            if (!code) return;
            var token = tokenOf(code.data);
            if (token && tokens.indexOf(token) < 0) tokens.push(token);
            var loc = code.location;
            var xs = [loc.topLeftCorner.x, loc.topRightCorner.x, loc.bottomLeftCorner.x, loc.bottomRightCorner.x];
            var ys = [loc.topLeftCorner.y, loc.topRightCorner.y, loc.bottomLeftCorner.y, loc.bottomRightCorner.y];
            var pad = 6;
            ctx.fillStyle = "#fff";
            ctx.fillRect(x + Math.min.apply(null, xs) - pad, y + Math.min.apply(null, ys) - pad,
                Math.max.apply(null, xs) - Math.min.apply(null, xs) + pad * 2,
                Math.max.apply(null, ys) - Math.min.apply(null, ys) + pad * 2);
        }
    }

    // A photo or scanned page: several codes side by side confuse jsQR on the whole image, so it
    // is read in overlapping windows of two sizes.
    function scanCanvas(width, height, tokens) {
        scanArea(0, 0, width, height, tokens);
        [3, 6].forEach(function (div) {
            var size = Math.round(Math.min(width, height) / div), step = Math.round(size / 2);
            for (var y = 0; ; y += step) {
                for (var x = 0; ; x += step) {
                    scanArea(x, y, size, size, tokens);
                    if (x + size >= width) break;
                }
                if (y + size >= height) break;
            }
        });
    }

    // Where the images are drawn on a PDF page, in canvas pixels. A DCS signature QR is an image,
    // so on a DCS PDF only those boxes need reading.
    function imageBoxes(page, viewport) {
        return page.getOperatorList().then(function (ops) {
            var O = pdfjsLib.OPS, U = pdfjsLib.Util, ctm = [1, 0, 0, 1, 0, 0], stack = [], boxes = [];
            for (var i = 0; i < ops.fnArray.length; i++) {
                var fn = ops.fnArray[i];
                if (fn === O.save) stack.push(ctm.slice());
                else if (fn === O.restore) ctm = stack.pop() || ctm;
                else if (fn === O.transform) ctm = U.transform(ctm, ops.argsArray[i]);
                else if (fn === O.paintImageXObject || fn === O.paintInlineImageXObject) {
                    var m = U.transform(viewport.transform, ctm);
                    var pts = [[0, 0], [1, 0], [0, 1], [1, 1]].map(function (p) { return U.applyTransform(p, m); });
                    var xs = pts.map(function (p) { return p[0]; }), ys = pts.map(function (p) { return p[1]; });
                    boxes.push({ x: Math.min.apply(null, xs), y: Math.min.apply(null, ys),
                        w: Math.max.apply(null, xs) - Math.min.apply(null, xs), h: Math.max.apply(null, ys) - Math.min.apply(null, ys) });
                }
            }
            return boxes;
        });
    }

    function renderPage(page, viewport) {
        canvas.width = Math.round(viewport.width);
        canvas.height = Math.round(viewport.height);
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        return page.render({ canvasContext: ctx, viewport: viewport }).promise;
    }

    function drawToCanvas(source, width, height, maxSide) {
        var scale = Math.min(1, maxSide / Math.max(width, height));
        canvas.width = Math.round(width * scale);
        canvas.height = Math.round(height * scale);
        ctx.fillStyle = "#fff";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.drawImage(source, 0, 0, canvas.width, canvas.height);
    }

    function showResult(tokens) {
        if (tokens.length === 0) {
            showStatus("bad", '<i class="fa fa-times-circle"></i> No DCS signature QR code was found in this file. Use a sharper image, or crop it closer to the QR code.');
            return;
        }
        if (tokens.length === 1) {
            showStatus("ok", '<i class="fa fa-check-circle"></i> QR code read. Opening the verification...');
            openToken(tokens[0]);
            return;
        }
        hideStatus();
        foundList.innerHTML = "";
        var items = {};
        tokens.forEach(function (token, i) {
            var a = document.createElement("a");
            a.className = "vd-found-item";
            a.href = verifyConfig.signatureUrl + token;
            a.innerHTML = '<i class="fa fa-qrcode"></i><span></span><small></small><i class="fa fa-chevron-right"></i>';
            a.querySelector("span").textContent = "Signature " + (i + 1);
            a.querySelector("small").textContent = token;
            foundList.appendChild(a);
            items[token] = a;
        });
        found.style.display = "";

        // Name each signature (signer, position, document) when DCS knows the code.
        var body = new URLSearchParams();
        tokens.forEach(function (t) { body.append("ids", t); });
        fetch(verifyConfig.lookupUrl, { method: "POST", body: body, credentials: "same-origin" })
            .then(function (r) { return r.json(); })
            .then(function (result) {
                (result.data || []).forEach(function (row) {
                    var a = items[row.id];
                    if (!a) return;
                    if (row.found) {
                        a.querySelector("span").textContent = row.signer + (row.position ? " (" + row.position + ")" : "");
                        a.querySelector("small").textContent = row.documentCode || "";
                    } else {
                        a.classList.add("vd-found-unknown");
                        a.querySelector("small").textContent = "Not found in DCS";
                    }
                });
            })
            .catch(function () { /* the list still works with the codes */ });
    }

    function readImage(file) {
        var url = URL.createObjectURL(file);
        var img = new Image();
        img.onload = function () {
            var tokens = [];
            drawToCanvas(img, img.naturalWidth, img.naturalHeight, 2000);
            scanCanvas(canvas.width, canvas.height, tokens);
            URL.revokeObjectURL(url);
            showResult(tokens);
        };
        img.onerror = function () {
            URL.revokeObjectURL(url);
            showStatus("bad", '<i class="fa fa-times-circle"></i> This image could not be opened.');
        };
        img.src = url;
    }

    function readPdf(file) {
        file.arrayBuffer().then(function (buf) {
            return pdfjsLib.getDocument({ data: buf }).promise;
        }).then(function (pdf) {
            var tokens = [];
            var pages = Math.min(pdf.numPages, MAX_PDF_PAGES);

            // Pass 1: read only the square images of each page (fast, exact on a DCS PDF). A page
            // that is one big image (a scan) is read in windows instead.
            // Pass 2, only when pass 1 found nothing: read every page in windows.
            function eachPage(label, work) {
                var chain = Promise.resolve();
                for (var p = 1; p <= pages; p++) {
                    (function (pageNo) {
                        chain = chain.then(function () {
                            showStatus("info", '<i class="fa fa-spinner fa-spin"></i> ' + label + ' page ' + pageNo + ' of ' + pages + '...');
                            return pdf.getPage(pageNo).then(function (page) {
                                return work(page, page.getViewport({ scale: 2 }));
                            });
                        });
                    })(p);
                }
                return chain;
            }

            return eachPage("Reading", function (page, viewport) {
                return imageBoxes(page, viewport).then(function (boxes) {
                    var pageArea = viewport.width * viewport.height;
                    var squares = boxes.filter(function (b) {
                        return b.w >= 30 && b.h >= 30 && b.w / b.h > 0.7 && b.w / b.h < 1.4 && b.w * b.h < pageArea * 0.4;
                    });
                    var hasScan = boxes.some(function (b) { return b.w * b.h >= pageArea * 0.4; });
                    if (squares.length === 0 && !hasScan) return null;
                    return renderPage(page, viewport).then(function () {
                        squares.forEach(function (b) {
                            var pad = Math.max(b.w, b.h) * 0.15;
                            scanArea(b.x - pad, b.y - pad, b.w + pad * 2, b.h + pad * 2, tokens);
                        });
                        if (hasScan) scanCanvas(canvas.width, canvas.height, tokens);
                    });
                });
            }).then(function () {
                if (tokens.length > 0) return null;
                return eachPage("Searching", function (page, viewport) {
                    return renderPage(page, viewport).then(function () { scanCanvas(canvas.width, canvas.height, tokens); });
                });
            }).then(function () { showResult(tokens); });
        }).catch(function () {
            showStatus("bad", '<i class="fa fa-times-circle"></i> This PDF could not be opened.');
        });
    }

    function readFile(file) {
        if (!file) return;
        stopCamera();
        showStatus("info", '<i class="fa fa-spinner fa-spin"></i> Reading the QR code...');
        if (file.type === "application/pdf" || /\.pdf$/i.test(file.name)) readPdf(file);
        else readImage(file);
    }

    var fileInput = document.getElementById("vdFile");
    fileInput.addEventListener("change", function () { readFile(fileInput.files[0]); fileInput.value = ""; });

    var drop = document.getElementById("vdDrop");
    ["dragenter", "dragover"].forEach(function (ev) {
        drop.addEventListener(ev, function (e) { e.preventDefault(); drop.classList.add("vd-drop-over"); });
    });
    ["dragleave", "drop"].forEach(function (ev) {
        drop.addEventListener(ev, function (e) { e.preventDefault(); drop.classList.remove("vd-drop-over"); });
    });
    drop.addEventListener("drop", function (e) { readFile(e.dataTransfer.files[0]); });

    // ---------- webcam ----------
    var video = document.getElementById("vdVideo");
    var stream = null;
    var lastScan = 0;

    function stopCamera() {
        if (stream) stream.getTracks().forEach(function (t) { t.stop(); });
        stream = null;
        video.srcObject = null;
        document.getElementById("vdCameraIdle").style.display = "";
        document.getElementById("vdStopCamera").style.display = "none";
    }

    function tick(time) {
        if (!stream) return;
        if (time - lastScan > 200 && video.readyState === video.HAVE_ENOUGH_DATA) {
            lastScan = time;
            drawToCanvas(video, video.videoWidth, video.videoHeight, 960);
            var data = ctx.getImageData(0, 0, canvas.width, canvas.height);
            var code = jsQR(data.data, canvas.width, canvas.height, { inversionAttempts: "dontInvert" });
            if (code) {
                var token = tokenOf(code.data);
                if (token) {
                    stopCamera();
                    showStatus("ok", '<i class="fa fa-check-circle"></i> QR code read. Opening the verification...');
                    openToken(token);
                    return;
                }
                showStatus("bad", '<i class="fa fa-times-circle"></i> This QR code is not a DCS signature.');
            }
        }
        requestAnimationFrame(tick);
    }

    document.getElementById("vdStartCamera").addEventListener("click", function () {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            showStatus("bad", '<i class="fa fa-times-circle"></i> This browser cannot use the camera here. Open DCS over HTTPS, or upload an image instead.');
            return;
        }
        hideStatus();
        navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment", width: { ideal: 1280 } } })
            .then(function (s) {
                stream = s;
                video.srcObject = s;
                video.play();
                document.getElementById("vdCameraIdle").style.display = "none";
                document.getElementById("vdStopCamera").style.display = "";
                requestAnimationFrame(tick);
            })
            .catch(function () {
                showStatus("bad", '<i class="fa fa-times-circle"></i> The camera could not be started. Allow camera access for this site, or upload an image instead.');
            });
    });
    document.getElementById("vdStopCamera").addEventListener("click", stopCamera);
    window.addEventListener("beforeunload", stopCamera);
})();
