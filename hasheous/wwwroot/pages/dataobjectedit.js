let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

let mustRedirect = true;
if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Admin')) {
            mustRedirect = false;
        }
    }
}

if (mustRedirect == true) { location.window.replace("/"); }

// save button
document.getElementById('dataObjectSave').addEventListener("click", function(e) {
    // start collecting data

    // signatures
    let signatures = [];
    let selectedSignatures = $('#signaturesselect').select2('data');
    for (let i = 0; i < selectedSignatures.length; i++) {
        let selectedValue = selectedSignatures[i].id;
        signatures.push({ "SignatureId": selectedValue });
    }

    // metadata
    let metadata = [];
    metadata.push(newMetadataObject('IGDB', document.getElementById('metadatamapigdb')));

    // attributes
    let attributes = [];
    attributes.push(
        newAttributeObject('LongString', 'Description', 'None', document.getElementById('attributedescriptioninput').value)
    );
    attributes.push(
        newAttributeObject('ObjectRelationship', 'Manufacturer', 'Company', document.getElementById('attributemanufacturerselect').value)
    );
    attributes.push(
        newAttributeObject('ObjectRelationship', 'Publisher', 'Company', document.getElementById('attributepublisherselect').value)
    );
    attributes.push(
        newAttributeObject('ObjectRelationship', 'Platform', 'Platform', document.getElementById('attributeplatformselect').value)
    );

    // compile final model
    let model = {
        name: document.getElementById('dataObject_object_name').value,
        attributes: attributes,
        metadata: metadata,
        signatureDataObjects: signatures
    };

    console.log(model);

    ajaxCall(
        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/FullObject',
        'PUT',
        function(success) {
            window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
        },
        function(error) {

        },
        JSON.stringify(model)
    )
});

// cancel button
document.getElementById('dataObjectCancel').addEventListener("click", function(e) {
    window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
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
    document.getElementById('dataObject_object_name').value = dataObject.name;

    // show required fields for this page type
    switch (pageType) {
        case "company":
            // no special fields beyond description required
            break;

        case "platform":
            // only show the manufacturer attribute
            document.getElementById('attributemanufacturer').style.display = '';
            break;

        case "game":
            document.getElementById('attributepublisher').style.display = '';
            document.getElementById('attributeplatform').style.display = '';
            break;

    }
    
    // attributes
    for (let i = 0; i < dataObject.attributes.length; i++) {
        switch (dataObject.attributes[i].attributeType) {
            case "LongString":
                switch (dataObject.attributes[i].attributeName) {
                    case "Description":
                        document.getElementById('attributedescriptioninput').innerHTML = dataObject.attributes[i].value;
                        break;
                }

            case "ObjectRelationship":
                let selectElement = null;
                if (dataObject.attributes[i].value) {
                    switch (dataObject.attributes[i].attributeName) {
                        case "Manufacturer":
                            selectElement = document.getElementById('attributemanufacturerselect');
                            break;
                        case "Publisher":
                            selectElement = document.getElementById('attributepublisherselect');
                            break;
                        case "Platform":
                            selectElement = document.getElementById('attributeplatformselect')
                            break;
                    }

                    if (selectElement != null) {
                        let selectOption = document.createElement('option');
                        selectOption.value = dataObject.attributes[i].value.id;
                        selectOption.selected = 'selected';
                        selectOption.innerHTML = dataObject.attributes[i].value.name;
                        selectElement.appendChild(selectOption);
                    }
                }
                
                break;

            default:
                break;
        }
    }

    // set up attributes menus
    SetupObjectMenus('attributemanufacturerselect', "Company");
    SetupObjectMenus('attributepublisherselect', "Company");
    SetupObjectMenus('attributeplatformselect', "Platform");

    // signatures
    let signatureElement = document.getElementById('signaturesselect');
    if (dataObject.signatureDataObjects.length > 0) {
        for (let i = 0; i < dataObject.signatureDataObjects.length; i++) {
            let sigItem = document.createElement('option');
            sigItem.value = dataObject.signatureDataObjects[i].SignatureId;
            sigItem.selected = "selected";
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
    let sigSearchType;
    let sigSearchId;
    let sigSearchName;
    switch (pageType) {
        case "company":
            sigSearchType = "Publisher";
            sigSearchId = "Id";
            sigSearchName = "Publisher";
            break;
        
        case "platform":
            sigSearchType = "Platform";
            sigSearchId = "Id";
            sigSearchName = "Platform";
            break;

        case "game":
            sigSearchType = "Game";
            sigSearchId = "id";
            sigSearchName = "name";
            break;

    }
    $(signatureElement).select2({
        minimumInputLength: 3,
        ajax: {
            url: '/api/v1/Signatures/Search',
            type: "POST",
            dataType: 'json',
            data: function (term, page) {
                return JSON.stringify(
                    {
                        "searchType": sigSearchType,
                        "name": term.term
                    }
                );
            },
            headers: {
                "Content-Type" : "application/json",
            },
            params: {
                contentType: "application/json"
            },
            processResults: function (data) {
                var arr = [];

                for (var i = 0; i < data.length; i++) {
                    let sigName;
                    
                    switch (pageType) {
                        case "game":
                            let year = "";
                            if (data[i].year) {
                                if (data[i].year != "") {
                                    year = " (" + data[i].year + ")";
                                }
                            }

                            let system = "";
                            if (data[i].system) {
                                if (data[i].system != "") {
                                    system = " - " + data[i].system;
                                }
                            }

                            sigName = data[i][sigSearchName] + year + system;
                            break;

                        default:
                            sigName = data[i][sigSearchName];
                            break;

                    }

                    arr.push({
                        id: data[i][sigSearchId],
                        text: sigName
                    });
                }

                return {
                    results: arr
                };
            }
        }
    });

    // metadata
    if (dataObject.metadata.length > 0) {
        for (let i = 0; i < dataObject.metadata.length; i++) {
            switch (dataObject.metadata[i].source) {
                case "IGDB":
                    let igdbLink = document.getElementById('metadatamapigdb');
                    igdbLink.value = dataObject.metadata[i].id;
                    break;
            }
        }
    }
}

function newMetadataObject(type, inputObject) {
    let metadataId = inputObject.value;
    if (metadataId == undefined) {
        metadataId = '';
    }

    let metadataObj = {
        id: metadataId,
        source: type
    };

    return metadataObj;
}

function newAttributeObject(type, name, relationType, value) {
    let attribute = {
        "attributeName": name,
        "attributeType": type,
        "attributeRelationType": relationType,
        "value": value
    }

    return attribute;
}

function SetupObjectMenus(dropdownName, endpoint) {
    $("#" + dropdownName).select2({
        ajax: {
            allowClear: true,
            placeholder: {
                "id": "",
                "text": "None"
            },
            url: '/api/v1/DataObjects/' + endpoint,
            data: function (params) {
                var query = {
                    search: params.term
                }

                return query;
            },
            processResults: function (data) {
                var arr = [];

                arr.push({
                    id: "",
                    text: "None"
                });

                for (var i = 0; i < data.length; i++) {
                    arr.push({
                        id: data[i].id,
                        text: data[i].name
                    });
                }

                return {
                    results: arr
                };
            }
        }
    });
}