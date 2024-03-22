let pageType = getQueryString('type', 'string');

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
        for (let i = 0; i < success.attributes.length; i++) {
            switch (success.attributes[i].attributeName) {
                case "Description":
                    let descriptionElement = document.getElementById('dataObjectDescription');
                    
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
            }
        }

        let newMetadataMapTable = generateTable(
            success.metadata,
            [ 'source:lang', 'matchMethod:lang', 'id', 'link:link' ],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }
);