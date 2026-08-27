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

    // Search menu (kolom yang menggantikan slot logo/brand di sidepanel -
    // request Hendra 2026-08-16). Cocokkan teks tiap item terhadap query;
    // grup (treeview) ikut tampil kalau salah satu anaknya cocok, dan
    // otomatis dibuka lewat class .search-open (terpisah dari .open manual
    // supaya mengosongkan pencarian tidak ikut menutup grup yang memang
    // sedang dibuka user).
    $(document).on('input', '#admMenuSearch', function () {
        var query = $(this).val().trim().toLowerCase();
        var $topLevel = $('.adm-sidepanel .sidebar-menu > li');

        if (query === '') {
            $topLevel.add($topLevel.find('li')).removeClass('search-hide search-open');
            return;
        }

        $topLevel.each(function () {
            var $li = $(this);
            var $submenuItems = $li.find('.treeview-menu > li');

            if ($submenuItems.length === 0) {
                var text = $li.children('a').find('span').first().text().toLowerCase();
                $li.toggleClass('search-hide', text.indexOf(query) === -1);
                return;
            }

            var ownText = $li.children('a').find('span').first().text().toLowerCase();
            var ownMatch = ownText.indexOf(query) !== -1;
            var anyChildMatch = false;

            $submenuItems.each(function () {
                var $child = $(this);
                var childText = $child.children('a').find('span').first().text().toLowerCase();
                var childMatch = ownMatch || childText.indexOf(query) !== -1;
                $child.toggleClass('search-hide', !childMatch);
                anyChildMatch = anyChildMatch || childMatch;
            });

            $li.toggleClass('search-hide', !anyChildMatch);
            $li.toggleClass('search-open', anyChildMatch);
        });
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
