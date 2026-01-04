let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

// determine which buttons should be displayed based on page and user roles
let showEditControls = false;
let showMergeControls = false;
let showRescanButton = false;
let showMetadataSubmitButton = false;

if (userProfile != null && userProfile.Roles != null) {
    switch (pageType) {
        case "company":
        case "game":
            if (userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Admin')) {
                showEditControls = true;
                showMergeControls = true;
            }

            // show metadata submit and rescan buttons to all signed in users
            showMetadataSubmitButton = true;
            showRescanButton = true;
            break;

        case "platform":
        case "app":
            if (userProfile.Roles.includes('Admin')) {
                showEditControls = true;
                showMergeControls = true;
                showRescanButton = true;
            }
            break;
    }
}

if (showEditControls) {
    document.getElementById('dataObjectAdminControls').style.display = '';
} else {
    document.getElementById('dataObjectAdminControls').style.display = 'none';
}

if (showMergeControls) {
    document.getElementById('dataObjectMergeButtons').style.display = '';
} else {
    document.getElementById('dataObjectMergeButtons').style.display = 'none';
}

if (showRescanButton) {
    document.getElementById('metadatarescan').style.display = '';
} else {
    document.getElementById('metadatarescan').style.display = 'none';
}

if (showMetadataSubmitButton) {
    let metadataSubmitButton = document.getElementById('metadatasubmit');
    metadataSubmitButton.style.display = '';
    metadataSubmitButton.addEventListener("click", function (e) {
        // navigate to the metadata submit page
        window.location.replace("/index.html?page=dataobjectmatchsubmit&type=" + pageType + "&id=" + getQueryString('id', 'int'));
    });
} else {
    document.getElementById('metadatasubmit').style.display = 'none';
}

document.getElementById('dataObjectEdit').addEventListener("click", function (e) {
    window.location.replace("/index.html?page=dataobjectedit&type=" + pageType + "&id=" + getQueryString('id', 'int'));
});

document.getElementById('dataObjectDelete').addEventListener("click", function (e) {
    postData('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'), 'DELETE', {})
        .then(function (success) {
            window.location.replace("/index.html?page=dataobjects&type=" + pageType);
        })
        .catch(function (error) {
            console.warn(error);
            window.location.replace("/index.html?page=dataobjects&type=" + pageType);
        });
});

document.getElementById('dataObjectMerge').addEventListener("click", function (e) {
    let mergeIntoId = Number($('#dataObjectMergeSelect').val());
    fetch('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/MergeObject?TargetId=' + mergeIntoId + '&commit=true', {
        method: 'GET'
    }).then(async function (response) {
        if (!response.ok) {
            throw new Error('Failed to merge data objects');
        }
        return response.json();
    }).then((success) => {
        console.log(success);
        if (success) {
            location.replace('index.html?page=dataobjectdetail&type=' + pageType + '&id=' + mergeIntoId);
        } else {
            alert('An error occurred while merging data objects. Please try again.');
        }
    }).catch((error) => {
        console.warn(error);
        alert('An error occurred while merging data objects: ' + error.message);
    });
});

