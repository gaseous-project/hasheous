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
    let metadataInputs = document.getElementsByName('metadatamap');
    for (let i = 0; i < metadataInputs.length; i++) {
        let metadataInput = metadataInputs[i];
        let metadataId = metadataInput.value;
        if (metadataId == undefined) {
            metadataId = '';
        }

        let source = metadataInput.getAttribute('data-source');
        if (source == undefined) {
            source = '';
        }

        let matchMethod = metadataInput.getAttribute('data-matchmethod');
        if (matchMethod == undefined) {
            matchMethod = '';
        }

        metadata.push(newMetadataObject(source, matchMethod, metadataInput));
    }

    // get attributes
    let attributes = [];
    for (let i = 0; i < renderedAttributes.length; i++) {
        let attribute = renderedAttributes[i];
        let attributeValue = attribute.getValue();

        if (attributeValue != undefined && attributeValue != "" && attributeValue != null) {
            attributes.push(
                newAttributeObject(attribute.attribute.attributeType, attribute.attribute.attributeName, attribute.attribute.attributeRelationType, attributeValue)
            );
        } else {
            attributes.push(
                newAttributeObject(attribute.attribute.attributeType, attribute.attribute.attributeName, attribute.attribute.attributeRelationType, "")
            );
        }
    }

    // get user access control list
    let userPermissions = {};
    if (pageType == "app") {
        let accessControlSelect = document.getElementById('dataObjectAccessControlSelect');
        let selectedUsers = $(accessControlSelect).select2('data');
        for (let i = 0; i < selectedUsers.length; i++) {
            userPermissions[selectedUsers[i].text] = ["Read", "Update"];
        }
    }

    // compile final model
    let model = {
        id: getQueryString('id', 'int'),
        dataObjectType: pageType,
        name: document.getElementById('dataObject_object_name').value,
        attributes: attributes,
        metadata: metadata,
        signatureDataObjects: signatures,
        permissions: [],
        userPermissions: userPermissions
    };

    console.log(model);

    postData(
        '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int') + '/FullObject',
        'PUT',
        model,
        true
    ).then(response => {
        if (response.ok) {
            return response.json();
        } else {
            throw new Error('Network response was not ok');
        }
    }).then(success => {
        console.log(success);
        // redirect to detail page
        window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
    }).catch(error => {
        console.error('There was a problem with the fetch operation:', error);
        // handle error
        alert('An error occurred while saving the data object. Please try again.');
    });
});

// cancel button
document.getElementById('dataObjectCancel').addEventListener("click", function (e) {
    window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
});

// load data object
fetch('/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'), {
    method: 'GET'
}).then(response => {
    if (response.ok) {
        return response.json();
    } else {
        throw new Error('Network response was not ok');
    }
}).then(success => {
    console.log(success);
    dataObject = success;
    renderContent();
    loadData();
}).catch(error => {
    console.error('There was a problem with the fetch operation:', error);
});

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
        dataObjectAttribute.render();

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

    if (pageType == "app") {
        document.getElementById('dataObjectAccessControl').style.display = '';
    }
}

