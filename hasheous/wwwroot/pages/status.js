function LoadStatusPage() {
    const statusTable = document.getElementById('status-table');
    statusTable.innerHTML = ''; // Clear previous content

    fetch('/api/v1.0/BackgroundTasks', {
        method: 'GET'
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            return response.json();
        })
        .then(data => {
            if (data && data.length > 0) {
                // build the table

                const headRow = document.createElement('tr');
                headRow.innerHTML = `
                    <th>${lang.getLang('serviceitemtype')}</th>
                    <th>${lang.getLang('serviceitemstate')}</th>
                    <th>${lang.getLang('serviceitemlastruntime')}</th>
                    <th>${lang.getLang('serviceitemnextruntime')}</th>
                `;
                statusTable.appendChild(headRow);

                const tableGroups = {
                    "signatures": [
                        "SignatureIngestor"
                    ],
                    "metadataproxy": [
                        "FetchIGDBMetadata",
                        "FetchTheGamesDbMetadata",
                        "FetchVIMMMetadata",
                        "FetchGiantBombMetadata"
                    ],
                    "maintenance": [
                        "DailyMaintenance",
                        "WeeklyMaintenance",
                        "CacheWarmer"
                    ],
                    "servicemanagement": [
                        "TallyVotes",
                        "MetadataMatchSearch",
                        "GetMissingArtwork"
                    ]
                }

                // Group tasks by type
                Object.keys(tableGroups).forEach(group => {
                    const groupItems = tableGroups[group];

                    const groupBody = document.createElement('tbody');
                    const groupHeader = document.createElement('tr');
                    groupHeader.innerHTML = `<td colspan="4"><h3>${lang.getLang('service' + group)}</h3></td>`;
                    groupBody.appendChild(groupHeader);

                    let groupFound = false;
                    tableGroups[group].forEach(itemType => {
                        const task = data.find(task => task.itemType === itemType);
                        if (task) {
                            const row = document.createElement('tr');
                            row.innerHTML = `
                                <td class="tablecell">${lang.getLang('service' + task.itemType)}</td>
                                <td class="tablecell">${lang.getLang('service' + task.itemState)}</td>
                                <td class="tablecell">${task.lastRunTime ? new Date(task.lastRunTime).toLocaleString() : '-'}</td>
                                <td class="tablecell">${task.nextRunTime ? new Date(task.nextRunTime).toLocaleString() : '-'}</td>
                            `;
                            groupBody.appendChild(row);
                            groupFound = true;
                        }
                    });

                    if (groupFound) {
                        statusTable.appendChild(groupBody);
                    }
                });
            } else {
                statusTable.innerHTML = '<tr><td colspan="4">No background tasks found.</td></tr>';
            }
        })
        .catch(error => {
            console.error('Error fetching background tasks:', error);
            statusTable.innerHTML = '<tr><td colspan="4">Error loading status. Please try again later.</td></tr>';
        });
}

LoadStatusPage();

let statusRefresh = setInterval(() => {
    LoadStatusPage();
}, 30000); // Refresh every 30 seconds

window.addEventListener('beforeunload', () => {
    clearInterval(statusRefresh);
});