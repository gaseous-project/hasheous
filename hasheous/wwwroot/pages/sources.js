function loadSourceCounts() {
    fetch('/api/v1/Sources/Statistics', {
        method: 'GET'
    }).then(response => {
        if (!response.ok) {
            throw new Error('Network response was not ok');
        }
        return response.json();
    }).then(success => {
        let sourcesTarget = document.getElementById('sources_hashes');

        for (const key in success) {
            let sourceBox = document.createElement('div');
            sourceBox.classList.add('source-box');

            let headingText = document.createElement('div');
            headingText.classList.add('heading-text');
            headingText.classList.add('color-' + key.toLowerCase());
            headingText.innerHTML = lang.getLang(key);
            sourceBox.appendChild(headingText);

            let description = document.createElement('div');
            description.classList.add('source-description');
            description.innerHTML = lang.getLang(key + 'desc');
            sourceBox.appendChild(description);

            let homepage = document.createElement('div');
            homepage.classList.add('source-homepage');
            homepage.innerHTML = lang.getLang('homepage') + '<br /><a href="' + lang.getLang(key + 'homepage') + '" target="_blank" rel="noopener noreferrer">' + lang.getLang(key + 'homepage') + '<img src="/images/link.svg" class="linkicon"></a>';
            sourceBox.appendChild(homepage);

            let count = document.createElement('div');
            count.classList.add('source-count');
            count.innerHTML = lang.getLang('romcount') + ': ' + success[key];
            sourceBox.appendChild(count);

            sourcesTarget.appendChild(sourceBox);
        }
    });
}

loadSourceCounts();