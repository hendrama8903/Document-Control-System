// WorkflowDoc. Server values come from workflowDocConfig in the view.
var gCanEdit = workflowDocConfig.canEdit;
var gCanDelete = workflowDocConfig.canDelete;
var gCanAdd = workflowDocConfig.canAdd;

function updateActionButtons() {
    var row = getSelectedRowData();
    var selected = row != null;

    $("#btn_edit").prop("disabled", !selected);
    $("#btn_delete").prop("disabled", !selected);
    $("#btn_duplicate").prop("disabled", !selected);
    $("#btn_toggle_active").prop("disabled", !selected);
    $("#btn_toggle_active span").text(selected && row.IS_ACTIVE === false ? "Activate" : "Deactivate");
}

function wfRenderStats(data) {
    var total = data.length;
    var levels = new Set(data.map(function (r) { return r.DOCUMENT_LEVEL; })).size;
    var steps = data.reduce(function (sum, r) { return sum + (parseInt(r.WORKFLOW_SEQ) || 0); }, 0);

    var lastUpdated = null;
    data.forEach(function (r) {
        var dt = r.CHANGED_DT || r.CREATED_DT;
        if (dt != null && (lastUpdated == null || moment(dt).isAfter(lastUpdated))) {
            lastUpdated = dt;
        }
    });

    $("#wfStatTotal").text(total);
    $("#wfStatLevels").text(levels);
    $("#wfStatSteps").text(steps);
    $("#wfStatLastUpdated").html(lastUpdated != null
        ? moment(lastUpdated).format("DD MMM YYYY") + "<br><span style='font-size:11px;font-weight:600;color:#66788A;'>" + moment(lastUpdated).format("HH:mm") + "</span>"
        : "-");
}

