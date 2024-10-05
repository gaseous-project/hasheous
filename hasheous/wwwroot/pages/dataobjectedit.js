let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

let selectedPlatform = undefined;

let suggestSignatures = false;

let mustRedirect = true;
if (userProfile != null) {
    if (userProfile.Roles != null) {
        if (userProfile.Roles.includes('Admin') || userProfile.Roles.includes('Moderator')) {
            mustRedirect = false;
        }
    }
}

if (mustRedirect == true) { location.window.replace("/"); }

// save button
document.getElementById('dataObjectSave').addEventListener("click", function (e) {
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

    // get attributes
    let attributes = [];
    for (let i = 0; i < renderedAttributes.length; i++) {
        let attribute = renderedAttributes[i];
        let attributeValue = attribute.getValue();

        if (attributeValue != undefined && attributeValue != "" && attributeValue != null) {
            attributes.push(
                newAttributeObject(attribute.attribute.attributeType, attribute.attribute.attributeName, attribute.attribute.attributeRelationType, attributeValue)
            );
        }
    }

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
        function (success) {
            window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
        },
        function (error) {

        },
        JSON.stringify(model)
    )
});

// cancel button
document.getElementById('dataObjectCancel').addEventListener("click", function (e) {
    window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
});

ajaxCall(
    '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'),
    'GET',
    function (success) {
        console.log(success);
        dataObject = success;
        renderContent();
        loadData();
    }
);

let renderedAttributes = [];
function renderContent() {
    setPageTitle(dataObject.name, true);
    let objectName = document.getElementById('dataObject_object_name');
    objectName.value = dataObject.name;

    // render attributes fields
    let dataObjectAttributesInput = document.getElementById('dataObjectAttributesInput');
    for (let i = 0; i < dataObjectDefinition.attributes.length; i++) {
        let attribute = dataObjectDefinition.attributes[i];

        let tableRow = document.createElement('tr');
        tableRow.id = 'attribute' + attribute.attributeName.toLowerCase();

        let tableCell = document.createElement('td');
        tableCell.classList.add("tablecell");
        tableCell.setAttribute('data-lang', attribute.attributeName);
        tableCell.innerHTML = lang.getLang(attribute.attributeName);
        tableRow.appendChild(tableCell);

        let typeCell = document.createElement('td');
        typeCell.classList.add("tablecell");
        typeCell.setAttribute('data-lang', attribute.attributeType);
        typeCell.innerHTML = lang.getLang(attribute.attributeType);
        tableRow.appendChild(typeCell);

        let inputCell = document.createElement('td');
        inputCell.classList.add("tablecell");

        // insert input fields
        let dataObjectAttribute = new dataObjectAttributes(attribute);
        renderedAttributes.push(dataObjectAttribute);
        inputCell.appendChild(dataObjectAttribute.inputElement);

        tableRow.appendChild(inputCell);

        dataObjectAttributesInput.appendChild(tableRow);
    }

    if (dataObjectDefinition.hasSignatures == true) {
        document.getElementById('dataObjectSignatureSection').style.display = '';
        objectName.addEventListener('keyup', function (e) {
            console.log('keyup');
            GetSuggestedSignatures();
        });
    } else {
        document.getElementById('dataObjectSignatureSection').style.display = 'none';
    }

    if (dataObjectDefinition.hasMetadata == true) {
        document.getElementById('dataObjectMetadataSection').style.display = '';
    } else {
        document.getElementById('dataObjectMetadataSection').style.display = 'none';
    }
}

