// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.
$(document).ready(function () {
    $(".select2s").select2({
        allowClear: true,
        placeholder: "- Choose -",
        width: '100%'
    });

    $('[data-toggle="tooltip"]').tooltip();

    $("#searchCard").on("click", function() {
        $(this).find('i').toggleClass('fa fa-chevron-up fa fa-chevron-down');

        setTimeout(function () {
            resizeGrid();
        }, 500);
    });

    $('.icon-picker').iconpicker({
        hideOnSelect: true,
        inputSearch: true
    });
});

function setDataSelect2(thisId, urlContent, title) {
        $("#" + thisId).select2({
            ajax: {
                url: urlContent,
                dataType: 'json',
                data: function (params) {
                    return {
                        q: params.term, // search term
                        pageLimit: 10,
                        page: params.page, // page number
                    };
                },
                processResults: function (data, page) {
                    var more = data.items.length >= 10;
                    //var more = (page * 10) < data.total; // whether or not there are more results available
                    return {
                        results: $.map(data.items, function (item) {
                            return {
                                text: item.text,
                                id: item.id,
                            }
                        }),
                        pagination: {
                            more: more
                        }
                    };
                }
            },
            allowClear: true,
            placeholder: title,
            width: '100%'
        });
};

function setDataSelect2ByClass(thisClass, urlContent, title) {
    $("." + thisClass).select2({
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        allowClear: true,
        placeholder: title,
        width: '100%'
    });
};

function setDataSelect2WithParam(thisId, urlContent, title, param) {
    $("#" + thisId).select2({
        allowClear: true,
        placeholder: title,
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                    param: param
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        debug: true,
        width: '100%'
    });
};

function setDataSelect2WithParamByClass(thisClass, urlContent, title, param) {
    $("." + thisClass).select2({
        allowClear: true,
        placeholder: title,
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                    param: param
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        debug: true,
        width: '100%'
    });
};

function setDataSelect2WithParamByClassInsideId(thisClass, thisId, urlContent, title, param) {
    $('#' + thisId).find('.' + thisClass).select2({
        allowClear: true,
        placeholder: title,
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                    param: param
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        debug: true,
        width: '100%'
    });
};

function setDataSelect2WithParamAndMode(thisId, urlContent, title, param, mode) {
    $("#" + thisId).select2({
        allowClear: true,
        placeholder: title,
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                    param: param,
                    mode: mode
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        debug: true,
        width: '100%'
    });
};

function setDataSelect2WithParamMultiple(thisId, urlContent, title, param) {
    $("#" + thisId).select2({
        tags: true,
        multiple: true,
        placeholder: title,
        ajax: {
            url: urlContent,
            dataType: 'json',
            data: function (params) {
                return {
                    q: params.term, // search term
                    pageLimit: 10,
                    page: params.page, // page number
                    param: param
                };
            },
            processResults: function (data, page) {
                var more = data.items.length >= 10;
                //var more = (page * 10) < data.total; // whether or not there are more results available
                return {
                    results: $.map(data.items, function (item) {
                        return {
                            text: item.text,
                            id: item.id,
                        }
                    }),
                    pagination: {
                        more: more
                    }
                };
            }
        },
        debug: true,
        width: '100%'
    });
};

function showSuccessMesgGrowl(mesg) {
    $.bootstrapGrowl(mesg, {
        type: 'success',
        allow_dismiss: true,
        offset: { from: 'top', amount: 20 },
        align: 'right',
        delay: 10000
    });
}

function showErrorMesgGrowl(mesg) {
    $.bootstrapGrowl(mesg, {
        type: 'danger',
        allow_dismiss: true,
        offset: { from: 'top', amount: 20 },
        align: 'right',
        delay: 10000
    });
}

function showInfoMesgGrowl(mesg) {
    $.bootstrapGrowl(mesg, {
        type: 'info',
        allow_dismiss: true,
        offset: { from: 'top', amount: 20 },
        align: 'right',
        delay: 10000
    });
}

function showSuccessCopyToClipboard(mesg) {
    $.bootstrapGrowl(mesg, {
        type: 'success',
        allow_dismiss: true,
        offset: { from: 'top', amount: 20 },
        align: 'right',
        delay: 1000
    });
}

function showConfirmMesg(mesg, funcYes, title, ButtonYes, ButtonNo, funcNo) {
    ButtonYes = ButtonYes == undefined ? "Yes" : ButtonYes;
    ButtonNo = ButtonNo == undefined ? "No" : ButtonNo;
    title = title == undefined ? "Are You Sure" : title;
    $.MessageBox({
        title: title,
        message: mesg,
        buttonDone: ButtonYes,
        buttonFail: ButtonNo,
        customClass: "msg-danger"
    }).done(function () {
        if (funcYes && (typeof funcYes == "function")) {
            funcYes();
        }
    }).fail(function () {
        if (funcNo && (typeof funcNo == "function")) {
            funcNo();
        }
    });

}

function resizeGrid(hheader) {
    if (hheader == undefined) {
        hheader = 178;
    }
    $(".dataTables_scrollBody").height($(window).height() - $("#divSearch").height() - $("#divTitle").height() - $("#divFooter").height() - hheader);
}

function popUpProgressShow() {
    $.blockUI({
        message: '</br><div style="font-weight: bold;font-size: 110%;font-family: Sans-Serif;">Loading</div><div style="font-weight: bold;font-size: 110%;font-family: Sans-Serif;">Please wait...</div></br></br><img src="/images/loading.gif" /></br></br>',
        css: {
            padding: 0,
            margin: 0,
            width: '22%',
            top: '33%',
            left: '39%',
            border: '1px solid black',
            backgroundColor: '#FFFFFF',
            cursor: 'wait'
        },
        overlayCSS: {
            backgroundColor: '#CCCCCC',
            opacity: 0.6,
            cursor: 'wait'
        },
        baseZ: 99990
    });
}