$(document).ready(function () {
    $("#btn_add").prop("disabled", false);
    $("#btn_edit").prop("disabled", true);
    $("#btn_delete").prop("disabled", true);
    $("#btn_duplicate").prop("disabled", true);
    $("#btn_toggle_active").prop("disabled", true);

    var gridStore = new DevExpress.data.CustomStore({
        key: "WORKFLOW_DOC_ID",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: workflowDocConfig.urls.search,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000,
                    initialMode: false
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                wfRenderStats(data);
                d.resolve(data);
            }).fail(function () {
                d.reject("Data loading error");
            });

            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: gridStore,
        height: computeGridHeight(),
        filterRow: { visible: false, applyFilter: "auto" },
        searchPanel: { visible: false, highlightCaseSensitive: false },
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
        },
        onSelectionChanged: function (e) {
            if (e.selectedRowKeys.length > 1) {
                e.component.selectRows(e.currentSelectedRowKeys, false);
                return;
            }
            updateActionButtons();
        },
        onRowPrepared: function (e) {
            if (e.rowType === "data" && e.data.IS_ACTIVE === false) {
                e.rowElement.addClass("wf-row-inactive");
            }
        },
        columns: [
            { type: "selection", width: 40, fixed: true, fixedPosition: "left" },
            {
                caption: "No",
                width: 50,
                alignment: "center",
                allowHiding: false,
                allowFiltering: false,
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    var pageIndex = options.component.pageIndex();
                    var pageSize = options.component.pageSize();
                    container.text(pageIndex * pageSize + options.row.rowIndex + 1);
                }
            },
            {
                dataField: "DOCUMENT_LEVEL",
                caption: "Document Level",
                alignment: "center",
                fixed: true,
                fixedPosition: "left",
                cellTemplate: function (container, options) {
                    $("<span>").addClass("wf-level-pill").text("Level " + options.data.DOCUMENT_LEVEL).appendTo(container);
                }
            },
            {
                // Kosong = alur default level (berlaku untuk semua kategori di level
                // itu yang tidak punya alur khusus); terisi = alur khusus kategori.
                dataField: "DOCUMENT_CODE",
                caption: "Document Category",
                cellTemplate: function (container, options) {
                    if (options.data.DOCUMENT_ID) {
                        $("<span>").addClass("wf-cat-pill").text(options.data.DOCUMENT_CODE).appendTo(container);
                    } else {
                        $("<span>").addClass("wf-cat-default").text("Default (all categories)").appendTo(container);
                    }
                }
            },
            {
                caption: "Status",
                alignment: "center",
                calculateCellValue: function (rowData) { return rowData.IS_ACTIVE === false ? "Inactive" : "Active"; },
                cellTemplate: function (container, options) {
                    $("<span>").addClass(options.value === "Active" ? "wf-status-active" : "wf-status-inactive").text(options.value).appendTo(container);
                }
            },
            { dataField: "CREATOR_NAME", caption: "Creator Level" },
            { dataField: "WORKFLOW_SEQ", caption: "Total Steps", alignment: "center" },
            {
                dataField: "POSITION_NAME",
                caption: "Approver Chain",
                cellTemplate: function (container, options) {
                    container.text(options.data.POSITION_NAME || "");
                    if ((options.data.POSITION_NAME || "").indexOf("*") >= 0) {
                        container.attr("title", "* = mandatory approver");
                    }
                }
            },
            {
                dataField: "NOTES",
                caption: "Notes",
                cellTemplate: function (container, options) {
                    if (options.data.NOTES) {
                        $("<span>").addClass("wf-note-warn")
                            .html('<i class="fa fa-exclamation-triangle"></i> ')
                            .append(document.createTextNode(options.data.NOTES))
                            .appendTo(container);
                    }
                }
            },
            { dataField: "CREATED_BY", caption: "Created By", visible: false },
            {
                dataField: "CREATED_DT",
                caption: "Created Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CREATED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            { dataField: "CHANGED_BY", caption: "Changed By", visible: false },
            {
                dataField: "CHANGED_DT",
                caption: "Changed Date",
                alignment: "right",
                visible: false,
                cellTemplate: function (container, options) {
                    var value = options.data.CHANGED_DT;
                    container.text(value != null ? moment(value).format("DD-MM-YYYY") : "");
                }
            },
            {
                caption: "Action",
                alignment: "center",
                width: 70,
                allowHiding: false,
                allowFiltering: false,
                fixed: true,
                fixedPosition: "right",
                cellTemplate: function (container, options) {
                    var row = options.data;

                    $("<button>")
                        .attr({ type: "button", title: "More actions" })
                        .addClass("dp-act-kebab")
                        .html('<i class="fa fa-ellipsis-v"></i>')
                        .on("click", function (e) {
                            e.stopPropagation();
                            openRowActionMenu($(this), row);
                        })
                        .appendTo(container);
                }
            }
        ]
    }));

    bindGridToolbar("columns filters reset refresh resize");

    $('body').tooltip({ selector: '[data-toggle="tooltip"]' });
});

function openRowActionMenu($btn, row) {
    var $menu = buildRowActionMenu();
    $menu.empty();
    var grid = $("#datagrid").dxDataGrid("instance");

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-pencil-square-o", "Edit", function () {
            grid.selectRows([row.WORKFLOW_DOC_ID], false);
            onEditPrepare();
        }));
    }

    if (gCanAdd) {
        $menu.append(menuItem("dp-menu-edit", "fa-files-o", "Duplicate", function () {
            onDuplicatePrepare(row);
        }));
    }

    if (gCanEdit) {
        $menu.append(menuItem("dp-menu-edit", "fa-power-off", row.IS_ACTIVE === false ? "Activate" : "Deactivate", function () {
            onToggleActivePrepare(row);
        }));
    }

    if (gCanDelete) {
        $menu.append('<li class="divider"></li>');
        $menu.append(menuItem("dp-menu-delete", "fa-trash-o", "Delete", function () {
            grid.selectRows([row.WORKFLOW_DOC_ID], false);
            onDeletePrepare();
        }));
    }

    var offset = $btn.offset();
    $menu.css({ top: offset.top + $btn.outerHeight() + 4, left: offset.left - $menu.outerWidth() + $btn.outerWidth() })
        .show();
}

