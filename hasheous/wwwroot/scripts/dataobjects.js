class dataObjectAttributes {
    constructor(attribute, options = null) {
        this.attribute = attribute;
        this.options = options;

        this.inputElement = null;
        this.endpoint = null;
        this.dataObject = true;
        this.paramName = null;
        this.isSelect2 = false;

        switch (attribute.attributeType) {
            case "LongString":
                this.inputElement = document.createElement('textarea');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'input';
                break;

            case "ShortString":
            case "Link":
                this.inputElement = document.createElement('input');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'input';
                this.inputElement.type = 'text';
                this.inputElement.classList.add('inputwide');
                break;

            case "Boolean":
                this.inputElement = document.createElement('input');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'checkbox';
                this.inputElement.type = 'checkbox';
                break;

            case "DateTime":
                this.inputElement = document.createElement('input');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'input';
                this.inputElement.type = 'datetime-local';
                break;

            case "ImageId":
                this.inputElement = document.createElement('table');

                // none radio button
                let inputRow_None = document.createElement('tr');

                let inputCell1_none = document.createElement('td');
                inputCell1_none.style.width = '10px';

                let inputElement_None = document.createElement('input');
                inputElement_None.id = 'attribute' + attribute.attributeName.toLowerCase() + 'selectnone';
                inputElement_None.type = 'radio';
                inputElement_None.name = attribute.attributeName.toLowerCase();
                inputElement_None.value = '0';
                inputElement_None.checked = 'checked';
                inputCell1_none.appendChild(inputElement_None);
                inputRow_None.appendChild(inputCell1_none);

                let inputCell2_none = document.createElement('td');
                inputCell2_none.setAttribute('data-lang', 'none');
                inputCell2_none.innerHTML = lang.getLang('none');
                inputRow_None.appendChild(inputCell2_none);

                let inputCell3_none = document.createElement('td');
                inputRow_None.appendChild(inputCell3_none);

                this.inputElement.appendChild(inputRow_None);

                // new image radio button
                let inputRow_Image = document.createElement('tr');

                let inputCell1_image = document.createElement('td');
                inputCell1_image.style.width = '10px';

                let inputElement_Image = document.createElement('input');
                inputElement_Image.id = 'attribute' + attribute.attributeName.toLowerCase() + 'selectnew';
                inputElement_Image.type = 'radio';
                inputElement_Image.name = attribute.attributeName.toLowerCase();
                inputElement_Image.value = '1';
                inputCell1_image.appendChild(inputElement_Image);
                inputRow_Image.appendChild(inputCell1_image);

                let inputCell2_image = document.createElement('td');

                let inputElement_ImageFile = document.createElement('input');
                inputElement_ImageFile.id = 'attribute' + attribute.attributeName.toLowerCase() + 'file';
                inputElement_ImageFile.type = 'file';
                inputElement_ImageFile.accept = 'image/*';
                inputCell2_image.appendChild(inputElement_ImageFile);

                let inputElement_ImageRef = document.createElement('input');
                inputElement_ImageRef.id = 'attribute' + attribute.attributeName.toLowerCase() + 'newref';
                inputElement_ImageRef.type = 'text';
                inputElement_ImageRef.style.display = 'none';
                inputCell2_image.appendChild(inputElement_ImageRef);

                inputRow_Image.appendChild(inputCell2_image);

                let inputCell3_image = document.createElement('td');
                inputCell3_image.id = 'attribute' + attribute.attributeName.toLowerCase() + 'uploadlabel';
                inputRow_Image.appendChild(inputCell3_image);

                this.inputElement.appendChild(inputRow_Image);

                // existing image radio button
                let inputRow_ImageExisting = document.createElement('tr');

                let inputCell1_imageExisting = document.createElement('td');
                inputCell1_imageExisting.style.width = '10px';

                let inputElement_ImageExisting = document.createElement('input');
                inputElement_ImageExisting.id = 'attribute' + attribute.attributeName.toLowerCase() + 'selectexisting';
                inputElement_ImageExisting.type = 'radio';
                inputElement_ImageExisting.name = attribute.attributeName.toLowerCase();
                inputElement_ImageExisting.value = '2';
                inputCell1_imageExisting.appendChild(inputElement_ImageExisting);

                inputRow_ImageExisting.appendChild(inputCell1_imageExisting);

                let inputCell2_imageExisting = document.createElement('td');
                inputCell2_imageExisting.setAttribute('data-lang', 'useexisting');
                inputCell2_imageExisting.innerHTML = lang.getLang('useexisting');
                inputRow_ImageExisting.appendChild(inputCell2_imageExisting);

                let inputCell3_imageExisting = document.createElement('td');
                inputRow_ImageExisting.appendChild(inputCell3_imageExisting);

                this.inputElement.appendChild(inputRow_ImageExisting);

                // original reference
                let imageRef = document.createElement('input');
                imageRef.id = 'attribute' + attribute.attributeName.toLowerCase() + 'ref';
                imageRef.type = 'text';
                imageRef.style.display = 'none';
                this.inputElement.appendChild(imageRef);

                // setup events
                inputElement_ImageFile.addEventListener("change", async function (e) {
                    let ofile = inputElement_ImageFile.files[0];
                    let formdata = new FormData();
                    formdata.append("file", ofile);

                    let uploadLabel = inputCell3_image;
                    uploadLabel.innerHTML = lang.getLang('uploadinglogo');

                    try {
                        const token = await fetchAntiforgeryToken();
                        const response = await fetch('/api/v1/Images/', {
                            method: 'POST',
                            body: formdata,
                            credentials: 'include',
                            headers: {
                                'X-XSRF-TOKEN': token
                            }
                        });

                        if (!response.ok) {
                            throw new Error('Network response was not ok');
                        }

                        const data = await response.text();
                        uploadLabel.innerHTML = lang.getLang('uploadlogocomplete');
                        inputElement_ImageRef.value = data;
                        inputElement_Image.checked = 'checked';
                        console.log(data);
                    } catch (error) {
                        console.warn("Error: " + error);
                    }
                });

                break;

            case "ObjectRelationship":
                this.inputElement = document.createElement('select');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'select';
                this.inputElement.classList.add('inputwide');

                let nullOption = document.createElement('option');
                nullOption.value = '';
                nullOption.innerHTML = "None";
                this.inputElement.appendChild(nullOption);

                switch (attribute.attributeName) {
                    case "Platform":
                        this.endpoint = '/api/v1/DataObjects/Platform';
                        this.isSelect2 = true;
                        break;

                    case "Publisher":
                    case "Manufacturer":
                        this.isSelect2 = true;
                        this.endpoint = '/api/v1/DataObjects/Company';
                        break;

                    default:
                        break;
                }

                break;

            case "EmbeddedList":
                switch (attribute.attributeName) {
                    case "Tags":
                        this.dataObject = false;
                        let regex = new RegExp(pageType, 'i');
                        this.paramName = this.options.replace(regex, '');

                        this.inputElement = document.createElement('select');
                        this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'select' + this.options;
                        this.inputElement.classList.add('inputwide');
                        this.inputElement.setAttribute('multiple', 'multiple');

                        this.endpoint = '/api/v1/Tags/' + this.options;
                        this.isSelect2 = true;
                        break;
                }
                break;

            default:
                this.inputElement = document.createElement('input');
                this.inputElement.id = 'attribute' + attribute.attributeName.toLowerCase() + 'input';
                break;
        }
    }

    render() {
        if (this.isSelect2 == true) {
            this.#SetupObjectMenus(this.inputElement, this.endpoint, this.dataObject, this.paramName);
        }
    }

    #SetupObjectMenus(dropdown, endpoint, isDataObject = true, searchParamName = null) {
        // $('body').on('DOMContentLoaded', 'select', function () {

        let tags = false;
        let tagSeparators = [','];
        if (isDataObject === false) {
            tags = true;
        }

        $(dropdown).select2({
            minimumInputLength: 3,
            width: '95%',
            tags: tags,
            tokenSeparators: tagSeparators,
            ajax: {
                allowClear: true,
                placeholder: {
                    "id": "",
                    "text": "None"
                },
                url: endpoint,
                data: function (params) {
                    if (isDataObject === true) {
                        return {
                            search: params["term"]
                        }
                    } else {
                        let query = {
                            search: params.term
                        };
                        return query;
                    }
                },
                processResults: function (data) {
                    let arr = [];

                    if (isDataObject === true) {
                        arr.push({
                            id: "",
                            text: "None"
                        });
                    }

                    if (isDataObject === true) {
                        for (let i = 0; i < data.objects.length; i++) {
                            arr.push({
                                id: "",
                                text: data.objects[i].name,
                                fullObject: data.objects[i]
                            });
                        }
                    } else {
                        {
                            for (let i = 0; i < data.length; i++) {
                                arr.push({
                                    id: data[i].text,
                                    text: data[i].text
                                });
                            }
                        }
                    }

                    return {
                        results: arr
                    };
                }
            }
        });
        // });
    }

    getValue() {
        switch (this.attribute.attributeType) {
            case "ImageId":
                let selectedRadioButton = this.inputElement.querySelector("input[type='radio'][name='" + this.attribute.attributeName.toLowerCase() + "']:checked");
                switch (selectedRadioButton.value) {
                    case "0":
                        // none
                        return null;

                    case "1":
                        // new
                        let newValue = this.inputElement.querySelector('#attribute' + this.attribute.attributeName.toLowerCase() + 'newref').value;
                        return newValue;

                    case "2":
                        // existing
                        let existingValue = this.inputElement.querySelector('#attribute' + this.attribute.attributeName.toLowerCase() + 'ref').value;
                        return existingValue;

                    default:
                        return null;
                }

            case "ObjectRelationship":
                return this.inputElement.value;

            case "Boolean":
                return this.inputElement.checked;

            case "EmbeddedList":
                switch (this.attribute.attributeName) {
                    case "Tags":
                        let selectedOptions = $(this.inputElement).val();
                        return selectedOptions;
                }
                break;

            default:
                return this.inputElement.value;
        }
    }
}

