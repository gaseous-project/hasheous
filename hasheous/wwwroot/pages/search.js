// set up page
let pageSearchBox = document.getElementById('search_terms');
pageSearchBox.value = decodeURIComponent(getQueryString('query', 'string'));
pageSearchBox.addEventListener("keypress", function(e) {
    let key = e.code;
    if (key == 'Enter') {
        e.preventDefault();
        performSearch();
    }
});

function createDataObjectsTable(targetDiv, pageType) {
    let resultDiv = document.getElementById(targetDiv);
    resultDiv.innerHTML = '';

    if (pageSearchBox.value.length >= 3) {
        let progressObj = document.createElement('progress');
        resultDiv.appendChild(progressObj);

        ajaxCall(
            '/api/v1/DataObjects/' + pageType + '?search=' + encodeURIComponent(pageSearchBox.value),
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
        '/api/v1/HashLookup/Lookup2',
        'POST',
        function(success) {
            let resultDiv = document.getElementById('gamesearchresults');
            console.log(success);

            if (success) {
                let arr = [success];
                let newTable = generateTable(
                    arr,
                    [ 'id', 'name' ],
                    'id',
                    true,
                    function(id) {
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
        function(error) {
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