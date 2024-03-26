let pageType = getQueryString('type', 'string').toLowerCase();

ajaxCall(
    '/api/v1/DataObjects/' + pageType + '/' + getQueryString('id', 'int'),
    'GET',
    function (success) {
        console.log(success);
        setPageTitle(success.name, true);
        document.getElementById('dataObject_object_name').innerHTML = success.name;
        document.getElementById('page_date_box_createdDate').innerHTML = moment(success.createdDate + 'Z').format('lll');
        document.getElementById('page_date_box_updatedDate').innerHTML = moment(success.updatedDate + 'Z').format('lll');

        let descriptionHeadingShown = false;

        let descriptionElement = document.getElementById('dataObjectDescription');
        let attributeValues = [];

        for (let i = 0; i < success.attributes.length; i++) {
            switch (success.attributes[i].attributeName) {
                case "Description":
                    // show heading if required
                    if (descriptionHeadingShown == false) {
                        let descHeader = document.createElement('h2');
                        descHeader.innerHTML = lang.getLang("description");
                        descriptionElement.appendChild(descHeader);
                        descriptionHeadingShown = true;
                    }

                    // add description body
                    let descBody = document.createElement('span');
                    descBody.classList.add('descriptionspan');
                    descBody.innerHTML = success.attributes[i].value;
                    descriptionElement.appendChild(descBody);

                    break;
                
                default:
                    switch (success.attributes[i].attributeType) {
                        case "ObjectRelationship":
                            attributeValues.push(
                                { 
                                    "attribute": success.attributes[i].attributeName,
                                    "value": "<a href=\"/index.html?page=dataobjectdetail&type=" + success.attributes[i].attributeRelationType + "&id=" + success.attributes[i].value.id + "\">" + success.attributes[i].value.name + "</a>"
                                }
                            );
                            break;

                        case "DateTime":
                            attributeValues.push(
                                {
                                    "attribute": success.attributes[i].attributeName,
                                    "value": moment(success.attributes[i].value + 'Z').format('lll')
                                }
                            )
                        default:
                            attributeValues.push(
                                { 
                                    "attribute": success.attributes[i].attributeName,
                                    "value": success.attributes[i].value
                                }
                            );
                            break;
                    }

                    break;
            }
        }

        if (attributeValues.length > 0) {
            let attributeElement = document.getElementById('dataObjectAttributes');
            
            let attrHeader = document.createElement('h2');
            attrHeader.innerHTML = lang.getLang('attributes');
            attributeElement.appendChild(attrHeader);
            
            attributeElement.appendChild(generateTable(attributeValues, [ 'attribute', 'value' ]));
        }

        if (success.signatureDataObjects.length > 0) {
            let signatureElement = document.getElementById('dataObjectSignatures');

            let sigHeader = document.createElement('h2');
            sigHeader.innerHTML = lang.getLang('signatures');
            signatureElement.appendChild(sigHeader);

            for (let i = 0; i < success.signatureDataObjects.length; i++) {
                let sigItem = document.createElement('span');
                sigItem.classList.add('signatureitem');
                switch (pageType) {
                    case "company":
                        sigItem.innerHTML = success.signatureDataObjects[i].Publisher;
                        break;

                    case "platform":
                        sigItem.innerHTML = success.signatureDataObjects[i].Platform;
                        break;

                    case "game":
                        sigItem.innerHTML = success.signatureDataObjects[i].Game;
                        break;

                }

                signatureElement.appendChild(sigItem);
            }
        }

        let newMetadataMapTable = generateTable(
            success.metadata,
            [ 'source:lang', 'matchMethod:lang', 'link:link' ],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }
);