function createDataObjectsTable(pageNumber, pageSize, objectType, filterByPlatformId) {
    if (!pageNumber) {
        pageNumber = 1;
    }
    if (!pageSize) {
        pageSize = 20;
    }
    if (!objectType) {
        objectType = pageType;
    }

    let filterString = '';
    if (filterByPlatformId) {
        filterString = '&filterAttribute=Platform&filterValue=' + filterByPlatformId;
    }

    fetch('/api/v1/DataObjects/' + objectType + '?pageSize=' + pageSize + '&pageNumber=' + pageNumber + '&getchildrelations=true' + filterString + '&getMetadata=true', {
        method: 'GET'
    }).then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    }).then(success => {
        console.log(success);
        let columns = [];

        switch (objectType) {
            case "game":
                columns = [
                    'id',
                    {
                        column: 'attributes[attributeName=Logo].value:image',
                        name: 'logo'
                    },
                    'name',
                    {
                        column: 'attributes[attributeName=Platform].value.name',
                        name: 'platform'
                    },
                    {
                        column: 'attributes[attributeName=Publisher].value.name',
                        name: 'publisher'
                    }//,
                    // {
                    //     column: 'metadata[source=IGDB].id',
                    //     name: 'igdb'
                    // }
                ];
                break;

            case "platform":
                columns = [
                    'id',
                    {
                        column: 'attributes[attributeName=Logo].value:image',
                        name: 'logo'
                    },
                    'name',
                    {
                        column: 'attributes[attributeName=Manufacturer].value.name',
                        name: 'manufacturer'
                    }//,
                    // {
                    //     column: 'metadata[source=IGDB].id',
                    //     name: 'igdb'
                    // }
                ];
                break;

            case "company":
                columns = [
                    'id',
                    {
                        column: 'attributes[attributeName=Logo].value:image',
                        name: 'logo'
                    },
                    'name'//,
                    // {
                    //     column: 'metadata[source=IGDB].id',
                    //     name: 'igdb'
                    // }
                ];
                break;

            case "app":
                columns = [
                    'id',
                    {
                        column: 'attributes[attributeName=Logo].value:image',
                        name: 'logo'
                    },
                    'name',
                    {
                        column: 'attributes[attributeName=Publisher].value',
                        name: 'publisher'
                    },
                    {
                        column: 'attributes[attributeName=HomePage].value:link',
                        name: 'link'
                    }
                ];
                break;

            default:
                columns = [
                    'id',
                    'name'
                ];
                break;

        }

        let newTable = new generateTable(
            success.objects,
            columns,
            'id',
            true,
            function (id) {
                window.location = '/index.html?page=dataobjectdetail&type=' + objectType + '&id=' + id;
            },
            success.count,
            success.pageNumber,
            success.totalPages,
            function (p) {
                createDataObjectsTable(p, pageSize, objectType, filterByPlatformId);
            }
        );
        let tableTarget = document.getElementById('dataObjectTable');
        tableTarget.innerHTML = '';
        tableTarget.appendChild(newTable);
    });
}