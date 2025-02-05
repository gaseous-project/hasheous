let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Admin')) {
            document.getElementById('metadatarescan').style.display = '';
            document.getElementById('dataObjectAdminControls').style.display = '';
            if (dataObjectDefinition.allowMerge == true) {
                document.getElementById('dataObjectMergeButtons').style.display = '';
            } else {
                document.getElementById('dataObjectMergeButtons').style.display = 'none';
            }
        } else {
            document.getElementById('dataObjectAdminControls').style.display = 'none';
        }
    } else {
        document.getElementById('dataObjectAdminControls').style.display = 'none';
    }
} else {
    document.getElementById('dataObjectAdminControls').style.display = 'none';
}

document.getElementById('dataObjectEdit').addEventListener("click", function (e) {
    window.location.replace("/index.html?page=dataobjectedit&type=" + pageType + "&id=" + getQueryString('id', 'int'));
});

document.getElementById('dataObjectDelete').addEventListener("click", function (e) {
    ajaxCall(
        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'),
        'DELETE',
        function (success) {
            window.location.replace("/index.html?page=dataobjects&type=" + pageType);
        },
        function (error) {
            window.location.replace("/index.html?page=dataobjects&type=" + pageType);
        }
    );
});

document.getElementById('dataObjectMerge').addEventListener("click", function (e) {
    let mergeIntoId = Number($('#dataObjectMergeSelect').val());
    ajaxCall(
        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/MergeObject?TargetId=' + mergeIntoId + '&commit=true',
        'GET',
        function (success) {
            location.replace('index.html?page=dataobjectdetail&type=' + pageType + '&id=' + mergeIntoId);
        },
        function (error) {
            console.warn(error);
        }
    );
});

document.getElementById('metadatarescan').addEventListener("click", function (e) {
    fetch('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/MetadataMap?forceScan=true', {
        method: 'GET'
    }).then(async function (response) {
        if (response.ok) {
            location.reload();
        } else {
            throw new Error('Failed to rescan metadata');
        }
    });
});

ajaxCall(
    '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'),
    'GET',
    function (success) {
        console.log(success);
        dataObject = success;

        // hide buttons if user is not an admin
        if (userProfile != null) {
            if (userProfile.Roles != null) {
                if (!userProfile.Roles.includes('Admin')) {
                    // check if dataObject.permissions has a value
                    if (dataObject.permissions != null) {
                        if (dataObject.permissions.includes('Update')) {
                            document.getElementById('dataObjectEdit').style.display = '';
                        } else {
                            document.getElementById('dataObjectEdit').style.display = 'none';
                        }
                        if (dataObject.permissions.includes('Delete')) {
                            document.getElementById('dataObjectDelete').style.display = '';
                        } else {
                            document.getElementById('dataObjectDelete').style.display = 'none';
                        }
                    } else {
                        document.getElementById('dataObjectAdminControls').style.display = 'none';
                    }
                }
            } else {
                document.getElementById('dataObjectAdminControls').style.display = 'none';
            }
        }

        renderContent();
    }
);