var gScreenMode = null;
var gId = null;
var gCode = null;
var gName = null;
var seq = 1;
var detailData = [];
var detailTable = null;
// Tombol "Add" di section "Add New Level & Position" dobel fungsi jadi
// "Update" begitu satu baris dipilih (lewat klik baris atau ikon pensil) -
// tidak ada tombol Edit terpisah lagi seperti desain lama (request Hendra
// 2026-08-18, redesign UI popup ini).
var gDetailEditMode = false;

// Kolom "Action" (ikon edit/delete per baris). Semua baris boleh dihapus: level
// sesudahnya otomatis naik satu (renumberDetail), jadi urutan tetap rapat
// (2026-11-10; dulu hanya level paling tinggi).
var detailActionColumnDef = {
    "targets": [4],
    "className": "text-center",
    "orderable": false,
    "render": function () {
        return '<button type="button" class="wfd-row-btn wfd-row-edit" title="Edit"><i class="fa fa-pencil"></i></button>' +
            '<button type="button" class="wfd-row-btn wfd-row-delete" title="Delete"><i class="fa fa-trash"></i></button>';
    }
};

// Konfigurasi DataTable detail - sama untuk Add dan Edit.
function buildDetailTable() {
    $("#detailgrid").dataTable().fnDestroy();
    detailTable = $('#detailgrid').DataTable({
        "ordering"      : false,
        "oLanguage"     : { "sEmptyTable": "Data not found." },
        "scrollY"       : '190px',
        "scrollCollapse": false,
        "paging"        : false,
        "info"          : false,
        "searching"     : false,
        "data"          : [],
        "columnDefs"    : [
            {
                "targets"   : [0],
                "width"     : "16%",
                "className" : "seq text-center",
                "render"    : function (data) {
                    return '<span class="wfd-drag" title="Drag to change the order"><i class="fa fa-bars"></i></span>' +
                        '<span class="wfd-level-badge">' + data + '</span>';
                }
            },
            {
                "targets": [2],
                "className": "hide"
            },
            {
                "targets": [3],
                "className": "text-center",
                "render": function (data) { return data === true || data === "true" ? '<span class="wfd-mandatory-badge">Mandatory</span>' : '-'; }
            },
            detailActionColumnDef
        ],
        "columns": [
            { "data": "WORKFLOW_SEQ", "name": "WORKFLOW_SEQ" },
            { "data": "POSITION_NAME", "name": "POSITION_NAME" },
            { "data": "POSITION_ID", "name": "POSITION_ID" },
            { "data": "IS_MANDATORY", "name": "IS_MANDATORY" },
            { "data": null, "name": "ACTION", "defaultContent": "" }
        ]
    });
}

// Satu jalur untuk semua perubahan daftar langkah: seq (level berikutnya) harus
// sudah benar SEBELUM tabel digambar, karena kolom Action memakainya untuk
// menentukan baris mana yang boleh dihapus.
function renderDetail() {
    detailData.sort((a, b) => parseInt(a.WORKFLOW_SEQ) - parseInt(b.WORKFLOW_SEQ));
    renumberDetail();
    seq = detailData.length + 1;

    detailTable.clear().draw();
    detailTable.rows.add(detailData).draw(false);

    $('#addEditWorkflowSeq').empty();
    for (let i = 1; i <= seq; i++) {
        $('#addEditWorkflowSeq').append(new Option(i, i, false, false));
    }
    onClearDetail();
}

// Level = position in the list, 1..n with no gaps (after delete, insert or drag).
function renumberDetail() {
    detailData.forEach(function (d, i) { d.WORKFLOW_SEQ = i + 1; });
}

// Drag & drop order (request Hendra 2026-11-10): native HTML5 drag on the grip in the Level
// column. A row is only draggable while its grip is held, so clicking a row still selects it.
var gDragIndex = null;

