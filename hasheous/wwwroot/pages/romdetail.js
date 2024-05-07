let romId = getQueryString('id', 'int');

ajaxCall(
    '/api/v1/Signatures/Rom/ById/' + romId,
    'GET',
    function (success) {
        console.log(success);

        let pageHeader = document.getElementById('dataObject_object_name');
        pageHeader.innerHTML = success.name;

        let detailsArray = [];
        for (const [key, value] of Object.entries(success)) {
            if (!['id', 'attributes', 'mediaDetail', 'score'].includes(key)) {
                if (value) {
                    detailsArray.push(
                        {
                            "attribute": lang.getLang(key),
                            "value": value
                        }
                    );
                }
            }
        }
        document.getElementById('dataObjectDetails').appendChild(
            new generateTable(
                detailsArray,
                ['attribute', 'value']
            )
        )

        // compile attributes into table entries
        if (success.attributes) {
            document.getElementById('dataObjectAttributesSection').style.display = '';
            let attributeArray = [];
            for (const [key, value] of Object.entries(success.attributes)) {
                attributeArray.push(
                    {
                        "attribute": lang.getLang(success.signatureSource.toLowerCase() + "." + key),
                        "value": value
                    }
                )
            }
            document.getElementById('dataObjectAttributes').appendChild(
                new generateTable(
                    attributeArray,
                    ['attribute', 'value']
                )
            )
        }
    }
);