function renderContent() {
    setPageTitle(dataObject.name, true);
    document.getElementById('dataObject_object_name').innerHTML = dataObject.name;
    document.getElementById('page_date_box_createdDate').innerHTML = moment(dataObject.createdDate + 'Z').format('lll');
    document.getElementById('page_date_box_updatedDate').innerHTML = moment(dataObject.updatedDate + 'Z').format('lll');

    let mergeIntoSelector = document.getElementById('dataObjectMergeSelect');
    $(mergeIntoSelector).select2({
        minimumInputLength: 3,
        placeholder: lang.getLang('mergeintoobject'),
        allowClear: true,
        ajax: {
            url: '/api/v1/DataObjects/' + pageType,
            type: 'GET',
            dataType: 'json',
            data: function (params) {
                var query = {
                    search: params.term
                }

                return query;
            },
            processResults: function (data) {
                var arr = [];

                for (var i = 0; i < data.objects.length; i++) {
                    console.log(data.objects[i]);
                    if (data.objects[i].id != getQueryString('id', 'int')) {
                        arr.push({
                            id: data.objects[i].id,
                            text: data.objects[i].name + ' (' + data.objects[i].id + ')',
                            fullObject: data.objects[i]
                        });
                    }
                }

                return {
                    results: arr
                };
            }
        }
    });

    let descriptionElement = document.getElementById('dataObjectDescription');
    let attributeValues = [];

    for (let i = 0; i < dataObject.attributes.length; i++) {
        switch (dataObject.attributes[i].attributeName) {
            case "Description":
                document.getElementById('dataObjectDescriptionSection').style.display = '';

                let descBody = document.createElement('span');
                descBody.classList.add('descriptionspan');
                descBody.innerHTML = dataObject.attributes[i].value;
                descriptionElement.appendChild(descBody);

                break;

            case "Country":
                let countries = dataObject.attributes[i].value.split(',');

                // replace country names with flags
                for (let j = 0; j < countries.length; j++) {
                    // get the country code from between the brackets
                    let countryCode = countries[j].substring(countries[j].indexOf('(') + 1, countries[j].indexOf(')'));
                    // get the country name from before the brackets
                    let countryName = countries[j].substring(0, countries[j].indexOf('(') - 1);

                    if (countryCode.length == 2) {
                        // get the flag emoji from the country code
                        let flagEmoji = countryCode.toUpperCase().replace(/./g, char => String.fromCodePoint(char.charCodeAt(0) + 127397));

                        countries[j] = "<span class=\"flagemoji\" title=\"" + countryName + "\">" + flagEmoji + "</span>";
                    } else {
                        countries[j] = countryName + ' (' + countryCode + ')';
                    }
                }

                attributeValues.push(
                    {
                        "attribute": dataObject.attributes[i].attributeName,
                        "value": countries.join(' ')
                    }
                );
                break;

            case "VIMMManualId":
                attributeValues.push(
                    {
                        "attribute": dataObject.attributes[i].attributeName,
                        "value": "<a href=\"https://vimm.net/manual/" + dataObject.attributes[i].value + "\" target=\"_blank\" rel=\"noopener noreferrer\">https://vimm.net/manual/" + dataObject.attributes[i].value + "<img src=\"/images/link.svg\" class=\"linkicon\"></a>"
                    }
                )
                break;

            case "Logo":
                let logoImageBox = document.getElementById('dataObjectLogo');
                logoImageBox.style.display = '';
                let logoImage = document.getElementById('dataObjectLogoImage');
                logoImage.setAttribute('src', '/api/v1/images/' + dataObject.attributes[i].value);
                break;

            case "Screenshot1":
            case "Screenshot2":
            case "Screenshot3":
            case "Screenshot4":
                if (dataObject.attributes[i].value) {
                    let screenshotSection = document.getElementById('dataObjectScreenshotsSection');
                    screenshotSection.style.display = '';

                    let screenshotBox = document.getElementById('dataObjectScreenshots');

                    let screenshotLink = document.createElement('a');
                    screenshotLink.setAttribute('href', '/api/v1/images/' + dataObject.attributes[i].value + '.png');
                    screenshotLink.setAttribute('target', '_blank');
                    screenshotLink.setAttribute('rel', 'noopener noreferrer');

                    let screenshotImage = document.createElement('img');
                    screenshotImage.setAttribute('src', '/api/v1/images/' + dataObject.attributes[i].value + '.png');
                    screenshotImage.classList.add('screenshotimage');

                    screenshotLink.appendChild(screenshotImage);
                    screenshotBox.appendChild(screenshotLink);
                }
                break;

            case "LogoAttribution":
                let logoImageAttributionBox = document.getElementById('dataObjectLogoAttribution');
                logoImageAttributionBox.style.display = '';
                let logoImageAttributionLabel = document.getElementById('dataObjectLogoImageAttribution');
                logoImageAttributionLabel.innerHTML = lang.getLang('logoattribution', [lang.getLang(dataObject.attributes[i].value)]);
                let logoImageAttributionLogo = document.getElementById('dataObjectLogoImageAttributionLogo');
                switch (dataObject.attributes[i].value) {
                    case "IGDB":
                        logoImageAttributionLogo.setAttribute('src', '/images/IGDB_logo.svg');
                        logoImageAttributionLogo.style.display = '';
                        break;
                }
                break;

            default:
                switch (dataObject.attributes[i].attributeType) {
                    case "ObjectRelationship":
                        if (dataObject.attributes[i].value) {
                            attributeValues.push(
                                {
                                    "attribute": dataObject.attributes[i].attributeName,
                                    "value": "<a href=\"/index.html?page=dataobjectdetail&type=" + dataObject.attributes[i].attributeRelationType.toLowerCase() + "&id=" + dataObject.attributes[i].value.id + "\">" + dataObject.attributes[i].value.name + "</a>"
                                }
                            );
                        }
                        break;

                    case "EmbeddedList":
                        switch (dataObject.attributes[i].attributeRelationType) {
                            case "ROM":
                                let romBox = document.getElementById('dataObjectRoms');
                                romBox.innerHTML = '';

                                let romHeader = document.createElement('h2');
                                romHeader.innerHTML = lang.getLang('associatedroms');
                                romBox.appendChild(romHeader);

                                // pre-process the dataset
                                let romData = [];
                                for (let j = 0; j < dataObject.attributes[i].value.length; j++) {
                                    let rom = dataObject.attributes[i].value[j];

                                    // convert countries list to flags
                                    let countries = "";
                                    // loop all keys in the countries object
                                    for (let key in rom.country) {
                                        // get the flag emoji from the country code
                                        let flagEmoji = key.toUpperCase().replace(/./g, char => String.fromCodePoint(char.charCodeAt(0) + 127397));

                                        countries += "<span class=\"flagemoji\" title=\"" + rom.country[key] + "\">" + flagEmoji + "</span>";
                                    }

                                    // convert languages list to flags
                                    let languages = "";
                                    // loop all keys in the language object
                                    for (let key in rom.language) {
                                        if (languages != "") {
                                            languages += ", ";
                                        }
                                        languages += rom.language[key];
                                    }

                                    // join hashes into a single string with a <br /> between them
                                    let hashes = '';
                                    if (rom.md5) {
                                        hashes += 'MD5: ' + rom.md5;
                                    }
                                    if (rom.sha1) {
                                        if (hashes != '') {
                                            hashes += '<br />';
                                        }
                                        hashes += 'SHA1: ' + rom.sha1;
                                    }
                                    if (rom.crc) {
                                        if (hashes != '') {
                                            hashes += '<br />';
                                        }
                                        hashes += 'CRC: ' + rom.crc;
                                    }
                                    hashes = '<span style="white-space: nowrap;">' + hashes + '</span>';

                                    romData.push({
                                        "id": rom.id,
                                        "countries": countries,
                                        "languages": languages,
                                        "name": rom.name,
                                        "size": rom.size,
                                        "hashes": hashes,
                                        "signatureSource": rom.signatureSource
                                    });
                                }

                                console.log(dataObject.attributes[i].value);
                                console.log(romData);
                                romBox.appendChild(
                                    new generateTable(
                                        romData,
                                        ['id', 'countries:hideheading', 'languages:hideheading', 'name', 'size:bytes', 'hashes', 'signatureSource'],
                                        'id',
                                        true,
                                        function (id) {
                                            window.location = '/index.html?page=romdetail&id=' + id;
                                        }
                                    )
                                );

                                break;
                        }
                        break;

                    case "DateTime":
                        attributeValues.push(
                            {
                                "attribute": dataObject.attributes[i].attributeName,
                                "value": moment(dataObject.attributes[i].value + 'Z').format('lll')
                            }
                        )
                        break;

                    case "Link":
                        attributeValues.push(
                            {
                                "attribute": dataObject.attributes[i].attributeName,
                                "value": "<a href=\"" + dataObject.attributes[i].value + "\" target=\"_blank\" rel=\"noopener noreferrer\">" + dataObject.attributes[i].value + "<img src=\"/images/link.svg\" class=\"linkicon\"></a>"
                            }
                        )
                        break;

                    default:
                        attributeValues.push(
                            {
                                "attribute": dataObject.attributes[i].attributeName,
                                "value": dataObject.attributes[i].value
                            }
                        );
                        break;
                }

                break;
        }
    }

    if (attributeValues.length > 0) {
        document.getElementById('dataObjectAttributesSection').style.display = '';

        let attributeElement = document.getElementById('dataObjectAttributes');
        attributeElement.appendChild(new generateTable(attributeValues, ['attribute:lang', 'value']));
    }

    if (dataObject.signatureDataObjects.length > 0) {
        document.getElementById('dataObjectSignaturesSection').style.display = '';

        let signatureElement = document.getElementById('dataObjectSignatures');

        for (let i = 0; i < dataObject.signatureDataObjects.length; i++) {
            let sigItem = document.createElement('span');
            sigItem.classList.add('signatureitem');

            let sigLabel = document.createElement('span');
            sigLabel.classList.add('signatureLabel');

            switch (pageType) {
                case "company":
                    sigLabel.innerHTML = dataObject.signatureDataObjects[i].Publisher;
                    break;

                case "platform":
                    sigLabel.innerHTML = dataObject.signatureDataObjects[i].Platform;
                    break;

                case "game":
                    if (dataObject.signatureDataObjects[i].MetadataSource != null) {
                        // create metadata source span
                        let sigSource = document.createElement('span');
                        sigSource.classList.add('signatureMetadataSource');
                        sigSource.innerHTML = signatureSources[Number(dataObject.signatureDataObjects[i].MetadataSource)];
                        sigSource.setAttribute('data-source', signatureSources[Number(dataObject.signatureDataObjects[i].MetadataSource)]);
                        sigItem.appendChild(sigSource);

                        sigItem.setAttribute('data-source', signatureSources[Number(dataObject.signatureDataObjects[i].MetadataSource)]);
                    }

                    let sigLabelText = dataObject.signatureDataObjects[i].Name;
                    if (dataObject.signatureDataObjects[i].Year != null && dataObject.signatureDataObjects[i].Year != '') {
                        sigLabelText += ' (' + dataObject.signatureDataObjects[i].Year + ')';
                    }
                    if (dataObject.signatureDataObjects[i].Platform != null && dataObject.signatureDataObjects[i].Platform != '') {
                        sigLabelText += ' - ' + dataObject.signatureDataObjects[i].Platform;
                    }

                    sigLabel.innerHTML = sigLabelText;

                    break;

            }

            sigItem.appendChild(sigLabel);

            signatureElement.appendChild(sigItem);
        }
    }

    if (
        dataObject.metadata.length > 0 &&
        (
            pageType == "company" ||
            pageType == "platform" ||
            pageType == "game"
        )
    ) {
        document.getElementById('dataObjectMetadataSection').style.display = '';
        let newMetadataMapTable = new generateTable(
            dataObject.metadata,
            ['source:lang', 'matchMethod:lang', 'link:link', 'status:lang'],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }

    if (pageType == "app") {
        // app specific handling

        // app permissions handling
        if (dataObject.userPermissions != null) {
            document.getElementById('dataObjectAccessControlSection').style.display = '';

            // loop through permissions and add to table
            // the value is a dictionary with the key being the users email address and the value being a list of permissions
            let userListTarget = document.getElementById('dataObjectAccessControl');
            for (let key in dataObject.userPermissions) {
                if (dataObject.userPermissions[key].includes('Update')) {
                    let userName = document.createElement('span');
                    userName.classList.add('signatureitem');
                    userName.innerHTML = key;
                    userListTarget.appendChild(userName);
                }
            }
        }

        // client api key handling
        if (dataObject.permissions.includes('Update')) {
            document.getElementById('dataObjectClientAPIKeysSection').style.display = '';

            // set up the create client api key button
            let createClientAPIKeyBtn = document.getElementById('dataObjectClientAPIKeyCreate');
            createClientAPIKeyBtn.addEventListener("click", function (e) {
                // create client api key model
                let clientAPIKeyUrl = '/api/v1/DataObjects/app/' + getQueryString('id', 'int') + '/ClientAPIKeys' + '?name=' + encodeURIComponent(document.getElementById('dataObjectClientAPIKeyName').value);

                if (
                    document.getElementById('dataObjectClientAPIKeyExpiresCustom').checked == true &&
                    document.getElementById('dataObjectClientAPIKeyExpires').value != ''
                ) {
                    clientAPIKeyUrl += '&expires=' + document.getElementById('dataObjectClientAPIKeyExpires').value;
                }

                fetch(clientAPIKeyUrl, {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/json'
                    }
                }).then(async function (response) {
                    if (response.ok) {
                        let value = await response.json();
                        console.log(response.json());
                        document.getElementById('dataObjectClientAPIKeysResponseSection').style.display = '';

                        document.getElementById('dataObjectClientAPIKeysResponse').innerHTML = lang.getLang('clientapikeyresponse', [value.key]);

                        GetApiKeys();
                    } else {
                        throw new Error('Failed to create client API key');
                    }
                });
            });

            GetApiKeys();
        }
    }
}

function GetApiKeys() {
    // get client api keys
    ajaxCall(
        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/ClientAPIKeys',
        'GET',
        function (success) {
            console.log(success);
            let clientAPIKeysTable = new generateTable(
                success,
                ['clientId', 'name', 'created:date', 'expires:date', 'expired', 'revoked'],
                'clientId',
                true,
                function (key, rows) {
                    for (let i = 0; i < rows.length; i++) {
                        if (rows[i].clientId == key) {
                            let row = rows[i];
                            if (row.revoked == false) {
                                let revokePrompt = prompt(lang.getLang('clientapirevokeprompt'));
                                if (revokePrompt == 'REVOKE') {
                                    ajaxCall(
                                        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/ClientAPIKeys/' + key,
                                        'DELETE',
                                        function (success) {
                                            GetApiKeys();
                                        },
                                        function (error) {
                                            GetApiKeys();
                                        }
                                    );
                                }
                            }
                        }
                    }
                }
            );

            let clientAPIKeysTableElement = document.getElementById('dataObjectClientAPIKeys');
            clientAPIKeysTableElement.innerHTML = '';
            clientAPIKeysTableElement.appendChild(clientAPIKeysTable);
        },
        function (error) {
            console.warn(error);
        }
    );
}