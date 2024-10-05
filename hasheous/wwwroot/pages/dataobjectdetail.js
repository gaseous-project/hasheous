let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Admin')) {
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

ajaxCall(
    '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'),
    'GET',
    function (success) {
        console.log(success);
        dataObject = success;
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

                    let screenshotImage = document.createElement('img');
                    screenshotImage.setAttribute('src', '/api/v1/images/' + dataObject.attributes[i].value);
                    screenshotImage.classList.add('screenshotimage');
                    screenshotBox.appendChild(screenshotImage);
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
                                    "value": "<a href=\"/index.html?page=dataobjectdetail&type=" + dataObject.attributes[i].attributeRelationType + "&id=" + dataObject.attributes[i].value.id + "\">" + dataObject.attributes[i].value.name + "</a>"
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

                                romBox.appendChild(
                                    new generateTable(
                                        dataObject.attributes[i].value,
                                        ['id', 'name', 'size:bytes', 'md5', 'sha1', 'signatureSource'],
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
            switch (pageType) {
                case "company":
                    sigItem.innerHTML = dataObject.signatureDataObjects[i].Publisher;
                    break;

                case "platform":
                    sigItem.innerHTML = dataObject.signatureDataObjects[i].Platform;
                    break;

                case "game":
                    sigItem.innerHTML = dataObject.signatureDataObjects[i].Game;
                    break;

            }

            signatureElement.appendChild(sigItem);
        }
    }

    if (dataObject.metadata.length > 0) {
        document.getElementById('dataObjectMetadataSection').style.display = '';
        let newMetadataMapTable = new generateTable(
            dataObject.metadata,
            ['source:lang', 'matchMethod:lang', 'link:link'],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }
}