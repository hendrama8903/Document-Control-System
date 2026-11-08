// User. Server values come from userConfig in the view.
var gCanEdit = userConfig.canEdit;
var gCanDelete = userConfig.canDelete;
var gCanPosition = userConfig.canPosition;
var gCanRestore = userConfig.canRestore;

var UM_ROLE_COLORS = [
    { bg: "#E7F1FB", fg: "#1C3FA6" },
    { bg: "#F1EAFB", fg: "#6C3FB5" },
    { bg: "#E4F7EC", fg: "#1E9E63" },
    { bg: "#FFF4DC", fg: "#B36B00" },
    { bg: "#FDECEC", fg: "#C62828" },
    { bg: "#E6F7F5", fg: "#0E8074" }
];

function umRoleColor(roleId) {
    var id = roleId || "";
    var hash = 0;
    for (var i = 0; i < id.length; i++) { hash = (hash * 31 + id.charCodeAt(i)) >>> 0; }
    return UM_ROLE_COLORS[hash % UM_ROLE_COLORS.length];
}

$(document).ready(function () {
    var gridStore = new DevExpress.data.CustomStore({
        key: "USERNAME",
        loadMode: "raw",
        load: function () {
            var d = $.Deferred();

            $.ajax({
                type: "POST",
                url: userConfig.urls.searchAll,
                data: {
                    draw: 1,
                    start: 0,
                    length: 100000
                }
            }).done(function (result) {
                var data = result.data == "" ? [] : result.data;
                renderUserStats(data);
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
        selection: {
            mode: "multiple",
            showCheckBoxesMode: "always",
            allowSelectAll: false
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
            { dataField: "USERNAME", caption: "Username", fixed: true, fixedPosition: "left" },
            { dataField: "REG_NO", caption: "Employee ID" },
            {
                caption: "Full Name",
                dataField: "FULL_NAME",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    var $cell = $("<div>").addClass("um-avatar-cell");
                    $("<img>").attr("src", row.FILE_PATH ? row.FILE_PATH : userConfig.urls.userImage)
                        .attr("onerror", "this.onerror=null;this.src='" + userConfig.urls.userImage + "';")
                        .appendTo($cell);
                    $("<span>").text(row.FULL_NAME || "").appendTo($cell);
                    $cell.appendTo(container);
                }
            },
            { dataField: "EMAIL", caption: "Email" },
            { dataField: "PHONE", caption: "Phone" },
            {
                dataField: "DEPARTMENT_NAME",
                caption: "Department",
                calculateDisplayValue: function (row) {
                    return row.DEPARTMENT_CODE != null ? row.DEPARTMENT_CODE + " - " + row.DEPARTMENT_NAME : "";
                }
            },
            { dataField: "POSITION_NAME", caption: "Position" },
            {
                caption: "Role",
                dataField: "ROLE_NAME",
                cellTemplate: function (container, options) {
                    var row = options.data;
                    if (!row.ROLE_NAME) { return; }
                    var c = umRoleColor(row.ROLE_ID);
                    $("<span>").addClass("um-role-pill").css({ background: c.bg, color: c.fg }).text(row.ROLE_NAME).appendTo(container);
                }
            },
            {
                caption: "Login Type",
                alignment: "center",
                calculateCellValue: function (row) {
                    return row.AD_USER == "1" ? "AD" : "Local";
                }
            },
            {
                caption: "Status",
                calculateCellValue: function (row) {
                    return row.DELETE_FLAG == "1" ? "Inactive" : "Active";
                },
                cellTemplate: function (container, options) {
                    var inactive = options.data.DELETE_FLAG == "1";
                    var $cell = $("<div>").addClass("um-status-cell");
                    $("<span>").addClass("um-status-dot").toggleClass("inactive", inactive).appendTo($cell);
                    $("<span>").text(inactive ? "Inactive" : "Active").appendTo($cell);
                    $cell.appendTo(container);
                }
            },
            { dataField: "POSITION_NAME", caption: "Position", visible: false },
            {
                caption: "Division",
                visible: false,
                calculateCellValue: function (row) {
                    return row.DIVISION != null ? row.DIVISION + " - " + row.DIVISION_NAME : "";
                }
            },
            {
                caption: "Section",
                visible: false,
                calculateCellValue: function (row) {
                    return row.SECTION_CODE != null ? row.SECTION_CODE + " - " + row.SECTION_NAME : "";
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
                    umAppendActionKebab(container, options.data);
                }
            }
        ]
    }));

    bindGridToolbar("columns filters reset refresh resize");

});

function renderUserStats(data) {
    var total = data.length;
    var inactive = data.filter(function (r) { return r.DELETE_FLAG == "1"; }).length;
    var active = total - inactive;
    var pct = total > 0 ? ((active / total) * 100).toFixed(1) : "0.0";

    $("#userStatTotal").text(total);
    $("#userStatActive").text(active);
    $("#userStatActivePct").text(pct + "% of total users");
    $("#userStatInactive").text(inactive);
}

function umCloseActionMenu() {
    $(".um-dropdown-menu").remove();
}

function umAppendActionKebab(container, row) {
    container.css("white-space", "nowrap");

    var $btn = $("<button>").attr({ type: "button", title: "Actions" }).addClass("dp-act-kebab")
        .html('<i class="fa fa-ellipsis-v"></i>')
        .on("click", function (e) {
            e.stopPropagation();
            var alreadyOpenForThisButton = $btn.data("menu-open");
            umCloseActionMenu();
            if (alreadyOpenForThisButton) {
                $btn.data("menu-open", false);
                return;
            }
            umOpenActionMenu($btn, row);
            $btn.data("menu-open", true);
        });

    container.append($btn);
}

function umOpenActionMenu($btn, row) {
    var $menu = $("<ul>").addClass("dropdown-menu dp-dropdown-menu um-dropdown-menu")
        .css({ position: "fixed", "z-index": 2000, "min-width": "160px", background: "#fff", margin: 0, "list-style": "none" });

    var inactive = row.DELETE_FLAG == "1";

    if (!inactive) {
        if (gCanEdit) {
            $("<li>").append($("<button>").attr("type", "button").addClass("dp-menu-edit").html('<i class="fa fa-pencil"></i> Edit').on("click", function (e) {
                e.stopPropagation(); umCloseActionMenu(); onEditPrepare(row);
            })).appendTo($menu);
        }
        if (gCanPosition) {
            $("<li>").append($("<button>").attr("type", "button").addClass("dp-menu-position").html('<i class="fa fa-sitemap"></i> Position').on("click", function (e) {
                e.stopPropagation(); umCloseActionMenu(); onAddPositionPrepare(row);
            })).appendTo($menu);
        }
        if (gCanDelete) {
            if ($menu.children().length > 0) {
                $menu.append('<li class="divider"></li>');
            }
            $("<li>").append($("<button>").attr("type", "button").addClass("dp-menu-delete").html('<i class="fa fa-trash-o"></i> Delete').on("click", function (e) {
                e.stopPropagation(); umCloseActionMenu(); onDeletePrepare(row);
            })).appendTo($menu);
        }
    } else if (gCanRestore) {
        $("<li>").append($("<button>").attr("type", "button").addClass("dp-menu-restore").html('<i class="fa fa-undo"></i> Restore').on("click", function (e) {
            e.stopPropagation(); umCloseActionMenu(); onRestorePrepare(row);
        })).appendTo($menu);
    }

    $menu.appendTo("body").show();

    var btnRect = $btn[0].getBoundingClientRect();
    var menuWidth = $menu.outerWidth();
    var menuHeight = $menu.outerHeight();
    var left = Math.min(btnRect.right - menuWidth, window.innerWidth - menuWidth - 8);
    var top = btnRect.bottom + 4;
    if (top + menuHeight > window.innerHeight) {
        top = btnRect.top - menuHeight - 4;
    }
    $menu.css({ left: Math.max(8, left) + "px", top: top + "px" });
}

$(document).on("click", function () {
    $(".dp-act-kebab").data("menu-open", false);
    umCloseActionMenu();
});

$(window).on("resize", function () {
    umCloseActionMenu();
});

$(document).on("scroll", ".dx-datagrid-rowsview, .dx-scrollable-container", function () {
    umCloseActionMenu();
});

// ---------- Add/Edit User popup: photo preview, password toggle, dropzone triggers ----------
// FILE_PATH (profile photo) and SIGNATURE_PATH are separate columns/uploads, same
// separation as the redesigned Profile page (UploadSignature/DeleteSignature +
// sp_User_UpdateSignature) - the signature file is staged client-side and only
// actually uploaded once the user record itself has been saved (needed for ADD mode,
// where the row doesn't exist yet to attach a signature to).
var gSignatureFile = null;
var gSignaturePath = null;

$(document).on('click', '#modalAddEdit .uf-toggle-pw', function () {
    var $input = $($(this).data('target'));
    var isPassword = $input.attr('type') === 'password';
    $input.attr('type', isPassword ? 'text' : 'password');
    $(this).toggleClass('fa-eye fa-eye-slash');
});

$(document).on('click', '#ufAvatarCam, #ufUploadPhotoBtn', function () {
    $('#filePath').trigger('click');
});

$(document).on('change', '#filePath', function (e) {
    var files = e.target.files;
    if (files && files.length > 0) {
        var reader = new FileReader();
        reader.onload = function (ev) {
            $('#ufAvatarImg').attr('src', ev.target.result).show();
            $('#ufAvatarPlaceholder').hide();
        };
        reader.readAsDataURL(files[0]);
    }
});

$(document).on('click', '#ufChooseFileBtn, #ufBrowseSignature', function () {
    $('#ufSignatureFile').trigger('click');
});

$(document).on('change', '#ufSignatureFile', function (e) {
    var file = e.target.files && e.target.files.length ? e.target.files[0] : null;
    if (!file) return;

    var ext = file.name.split('.').pop().toLowerCase();
    if (['jpg', 'jpeg', 'png'].indexOf(ext) === -1) {
        showErrorMesgGrowl('Only JPG and PNG files are allowed.');
        $(this).val('');
        return;
    }
    if (file.size > 2 * 1024 * 1024) {
        showErrorMesgGrowl('Signature file must be 2 MB or smaller.');
        $(this).val('');
        return;
    }

    gSignatureFile = file;
    $('#ufSignatureFileName').text(file.name + ' (will be saved with the user)');
    $('#btn_delete_signature').show();

    var reader = new FileReader();
    reader.onload = function (ev) {
        $('#ufSignaturePreview').attr('src', ev.target.result).show();
        $('#ufSignatureIcon').hide();
    };
    reader.readAsDataURL(file);
});

$(document).on('click', '#btn_delete_signature', function () {
    if (gSignaturePath) {
        // an already-saved signature exists server-side - remove it for real
        showDangerConfirm('Are you sure you want to remove this signature?', function () {
            popUpProgressShow();
            $.ajax({
                type: 'POST',
                url: userConfig.urls.deleteSignature,
                data: { USERNAME: $('#addEditUsername').val(), SIGNATURE_PATH: gSignaturePath },
                success: function (result) {
                    popUpProgressHide();
                    if (result.status == true) {
                        gSignaturePath = null;
                        resetSignatureWidget();
                        showSuccessMesgGrowl(result.message);
                    } else {
                        showErrorMesgGrowl(result.message);
                    }
                },
                error: function (result) {
                    popUpProgressHide();
                    showErrorMesgGrowl(result);
                }
            });
        }, "Remove");
    } else {
        // only a staged, not-yet-saved file - just clear it client-side
        gSignatureFile = null;
        resetSignatureWidget();
    }
});

function resetSignatureWidget() {
    gSignatureFile = null;
    $('#ufSignatureFile').val('');
    $('#ufSignatureFileName').text('');
    $('#ufSignaturePreview').attr('src', '').hide();
    $('#ufSignatureIcon').show();
    $('#btn_delete_signature').hide();
}

function updateSignaturePreview(signaturePath) {
    gSignaturePath = signaturePath || null;
    if (signaturePath) {
        $('#ufSignaturePreview').attr('src', signaturePath).show();
        $('#ufSignatureIcon').hide();
        $('#ufSignatureFileName').text('');
        $('#btn_delete_signature').show();
    } else {
        resetSignatureWidget();
    }
}

// called after the user record itself has been successfully saved (ADD or EDIT) -
// uploads the staged signature file, if any, then invokes done() regardless of outcome
// so the main save flow (refresh grid, close modal) isn't blocked by this side-effect.
function uploadStagedSignatureIfAny(username, done) {
    if (!gSignatureFile) {
        done();
        return;
    }

    var data = new FormData();
    data.append('USERNAME', username);
    data.append('file0', gSignatureFile);

    $.ajax({
        type: 'POST',
        url: userConfig.urls.uploadSignature,
        contentType: false,
        processData: false,
        data: data,
        success: function (result) {
            if (result.status != true) {
                showErrorMesgGrowl(result.message);
            }
            done();
        },
        error: function (result) {
            showErrorMesgGrowl(result);
            done();
        }
    });
}

var gScreenMode = null;
var gId = null;
var url = $("#txtUrl").val();
var gAttachmentPath = null;
var gHasLocalPassword = true;

$(document).ready(function () {
    setDataSelect2("addEditRoleId", userConfig.urls.roleGetRoleIdName, "- Choose -");

    setDataSelect2("addEditPosition", userConfig.urls.getPositionNameAndPositionID, "- Choose -");
    setDataSelect2WithParam("addEditDivision", userConfig.urls.systemValues, "- Choose -", "DIVISION");
    setDataSelect2WithParamMultiple("addEditDivisionMultiple", userConfig.urls.systemValues, "", "DIVISION");
    setDataSelect2("addEditDepartment", userConfig.urls.departmentMasterGetDepartmentIdAndNameByDivision, "- Choose -");
    setDataSelect2WithParam("addEditSection", userConfig.urls.sectionMasterGetSectionNameAndSectionIDByDepartment, "- Choose -", "");

    $("#rowDivisionMultiple").hide();
    $("#rowDivision").show();

    $("#btn_add").on("click", function () {
        onAddPrepare();
    });

    $("#btn_save").on("click", function () {
        confirmAddEditData();
    });

    $("#btn_cancel_profile").on("click", function () {
        window.history.back();
    });

    if (url === "/User/Profile") {
        getProfileData();
    }

    $("#btn_save_position").on("click", function () {
        confirmAddEditPositionData();
    });

    $("#btn_delete_attachment").on("click", function () {
        confirmDeleteAttachment()
    });

    $("#addEditAdUser").on("change", function () {
        toggleAdUserFields();
    });

    $("#btn_export").on("click", function () {
        window.location.href = userConfig.urls.downloadExcel;
    });

    bindGridToolbar("stats");

});

function toggleAdUserFields() {
    var isAdUser = $('#addEditAdUser').is(':checked');

    if (isAdUser) {
        $('#rowOldPassword').hide();
        $('#rowNewPassword').hide();
        $('#rowConfirmPassword').hide();
    } else {
        if (gScreenMode === 'EDIT' && gHasLocalPassword) {
            $('#rowOldPassword').show();
        } else {
            $('#rowOldPassword').hide();
        }
        $('#rowNewPassword').show();
        $('#rowConfirmPassword').show();
    }
}

function onAddPrepare() {
    gScreenMode = 'ADD';
    $('#modal-title').html("New User");

    $('#addEditUsername').prop('disabled', false);

    $('#addEditUsername').val("");
    $('#addEditRegNo').val("");
    $('#addEditFullname').val("");
    $('#addEditOldPassword').val("");
    $('#addEditPassword').val("");
    $('#addEditConfirmPassword').val("");
    $('#addEditEmail').val("");
    $('#addEditPhone').val("");
    $('#addEditRoleId').val("").trigger("change");
    $('#addEditAdUser').prop('checked', false);
    //$('#addEditDivision').val("").trigger("change");
    //$('#addEditDepartment').val("").trigger("change");
    $('#fileNameContainer :input[type="text"]').val("");
    
    $('#btn_download_attachment').hide();
    $('#btn_delete_attachment').hide();
    $('#ufAvatarImg').attr('src', '').hide();
    $('#ufAvatarPlaceholder').show();
    resetSignatureWidget();

    $('#rowOldPassword').hide();
    $('#spanEditPassword').hide();
    $('#labelNewPassword').text("");
    $('#labelNewPassword').append(
        'Password <span class="text-red"> *</span>'
    );

    gAttachmentPath = null;
    gHasLocalPassword = true;

    //$('#addEditDivision').prop('disabled', false);
    //$('#addEditDepartment').prop('disabled', true);

    toggleAdUserFields();

    $("#modalAddEdit").modal();
    $('#modalAddEdit').on('shown.bs.modal', function () {
        $('#addEditUsername').focus();
    });
}

function onEditPrepare(row) {
    gId = row.USERNAME;

    $.ajax({
        type: "POST",
        url: userConfig.urls.getByKey,
        async: false,
        data: {
            USERNAME: gId
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;

                gScreenMode = 'EDIT';
                $('#modal-title').html("Edit User");
                $("#addEditRoleId").empty().trigger('change');

                $('#rowOldPassword').show();
                $('#spanEditPassword').show();
                $('#labelNewPassword').text("");
                $('#labelNewPassword').append(
                    'New Password <span class="text-red"> *</span>'
                );

                $('#addEditUsername').val(data.USERNAME);
                $('#addEditRegNo').val(data.REG_NO);
                $('#addEditFullname').val(data.FULL_NAME);
                $('#addEditOldPassword').val("");
                $('#addEditPassword').val("");
                $('#addEditConfirmPassword').val("");
                $('#addEditEmail').val(data.EMAIL);
                $('#addEditPhone').val(data.PHONE);
                $('#addEditRoleId').select2("trigger", "select", { data: { id: data.ROLE_ID, text: data.ROLE_NAME } });
                $('#addEditAdUser').prop('checked', data.AD_USER == "1");
                gHasLocalPassword = (data.PASSWORD != null);
                toggleAdUserFields();
                $('#inputContainer :input[type="text"]').val(data.FILE_PATH);

                gAttachmentPath = data.FILE_PATH;
                if (data.FILE_PATH == null) {
                    $('#btn_delete_attachment').hide();
                    $('#ufAvatarImg').attr('src', '').hide();
                    $('#ufAvatarPlaceholder').show();
                } else {
                    $('#ufAvatarImg').attr('src', data.FILE_PATH).show();
                    $('#ufAvatarPlaceholder').hide();

                    if (userConfig.deleteAttachmentFlag == "False") {
                        $('#btn_delete_attachment').hide();
                    } else {
                        $('#btn_delete_attachment').show();
                    }
                }

                // SIGNATURE_PATH lives in a separate column from FILE_PATH (profile photo)
                // and isn't returned by GetByKey (kept untouched for the admin grid) -
                // fetch it via the same GetProfileDetail endpoint the Profile page uses.
                resetSignatureWidget();
                $.ajax({
                    type: "POST",
                    url: userConfig.urls.getProfileDetail,
                    async: false,
                    data: { USERNAME: gId },
                    success: function (profileResult) {
                        if (profileResult.status == true) {
                            updateSignaturePreview(profileResult.data.SIGNATURE_PATH);
                        }
                    }
                });

                //if (data.DIVISION != null) {
                //    $('#addEditDivision').select2("trigger", "select", { data: { id: data.DIVISION, text: data.DIVISION + " - " + data.DIVISION_NAME } });
                //}

                //if (data.DEPARTMENT_NAME != null) {
                //    $('#addEditDepartment').select2("trigger", "select", { data: { id: data.DEPARTMENT_ID, text: data.DEPARTMENT_NAME } });
                //}

                //$('#addEditDepartment').prop('disabled', false);
                $('#addEditUsername').prop('disabled', true);

                $("#modalAddEdit").modal();
                $('#modalAddEdit').on('shown.bs.modal', function () {
                    $('#addEditRegNo').focus();
                });
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result){
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function setDivision(val) {
    var position = val;

    if (position == '5') { //Executive Officer
        //Set Multiple Division
        $("#rowDivisionMultiple").show();
        $("#addEditDivisionMultiple").val([]).trigger('change');
        $("#rowDivision").hide();

        $('#addEditDivision').prop('disabled', true);
        $('#addEditDepartment').prop('disabled', true);
        $('#addEditSection').prop('disabled', true);
    } else if (position == '6' || position == '7') { //Direktur || Presiden Direktur
        $("#rowDivision").show();
        $("#rowDivisionMultiple").hide();
        $('#addEditDivision').prop('disabled', true);
        $('#addEditDepartment').prop('disabled', true);
        $('#addEditSection').prop('disabled', true);            
    } else {
        $("#rowDivision").show();
        $("#rowDivisionMultiple").hide();
        $('#addEditDivision').prop('disabled', false);
        $('#addEditDepartment').prop('disabled', true);
        $('#addEditSection').prop('disabled', true);
    }

    $('#addEditDivision').val("").trigger("change");
}

function setDepartment(val) {
    var division = val;
    var position = $("#addEditPosition").val();

    if (division == null || division == "") {
        $('#addEditDepartment').prop('disabled', true);
    } else {
        if (position == '3' || position == '2' || position == '1') { //Dept. Head || Section Head || Staff
            $('#addEditDepartment').prop('disabled', false);
        } else {
            $('#addEditDepartment').prop('disabled', true);
        }
    }

    $('#addEditDepartment').val("").trigger("change");
    setDataSelect2WithParam("addEditDepartment", userConfig.urls.departmentMasterGetDepartmentIdAndNameByDivision, "- Choose -", division);
}

function setSection(val) {
    var department = val;
    var position = $("#addEditPosition").val();

    if (department == null || department == "") {
        $('#addEditSection').prop('disabled', true);
    } else {
        if (position == '2' || position == '1') { //Section Head || Staff
            $('#addEditSection').prop('disabled', false);
        } else {
            $('#addEditSection').prop('disabled', true);
        }
    }

    $('#addEditSection').val("").trigger("change");
    setDataSelect2WithParam("addEditSection", userConfig.urls.sectionMasterGetSectionNameAndSectionIDByDepartment, "- Choose -", department);
}

function confirmAddEditData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditData, "Confirmation");
}

$(document).on('change', '#filePath', function (e) {
    ex = e;

    validateExtension($('#filePath').val(), ["jpg", "png", "jpeg"]);
});

function addEditData() {
    if (url === "/User/Profile") {
        ROLE_ID = $('#addEditRoleIdProfile').val();
    } else {
        ROLE_ID = $('#addEditRoleId').val();
    }

    popUpProgressShow();
    var data = new FormData();
    
    var isAdUser = $('#addEditAdUser').is(':checked');

    data.append("screenMode", gScreenMode);
    data.append("USERNAME", $('#addEditUsername').val());
    data.append("REG_NO", $('#addEditRegNo').val());
    data.append("FULL_NAME", $('#addEditFullname').val());
    if (!isAdUser) {
        data.append("OLD_PASSWORD", $('#addEditOldPassword').val());
        data.append("PASSWORD", $('#addEditPassword').val());
        data.append("CONFIRM_PASSWORD", $('#addEditConfirmPassword').val());
    }
    data.append("EMAIL", $('#addEditEmail').val());
    data.append("PHONE", $('#addEditPhone').val());
    data.append("ROLE_ID", ROLE_ID);
    data.append("AD_USER", isAdUser ? "1" : "0");

    if (gAttachmentPath !== null) {
        data.append("FILE_PATH", gAttachmentPath);
    }

    if ($('#filePath').val() !== "" && $('#filePath').val() !== null && $('#filePath').val() !== undefined) {
        e = ex;
        var files = e.target.files;
        if (files.length > 0) {
            if (window.FormData !== undefined) {
                for (var x = 0; x < files.length; x++) {
                    data.append("file" + x, files[x]);
                }
            }
        }
    }

    $.ajax({
        type: "POST",
        url: userConfig.urls.addEdit,
        contentType: false,
        processData: false,
        data: data,
        success: function (result) {
            //popUpProgressHide();
            if (result.status == true) {
                //if (gScreenMode === 'EDIT_PROFILE') {
                if (url === "/User/Profile") {
                    showSuccessMesgGrowl(result.message);
                    getProfileData();
                    popUpProgressHide();
                } else {
                    uploadStagedSignatureIfAny($('#addEditUsername').val(), function () {
                        showSuccessMesgGrowl(result.message);
                        onSearch();
                        $("#modalAddEdit").modal('hide');
                        popUpProgressHide();
                    });
                }
            }else {
               showErrorMesgGrowl(result.message);
               popUpProgressHide();
            }
        },
        error: function (result) {
            popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function getProfileData() {
  $.ajax({
        type: "POST",
        url: userConfig.urls.getByKey,
        async: false,
        data: {
            USERNAME: $("#txtUserNameLayout").text()
        },
        success: function (result) {
            if (result.status == true) {
                var data = result.data;

                gScreenMode = 'EDIT';
                $("#txtUserName").text(data.USERNAME);
                $("#txtFullName").text(data.FULL_NAME);
                $('#addEditUsername').val(data.USERNAME);
                $('#addEditRegNo').val(data.REG_NO);
                $('#addEditFullname').val(data.FULL_NAME);
                $('#addEditOldPassword').val("");
                $('#addEditPassword').val("");
                $('#addEditConfirmPassword').val("");
                $('#addEditEmail').val(data.EMAIL);
                $('#addEditPhone').val(data.PHONE);
                $('#addEditRoleIdProfile').val(data.ROLE_ID);
                $('#addEditRoleName').val(data.ROLE_NAME);

                if (data.FILE_PATH != null) {
                    $('#imgSignature').attr('src', data.FILE_PATH).show();
                    $('#noSignatureText').hide();
                } else {
                    $('#imgSignature').attr('src', '').hide();
                    $('#noSignatureText').show();
                }

                $('#addEditRoleId').prop('disabled', true);
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function(result){
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onDeletePrepare(row) {
    gId = row.USERNAME;

    showDangerConfirm("Delete user \"" + confirmText(gId) + "\"? You can restore this user later.", deleteData, "Delete");
}

function deleteData() {
    $.ajax({
        type: "POST",
        url: userConfig.urls.deleteData,
        dataType: 'json',
        traditional: true,
        data: {
            USERNAME: gId
        },
        success: function (result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}

function onRestorePrepare(row) {
    gId = row.USERNAME;

    showConfirmMesg("Are you sure you want to restore user " + gId + "?", restoreData, "Confirmation");
}

function restoreData() {
    $.ajax({
        type: "POST",
        url: userConfig.urls.restore,
        dataType: 'json',
        traditional: true,
        data: {
            USERNAME: gId
        },
        success: function (result) {
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
        }
    });
}

function onAddPositionPrepare(row) {
    gId = row.USERNAME;

    $('#addEditUsernamePosition').prop('disabled', true);
    $('#addEditUsernamePosition').val(gId);

    $.ajax({
        type: "POST",
        url: userConfig.urls.positionGetByKey,
        async: false,
        data: {
            USERNAME: gId
        },
        success: function (result) {
            if (result.status == true) {
                if (result.data != null) {
                    var data = result.data;
                    var mode = result.mode;

                    if (mode == "multiple") {
                        //set division multiple
                        $("#rowDivisionMultiple").show();
                        $("#addEditDivisionMultiple").val([]).trigger('change');
                       
                        $("#rowDivision").hide();
                        $('#addEditDivision').prop('disabled', true);

                        if (data[0].POSITION_ID != null) {
                            $('#addEditPosition').select2("trigger", "select", { data: { id: data[0].POSITION_ID, text: data[0].POSITION_NAME } });
                        }

                        var select2 = $("#addEditDivisionMultiple");

                        select2.val(null).trigger('change');
                        console.log(data)

                        data.forEach((item, index) => {
                            const divisionID = item.DIVISION;
                            const divisionIDName = item.DIVISION + ' - ' + item.DIVISION_NAME;

                            var option = new Option(divisionIDName, divisionID, true, true);
                            $('#addEditDivisionMultiple').append(option).trigger('change');
                        });

                    } else {
                        //set data normal
                        $("#rowDivisionMultiple").hide();
                        $("#addEditDivisionMultiple").val([]).trigger('change');

                        $("#rowDivision").show();

                        if (data.POSITION_ID != null) {
                            $('#addEditPosition').select2("trigger", "select", { data: { id: data.POSITION_ID, text: data.POSITION_NAME } });
                        }

                        if (data.DIVISION != null) {
                            $('#addEditDivision').select2("trigger", "select", { data: { id: data.DIVISION, text: data.DIVISION + " - " + data.DIVISION_NAME } });
                        }

                        if (data.DEPARTMENT_ID != null) {
                            $('#addEditDepartment').select2("trigger", "select", { data: { id: data.DEPARTMENT_ID, text: data.DEPARTMENT_NAME } });
                        }

                        if (data.SECTION_ID != null) {
                            $('#addEditSection').select2("trigger", "select", { data: { id: data.SECTION_ID, text: data.SECTION_NAME } });
                        }
                    }
                } else {
                    //Set data kosong
                    $("#rowDivision").show();
                    $("#rowDivisionMultiple").hide();
                    $("#addEditDivisionMultiple").val([]).trigger('change');

                    $('#addEditPosition').val("").trigger("change");

                    $('#addEditPosition').prop('disabled', false);
                    $('#addEditDepartment').prop('disabled', true);
                }
            } else {
                showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });

    $("#modalAddEditPosition").modal();
    $('#modalAddEditPosition').on('shown.bs.modal', function () {
        $('#addEditUsernamePosition').focus();
    });
}

function confirmAddEditPositionData() {
    showConfirmMesg("Are you sure you want to continue process?", addEditPositionData, "Confirmation");
}

function addEditPositionData() {
    $.ajax({
        type: "POST",
        url: userConfig.urls.addEditPosition,
        dataType: 'json',
        traditional: true,
        data: {
            screenMode: gScreenMode,
            USERNAME: $('#addEditUsernamePosition').val(),
            POSITION_ID: $('#addEditPosition').val(),
            DIVISION: $('#addEditDivision').val(),
            arrDivision: $("#addEditDivisionMultiple").val(),
            DEPARTMENT_ID: $('#addEditDepartment').val(),
            SECTION_ID: $('#addEditSection').val()
        },
        success: function (result) {
            //popUpProgressHide();
            if (result.status == true) {
                showSuccessMesgGrowl(result.message);
                onSearch();
                $("#modalAddEditPosition").modal('hide');
            }else {
               showErrorMesgGrowl(result.message);
            }
        },
        error: function (result) {
            //popUpProgressHide();
            showErrorMesgGrowl(result);
        }
    });
}

function onDonload(id) {
    gAttachmentPath = id;
    downloadAttachment();
}

function downloadAttachment() {
    if (gAttachmentPath !== "null" && gAttachmentPath !== null) {
        var link = userConfig.urls.downloadAttachment;
        link = link.replace("-1", gAttachmentPath);
        window.location.href = link;
    } else {
        showErrorMesgGrowl("This data does not have an attachment");
    }
}

function confirmDeleteAttachment() {
    showDangerConfirm("Delete this attachment? The file will be removed from the record. This cannot be undone.", deleteAttachment, "Delete");
}

function deleteAttachment() {
    popUpProgressShow();
    $.ajax({
        type: "POST",
        url: userConfig.urls.deleteAttachment,
        dataType: 'json',
        data: {
            USERNAME    : gId,
            FILE_PATH   : gAttachmentPath
        },
        success: function (result) {
            if (result.status != false) {
                $('#inputContainer :input[type="text"]').val("");
                $('#btn_download_attachment').hide();
                $('#btn_delete_attachment').hide();
                $('#ufAvatarImg').attr('src', '').hide();
                $('#ufAvatarPlaceholder').show();
                $('#ufSignatureFileName').text('');

                gAttachmentPath = null;
                $(":file").filestyle('clear');

                onSearch();
                showSuccessMesgGrowl(result.message);
                popUpProgressHide();
            } else {
                showErrorMesgGrowl(result.message);
                popUpProgressHide();
            }
        },
        error: function (result) {
            showErrorMesgGrowl(result);
            popUpProgressHide();
        }
    });
}
