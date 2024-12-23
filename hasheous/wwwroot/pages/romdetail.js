let romId = getQueryString('id', 'int');

ajaxCall(
    '/api/v1/Signatures/Rom/ById/' + romId,
    'GET',
    function (success) {
        console.log(success);

        let pageHeader = document.getElementById('dataObject_object_name');
        pageHeader.innerHTML = success.name;

        let detailsArray = [];
        let jsonLoaded = false;
        let jsonModel = null;
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

            if (jsonLoaded === false) {
                if (key === 'md5') {
                    if (value.length > 0) {
                        jsonLoaded = true;
                        jsonModel = {
                            "md5": value
                        }
                    }
                }
                if (key === "sha1") {
                    if (value.length > 0) {
                        jsonLoaded = true;
                        jsonModel = {
                            "sha1": value
                        }
                    }
                }
            }
        }
        document.getElementById('dataObjectDetails').appendChild(
            new generateTable(
                detailsArray,
                ['attribute', 'value']
            )
        )
        if (jsonLoaded === true) {
            fetch('/api/v1/Lookup/ByHash', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json'
                },
                body: JSON.stringify(jsonModel)
            })
                .then(response => response.json())
                .then(data => {
                    console.log(data);
                    let romJson = JSON.stringify(data, null, 2);

                    let jsonElement = document.createElement('pre');
                    jsonElement.innerHTML = romJson;
                    document.getElementById('apiResponse').appendChild(jsonElement);
                })
                .catch((error) => {
                    console.error('Error:', error);
                });
        }

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