async function loadData() {
    if (dataObjectDefinition.hasSignatures == true) {
        GetSuggestedSignatures();
    }

    // access control
    if (pageType == "app") {
        let accessControlSelect = document.getElementById('dataObjectAccessControlSelect');
        if (dataObject.userPermissions != null) {
            for (key in dataObject.userPermissions) {
                let userOption = document.createElement('option');
                userOption.value = key;
                userOption.innerHTML = key;
                userOption.selected = 'selected';
                accessControlSelect.appendChild(userOption);
            }
        }

        $(accessControlSelect).select2({
            closeOnSelect: true,
            tags: true,
            tokenSeparators: [',', ' ']
        });
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
                    case "Wikipedia":
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
                    let sigLabelText = signatureSources[Number(dataObject.signatureDataObjects[i].MetadataSource)] + ' - ' + dataObject.signatureDataObjects[i].Name;
                    if (dataObject.signatureDataObjects[i].Year != null && dataObject.signatureDataObjects[i].Year != '') {
                        sigLabelText += ' (' + dataObject.signatureDataObjects[i].Year + ')';
                    }
                    if (dataObject.signatureDataObjects[i].Platform != null && dataObject.signatureDataObjects[i].Platform != '') {
                        sigLabelText += ' - ' + dataObject.signatureDataObjects[i].Platform;
                    }

                    sigItem.innerHTML = sigLabelText;
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
        templateResult: signatureSelectionFormatter,
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
                'X-XSRF-TOKEN': await fetchAntiforgeryToken()
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
                        text: sigName,
                        data: data[i]
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
            // append a row for each metadata item
            let metadataRow = document.createElement('tr');

            let metadataCell = document.createElement('td');
            metadataCell.innerHTML = lang.getLang(dataObject.metadata[i].source);
            metadataCell.setAttribute('data-lang', dataObject.metadata[i].source);
            metadataCell.classList.add('tablecell');
            metadataRow.appendChild(metadataCell);

            let metadataValueCell = document.createElement('td');
            metadataValueCell.classList.add('tablecell');
            metadataValueCell.innerHTML = dataObject.metadata[i].id;
            metadataRow.appendChild(metadataValueCell);

            let metadataInputCell = document.createElement('td');
            metadataInputCell.classList.add('tablecell');

            let metadataInput = document.createElement('input');
            metadataInput.type = 'text';
            metadataInput.classList.add('attributeeditselect');
            metadataInput.id = 'metadatamap' + dataObject.metadata[i].source.toLowerCase();
            metadataInput.name = 'metadatamap';
            metadataInput.setAttribute('data-source', dataObject.metadata[i].source.toLowerCase());
            metadataInput.setAttribute('data-matchmethod', dataObject.metadata[i].matchMethod);
            metadataInput.value = dataObject.metadata[i].id;
            metadataInputCell.appendChild(metadataInput);

            metadataRow.appendChild(metadataInputCell);

            document.getElementById('dataObjectMetadataMapInput').appendChild(metadataRow);
        }
    }
}

function signatureSelectionFormatter(state) {
    if (!state.id) {
        return state.text;
    }

    let data = state.data;

    let sigName;

    let labelBox = document.createElement('div');
    labelBox.classList.add('signatureMetadataLabel');

    let valueLabel = document.createElement('span');
    switch (pageType) {
        case "game":
            let year = "";
            if (data.year) {
                if (data.year != "") {
                    year = " (" + data.year + ")";
                }
            }

            let system = "";
            if (data.system) {
                if (data.system != "") {
                    system = " - " + data.system;
                }
            }

            sigName = data.name + year + system;

            valueLabel.innerHTML = data.name + year;
            valueLabel.classList.add('signatureMetadataName');
            labelBox.appendChild(valueLabel);

            let systemLabel = document.createElement('span');
            systemLabel.innerHTML = data.system;
            systemLabel.classList.add('signatureMetadataSystem');
            labelBox.appendChild(systemLabel);

            break;

        case "platform":
            sigName = data.Platform;

            valueLabel.innerHTML = sigName;
            labelBox.appendChild(valueLabel);
            break;

        case "publisher":
            sigName = data.Publisher;

            valueLabel.innerHTML = sigName;
            labelBox.appendChild(valueLabel);
            break;

    }

    if (data.metadataSource != null) {
        let sourceLabelBox = document.createElement('div');
        sourceLabelBox.classList.add('signatureMetadataSourceBox');

        let sourceLabel = document.createElement('span');
        sourceLabel.innerHTML = signatureSources[Number(data.metadataSource)];
        sourceLabel.classList.add('signatureMetadataSource');
        sourceLabel.setAttribute('data-source', signatureSources[Number(data.metadataSource)]);
        sourceLabelBox.appendChild(sourceLabel);

        labelBox.appendChild(sourceLabelBox);
    }

    return labelBox;
}

function newMetadataObject(type, matchMethod, inputObject) {
    let metadataId = inputObject.value;
    if (metadataId == undefined) {
        metadataId = '';
    }

    let metadataObj = {
        id: metadataId,
        source: type,
        matchMethod: matchMethod
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
        postData(
            url,
            'POST',
            searchModel,
            true
        ).then(response => {
            if (response.ok) {
                return response.json();
            } else {
                throw new Error('Network response was not ok');
            }
        }).then(success => {
            console.log(success);
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
        }).catch(error => {
            console.error('There was a problem with the fetch operation:', error);
        });
    }
}