function loadData() {
    if (dataObjectDefinition.hasSignatures == true) {
        GetSuggestedSignatures();
    }

    // attributes
    for (let i = 0; i < dataObject.attributes.length; i++) {
        switch (dataObject.attributes[i].attributeType) {
            case "LongString":
                switch (dataObject.attributes[i].attributeName) {
                    case "Description":
                        document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'input').innerHTML = dataObject.attributes[i].value;
                        break;
                }
                break;

            case "ShortString":
            case "Link":
                switch (dataObject.attributes[i].attributeName) {
                    case "VIMMManualId":
                    case "VIMMPlatformName":
                    case "HomePage":
                    case "IssueTracker":
                    case "Publisher":
                        document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'input').value = dataObject.attributes[i].value;
                        break;
                }
                break;

            case "ImageId":
                switch (dataObject.attributes[i].attributeName) {
                    case "Logo":
                    case "Screenshot1":
                    case "Screenshot2":
                    case "Screenshot3":
                    case "Screenshot4":
                        document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'ref').value = dataObject.attributes[i].value;
                        document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'selectexisting').checked = 'checked';
                        break;
                }
                break;

            case "ObjectRelationship":
                let selectElement = null;
                if (dataObject.attributes[i].value) {
                    switch (dataObject.attributes[i].attributeName) {
                        case "Manufacturer":
                            selectElement = document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'select');
                            break;
                        case "Publisher":
                            selectElement = document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'select');
                            break;
                        case "Platform":
                            selectedPlatform = dataObject.attributes[i].value;
                            selectElement = document.getElementById('attribute' + dataObject.attributes[i].attributeName.toLowerCase() + 'select')

                            $(selectElement).on('select2:select', function (e) {
                                let data = e.params.data;
                                selectedPlatform = data.fullObject;
                                GetSuggestedSignatures();
                            });
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

    // // set up attributes menus
    // SetupObjectMenus('attributemanufacturerselect', "Company");
    // SetupObjectMenus('attributepublisherselect', "Company");
    // SetupObjectMenus('attributeplatformselect', "Platform");

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
        closeOnSelect: false,
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
                "Content-Type": "application/json",
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
    $(signatureElement).on('select2:select', function (e) {
        GetSuggestedSignatures();
    });
    $(signatureElement).on('select2:unselect', function (e) {
        GetSuggestedSignatures();
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

function GetSuggestedSignatures() {
    // get the name of the data object
    let searchName = document.getElementById('dataObject_object_name');

    let searchType;
    switch (pageType.toLowerCase()) {
        case "company":
            searchType = "Publisher";
            break;

        default:
            searchType = pageType;
            break;
    }

    // search
    let url = '/api/v1/Signatures/Search';
    let searchModel = {
        "searchType": searchType,
        "name": searchName.value
    };

    if (searchName.value.length > 3) {
        // get search results
        ajaxCall(
            url,
            'POST',
            function (success) {
                if (success.length > 0) {
                    let signatureElement = document.getElementById('dataObjectSuggestedSignatures');
                    signatureElement.innerHTML = '';

                    let selectedSignatures = [];
                    let selectedSignaturesObj = $('#signaturesselect').select2('data');
                    for (let i = 0; i < selectedSignaturesObj.length; i++) {
                        let selectedValue = selectedSignaturesObj[i].id;
                        selectedSignatures.push(selectedValue);
                    }

                    for (let i = 0; i < success.length; i++) {
                        let resultid;
                        switch (pageType) {
                            case "game":
                                resultid = success[i].id;
                                break;
                            default:
                                resultid = success[i].Id;
                                break;
                        }

                        if (!selectedSignatures.includes(resultid)) {
                            let sigItem = document.createElement('span');
                            sigItem.classList.add('signatureitem');
                            sigItem.classList.add('selectable');

                            let useSig = true;
                            switch (pageType) {
                                case "company":
                                    sigItem.setAttribute('data-id', success[i].Id);
                                    sigItem.innerHTML = success[i].Publisher;
                                    break;

                                case "platform":
                                    sigItem.setAttribute('data-id', success[i].Id);
                                    sigItem.innerHTML = success[i].Platform;
                                    break;

                                case "game":
                                    // check the selected platform, and filter accordingly
                                    useSig = false;
                                    if (selectedPlatform) {
                                        if (selectedPlatform.id != "") {
                                            if (selectedPlatform.signatureDataObjects) {
                                                for (let s = 0; s < selectedPlatform.signatureDataObjects.length; s++) {
                                                    if (
                                                        selectedPlatform.signatureDataObjects[s].SignatureId == success[i].systemId
                                                    ) {
                                                        useSig = true;
                                                    }
                                                }
                                            }
                                        }
                                    }

                                    if (useSig == true) {
                                        sigItem.setAttribute('data-id', success[i].id);

                                        let year = "";
                                        if (success[i].year) {
                                            if (success[i].year != "") {
                                                year = " (" + success[i].year + ")";
                                            }
                                        }

                                        let system = "";
                                        if (success[i].system) {
                                            if (success[i].system != "") {
                                                system = " - " + success[i].system;
                                            }
                                        }

                                        sigItem.innerHTML = success[i].name + year + system;
                                    }

                                    break;

                            }

                            if (useSig == true) {
                                sigItem.addEventListener('click', function (e) {
                                    let signatureId = this.getAttribute('data-id');
                                    let signatureLabel = this.innerText;

                                    if ($('#signaturesselect').find("option[value='" + signatureId + "']").length) {
                                        $('#signaturesselect').val(signatureId).trigger('change');
                                    } else {
                                        // Create a DOM Option and pre-select by default
                                        var newOption = new Option(signatureLabel, signatureId, true, true);
                                        // Append it to the select
                                        $('#signaturesselect').append(newOption).trigger('change');
                                    }

                                    GetSuggestedSignatures();
                                });

                                signatureElement.appendChild(sigItem);
                            }
                        }
                    }
                }
            },
            function (error) {
                console.log(error);
            },
            JSON.stringify(searchModel)
        );
    }
}