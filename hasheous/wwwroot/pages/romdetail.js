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
                    let displayValue = '';

                    switch (key) {
                        case 'country':
                            // convert countries list to flags
                            // loop all keys in the countries object
                            for (let ckey in value) {
                                // get the flag emoji from the country code
                                let flagEmoji = ckey.toUpperCase().replace(/./g, char => String.fromCodePoint(char.charCodeAt(0) + 127397));

                                displayValue += "<span class=\"flagemoji\" title=\"" + value[ckey] + "\">" + flagEmoji + "</span>";
                            }
                            break;

                        case 'language':
                            // convert languages list to flags
                            // loop all keys in the language object
                            for (let lkey in value) {
                                if (displayValue != "") {
                                    displayValue += ", ";
                                }
                                displayValue += value[lkey];
                            }
                            break;

                        default:
                            displayValue = value
                            break;
                    }

                    detailsArray.push(
                        {
                            "attribute": lang.getLang(key),
                            "value": displayValue
                        }
                    );
                }
            }

            if (jsonLoaded === false) {
                if (key === 'sha256') {
                    if (value.length > 0) {
                        jsonLoaded = true;
                        jsonModel = {
                            "sha256": value
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
                if (key === 'md5') {
                    if (value.length > 0) {
                        jsonLoaded = true;
                        jsonModel = {
                            "md5": value
                        }
                    }
                }
                if (key === "crc") {
                    if (value.length > 0) {
                        jsonLoaded = true;
                        jsonModel = {
                            "crc": value
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
            fetch('/api/v1/Lookup/ByHash?returnAllSources=true', {
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
                        "value": value.trim().replace(/(?:\r\n|\r|\n)/g, '<br>').trim()
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