// set up page
let pageSearchBox = document.getElementById('search_terms');
let pageQuerySearchString = decodeURIComponent(getQueryString('query', 'string'));
if (pageQuerySearchString != 'null') {
    pageSearchBox.value = pageQuerySearchString;
}
pageSearchBox.addEventListener("keypress", function (e) {
    let key = e.code;
    if (key == 'Enter') {
        e.preventDefault();
        performSearch();
    }
});

function createDataObjectsTable(targetDiv, pageType, pageNumber, pageSize) {
    let resultDiv = document.getElementById(targetDiv);
    resultDiv.innerHTML = '';

    if (!pageNumber) {
        pageNumber = 1;
    }
    if (!pageSize) {
        pageSize = 5;
    }

    if (pageSearchBox.value.length >= 3) {
        let progressObj = document.createElement('progress');
        resultDiv.appendChild(progressObj);

        ajaxCall(
            '/api/v1/DataObjects/' + pageType + '?search=' + encodeURIComponent(pageSearchBox.value) + '&pageSize=' + pageSize + '&pageNumber=' + pageNumber + '&getchildrelations=true',
            'GET',
            function (success) {
                let columns;
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
                            }
                        ];
                        break;

                    default:
                    case "company":
                        columns = [
                            'id',
                            {
                                column: 'attributes[attributeName=Logo].value:image',
                                name: 'logo'
                            },
                            'name'
                        ];
                        break;
                }

                let resultsPanel = document.getElementById('searchresultspanel');
                resultsPanel.style.display = '';

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
                        createDataObjectsTable(targetDiv, pageType, p, pageSize);
                    }
                );
                let resultDiv = document.getElementById(targetDiv);
                resultDiv.innerHTML = '';
                resultDiv.appendChild(newTable);
            }
        );
    } else {
        ShowError(targetDiv);
    }
}

function createDataObjectsTableFromMD5Search(hashType) {
    let resultDiv = document.getElementById('gamesearchresults');
    resultDiv.innerHTML = '';

    let progressObj = document.createElement('progress');
    resultDiv.appendChild(progressObj);

    let searchModel;
    switch (hashType) {
        case 'md5':
            searchModel = {
                'md5': pageSearchBox.value
            };
            break;

        case 'sha1':
            searchModel = {
                'sha1': pageSearchBox.value
            };
            break;

        default:
            resultDiv.innerHTML = '';
            return;

    }

    ajaxCall(
        '/api/v1/Lookup/ByHash/?getchildrelations=true',
        'POST',
        function (success) {
            let resultDiv = document.getElementById('gamesearchresults');

            if (success) {
                let arr = [success];

                let resultsPanel = document.getElementById('searchresultspanel');
                resultsPanel.style.display = '';

                let newTable = new generateTable(
                    arr,
                    [
                        'id',
                        'name',
                        {
                            column: 'platform.name',
                            name: 'platform'
                        }
                    ],
                    'id',
                    true,
                    function (id) {
                        window.location = '/index.html?page=dataobjectdetail&type=game&id=' + id;
                    }
                );
                resultDiv.innerHTML = '';
                resultDiv.appendChild(newTable);
            } else {
                ShowError('gamesearchresults');
            }
            ShowError('platformsearchresults');
            ShowError('companysearchresults');
        },
        function (error) {
            console.error(error);
            ShowError('gamesearchresults');
            ShowError('platformsearchresults');
            ShowError('companysearchresults');
        },
        JSON.stringify(searchModel)
    );
}

function performSearch() {
    const regexExp_md5 = /^[a-f0-9]{32}$/gi;
    const regexExp_sha1 = /\b([a-f0-9]{40})\b/;
    if (regexExp_md5.test(pageSearchBox.value)) {
        // is an MD5
        createDataObjectsTableFromMD5Search('md5');
    } else if (regexExp_sha1.test(pageSearchBox.value)) {
        // is an SHA1
        createDataObjectsTableFromMD5Search('sha1');
    } else {
        createDataObjectsTable('gamesearchresults', 'game');
        createDataObjectsTable('platformsearchresults', 'platform');
        createDataObjectsTable('companysearchresults', 'company');
    }
}

function ShowError(targetDiv) {
    let errorDiv = document.getElementById(targetDiv);
    errorDiv.innerHTML = '';

    let errorMessage = document.createElement('span');
    errorMessage.innerHTML = lang.getLang('norecords');

    errorDiv.appendChild(errorMessage);
}

performSearch();