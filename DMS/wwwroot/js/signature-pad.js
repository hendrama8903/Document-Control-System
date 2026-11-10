// SignaturePad.
(function () {
    var FONTS = ["Segoe Script", "Segoe Print", "Ink Free", "Gabriola"];   // = SignatureService.TypeFonts
    var W = 600, H = 200;
    var overlay = document.getElementById('esignOverlay');
    var canvas = document.getElementById('esignCanvas');
    var ctx = canvas.getContext('2d');
    var hint = document.getElementById('esignHint');
    var textInput = document.getElementById('esignText');
    var fontsBox = document.getElementById('esignFonts');
    var confirmBtn = document.getElementById('esignConfirm');
    var errorBox = document.getElementById('esignError');
    var strokes = [], current = null, mode = 'DRAW', font = FONTS[0], options = {};
    // Signing method from Administration > Signature Settings: TULIS (draw/type), QR or UPLOAD.
    var setting = 'TULIS', hasProfileImage = false, loading = false;
    var tabsBox = document.getElementById('esignTabs');
    var loadingBox = document.getElementById('esignLoading');
    var qrPanel = document.getElementById('esignQrPanel');
    var uploadPanel = document.getElementById('esignUploadPanel');

    // Kanvas tajam di layar high-DPI; koordinat yang disimpan tetap skala logis 600x200
    function setupCanvas() {
        var ratio = window.devicePixelRatio || 1;
        canvas.width = W * ratio; canvas.height = H * ratio;
        ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
        redraw();
    }

    function redraw() {
        ctx.clearRect(0, 0, W, H);
        ctx.strokeStyle = '#142878'; ctx.fillStyle = '#142878';
        ctx.lineWidth = 2.6; ctx.lineCap = 'round'; ctx.lineJoin = 'round';
        strokes.forEach(function (s) {
            if (s.length === 1) { ctx.beginPath(); ctx.arc(s[0][0], s[0][1], 1.4, 0, Math.PI * 2); ctx.fill(); return; }
            ctx.beginPath(); ctx.moveTo(s[0][0], s[0][1]);
            for (var i = 1; i < s.length - 1; i++) {
                var mx = (s[i][0] + s[i + 1][0]) / 2, my = (s[i][1] + s[i + 1][1]) / 2;
                ctx.quadraticCurveTo(s[i][0], s[i][1], mx, my);
            }
            ctx.lineTo(s[s.length - 1][0], s[s.length - 1][1]);
            ctx.stroke();
        });
        hint.style.display = strokes.length ? 'none' : '';
    }

    function point(e) {
        var r = canvas.getBoundingClientRect();
        return [Math.round((e.clientX - r.left) * W / r.width * 10) / 10, Math.round((e.clientY - r.top) * H / r.height * 10) / 10];
    }

    canvas.addEventListener('pointerdown', function (e) {
        e.preventDefault();
        canvas.setPointerCapture(e.pointerId);
        current = [point(e)]; strokes.push(current); redraw(); refresh();
    });
    canvas.addEventListener('pointermove', function (e) {
        if (!current) return;
        var p = point(e), last = current[current.length - 1];
        if (Math.abs(p[0] - last[0]) + Math.abs(p[1] - last[1]) < 1.5) return;   // buang titik yang terlalu rapat
        current.push(p); redraw();
    });
    ['pointerup', 'pointercancel', 'pointerleave'].forEach(function (ev) {
        canvas.addEventListener(ev, function () { current = null; refresh(); });
    });

    function pointCount() { return strokes.reduce(function (n, s) { return n + s.length; }, 0); }

    function refresh() {
        var ok = loading ? false
            : setting === 'QR' ? true
            : setting === 'UPLOAD' ? hasProfileImage
            : mode === 'DRAW' ? pointCount() >= 10 : textInput.value.trim().length >= 2;
        confirmBtn.disabled = !ok;
        errorBox.textContent = '';
        if (mode === 'TYPE') renderFonts();
    }

    function renderFonts() {
        var text = textInput.value.trim() || 'Your Name';
        fontsBox.innerHTML = '';
        FONTS.forEach(function (f) {
            var b = document.createElement('button');
            b.type = 'button'; b.className = 'esign-font' + (f === font ? ' active' : '');
            b.style.fontFamily = '"' + f + '", cursive'; b.textContent = text; b.title = f;
            b.onclick = function () { font = f; renderFonts(); };
            fontsBox.appendChild(b);
        });
    }

    function setMode(m) {
        mode = m;
        document.querySelectorAll('.esign-tab').forEach(function (t) { t.classList.toggle('active', t.getAttribute('data-tab') === m); });
        document.getElementById('esignDrawPanel').style.display = m === 'DRAW' ? '' : 'none';
        document.getElementById('esignTypePanel').style.display = m === 'TYPE' ? '' : 'none';
        if (m === 'DRAW') setupCanvas(); else setTimeout(function () { textInput.focus(); }, 0);
        refresh();
    }

    document.querySelectorAll('.esign-tab').forEach(function (t) {
        t.addEventListener('click', function () { setMode(t.getAttribute('data-tab')); });
    });
    document.getElementById('esignClear').onclick = function () { strokes = []; redraw(); refresh(); };
    textInput.addEventListener('input', refresh);

    function close() { overlay.classList.remove('open'); options = {}; }
    document.getElementById('esignCancel').onclick = close;
    document.getElementById('esignClose').onclick = close;
    document.addEventListener('keydown', function (e) { if (e.key === 'Escape' && overlay.classList.contains('open')) close(); });

    confirmBtn.onclick = function () {
        // QR token and the uploaded image are filled in by the server (SignatureService.Prepare).
        var sig = setting === 'QR' ? { SIGN_METHOD: 'QR', SIGN_DATA: 'CONFIRM', SIGN_FONT: '' }
            : setting === 'UPLOAD' ? { SIGN_METHOD: 'UPLOAD', SIGN_DATA: 'CONFIRM', SIGN_FONT: '' }
            : mode === 'DRAW'
            ? { SIGN_METHOD: 'DRAW', SIGN_DATA: JSON.stringify({ w: W, h: H, strokes: strokes }), SIGN_FONT: '' }
            : { SIGN_METHOD: 'TYPE', SIGN_DATA: textInput.value.trim(), SIGN_FONT: font };
        var callback = options.onSigned;
        close();
        if (callback) callback(sig);
    };

    window.openSignaturePad = function (opts) {
        options = opts || {};
        document.getElementById('esignTitle').textContent = options.title || 'Sign document';
        document.getElementById('esignSub').textContent = options.subtitle || '';
        document.getElementById('esignConsent').textContent = options.consent ||
            'By signing, I confirm that I have reviewed this document and that this electronic signature is mine.';
        confirmBtn.textContent = options.confirmText || 'Sign';
        strokes = []; textInput.value = options.defaultName || ''; font = FONTS[0];
        overlay.classList.add('open');
        showPanels(null);
        loading = true; refresh();

        fetch('/SignatureSetting/Current', { credentials: 'same-origin', cache: 'no-store' })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                if (!res || res.status !== true) throw new Error((res && res.message) || 'Signature settings could not be loaded.');
                setting = res.method || 'TULIS';
                hasProfileImage = !!res.profileImage;
                if (hasProfileImage) document.getElementById('esignUploadImg').src = res.profileImage;
                loading = false;
                showPanels(setting);
            })
            .catch(function (err) {
                loading = false;
                showPanels(null);
                refresh();
                errorBox.textContent = err.message || 'Signature settings could not be loaded. Please try again.';
            });
    };

    // null = only the loading text; otherwise the panels for that signing method.
    function showPanels(method) {
        loadingBox.style.display = method === null && loading ? '' : 'none';
        tabsBox.style.display = method === 'TULIS' ? '' : 'none';
        qrPanel.style.display = method === 'QR' ? '' : 'none';
        uploadPanel.style.display = method === 'UPLOAD' ? '' : 'none';
        document.getElementById('esignUploadPreview').style.display = hasProfileImage ? '' : 'none';
        document.getElementById('esignUploadText').style.display = hasProfileImage ? '' : 'none';
        document.getElementById('esignUploadMissing').style.display = hasProfileImage ? 'none' : '';
        if (method === 'TULIS') {
            setMode('DRAW');
        } else {
            document.getElementById('esignDrawPanel').style.display = 'none';
            document.getElementById('esignTypePanel').style.display = 'none';
            refresh();
        }
    }

    window.appendSignature = function (target, sig) {
        if (!sig) return target;
        if (typeof FormData !== 'undefined' && target instanceof FormData) {
            target.append('SIGN_METHOD', sig.SIGN_METHOD); target.append('SIGN_DATA', sig.SIGN_DATA); target.append('SIGN_FONT', sig.SIGN_FONT || '');
        } else {
            target.SIGN_METHOD = sig.SIGN_METHOD; target.SIGN_DATA = sig.SIGN_DATA; target.SIGN_FONT = sig.SIGN_FONT || '';
        }
        return target;
    };

    window.addEventListener('resize', function () { if (overlay.classList.contains('open') && mode === 'DRAW') setupCanvas(); });
})();