function popUpProgressHide() {
    $.unblockUI();
}

function toggleColumn() {
    var count = $('#datagrid').dataTable().fnSettings().aoColumns.length;

    for (let i = 1; i <= 4; i++) {
        var column = $("#datagrid").DataTable().column(count - i);
        column.visible(!column.visible());
    }
    $("#btn_hide").find('i').toggleClass('fa fa-eye fa fa-eye-slash');
}

function toggleColumnWithId(id) {
    var count = $('#' + id).dataTable().fnSettings().aoColumns.length;

    for (let i = 1; i <= 4; i++) {
        var column = $("#" + id).DataTable().column(count - i);
        column.visible(!column.visible());
    }
    $("#btn_hide").find('i').toggleClass('fa fa-eye fa fa-eye-slash');
}

function validateExtension(filename, arrayExtensions) {
    var ext = filename.split(".");
    ext = ext[ext.length - 1].toLowerCase();

    if (arrayExtensions.lastIndexOf(ext) == -1) {
        showErrorMesgGrowl("Extension file should be " + arrayExtensions);
        $(":file").filestyle('clear');
    }
}

function removeWhiteSpace(input) {
    if (/^\s/.test(input.value))
    input.value = '';
}


function formatMoney(value, prefix) {
    var number_string = value.replace(/[^.\d]/g, '').toString();
    var split = number_string.split('.');
    var remain = split[0].length % 3;
    var money = split[0].substr(0, remain);
    var thousand = split[0].substr(remain).match(/\d{3}/gi);

    if (thousand) {
        separator = remain ? ',' : '';
        money += separator + thousand.join(',');
    }

    money = split[1] != undefined ? money + '.' + split[1] : money;
    return prefix == undefined ? money : (money ? prefix + money : '');
}

function validateExtension(filename, arrayExtensions) {
    var ext = filename.split(".");
    ext = ext[ext.length - 1].toLowerCase();

    if (arrayExtensions.lastIndexOf(ext) == -1) {
        showErrorMesgGrowl("Extension file should be " + arrayExtensions);
        $(":file").filestyle('clear');
    }
}

function populateFieldsFromURL(dataArray) {
    var params = new URLSearchParams(window.location.search);

    dataArray.forEach(function (data) {
        var paramValue = params.get(data.paramName);

        if (paramValue != null && paramValue !== "null" && paramValue !== "") {
            if (data.type === "select") {
                if (paramValue.includes(",")) {
                    var selectedArray = paramValue.split(",");
                    selectedArray.forEach(function (value) {
                        $(data.fieldId).select2("trigger", "select", {
                            data: { id: value, text: value }
                        });
                    });
                } else {
                    $(data.fieldId).select2("trigger", "select", { data: { id: paramValue, text: paramValue } });
                    $(data.fieldId).val(paramValue).trigger('change.select2');
                }
            } else if (data.type === "text") {
                $(data.fieldId).val(paramValue);
            }
            // You can add more conditions for handling other types of fields like checkboxes, radio buttons, etc.
        }
    });
}

// Membuat semua popup form (.modal-dialog) bisa dipindah (drag) lewat header-nya.
(function () {
    var $draggedDialog = null;
    var startX, startY, startTop, startLeft;

    $(document).on('mousedown', '.modal-header', function (e) {
        if ($(e.target).closest('.close').length || e.which !== 1) {
            return;
        }

        var $modal = $(this).closest('.modal');
        $draggedDialog = $modal.find('.modal-dialog');

        var offset = $draggedDialog.offset();
        startX = e.pageX;
        startY = e.pageY;
        startTop = offset.top;
        startLeft = offset.left;

        $draggedDialog.css({
            position: 'absolute',
            margin: 0,
            top: startTop + 'px',
            left: startLeft + 'px'
        });

        $('body').css('user-select', 'none');
        e.preventDefault();
    });

    $(document).on('mousemove', function (e) {
        if (!$draggedDialog) {
            return;
        }

        $draggedDialog.css({
            top: (startTop + (e.pageY - startY)) + 'px',
            left: (startLeft + (e.pageX - startX)) + 'px'
        });
    });

    $(document).on('mouseup', function () {
        $draggedDialog = null;
        $('body').css('user-select', '');
    });

    // Reset posisi supaya modal kembali ke tengah setiap kali dibuka lagi.
    $(document).on('hidden.bs.modal', '.modal', function () {
        $(this).find('.modal-dialog').css({
            position: '',
            margin: '',
            top: '',
            left: ''
        });
    });
})();

function addDataToURLWithoutReload(data) {
    // Get the current URL
    let url = window.location.href;

    // Remove existing query string, if any
    const indexOfQueryString = url.indexOf('?');
    if (indexOfQueryString !== -1) {
        url = url.substring(0, indexOfQueryString);
    }

    // Construct the query string from the data object
    let queryString = '';
    for (const key in data) {
        if (data.hasOwnProperty(key)) {
            const encodedValue = encodeURIComponent(data[key]);
            if (encodedValue !== 'null' && encodedValue !== null && encodedValue !== '' & encodedValue !== 'undefined') {
                queryString += (queryString ? '&' : '?') + key + '=' + encodedValue;
            }
        }
    }

    // Append the new data to the URL
    url += queryString;

    // Use pushState to modify the URL without reloading the page
    window.history.pushState({ data: data }, '', url);
}