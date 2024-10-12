let pageType = getQueryString('type', 'string');

function setupPage() {
    if (userProfile != null) {
        if (userProfile.Roles.includes("Admin")) {
            // show admin tools
            let newObjectBtn = document.getElementById('dataObjectNew');
            newObjectBtn.style.display = '';
            newObjectBtn.addEventListener("click", function (e) {
                window.location = '/index.html?page=dataobjectnew&type=' + pageType;
            });
        }
    }

    setPageTitle(lang.langMapping[pageType]['dataobject_objects']);
}

function createDataObjectsTable(pageNumber, pageSize) {
    if (!pageNumber) {
        pageNumber = 1;
    }
    if (!pageSize) {
        pageSize = 20;
    }

    ajaxCall(
        '/api/v1/DataObjects/' + pageType + '?pageSize=' + pageSize + '&pageNumber=' + pageNumber + '&getchildrelations=true',
        'GET',
        function (success) {
            switch (pageType) {
                case "game":
                    columns = [
                        'id',
                        {
                            column: 'attributes[attributeName=Logo].value:image',
                            name: 'logo'
                        },
                        'name',
                        {
                            column: 'attributes[attributeName=Platform].value.name',
                            name: 'platform'
                        },
                        {
                            column: 'attributes[attributeName=Publisher].value.name',
                            name: 'publisher'
                        },
                        {
                            column: 'metadata[source=IGDB].id',
                            name: 'igdb'
                        }
                    ];
                    break;

                case "platform":
                    columns = [
                        'id',
                        {
                            column: 'attributes[attributeName=Logo].value:image',
                            name: 'logo'
                        },
                        'name',
                        {
                            column: 'attributes[attributeName=Manufacturer].value.name',
                            name: 'manufacturer'
                        },
                        {
                            column: 'metadata[source=IGDB].id',
                            name: 'igdb'
                        }
                    ];
                    break;

                case "company":
                    columns = [
                        'id',
                        {
                            column: 'attributes[attributeName=Logo].value:image',
                            name: 'logo'
                        },
                        'name',
                        {
                            column: 'metadata[source=IGDB].id',
                            name: 'igdb'
                        }
                    ];
                    break;

                case "app":
                    columns = [
                        'id',
                        {
                            column: 'attributes[attributeName=Logo].value:image',
                            name: 'logo'
                        },
                        'name',
                        {
                            column: 'attributes[attributeName=Publisher].value',
                            name: 'publisher'
                        },
                        {
                            column: 'attributes[attributeName=HomePage].value:link',
                            name: 'link'
                        }
                    ];
                    break;

                default:
                    columns = [
                        'id',
                        'name'
                    ];
                    break;

            }

            let newTable = new generateTable(
                success.objects,
                columns,
                'id',
                true,
                function (id) {
                    window.location = '/index.html?page=dataobjectdetail&type=' + pageType + '&id=' + id;
                },
                success.count,
                success.pageNumber,
                success.totalPages,
                function (p) {
                    createDataObjectsTable(p, pageSize);
                }
            );
            let tableTarget = document.getElementById('dataObjectTable');
            tableTarget.innerHTML = '';
            tableTarget.appendChild(newTable);
        }
    );
}

setupPage();
createDataObjectsTable();