let rescanButton = document.getElementById('metadatarescan');
rescanButton.addEventListener("click", (e) => {
    rescanButton.setAttribute('disabled', 'disabled');
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

// Fetch the data object details
fetch('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'), {
    method: 'GET'
}).then(async function (response) {
    if (!response.ok) {
        throw new Error('Failed to fetch data object details');
    }
    return response.json();
}).then(function (success) {
    console.log(success);
    dataObject = success;

    renderContent();
}).catch(function (error) {
    console.warn(error);
    document.getElementById('content').innerHTML = '<div class="alert alert-danger" role="alert">' + lang.getLang('dataobjectdetailerror') + '</div>';
    console.error('Error fetching data object details:', error);
});

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
                    search: params.term,
                    getchildrelations: false
                }

                return query;
            },
            processResults: function (data) {
                var arr = [];

                for (var i = 0; i < data.objects.length; i++) {
                    console.log(data.objects[i]);
                    if (data.objects[i].id != getQueryString('id', 'int')) {
                        let platformIdAttribute = data.objects[i].attributes.find(attr => attr.attributeName === 'Platform');
                        let platformId;
                        let platformName = '';
                        if (platformIdAttribute) {
                            platformId = platformIdAttribute.value.relationId;

                            // check platforms for a matching ID
                            console.log(platforms);
                            platformName = platforms.find(platform => platform.id === platformId)?.name || '';
                        }

                        arr.push({
                            id: data.objects[i].id,
                            text: data.objects[i].name + ' (' + data.objects[i].id + ') ' + platformName,
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
    let aiDescriptionElement = document.getElementById('dataObjectAIDescription');
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

            case "AIDescription":
                document.getElementById('dataObjectAIDescriptionSection').style.display = '';

                // render AI description - content is markdown
                let markdownText = dataObject.attributes[i].value;
                // convert markdown to HTML using marked
                let htmlContent = marked.parse(markdownText);

                let aiDescBody = document.createElement('span');
                aiDescBody.classList.add('descriptionspan');
                aiDescBody.innerHTML = htmlContent;
                aiDescriptionElement.appendChild(aiDescBody);

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

            case "Tags":
                let tags = dataObject.attributes[i].value;

                for (const tagType of Object.values(tagTypes)) {
                    if (tags[tagType]) {
                        let tagOutput = '<table>';
                        let tagColourValue = `#${intToRGB(hashCode(tagType.toLowerCase()))}`;
                        let tagColour = `style="background-color: ${tagColourValue};"`;

                        for (const tag of tags[tagType].tags) {
                            let aiImage = '';
                            if (tag.aiGenerated) {
                                aiImage = ' <img src="/images/ai.svg" class="banner_button_image aigeneratedicon" title="' + lang.getLang('aigeneratedtag') + '">';
                                tagColour = ` style="background: linear-gradient(156deg,rgba(255, 0, 217, 1) 0px, rgba(255, 0, 208, 1) 10px, ${tagColourValue} 40px, ${tagColourValue} 100%);" `;
                            }

                            if (tagOutput != '') {
                                tagOutput += ' ';
                            }
                            tagOutput += `<span class="badge badge-tag badge-tag-type-${tagType.toLowerCase()}" ${tagColour}>${aiImage} ${tag.text}</span>`;
                        }

                        attributeValues.push(
                            {
                                "attribute": `tagtypes.${tagType}`,
                                "value": tagOutput
                            }
                        );
                        tagOutput += "</table>";
                    }
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
                                    let hashes = "";

                                    if (rom.sha256) {
                                        hashes += "<tr><td style=\"width: 50px; padding: 1px;\">" + lang.getLang('SHA256') + ":</td><td style=\"word-break: break-word; padding: 1px;\">" + rom.sha256 + "</td></tr>";
                                    }
                                    if (rom.sha1) {
                                        hashes += "<tr><td style=\"width: 50px; padding: 1px;\">" + lang.getLang('sha1') + ":</td><td style=\"word-break: break-word; padding: 1px;\">" + rom.sha1 + "</td></tr>";
                                    }
                                    if (rom.md5) {
                                        hashes += "<tr><td style=\"width: 50px; padding: 1px;\">" + lang.getLang('md5') + ":</td><td style=\"word-break: break-word; padding: 1px;\">" + rom.md5 + "</td></tr>";
                                    }
                                    if (rom.crc) {
                                        hashes += "<tr><td style=\"width: 50px; padding: 1px;\">" + lang.getLang('crc') + ":</td><td style=\"word-break: break-word; padding: 1px;\">" + rom.crc + "</td></tr>";
                                    }
                                    hashes = "<table cellspacing=\"0\">" + hashes + "</table>";

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

                                // console.log(dataObject.attributes[i].value);
                                // console.log(romData);
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

                    case "Boolean":
                        attributeValues.push(
                            {
                                "attribute": dataObject.attributes[i].attributeName,
                                "value": lang.getLang(dataObject.attributes[i].value ? 'yes' : 'no')
                            }
                        );
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
                        let sourceName = signatureSources[Number(dataObject.signatureDataObjects[i].MetadataSource)];

                        if (sourceName != null) {
                            let sigSource = document.createElement('span');
                            sigSource.classList.add('signatureMetadataSource');
                            sigSource.classList.add('color-' + sourceName.toLowerCase());
                            sigSource.innerHTML = sourceName;
                            sigSource.setAttribute('data-source', sourceName);
                            sigItem.appendChild(sigSource);
                        }

                        sigItem.setAttribute('data-source', sourceName);
                        sigItem.setAttribute('data-source-id', Number(dataObject.signatureDataObjects[i].MetadataSource));
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

        // sort dataObject.metadata by source
        dataObject.metadata.sort(function (a, b) {
            if (lang.getLang(a.source) < lang.getLang(b.source)) {
                return -1;
            }
            if (lang.getLang(a.source) > lang.getLang(b.source)) {
                return 1;
            }
            return 0;
        });

        // if the user is a moderator or admin, show all metadata sources, otherwise, filter out sources with the matchMethod 'nomatch'
        if (userProfile != null && (userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Admin'))) {
            dataObject.metadata = dataObject.metadata.filter(m => m.source != 'NoMatch');
        } else {
            dataObject.metadata = dataObject.metadata.filter(m => m.matchMethod != 'NoMatch' && m.id != null && m.id != '');
        }

        let newMetadataMapTable = new generateTable(
            dataObject.metadata,
            ['source:lang', 'matchMethod:lang', 'link:link', 'status:lang'],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }

    switch (pageType) {
        case "platform":
            let linkedGamesSection = document.getElementById('dataObjectLinkedGames');
            linkedGamesSection.style.display = '';

            createDataObjectsTable(1, 20, 'game', dataObject.id);

            break;

        case "app":
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
                        userName.classList.add('badge');
                        userName.innerHTML = key;
                        userListTarget.appendChild(userName);
                    }
                }
            }

            // show edit buttons if the user has edit permissions
            if (dataObject.permissions.includes('Update')) {
                document.getElementById('dataObjectAdminControls').style.display = '';
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

                    postData(
                        clientAPIKeyUrl,
                        'POST',
                        {},
                        true)
                        .then(async function (response) {
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

            // insights handling
            if (userProfile) {
                fetch('/api/v1.0/Insights/' + pageType + '/' + getQueryString('id', 'int') + '/Insights', {
                    method: 'GET'
                }).then(async function (response) {
                    if (response.ok) {
                        let insights = await response.json();

                        let displayInsights = false;
                        if (insights != null && Object.keys(insights).length > 0) {
                            for (const [key, value] of Object.entries(insights)) {
                                if (value == null || value == "") {
                                    continue; // skip empty insights
                                }

                                displayInsights = true;

                                // create insight element
                                let insightElement = document.createElement('div');
                                insightElement.classList.add('dataObjectInsight');

                                let insightTitle = document.createElement('span');
                                insightTitle.classList.add('insightHeading');
                                insightTitle.innerHTML = lang.getLang(key);
                                insightElement.appendChild(insightTitle);

                                let insightContent = document.createElement('div');

                                if (typeof value === 'object') {
                                    // value is a hashtable, create a table with the keys and values
                                    let insightList = document.createElement('table');
                                    insightList.classList.add('tablerowhighlight');

                                    for (const subKey of Object.keys(value)) {
                                        let row = document.createElement('tr');
                                        let keyCell = true;
                                        for (const [subSubKey, subSubValue] of Object.entries(value[subKey])) {
                                            let valueCell = document.createElement('td');
                                            valueCell.classList.add('tablecell');
                                            valueCell.style.width = '50%';
                                            if (keyCell && subSubValue == null || subSubValue == "") {
                                                valueCell.innerHTML = lang.getLang('unknown');
                                            } else {
                                                if (Number(subSubValue)) {
                                                    valueCell.style.textAlign = 'right';
                                                }
                                                valueCell.innerHTML = subSubValue;
                                            }
                                            row.appendChild(valueCell);

                                            keyCell = false;
                                        }
                                        insightList.appendChild(row);
                                    }

                                    insightContent.appendChild(insightList);
                                } else {
                                    if (value != null && value != "") {
                                        displayInsights = true;

                                        // otherwise, create a single row table with the value
                                        let insightValue = document.createElement('table');

                                        let valueRow = document.createElement('tr');
                                        valueRow.classList.add('tablerowhighlight');

                                        let valueCell = document.createElement('td');
                                        valueCell.classList.add('tablecell');
                                        if (Number(value)) {
                                            valueCell.style.textAlign = 'right';
                                        }
                                        valueCell.innerHTML = value;
                                        valueRow.appendChild(valueCell);
                                        insightValue.appendChild(valueRow);

                                        insightContent.appendChild(insightValue);
                                    }
                                }

                                insightElement.appendChild(insightContent);
                                insightElement.setAttribute('data-insight', key);

                                document.getElementById('dataObjectInsights').appendChild(insightElement);

                            }
                        }

                        if (displayInsights) {
                            document.getElementById('dataObjectInsightsSection').style.display = '';
                        }
                    } else {
                        throw new Error('Failed to fetch insights');
                    }
                });
            }

            break;
    }
}

function GetApiKeys() {
    // get client api keys
    fetch('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/ClientAPIKeys', {
        method: 'GET'
    }).then(async function (response) {
        if (!response.ok) {
            throw new Error('Failed to fetch client API keys');
        }
        return response.json();
    }).then(function (success) {
        let clientAPIKeysTable = new generateTable(
            success,
            ['keyId', 'name', 'created:date', 'expires:date', 'expired', 'revoked'],
            'keyId',
            true,
            function (key, rows) {
                for (let i = 0; i < rows.length; i++) {
                    if (rows[i].keyId == key) {
                        let row = rows[i];
                        if (row.revoked == false) {
                            let revokePrompt = prompt(lang.getLang('clientapirevokeprompt'));
                            if (revokePrompt == 'REVOKE') {
                                postData(
                                    '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/ClientAPIKeys/' + key,
                                    'DELETE',
                                    {}
                                ).then(function (success) {
                                    GetApiKeys();
                                }).catch(function (error) {
                                    console.warn(error);
                                    GetApiKeys();
                                });
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

let platforms = fetch('/api/v1/DataObjects/platform?getchildrelations=false', {
    method: 'GET'
}).then(response => response.json())
    .then(data => {
        platforms = data.objects;
    })
    .catch((error) => {
        console.error('Error fetching platforms:', error);
    });