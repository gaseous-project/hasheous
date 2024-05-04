let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;
let editMode = false;

let selectedPlatform = undefined;

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
    attributes.push(
        newAttributeObject('ShortString', 'VIMMManualId', 'None', document.getElementById('attributevimmmanualidinput').value)
    );
    let logoOpt = document.querySelector('input[name=logo]:checked');
    switch (logoOpt.value) {
        case "0":
            // no logo
            attributes.push(
                newAttributeObject('ImageId', 'Logo', 'None', '')
            );
            break;

        case "1":
            // uploaded logo
            let uploadedLogo = document.getElementById('attributelogonewref');
            if (uploadedLogo.value.length > 0) {
                attributes.push(
                    newAttributeObject('ImageId', 'Logo', 'None', uploadedLogo.value)
                );
            } else {
                // user didn't actually specify a new logo - set to existing
                attributes.push(
                    newAttributeObject('ImageId', 'Logo', 'None', document.getElementById('attributelogoref').value)
                );
            }
            break;

        case "2":
            // existing logo
            attributes.push(
                newAttributeObject('ImageId', 'Logo', 'None', document.getElementById('attributelogoref').value)
            );
            break;
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

// logo upload button
document.getElementById('attributelogofile').addEventListener("change", function (e) {
    let ofile = document.getElementById('attributelogofile').files[0];
    let formdata = new FormData();
    formdata.append("file", ofile);

    let uploadLabel = document.getElementById('attributelogouploadlabel');
    uploadLabel.innerHTML = lang.getLang('uploadinglogo');

    $.ajax({
        url: '/api/v1/Images/',
        type: 'POST',
        data: formdata,
        processData: false,
        contentType: false,
        success: function (data) {
            uploadLabel.innerHTML = lang.getLang('uploadlogocomplete');
            document.getElementById('attributelogonewref').value = data;
            document.getElementById('attributelogoselectnew').checked = 'checked';
            console.log(data);
        },
        error: function (error) {
            console.warn("Error: " + error);
        }
    });
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
    GetSuggestedSignatures();

    // show required fields for this page type
    switch (pageType) {
        case "company":
            document.getElementById('attributelogo').style.display = '';
            break;

        case "platform":
            document.getElementById('attributemanufacturer').style.display = '';
            document.getElementById('attributelogo').style.display = '';
            break;

        case "game":
            document.getElementById('attributepublisher').style.display = '';
            document.getElementById('attributeplatform').style.display = '';
            document.getElementById('attributelogo').style.display = '';
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
                break;

            case "ShortString":
                switch (dataObject.attributes[i].attributeName) {
                    case "VIMMManualId":
                        document.getElementById('attributevimmmanualidinput').value = dataObject.attributes[i].value;
                        break;
                }
                break;

            case "ImageId":
                switch (dataObject.attributes[i].attributeName) {
                    case "Logo":
                        document.getElementById('attributelogoref').value = dataObject.attributes[i].value;
                        document.getElementById('attributelogoselectexisting').checked = 'checked';
                        break;
                }
                break;

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
                            selectedPlatform = dataObject.attributes[i].value;
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

                for (var i = 0; i < data.objects.length; i++) {
                    arr.push({
                        id: data.objects[i].id,
                        text: data.objects[i].name,
                        fullObject: data.objects[i]
                    });
                }

                return {
                    results: arr
                };
            }
        }
    });

    switch (endpoint) {
        case "Platform":
            $("#" + dropdownName).on('select2:select', function (e) {
                var data = e.params.data;
                selectedPlatform = data.fullObject;
                GetSuggestedSignatures();
            });
            break;
    }
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