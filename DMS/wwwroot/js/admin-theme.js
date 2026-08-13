// =====================================================================
// DMS Admin Theme — perilaku shell (pengganti widget AdminLTE)
// Dipakai oleh _AdminLayout.cshtml
// =====================================================================

$(function () {
    // Menu sidebar (pengganti data-widget="tree" AdminLTE) - tiap grup
    // di-toggle independen, tidak lagi accordion (dulu buka satu grup
    // otomatis nutup grup lain - request user 2026-08-12: harus bisa
    // buka banyak grup sekaligus).
    // Markup menu berasal dari session "menuString" (BaseController.GenerateUL).
    $(document).on('click', '.adm-sidepanel .treeview > a', function (e) {
        e.preventDefault();
        $(this).parent().toggleClass('open');
    });

    // Collapse box (pengganti data-widget="collapse" AdminLTE).
    // Catatan: toggle ikon chevron utk #searchCard sudah ditangani site.js.
    $(document).on('click', '[data-widget="collapse"]', function () {
        $(this).closest('.box').toggleClass('collapsed-box');
        setTimeout(function () {
            if (typeof resizeGrid === 'function') {
                resizeGrid();
            }
        }, 100);
    });
});

// Drawer sidebar di layar kecil
function admToggleNav(open) {
    document.getElementById('admSidebar').classList.toggle('open', open);
    document.getElementById('admScrim').classList.toggle('show', open);
}

// Collapse/expand panel menu di desktop (sisakan rail)
function admToggleCollapse() {
    document.getElementById('admApp').classList.toggle('collapsed');
    setTimeout(function () {
        if (typeof resizeGrid === 'function') {
            resizeGrid();
        }
    }, 300);
}
