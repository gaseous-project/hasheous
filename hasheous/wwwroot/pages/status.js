function LoadStatusPage() {
    const statusTable = document.getElementById('status-table');
    statusTable.innerHTML = ''; // Clear previous content

    let columnCount = 5; // Default column count

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
                    <th></th>
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
                        "FetchGiantBombMetadata",
                        "FetchRetroAchievementsMetadata"
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
                    groupHeader.innerHTML = `<td colspan="${columnCount}"><h3>${lang.getLang('service' + group)}</h3></td>`;
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

                            if (userProfile != null) {
                                if (userProfile.Roles != null) {
                                    if (userProfile.Roles.includes('Admin') && ['Stopped', 'NeverStarted'].includes(task.itemState)) {
                                        row.innerHTML += `
                                            <td class="tablecell"><button class="btn btn-primary" onclick="StartTask('${task.itemType}');">${lang.getLang('servicestart')}</button></td>
                                        `;
                                    }
                                }
                            }

                            groupBody.appendChild(row);
                            groupFound = true;
                        }
                    });

                    if (groupFound) {
                        statusTable.appendChild(groupBody);
                    }
                });
            } else {
                statusTable.innerHTML = `<tr><td colspan="${columnCount}">No background tasks found.</td></tr>`;
            }
        })
        .catch(error => {
            console.error('Error fetching background tasks:', error);
            statusTable.innerHTML = `<tr><td colspan="${columnCount}">Error loading status. Please try again later.</td></tr>`;
        });
}

LoadStatusPage();

let statusRefresh = setInterval(() => {
    LoadStatusPage();
}, 30000); // Refresh every 30 seconds

window.addEventListener('beforeunload', () => {
    clearInterval(statusRefresh);
});

function StartTask(itemType) {
    fetch(`/api/v1.0/BackgroundTasks/${itemType}?ForceRun=true`, {
        method: 'GET'
    })
        .then(response => {
            if (!response.ok) {
                throw new Error('Network response was not ok');
            }
            LoadStatusPage(); // Reload the status page after starting the task
            return response.json();
        })
        .catch(error => {
            console.error('Error starting task:', error);
            alert(lang.getLang('servicestarterror') + ': ' + error.message);
        });
}
