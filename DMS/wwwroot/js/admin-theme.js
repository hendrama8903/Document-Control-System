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

    admBuildRailNav();
    admSyncCollapseButton();
});

// Drawer sidebar di layar kecil
function admToggleNav(open) {
    document.getElementById('admSidebar').classList.toggle('open', open);
    document.getElementById('admScrim').classList.toggle('show', open);
}

// Collapse/expand panel menu di desktop (sisakan rail)
function admToggleCollapse() {
    var collapsed = document.getElementById('admApp').classList.toggle('collapsed');
    try {
        localStorage.setItem('dms.sidebarCollapsed', collapsed ? '1' : '0');
    } catch (e) { /* tanpa storage, pilihan hanya berlaku di halaman ini */ }

    admCloseRailFlyout();
    admSyncCollapseButton();

    // Setelah animasi lebar selesai (.26s).
    setTimeout(function () {
        if (typeof resizeGrid === 'function') {
            resizeGrid();
        }
        // Tanpa hitung ulang, browser meregangkan tabel utama tapi kolom fixed tidak
        // ikut, sehingga keduanya tidak sejajar.
        if (window.DevExpress && DevExpress.ui && DevExpress.ui.dxDataGrid) {
            $('.dx-widget:visible').each(function () {
                var grid = DevExpress.ui.dxDataGrid.getInstance(this);
                if (grid) {
                    grid.updateDimensions();
                }
            });
        }
    }, 300);
}

function admSyncCollapseButton() {
    var collapsed = document.getElementById('admApp').classList.contains('collapsed');
    $('.adm-rail-collapse')
        .attr('title', collapsed ? 'Show menu' : 'Hide menu')
        .attr('aria-expanded', collapsed ? 'false' : 'true');
}

// Ikon di rail saat sidebar diciutkan, dibangun dari menu sidepanel supaya isinya
// tetap mengikuti hak akses role (menuString). Grup membuka submenu melayang.
var admFlyoutCloseTimer = null;

function admCloseRailFlyout() {
    clearTimeout(admFlyoutCloseTimer);
    $('#admRailFlyout').removeClass('show');
    $('.adm-rail-item.flyout-open').removeClass('flyout-open').attr('aria-expanded', 'false');
}

function admOpenRailFlyout($item) {
    admCloseRailFlyout();

    var $li = $item.data('menuLi');
    var $flyout = $('#admRailFlyout').empty();
    $('<div class="adm-rail-flyout-title">').text($item.attr('aria-label')).appendTo($flyout);
    $li.find('.treeview-menu a').each(function () {
        var $link = $(this);
        $link.clone().toggleClass('active', $link.parent().hasClass('active')).appendTo($flyout);
    });

    // Tampil dulu supaya tingginya terukur, lalu jaga tetap di dalam layar.
    $flyout.addClass('show');
    var top = $item[0].getBoundingClientRect().top;
    var maxTop = window.innerHeight - $flyout.outerHeight() - 8;
    $flyout.css('top', Math.max(8, Math.min(top, maxTop)));

    $item.addClass('flyout-open').attr('aria-expanded', 'true');
}

function admScheduleFlyoutClose() {
    clearTimeout(admFlyoutCloseTimer);
    admFlyoutCloseTimer = setTimeout(admCloseRailFlyout, 200);
}

function admBuildRailNav() {
    var $nav = $('<nav class="adm-rail-nav" aria-label="Main menu">');

    $('.adm-sidepanel .sidebar-menu > li').each(function () {
        var $li = $(this);
        var $link = $li.children('a');
        var label = $.trim($link.find('span').first().text());
        var $item = $li.hasClass('treeview')
            ? $('<button type="button" class="adm-rail-item" aria-haspopup="true" aria-expanded="false">').data('menuLi', $li)
            : $('<a class="adm-rail-item">').attr({ href: $link.attr('href'), title: label });

        $item.attr('aria-label', label)
            .toggleClass('active', $li.hasClass('active'))
            .append($('<i>').attr('class', $link.children('.fa').first().attr('class')))
            .appendTo($nav);
    });

    $nav.insertAfter('.adm-rail-ic');
    var $flyout = $('<div class="adm-rail-flyout" id="admRailFlyout">').appendTo('body');

    $nav.on('mouseenter', 'button.adm-rail-item', function () {
        admOpenRailFlyout($(this));
    });
    $nav.on('mouseleave', 'button.adm-rail-item', admScheduleFlyoutClose);
    $flyout.on('mouseenter', function () {
        clearTimeout(admFlyoutCloseTimer);
    });
    $flyout.on('mouseleave', admScheduleFlyoutClose);

    // Mouse sudah terbuka lewat hover, jadi klik tidak menutupnya. e.detail 0 = keyboard.
    $nav.on('click', 'button.adm-rail-item', function (e) {
        var $item = $(this);
        if (e.detail === 0 && $item.hasClass('flyout-open')) {
            admCloseRailFlyout();
            return;
        }
        admOpenRailFlyout($item);
        if (e.detail === 0) {
            $flyout.find('a').first().focus();
        }
    });

    $flyout.on('focusout', function (e) {
        if (!e.relatedTarget || !$.contains(this, e.relatedTarget)) {
            admScheduleFlyoutClose();
        }
    });
    $flyout.on('focusin', function () {
        clearTimeout(admFlyoutCloseTimer);
    });

    $(document).on('keydown', function (e) {
        if (e.key === 'Escape' && $flyout.hasClass('show')) {
            var $opener = $('.adm-rail-item.flyout-open');
            admCloseRailFlyout();
            $opener.focus();
        }
    });
    $(document).on('click', function (e) {
        if (!$(e.target).closest('.adm-rail-item, #admRailFlyout').length) {
            admCloseRailFlyout();
        }
    });
    $(window).on('resize', admCloseRailFlyout);
}
