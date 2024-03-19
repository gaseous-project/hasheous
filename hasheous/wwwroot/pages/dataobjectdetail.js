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

        let newMetadataMapTable = generateTable(
            success.metadata,
            [ 'source:lang', 'matchMethod:lang', 'id', 'link:link' ],
            'id',
            false
        );
        document.getElementById('dataObjectMetadataMap').appendChild(newMetadataMapTable);
    }
);