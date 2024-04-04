let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Admin')) {
            document.getElementById('dataObjectAdminControls').style.display = '';
        } else {
            document.getElementById('dataObjectAdminControls').style.display = 'none';
        }
    } else {
        document.getElementById('dataObjectAdminControls').style.display = 'none';
    }
} else {
    document.getElementById('dataObjectAdminControls').style.display = 'none';
}

document.getElementById('dataObjectEdit').addEventListener("click", function(e) {
    window.location.replace("/index.html?page=dataobjectedit&type=" + pageType + "&id=" + getQueryString('id', 'int'));
});

document.getElementById('dataObjectDelete').addEventListener("click", function(e) {
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

                                romBox.appendChild(generateTable(dataObject.attributes[i].value, [ 'name', 'size:bytes', 'md5', 'sha1', 'signatureSource']));

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
        attributeElement.appendChild(generateTable(attributeValues, [ 'attribute', 'value' ]));
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

    let newMetadataMapTable = generateTable(
        dataObject.metadata,
        [ 'source:lang', 'matchMethod:lang', 'link:link' ],
        'id',
        false
    );
    document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
}