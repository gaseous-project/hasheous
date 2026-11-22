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

            if (lang.getLang(key + 'homepage')) {
                let homepage = document.createElement('div');
                homepage.classList.add('source-homepage');
                homepage.innerHTML = lang.getLang('homepage') + '<br />';
                const homepageUrl = lang.getLang(key + 'homepage', [], true);
                const homepageLink = document.createElement('a');
                if (isSafeUrl(homepageUrl)) {
                    homepageLink.href = homepageUrl;
                }
                homepageLink.target = '_blank';
                homepageLink.rel = 'noopener noreferrer';
                homepageLink.textContent = homepageUrl;
                const linkIcon = document.createElement('img');
                linkIcon.src = '/images/link.svg';
                linkIcon.className = 'linkicon';
                linkIcon.alt = '';
                homepageLink.appendChild(linkIcon);
                homepage.appendChild(homepageLink);
                sourceBox.appendChild(homepage);
            }

            let count = document.createElement('div');
            count.classList.add('source-count');
            count.innerHTML = lang.getLang('romcount') + ' ' + success[key];
            sourceBox.appendChild(count);

            sourcesTarget.appendChild(sourceBox);
        }
    });
}

loadSourceCounts();