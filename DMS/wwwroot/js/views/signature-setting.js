// SignatureSetting. Server values come from signatureSettingConfig in the view.
function ssSelectedMethod() {
    return $("input[name='ssMethod']:checked").val();
}

function ssRefreshQrField() {
    $("#ssQrField").toggleClass("visible", ssSelectedMethod() === "QR");
}

$(document).ready(function () {
    ssRefreshQrField();
    $("input[name='ssMethod']").on("change", ssRefreshQrField);

    $("#ssSave").on("click", function () {
        var method = ssSelectedMethod();
        if (!method) {
            showErrorMesgGrowl("Choose a signing method.");
            return;
        }
        showConfirmMesg("Save the signing method? It applies to every new signature from now on.", function () {
            popUpProgressShow();
            $.ajax({
                type: "POST",
                url: signatureSettingConfig.urls.save,
                dataType: "json",
                data: { method: method, verifyBaseUrl: $("#ssVerifyBaseUrl").val() },
                success: function (result) {
                    popUpProgressHide();
                    if (result.status == true) showSuccessMesgGrowl(result.message);
                    else showErrorMesgGrowl(result.message);
                },
                error: function (result) {
                    popUpProgressHide();
                    showErrorMesgGrowl(result);
                }
            });
        }, "Confirmation", "Save", "Cancel");
    });
});