function bindDetailDragDrop() {
    var $grid = $("#detailgrid");

    $grid.on("mousedown", ".wfd-drag", function () {
        if (detailData.length > 1) $(this).closest("tr").attr("draggable", "true");
    });
    $(document).on("mouseup", function () {
        $("#detailgrid tbody tr[draggable]").removeAttr("draggable");
    });

    $grid.on("dragstart", "tbody tr", function (e) {
        gDragIndex = $(this).index();
        e.originalEvent.dataTransfer.effectAllowed = "move";
        e.originalEvent.dataTransfer.setData("text/plain", String(gDragIndex));
        $(this).addClass("wfd-dragging");
    });

    $grid.on("dragover", "tbody tr", function (e) {
        if (gDragIndex === null) return;
        e.preventDefault();
        e.originalEvent.dataTransfer.dropEffect = "move";
        var rect = this.getBoundingClientRect();
        var below = e.originalEvent.clientY > rect.top + rect.height / 2;
        $("#detailgrid tbody tr").removeClass("wfd-drop-above wfd-drop-below");
        $(this).addClass(below ? "wfd-drop-below" : "wfd-drop-above");
    });

    $grid.on("drop", "tbody tr", function (e) {
        if (gDragIndex === null) return;
        e.preventDefault();
        var target = $(this).index();
        if ($(this).hasClass("wfd-drop-below")) target++;
        var from = gDragIndex;
        endDetailDrag();

        if (target > from) target--;
        if (target === from) return;
        var moved = detailData.splice(from, 1)[0];
        detailData.splice(target, 0, moved);
        renumberDetail();
        renderDetail();
    });

    $grid.on("dragend", "tbody tr", endDetailDrag);
}

function endDetailDrag() {
    gDragIndex = null;
    $("#detailgrid tbody tr").removeClass("wfd-dragging wfd-drop-above wfd-drop-below").removeAttr("draggable");
}

function mapDetailRows(data) {
    return data.map(function (d) {
        return {
            WORKFLOW_SEQ : d.WORKFLOW_SEQ,
            POSITION_NAME: d.POSITION_NAME,
            POSITION_ID  : d.POSITION_ID,
            IS_MANDATORY : d.IS_MANDATORY === true,
            LABEL        : d.LABEL
        };
    });
}

// Peringatan dampak dari server, lalu konfirmasi sebelum perubahan dijalankan.
function confirmWithImpact(id, action, question, onYes) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: workflowDocConfig.urls.getImpact,
        data: { id: id, action: action },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
                var lines = result.lines.concat([question]).map(function (line) { return $("<div>").text(line).html(); });
                var message = lines.join("<br><br>");
                if (action == "DELETE") {
                    showDangerConfirm(message, onYes, "Delete");
                } else if (result.warning) {
                    showWarningConfirm(message, onYes, "Deactivate");
                } else {
                    showConfirmMesg(message, onYes, "Confirmation");
                }
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function () {
            popUpProgressHide();
            showErrorMesgGrowl("Could not check the impact of this change.");
        }
    });
}

// Dropdown kategori bergantung pada Document Level terpilih.
function setCategorySelect(level) {
    setDataSelect2WithParam("addEditWorkflowCategory", workflowDocConfig.urls.getDocumentCategories, "- Default (all categories) -", level || "");
}

