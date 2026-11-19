// Reports > Organization Change History (2026-11-19). Server values: orgChangeReportConfig.
// DevExtreme grid like the other lists; the period filter goes to the server, the
// column filters / sorting stay in the grid. Excel / PDF use the period.
function ochParams() {
    return { dateFrom: $("#ochFrom").val(), dateTo: $("#ochTo").val() };
}

// one line per old / new unit
function ochLines(container, text) {
    (text || "").split("\n").forEach(function (line) {
        if (line) $("<div>").text(line).appendTo(container);
    });
}

$(document).ready(function () {
    var store = new DevExpress.data.CustomStore({
        key: "Id",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();
            $.ajax({ type: "GET", url: orgChangeReportConfig.urls.data, data: ochParams(), cache: false })
                .done(function (result) { d.resolve((result && result.data) || []); })
                .fail(function () { d.reject("Data loading error"); });
            return d.promise();
        }
    });

    $("#datagrid").dxDataGrid(dmsGridOptions({
        dataSource: store,
        height: computeGridHeight(),
        wordWrapEnabled: true,
        sorting: { mode: "multiple" },
        filterRow: { visible: false, applyFilter: "auto" },
        noDataText: "No organization change in this period.",
        columns: [
            {
                caption: "No", width: 50, alignment: "center", allowFiltering: false, allowSorting: false, allowHiding: false,
                fixed: true, fixedPosition: "left",
                cellTemplate: function (c, o) {
                    c.text(o.component.pageIndex() * o.component.pageSize() + o.row.rowIndex + 1);
                }
            },
            {
                dataField: "EffectiveDate", caption: "Tanggal Efektif", dataType: "date", format: "dd MMM yyyy",
                width: 130, alignment: "center", sortIndex: 0, sortOrder: "asc", fixed: true, fixedPosition: "left",
                cellTemplate: function (c, o) {
                    if (o.value) c.text(moment(o.value).format("DD MMM YYYY"));
                    else $("<span class='oc-missing'>").text("Not filled").appendTo(c);
                }
            },
            { dataField: "TypeLabel", caption: "Jenis Perubahan", minWidth: 160 },
            { dataField: "LevelLabel", caption: "Level", width: 110, alignment: "center" },
            {
                caption: "Unit Organisasi",
                columns: [
                    { dataField: "Old", caption: "Lama", minWidth: 180, cellTemplate: function (c, o) { ochLines(c, o.value); } },
                    { dataField: "New", caption: "Baru", minWidth: 200, cellTemplate: function (c, o) { ochLines(c, o.value); } }
                ]
            },
            {
                dataField: "HrReference", caption: "Referensi HR", minWidth: 150,
                cellTemplate: function (c, o) {
                    if (o.value) c.text(o.value);
                    else $("<span class='oc-missing'>").text("Not filled").appendTo(c);
                    if (o.data.HasAttachment) {
                        $("<a class='oc-file' target='_blank'>").attr("href", orgChangeReportConfig.urls.attachment + "?id=" + o.data.Id)
                            .html('<i class="fa fa-paperclip"></i> Memo').appendTo(c);
                    }
                }
            },
            { dataField: "Description", caption: "Keterangan", minWidth: 220 },
            {
                dataField: "State", caption: "Status Kode Lama", minWidth: 230,
                cellTemplate: function (c, o) { ochLines(c, o.value); }
            },
            {
                dataField: "CreatedBy", caption: "Dicatat Oleh", width: 130,
                cellTemplate: function (c, o) {
                    c.text(o.value || "");
                    if (o.data.CreatedDt) $("<span class='oc-sub'>").text(moment(o.data.CreatedDt).format("DD MMM YYYY")).appendTo(c);
                }
            }
        ]
    }));

    bindGridToolbar("columns filters reset refresh resize");

    // the period is read by the server: reload when it changes
    $("#ochFrom, #ochTo").on("change", function () { $("#datagrid").dxDataGrid("instance").refresh(); });
    $("#btn_excel").on("click", function () { window.location.href = orgChangeReportConfig.urls.excel + "?" + $.param(ochParams()); });
    $("#btn_pdf").on("click", function () { window.open(orgChangeReportConfig.urls.pdf + "?" + $.param(ochParams()), "_blank"); });
});
