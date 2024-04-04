const lang = new language();
console.log("Browser locale: " + lang.locale);

function ajaxCall(endpoint, method, successFunction, errorFunction, body) {
    $.ajax({

        // Our sample url to make request
        url:
            endpoint,

        // Type of Request
        type: method,

        // data to send to the server
        data: body,

        dataType: 'json',
        contentType: 'application/json',

        // Function to call when to
        // request is ok
        success: function (data) {
            //let x = JSON.stringify(data);
            //console.log(x);
            successFunction(data);
        },

        // Error handling
        error: function (error) {
            console.log(`Error ${JSON.stringify(error)}`);

            if (errorFunction) {
                errorFunction(error);
            }
        }
    });
}

function getQueryString(stringName, type) {
    const urlParams = new URLSearchParams(window.location.search);
    let myParam =  urlParams.get(stringName);

    switch (type) {
        case "int":
            if (typeof (Number(myParam)) == 'number') {
                return Number(myParam);
            } else {
                return null;
            }
        case "string":
            if (typeof (myParam) == 'string') {
                return encodeURIComponent(myParam);
            } else {
                return null;
            }
        default:
            return null;
    }
}

function setCookie(cname, cvalue, exdays) {
    const d = new Date();
    d.setTime(d.getTime() + (exdays*24*60*60*1000));
    if (exdays) {
        let expires = "expires="+ d.toUTCString();
        document.cookie = cname + "=" + cvalue + ";" + expires + ";path=/";
    } else {
        document.cookie = cname + "=" + cvalue + ";path=/";
    }
}

function getCookie(cname) {
    let name = cname + "=";
    let decodedCookie = decodeURIComponent(document.cookie);
    let ca = decodedCookie.split(';');
    for(let i = 0; i <ca.length; i++) {
      let c = ca[i];
      while (c.charAt(0) == ' ') {
        c = c.substring(1);
      }
      if (c.indexOf(name) == 0) {
        return c.substring(name.length, c.length);
      }
    }
    return "";
}

function formatBytes(bytes, decimals = 2) {
    if (!+bytes) return '0 Bytes'

    const k = 1024
    const dm = decimals < 0 ? 0 : decimals
    const sizes = ['Bytes', 'KiB', 'MiB', 'GiB', 'TiB', 'PiB', 'EiB', 'ZiB', 'YiB']

    const i = Math.floor(Math.log(bytes) / Math.log(k))

    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(dm))} ${sizes[i]}`
}

function generateTable(resultSet, columns, indexColumn, hideIndex, rowClickCallback) {
    if (hideIndex == undefined) {
        hideIndex = false;
    }

    if (resultSet.length == 0) {
        let errorMessage = document.createElement('span');
        errorMessage.innerHTML = lang.getLang('norecords');
        
        return errorMessage;
    } else {
        let table = document.createElement('table');
        
        // create header from attribute names in columns
        let headerRow = document.createElement('tr');
        if (!indexColumn) {
            indexColumn = "";
        }
        for (let i = 0; i < columns.length; i++) {
            let headerName = lang.getLang(columns[i].split(":")[0]);
            if (
                (hideIndex === true && (headerName.toLowerCase() !== indexColumn.toLowerCase())) ||
                (hideIndex === false)
                ) {
                let headerCell = document.createElement('th');
                headerCell.innerHTML = headerName;
                headerCell.classList.add('tableheadcell');
                headerRow.appendChild(headerCell);
            }
        }
        table.appendChild(headerRow);

        for (let i = 0; i < resultSet.length; i++) {
            let dataRow = document.createElement('tr');
            let rowId = null;

            for (let x = 0; x < columns.length; x++) {
                let cellDetails = columns[x].split(":");
                let cellName = cellDetails[0];
                let cellType = '';
                if (cellDetails[1]) {
                    cellType = cellDetails[1];
                }
                
                let cellContent = document.createElement('span');
                switch (cellType) {
                    case "date":
                        cellContent.innerHTML = moment(resultSet[i][cellName] + "Z").format('llll');
                        break;
                    
                    case "lang":
                        cellContent.innerHTML = lang.getLang(resultSet[i][cellName]);
                        break;

                    case "link":
                        if (resultSet[i][cellName].length > 0) {
                            cellContent.innerHTML = "<a href=\"" + resultSet[i][cellName] + "\" target=\"_blank\" rel=\"noopener noreferrer\">" + resultSet[i][cellName] + "<img src=\"/images/link.svg\" class=\"linkicon\" /></a>";
                        } else {
                            cellContent.innerHTML = "";
                        }
                        break;

                    case "bytes":
                        cellContent.innerHTML = formatBytes(resultSet[i][cellName], 1);
                        break;

                    case "object":
                        cellContent = resultSet[i][cellName];
                        break;

                    default:
                        // default to plain text
                        cellContent.innerHTML = resultSet[i][cellName];
                        break;

                }

                if (
                    (hideIndex === true && (cellName.toLowerCase() !== indexColumn.toLowerCase())) ||
                    (hideIndex === false)
                ) {
                    let cell = document.createElement('td');
                    cell.classList.add('tablecell');
                    cell.appendChild(cellContent);
                    dataRow.appendChild(cell);
                }

                if (Object.keys(resultSet[0])[x] == indexColumn) {
                    dataRow.setAttribute('data-' + cellName, cellContent.innerHTML);
                    rowId = cellContent.innerHTML;
                }
            }

            if (rowId != null) {
                if (rowClickCallback) {
                    dataRow.classList.add('tablerowhighlight');
                    dataRow.addEventListener("click", function() {
                        rowClickCallback(rowId);
                    }, true);
                }
            }

            table.appendChild(dataRow);
        }

        return table;
    }
}