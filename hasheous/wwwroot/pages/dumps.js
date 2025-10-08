fetch('/api/v1/Dumps/platforms?includeDetails=true', {
    method: 'GET'
}).then(response => {
    if (!response.ok) {
        throw new Error('Network response was not ok');
    }
    return response.json();
}).then(success => {
    let dumpsTarget = document.getElementById('dumps_list');

    if (success.length === 0) {
        dumpsTarget.innerHTML = '<p>No dumps are currently available.</p>';
        return;
    }

    let dumpTable = document.createElement('table');
    dumpTable.classList.add('dump-table');

    let headerRow = document.createElement('tr');
    let headers = [lang.getLang('platform'), lang.getLang('size'), lang.getLang('updateddate'), lang.getLang('download')];
    headers.forEach(headerText => {
        let th = document.createElement('th');
        th.classList.add('tableheadcell');
        th.textContent = headerText;
        headerRow.appendChild(th);
    });
    dumpTable.appendChild(headerRow);

    success.forEach(dump => {
        let row = document.createElement('tr');

        let platformCell = document.createElement('td');
        platformCell.classList.add('tablecell');
        const lastDot = dump.name ? dump.name.lastIndexOf('.') : -1;
        const displayName = (lastDot > 0) ? dump.name.substring(0, lastDot) : dump.name;
        platformCell.textContent = displayName;
        row.appendChild(platformCell);

        let sizeCell = document.createElement('td');
        sizeCell.classList.add('tablecell');
        sizeCell.textContent = dump.sizeBytes ? (dump.sizeBytes / (1024)).toFixed(2) + ' KB' : '-';
        row.appendChild(sizeCell);

        let updatedCell = document.createElement('td');
        updatedCell.classList.add('tablecell');
        let updatedDate = new Date(dump.lastModifiedUtc);
        updatedCell.textContent = updatedDate.toLocaleDateString() + ' ' + updatedDate.toLocaleTimeString();
        row.appendChild(updatedCell);

        let downloadCell = document.createElement('td');
        downloadCell.classList.add('tablecell');
        let downloadLink = document.createElement('a');
        downloadLink.href = '/api/v1/Dumps/platforms/' + encodeURIComponent(dump.name);
        downloadLink.textContent = lang.getLang('download');
        downloadCell.appendChild(downloadLink);
        row.appendChild(downloadCell);

        dumpTable.appendChild(row);
    });

    dumpsTarget.appendChild(dumpTable);
});