$(document).ready(function() {

    setDataSelect2WithParam("addEditWorkflowDocLevel", workflowDocConfig.urls.systemValues, "- Choose -", "LEVEL_CODE");
    // CREATOR_LEVEL = POSITION_LEVEL pembuat (bukan ID jabatan) - satu
    // pilihan per level, berisi semua jabatan di level itu.
    setDataSelect2("addEditPositionName", workflowDocConfig.urls.getCreatorLevels, "- Choose -");
    setDataSelect2("addEditPositionName2", workflowDocConfig.urls.getPositionName, "- Choose -");
    setCategorySelect("");

    $("#addEditWorkflowDocLevel").on("change", function () {
        if (gScreenMode == 'EDIT') return;
        $("#addEditWorkflowCategory").val(null).trigger("change");
        setCategorySelect($(this).val());
    });

    $("#btn_add").on("click", function() {
        onAddPrepare();
    });

    $("#btn_edit").on("click", function() {
        onEditPrepare();
    });

    $("#btn_delete").on("click", function() {
        onDeletePrepare();
    });

    $("#btn_duplicate").on("click", function() {
        onDuplicatePrepare(getSelectedRowData());
    });

    $("#btn_toggle_active").on("click", function() {
        onToggleActivePrepare(getSelectedRowData());
    });

    $("#btn_save").on("click", function() {
        confirmAddEditData();
    });

    // Tombol tunggal "Add"/"Update" - lihat gDetailEditMode.
    $("#btn_add_detail").on("click", function () {
        if (gDetailEditMode) {
            onEditDetail();
        } else {
            onAddDetail();
        }
    });

    $("#btn_clear_detail").on("click", function () {
        onClearDetail();
    });

    $("#detailgrid").on("click", "tbody tr", function (e) {
        if ($(e.target).closest(".wfd-drag").length) return;
        onSelectDetail($(this));
    });

    bindDetailDragDrop();

    $("#detailgrid").on("click", ".wfd-row-edit", function (e) {
        e.stopPropagation();
        onSelectDetail($(this).closest("tr"));
    });

    $("#detailgrid").on("click", ".wfd-row-delete", function (e) {
        e.stopPropagation();
        if ($(this).prop("disabled")) return;
        onSelectDetail($(this).closest("tr"));
        onDeleteDetail();
    });

    bindGridToolbar("stats");

});

function onAddPrepare() {
    gScreenMode = 'ADD';
    gId = null;
    $("#datagrid").dxDataGrid("instance").clearSelection();
    $('#modal-title').html("New Workflow");
    $("#addEditWorkflowDocLevel").val("").trigger("change");
    $("#addEditPositionName").val("").trigger("change");
    $("#addEditWorkflowCategory").val(null).trigger("change");
    setCategorySelect("");
    $("#addEditWorkflowSeq").val("").trigger("change");
    $("#addEditPositionName2").val("").trigger("change");

    $('#addEditWorkflowDocLevel').prop('disabled', false);
    $('#addEditPositionName').prop('disabled', false);
    $('#addEditWorkflowCategory').prop('disabled', false);
    $('#addEditWorkflowSeq').prop('disabled', false);
    $('#addEditPositionName2').prop('disabled', false);

    buildDetailTable();

    seq = 1;
    detailData = [];
    onClearDetail();

    $('#addEditWorkflowSeq').empty();
    $('#addEditWorkflowSeq').append(new Option(seq, seq, false, false)).trigger('change');

    $("#modalAddEdit").modal({
        backdrop: 'static',
        keyboard: false
    });
    $('#modalAddEdit').on('shown.bs.modal', function() {
        $("#detailgrid").DataTable().columns.adjust().draw();
    });
}

function onEditPrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gId = row.WORKFLOW_DOC_ID;
    gCode = row.DOCUMENT_LEVEL;
    gName = row.CREATOR_LEVEL;

    $.ajax({
        type: "POST",
        url: workflowDocConfig.urls.getByCode,
        async: false,
        data: {
            WORKFLOW_DOC_ID: gId,
            DOCUMENT_LEVEL : gCode
        },
        success: function(result) {
            if (result.status == true) {
                var data = result.data;
                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit Workflow" + (row.IS_ACTIVE === false ? " (Inactive)" : ""));
                $('#addEditWorkflowDocLevel').select2("trigger", "select", { data: { id: gCode.toString(), text: gCode.toString() } });
                $('#addEditPositionName').select2("trigger", "select", { data: { id: gName.toString(), text: result.creatorText || ("Level " + gName) } });
                if (row.DOCUMENT_ID) {
                    $('#addEditWorkflowCategory').select2("trigger", "select", { data: { id: row.DOCUMENT_ID.toString(), text: result.categoryText || row.DOCUMENT_CODE } });
                } else {
                    $('#addEditWorkflowCategory').val(null).trigger("change");
                }

                // Level, level pembuat, dan kategori adalah kunci alur - tidak
                // bisa diubah saat edit (buat alur baru kalau perlu kombinasi lain).
                $("#addEditWorkflowDocLevel").prop("disabled", true);
                $("#addEditPositionName").prop("disabled", true);
                $("#addEditWorkflowCategory").prop("disabled", true);

                buildDetailTable();

                detailData = mapDetailRows(data);
                renderDetail();

                $("#modalAddEdit").modal({
                    backdrop: 'static',
                    keyboard: false
                });
                $('#modalAddEdit').on('shown.bs.modal', function() {
                    $("#detailgrid").DataTable().columns.adjust().draw();
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            showErrorMesgGrowl(result);
        }
    });
}

function onDeletePrepare() {

    var row = getSelectedRowData();
    if (row == null) {
        return;
    }

    gId = row.WORKFLOW_DOC_ID;

    confirmWithImpact(gId, "DELETE", "Delete this Level " + row.DOCUMENT_LEVEL + " workflow? This cannot be undone.", deleteData);
}

function onToggleActivePrepare(row) {
    if (row == null) {
        return;
    }

    var activate = row.IS_ACTIVE === false;
    confirmWithImpact(row.WORKFLOW_DOC_ID, activate ? "ACTIVATE" : "DEACTIVATE",
        activate ? "Activate this workflow?" : "Deactivate this workflow?",
        function () { setActive(row.WORKFLOW_DOC_ID, activate); });
}

function setActive(id, isActive) {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: workflowDocConfig.urls.setActive,
        data: { id: id, isActive: isActive },
        success: function (result) {
            popUpProgressHide();
            if (result.status == true) {
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

// Duplikat = form Add yang sudah terisi dari alur sumber. Disimpan sebagai alur
// baru; kombinasi yang sudah punya alur aktif ditolak server saat disimpan.
function onDuplicatePrepare(row) {
    if (row == null) {
        return;
    }

    $.ajax({
        type: "POST",
        url: workflowDocConfig.urls.getByCode,
        data: { WORKFLOW_DOC_ID: row.WORKFLOW_DOC_ID, DOCUMENT_LEVEL: row.DOCUMENT_LEVEL },
        success: function (result) {
            if (result.status != true) {
                showErrorMesgGrowl(result.message);
                return;
            }

            onAddPrepare();
            $('#modal-title').html("Duplicate Workflow");
            $('#addEditWorkflowDocLevel').select2("trigger", "select", { data: { id: row.DOCUMENT_LEVEL.toString(), text: row.DOCUMENT_LEVEL.toString() } });
            $('#addEditPositionName').select2("trigger", "select", { data: { id: row.CREATOR_LEVEL.toString(), text: result.creatorText || ("Level " + row.CREATOR_LEVEL) } });
            if (row.DOCUMENT_ID) {
                $('#addEditWorkflowCategory').select2("trigger", "select", { data: { id: row.DOCUMENT_ID.toString(), text: result.categoryText || row.DOCUMENT_CODE } });
            }

            detailData = mapDetailRows(result.data);
            renderDetail();
            showSuccessMesgGrowl("Choose a different Document Category or Creator Level, then save.");
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}

// Ganti tampilan tombol Add/Update sesuai gDetailEditMode.
function refreshDetailActionButton() {
    var $btn = $("#btn_add_detail");
    if (gDetailEditMode) {
        $btn.addClass("wfd-btn-update").html('<i class="fa fa-check"></i> Update');
    } else {
        $btn.removeClass("wfd-btn-update").html('<i class="fa fa-plus"></i> Add');
    }
}

function onClearDetail() {
    $('#addEditWorkflowSeq').val(seq).trigger("change");
    $('#addEditPositionName2').val("").trigger("change");
    $('#addEditMandatory').prop("checked", false);

    $("#addEditWorkflowSeq").prop("disabled", false);
    $("#btn_add_detail").prop("disabled", false);

    gDetailEditMode = false;
    refreshDetailActionButton();

    $('#detailgrid tr').removeClass("selected");
}

function onSelectDetail($row) {
    if ($row.find("td:nth-child(1)").hasClass("dataTables_empty")) return;
    var d = detailTable.row($row).data();
    if (!d) return;

    $('#addEditWorkflowSeq').val(d.WORKFLOW_SEQ).trigger("change");
    $('#addEditPositionName2').select2("trigger", "select", { data: { id: d.POSITION_ID, text: d.POSITION_NAME } });
    $('#addEditMandatory').prop("checked", d.IS_MANDATORY === true);

    $("#addEditWorkflowSeq").prop("disabled", true);

    gDetailEditMode = true;
    refreshDetailActionButton();

    $('#detailgrid tr').removeClass("selected");
    $row.addClass('selected');
}

function readDetailForm(previous) {
    var positionId = $('#addEditPositionName2').val();
    return {
        "WORKFLOW_SEQ" : $('#addEditWorkflowSeq').val(),
        "POSITION_NAME": $('#addEditPositionName2').select2('data')[0].text,
        "POSITION_ID"  : positionId,
        "IS_MANDATORY" : $('#addEditMandatory').is(':checked'),
        // Label lama ("Dibuat Oleh", dst.) dipertahankan selama jabatannya
        // tidak diganti; jabatan baru -> label = nama jabatan (diisi SP).
        "LABEL"        : previous && String(previous.POSITION_ID) == String(positionId) ? previous.LABEL : null
    };
}

function onAddDetail(){
    if ($('#addEditPositionName2').val() != null) {
        // Choosing a level that already exists inserts the position there and moves the
        // later levels down one, instead of putting two positions on the same level.
        var item = readDetailForm(null);
        var level = parseInt(item.WORKFLOW_SEQ) || seq;
        detailData.forEach(function (d) {
            if (parseInt(d.WORKFLOW_SEQ) >= level) d.WORKFLOW_SEQ = parseInt(d.WORKFLOW_SEQ) + 1;
        });
        item.WORKFLOW_SEQ = level;
        detailData.push(item);
        renderDetail();
    }
}

function onEditDetail(){
    if ($('#addEditPositionName2').val() != null) {
        var i = $('#detailgrid tr.selected').index();

        detailData[i] = readDetailForm(detailData[i]);
        renderDetail();
    }
}

function onDeleteDetail(){
    if ($('#addEditPositionName2').val() != null) {
        var i = $('#detailgrid tr.selected').index();

        detailData.splice(i, 1);
        renderDetail();
    }
}

function confirmAddEditData() {
    if (!$("#addEditWorkflowDocLevel").val()) {
        showErrorMesgGrowl("Document Level cannot be empty");
    }
    else if (!$("#addEditPositionName").val()) {
        showErrorMesgGrowl("Creator Level cannot be empty");
    }
    else if (detailData.length == 0) {
        showErrorMesgGrowl("Detail Workflow cannot be empty");
    }
    else if (gScreenMode == 'EDIT') {
        var row = getSelectedRowData();
        confirmWithImpact(row ? row.WORKFLOW_DOC_ID : gId, "EDIT", "Are you sure you want to continue process?", addEditData);
    }
    else {
        showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
    }
}

function addEditData() {
    var row = getSelectedRowData();
    if (gScreenMode == 'EDIT' && row != null) {
        gId = row.WORKFLOW_DOC_ID;
    }
    if (gScreenMode == 'ADD') {
        gId = null;
    }

    var category = $('#addEditWorkflowCategory').val();
    detailData.forEach(function (data) {
        data.WORKFLOW_DOC_ID = gId;
        data.DOCUMENT_LEVEL  = $('#addEditWorkflowDocLevel').val();
        data.CREATOR_LEVEL   = $('#addEditPositionName').val();
        data.DOCUMENT_ID     = category ? category : null;
    });

    popUpProgressShow();
    $.ajax({
        contentType: 'application/json; charset=utf-8',
        type       : "POST",
        url        : workflowDocConfig.urls.addEdit,
        data       : JSON.stringify(detailData),
        success: function(result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                $("#modalAddEdit").modal('hide');
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function deleteData() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: workflowDocConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            WORKFLOW_DOC_ID: gId
        },
        success: function(result) {
            popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}
