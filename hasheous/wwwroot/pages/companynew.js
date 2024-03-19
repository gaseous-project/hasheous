setPageTitle("newcompany");

function saveCompany() {
    let model = {
        "name": document.getElementById('company_name').value
    }

    ajaxCall(
        '/api/v1/Companies',
        'POST',
        function (success) {
            window.location = '/index.html?page=companies';
        },
        function (error) {
            window.location = '/index.html?page=companies';
        },
        JSON.stringify(model)
    );
}