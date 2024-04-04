let pageType = getQueryString('type', 'string');

function setupPage() {
    if (userProfile != null) {
        if (userProfile.Roles.includes("Admin")) {
            // show admin tools
            let newObjectBtn = document.getElementById('dataObjectNew');
            newObjectBtn.style.display = '';
            newObjectBtn.addEventListener("click", function(e) {
                window.location = '/index.html?page=dataobjectnew&type=' + pageType;
            });
        }
    }
}

function createDataObjectsTable() {
    ajaxCall(
        '/api/v1/DataObjects/' + pageType,
        'GET',
        function(success) {
            let newTable = generateTable(
                success,
                [ 'id', 'name' ],
                'id',
                true,
                function(id) {
                    window.location = '/index.html?page=dataobjectdetail&type=' + pageType + '&id=' + id;
                }
            );
            document.getElementById('dataObjectTable').appendChild(newTable);
        }
    );
}

setupPage();
createDataObjectsTable();
