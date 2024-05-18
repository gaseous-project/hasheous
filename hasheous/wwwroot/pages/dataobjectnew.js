let pageType = getQueryString('type', 'string');
document.getElementById('dataObject_cancel').addEventListener("click", function(e) {
    window.location = '/index.html?page=dataobjects&type=' + pageType;
});
setPageTitle("newcompany");

function saveDataObject() {
    let model = {
        "name": document.getElementById('dataObject_object_name').value
    }

    ajaxCall(
        '/api/v1/DataObjects/' + pageType,
        'POST',
        function (success) {
            window.location = '/index.html?page=dataobjects&type=' + pageType;
        },
        function (error) {
            window.location = '/index.html?page=dataobjects&type=' + pageType;
        },
        JSON.stringify(model)
    );
}