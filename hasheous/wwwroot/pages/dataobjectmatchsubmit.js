let pageType = getQueryString('type', 'string').toLowerCase();
let dataObject = undefined;

// Fetch the data object details
fetch('/api/v1.0/DataObjects/' + pageType + '/' + getQueryString('id', 'int'), {
    method: 'GET'
}).then(async function (response) {
    if (!response.ok) {
        throw new Error('Failed to fetch data object details');
    }
    return response.json();
}).then(function (success) {
    console.log(success);
    dataObject = success;

    renderContent();
}).catch(function (error) {
    console.warn(error);
    document.getElementById('content').innerHTML = '<div class="alert alert-danger" role="alert">' + lang.getLang('dataobjectdetailerror') + '</div>';
    console.error('Error fetching data object details:', error);
});

let tableRows = [];

function renderContent() {
    setPageTitle(dataObject.name, true);
    document.getElementById('dataObject_object_name').innerHTML = dataObject.name;

    let platformObject = dataObject.attributes.find(attr => attr.attributeName === "Platform");
    if (platformObject) {
        document.getElementById('dataObject_platform_name').innerHTML = platformObject.value.name;
        document.getElementById('dataObject_platform_name').style.display = '';
    }

    let metadataMapList = document.getElementById('metadatamaplist');
    metadataMapList.innerHTML = ''; // Clear existing content

    if (dataObject.metadata.length === 0) {
        metadataMapList.innerHTML = '<tr><td>' + lang.getLang('nometadataavailable') + '</td></tr>';
        return;
    }

    // Sort metadata by source
    dataObject.metadata.sort((a, b) => {
        if (a.source < b.source) return -1;
        if (a.source > b.source) return 1;
        return 0;
    });

    // Create table headers
    let headerRow = document.createElement('tr');
    headerRow.innerHTML = `
        <th>${lang.getLang('source')}</th>
        <th>${lang.getLang('matchmethod')}</th>
        <th></th>
        <th colspan="2" style="width: 50%;">${lang.getLang('newvalue')}</th>
    `;
    metadataMapList.appendChild(headerRow);

    // Populate the table with metadata
    dataObject.metadata.forEach(element => {
        // get platform id if available
        let platformId = undefined;
        if (platformObject.value) {
            let platformObjectMetadata = platformObject.value.metadata.find(m => m.source === element.source);
            if (platformObjectMetadata) {
                platformId = platformObjectMetadata.id;
            }
        }

        // Create a new MetadataRow instance
        let metadataRow = new MetadataRow(
            element.source,
            element.matchMethod,
            element.id
        );

        // Append the row to the table
        metadataMapList.appendChild(metadataRow.tableRow);

        // Store the row in the tableRows array
        tableRows.push(metadataRow);
    });

    document.getElementById('dataObjectSave').addEventListener("click", function (e) {
        // build model for submission
        let metadataSubmissions = [];

        tableRows.forEach(row => {
            if (row.changed.checked) {
                let newValue = row.input.value.trim();
                if (newValue) {
                    metadataSubmissions.push({
                        Source: row.input.dataset.source,
                        GameId: newValue
                    });
                }
            }
        });

        if (metadataSubmissions.length === 0) {
            alert(lang.getLang('nometadatachanges'));
            return;
        }

        let submissionData = {
            DataObjectId: dataObject.id,
            MetadataMatches: metadataSubmissions
        };

        console.log(submissionData);

        // Submit the data
        postData(
            '/api/v1.0/Submissions/FixMatch',
            'POST',
            submissionData,
            true
        ).then(function (response) {
            if (response.ok) {
                response.json().then(data => {
                    let errorOccurred = false;
                    let errorDiv = document.getElementById('dataobject_submissionerrors');
                    errorDiv.innerHTML = '';
                    for (const key of Object.keys(data)) {
                        if (data[key] !== 'OK') {
                            errorOccurred = true;
                            let errorMessage = document.createElement('div');
                            errorMessage.innerHTML = `<strong>${lang.getLang(key)}:</strong> ${data[key]}`;
                            errorDiv.appendChild(errorMessage);
                        }
                    }
                    if (errorOccurred) {
                        errorDiv.style.display = 'block';
                    } else {
                        window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
                    }
                });
            } else {
                response.json().then(errorData => {
                    console.error('Error submitting data:', errorData);
                    alert(lang.getLang('submissionerror') + ': ' + errorData.message);
                });
            }
        }).catch(function (error) {
            console.error('Error during submission:', error);
            alert(lang.getLang('submissionerror') + ': ' + error.message);
        });
    });

    document.getElementById('dataObjectCancel').addEventListener("click", function (e) {
        window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + getQueryString('id', 'int'));
    });
}

class MetadataRow {
    constructor(source, matchMethod, value) {
        this.source = source;
        this.matchMethod = matchMethod;
        this.value = value;

        let fieldLocked = false;
        if (['Manual', 'ManualByAdmin'].includes(this.matchMethod)) {
            fieldLocked = true; // disallow editing for manual match methods
        }

        this.tableRow = document.createElement('tr');

        let sourceCell = document.createElement('td');
        sourceCell.innerHTML = lang.getLang(this.source);
        this.tableRow.appendChild(sourceCell);

        let matchMethodCell = document.createElement('td');
        matchMethodCell.innerHTML = lang.getLang(this.matchMethod);
        this.tableRow.appendChild(matchMethodCell);

        let lockCell = document.createElement('td');
        if (fieldLocked) {
            lockCell.innerHTML = `<img src="/images/lock.svg" class="banner_button_image" alt="${lang.getLang('locked')}" title="${lang.getLang('fieldlocked')}">`;
        }
        this.tableRow.appendChild(lockCell);

        let changedCell = document.createElement('td');
        this.changed = document.createElement('input');
        this.changed.type = 'checkbox';
        this.changed.disabled = fieldLocked; // Disable checkbox for non-manual match methods
        this.changed.className = 'inputwide';
        this.changed.setAttribute('name', 'metadatasourcechanged');
        changedCell.appendChild(this.changed);
        this.tableRow.appendChild(changedCell);

        let newValueCell = document.createElement('td');
        this.input = document.createElement('input');
        this.input.type = 'text';
        this.input.placeholder = this.value;
        this.input.className = 'inputwide';
        this.input.dataset.source = this.source; // Store the source for later use
        if (fieldLocked) {
            this.input.disabled = true; // Disable input for non-manual match methods
        } else {
            this.input.setAttribute('name', 'metadatasourcevalues');
            this.input.addEventListener('input', () => {
                this.changed.checked = true; // Check the changed checkbox when input is modified
            });
            this.changed.addEventListener('change', () => {
                this.input.value = ''; // Clear input when checkbox is unchecked
            });
        }
        newValueCell.appendChild(this.input);
        this.tableRow.appendChild(newValueCell);
    }
}