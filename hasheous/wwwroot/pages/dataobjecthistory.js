let pageType = getQueryString('type', 'string').toLowerCase();
let objectId = getQueryString('id', 'int');
let currentPage = 1;
let pageSize = 10;
let totalPages = 0;

// Fetch the data object name first
fetch('/api/v1/DataObjects/' + pageType + '/' + objectId)
    .then(response => {
        if (!response.ok) {
            throw new Error('Failed to fetch data object');
        }
        return response.json();
    })
    .then(dataObject => {
        document.getElementById('dataObjectName').textContent = dataObject.name;
    })
    .catch(error => {
        console.error('Error fetching data object:', error);
        document.getElementById('dataObjectName').textContent = 'Unknown';
    });

// Load history records
function loadHistory(page) {
    currentPage = page;

    document.getElementById('historyLoading').style.display = 'block';
    document.getElementById('historyContent').style.display = 'none';
    document.getElementById('noHistory').style.display = 'none';

    fetch('/api/v1/DataObjects/' + pageType + '/' + objectId + '/History?pageNumber=' + page + '&pageSize=' + pageSize)
        .then(response => {
            if (!response.ok) {
                throw new Error('Failed to fetch history');
            }
            return response.json();
        })
        .then(data => {
            document.getElementById('historyLoading').style.display = 'none';

            if (data.history && data.history.length > 0) {
                displayHistory(data);
                document.getElementById('historyContent').style.display = 'block';
            } else {
                document.getElementById('noHistory').style.display = 'block';
            }
        })
        .catch(error => {
            console.error('Error fetching history:', error);
            document.getElementById('historyLoading').style.display = 'none';
            document.getElementById('noHistory').style.display = 'block';
        });
}

function displayHistory(data) {
    const historyRecordsDiv = document.getElementById('historyRecords');
    historyRecordsDiv.innerHTML = '';

    totalPages = data.totalPages;

    data.history.forEach(record => {
        const recordDiv = document.createElement('div');
        recordDiv.className = 'history-record';

        // Format timestamp
        const timestamp = new Date(record.ChangeTimestamp);
        const formattedTime = timestamp.toLocaleString();

        // Create header with timestamp and rollback button
        const headerDiv = document.createElement('div');
        headerDiv.className = 'history-header';

        const timestampDiv = document.createElement('div');
        timestampDiv.className = 'history-timestamp';
        timestampDiv.textContent = formattedTime;

        // Create rollback section
        const actionsDiv = document.createElement('div');
        actionsDiv.className = 'history-actions';

        // Check if user has moderator or admin role
        if (userProfile && userProfile.Roles &&
            (userProfile.Roles.includes('Moderator') || userProfile.Roles.includes('Admin'))) {

            const checkboxDiv = document.createElement('div');
            checkboxDiv.className = 'history-checkbox';

            const checkbox = document.createElement('input');
            checkbox.type = 'checkbox';
            checkbox.id = 'confirm-' + record.Id;
            checkbox.className = 'rollback-confirm-checkbox';

            const checkboxLabel = document.createElement('label');
            checkboxLabel.htmlFor = 'confirm-' + record.Id;
            checkboxLabel.setAttribute('data-lang', 'rollbackconfirm');
            checkboxLabel.textContent = 'I confirm I want to rollback to this version';

            checkboxDiv.appendChild(checkbox);
            checkboxDiv.appendChild(checkboxLabel);

            const rollbackButton = document.createElement('button');
            rollbackButton.className = 'rollback-button';
            rollbackButton.disabled = true;
            rollbackButton.innerHTML = '<span data-lang="rollback">Rollback</span>';
            rollbackButton.onclick = () => performRollback(record.Id);

            // Enable/disable rollback button based on checkbox
            checkbox.addEventListener('change', function () {
                rollbackButton.disabled = !this.checked;
            });

            actionsDiv.appendChild(checkboxDiv);
            actionsDiv.appendChild(rollbackButton);
        }

        headerDiv.appendChild(timestampDiv);
        headerDiv.appendChild(actionsDiv);

        // Create JSON sections
        const jsonDiv = document.createElement('div');
        jsonDiv.className = 'history-json';

        // Pre-edit JSON section
        if (record.PreEditJson) {
            const preEditSection = document.createElement('div');
            preEditSection.className = 'json-section';

            const preEditTitle = document.createElement('h4');
            preEditTitle.setAttribute('data-lang', 'preeditjson');
            preEditTitle.textContent = 'Previous State';

            const preEditContent = document.createElement('div');
            preEditContent.className = 'json-content';

            const preEditPre = document.createElement('pre');
            preEditPre.textContent = JSON.stringify(record.PreEditJson, null, 2);

            preEditContent.appendChild(preEditPre);
            preEditSection.appendChild(preEditTitle);
            preEditSection.appendChild(preEditContent);
            jsonDiv.appendChild(preEditSection);
        }

        // Diff JSON section
        if (record.DiffJson) {
            const diffSection = document.createElement('div');
            diffSection.className = 'json-section';

            const diffTitle = document.createElement('h4');
            diffTitle.setAttribute('data-lang', 'diffjson');
            diffTitle.textContent = 'Changes Made';

            const diffContent = document.createElement('div');
            diffContent.className = 'json-content';

            const diffPre = document.createElement('pre');
            diffPre.textContent = JSON.stringify(record.DiffJson, null, 2);

            diffContent.appendChild(diffPre);
            diffSection.appendChild(diffTitle);
            diffSection.appendChild(diffContent);
            jsonDiv.appendChild(diffSection);
        }

        recordDiv.appendChild(headerDiv);
        recordDiv.appendChild(jsonDiv);
        historyRecordsDiv.appendChild(recordDiv);
    });

    // Update pagination
    updatePagination(data);
}

function updatePagination(data) {
    const pageInfo = document.getElementById('historyPageInfo');
    pageInfo.textContent = `Page ${data.pageNumber} of ${data.totalPages}`;

    const prevButton = document.getElementById('historyPrevPage');
    const nextButton = document.getElementById('historyNextPage');

    if (data.pageNumber > 1) {
        prevButton.style.display = 'inline-block';
        prevButton.onclick = () => loadHistory(currentPage - 1);
    } else {
        prevButton.style.display = 'none';
    }

    if (data.pageNumber < data.totalPages) {
        nextButton.style.display = 'inline-block';
        nextButton.onclick = () => loadHistory(currentPage + 1);
    } else {
        nextButton.style.display = 'none';
    }
}

function performRollback(historyId) {
    if (!confirm('Are you sure you want to rollback to this version? This will create a new history entry.')) {
        return;
    }

    postData('/api/v1/DataObjects/' + pageType + '/' + objectId + '/Rollback/' + historyId, 'POST', {})
        .then(response => {
            alert(lang.getLang('rollbacksuccess', 'Successfully rolled back to previous version'));
            // Reload history to show new entry
            loadHistory(currentPage);
            // Optionally redirect back to detail page
            // window.location.replace("/index.html?page=dataobjectdetail&type=" + pageType + "&id=" + objectId);
        })
        .catch(error => {
            console.error('Error performing rollback:', error);
            alert(lang.getLang('rollbackfailed', 'Failed to rollback') + ': ' + error.message);
        });
}

// Load initial history
loadHistory(1);
