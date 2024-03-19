function setupPage() {
    if (userProfile.Roles.includes("Admin")) {
        // show admin tools
        document.getElementById('companyNew').style.display = '';
    }
}

function createCompaniesTable() {
    ajaxCall(
        '/api/v1/Companies',
        'GET',
        function(success) {
            let newTable = generateTable(
                success,
                [ 'id', 'name' ],
                'id',
                true,
                function(id) {
                    window.location = '/index.html?page=companydetail&id=' + id;
                }
            );
            document.getElementById('companyTable').appendChild(newTable);
        }
    );
}

setupPage();
createCompaniesTable();