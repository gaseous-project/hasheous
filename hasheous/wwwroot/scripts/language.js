class language {
    locale = undefined;

    languageDefault = undefined;
    languageOverlay = undefined;

    // Optional resolved chain (for debugging / introspection)
    resolvedChain = [];

    langMapping =
        {
            "company":
            {
                "dataobject_objects": "companies",
                "dataobject_new": "newcompany"
            },
            "platform":
            {
                "dataobject_objects": "platforms",
                "dataobject_new": "newplatform"
            },
            "game":
            {
                "dataobject_objects": "games",
                "dataobject_new": "newgame"
            },
            "app":
            {
                "dataobject_objects": "apps",
                "dataobject_new": "newapp"
            },
        };

    constructor() {

    }

    async Init(callback) {
        await this.InitAsync();
        console.log('Language files loaded');
        if (callback) {
            callback();
        }
    }

    async InitAsync() {
        // load base language
        if (getCookie("userLocale")) {
            this.locale = getCookie("userLocale");
        } else {
            this.locale = window.navigator.userLanguage || window.navigator.language;
        }
        console.log("Browser locale: " + this.locale);
        await this.#loadWithFallbackChain();
        this.applyLanguage();
    }

    // async getJSON(url) {
    //     return fetch(url)
    //         .then((response) => response.json())
    //         .then((responseJson) => { return responseJson });
    // }

    getLang(token, substituteArray) {
        let page = getQueryString('page', 'string');
        switch (page) {
            case "dataobjects":
            case "dataobjectnew":
                let pageType = getQueryString('type', 'string');

                let newToken = this.langMapping[pageType][token];
                if (newToken) {
                    token = newToken;
                }

                break;

            default:
                break;
        }
        // Auto pluralisation support: if caller supplies a numeric (or first element numeric in array)
        // and token_one/token_other exist, prefer them transparently so existing calls need no refactor.
        let isNumberSingle = (typeof substituteArray === 'number');
        let count = undefined;
        if (isNumberSingle) {
            count = substituteArray;
            // normalize to array for downstream replacement so {0} works
            substituteArray = [substituteArray];
        } else if (Array.isArray(substituteArray) && substituteArray.length > 0 && typeof substituteArray[0] === 'number') {
            count = substituteArray[0];
        }

        const baseTokenLower = token.toLowerCase();
        if (count !== undefined) {
            const category = (count === 1) ? 'one' : 'other';
            const pluralToken = baseTokenLower + '_' + category;
            // Try overlay plural variant
            if (this.languageOverlay && pluralToken in this.languageOverlay) {
                return this.#replaceTokens(this.languageOverlay[pluralToken], substituteArray);
            }
            // Try base plural variant
            if (this.languageDefault && pluralToken in this.languageDefault) {
                return this.#replaceTokens(this.languageDefault[pluralToken], substituteArray);
            }
        }

        // Fallback to singular / base token lookup as before
        if (this.languageOverlay && baseTokenLower in this.languageOverlay) {
            return this.#replaceTokens(this.languageOverlay[baseTokenLower], substituteArray);
        }
        if (this.languageDefault) {
            if (baseTokenLower in this.languageDefault) {
                return this.#replaceTokens(this.languageDefault[baseTokenLower], substituteArray);
            }
            return baseTokenLower;
        }
    }

    /**
     * Returns a pluralised string based on simple one/other rules.
     * Example usage in UI code: lang.getPlural('unique_visitors', count)
     * Falls back to base key if _one/_other forms are missing.
     */
    getPlural(baseKey, count, substituteArray) {
        const category = (count === 1) ? 'one' : 'other';
        const key = baseKey + '_' + category;
        let text = this.getLang(key, substituteArray);
        if (!text || text === key) {
            // fallback to base key if specialised form absent
            text = this.getLang(baseKey, substituteArray);
        }
        return this.#replaceTokens(text, [count]);
    }

    async #loadWithFallbackChain() {
        const original = this.locale;
        const parts = original.split('-');
        const language = parts[0].toLowerCase();
        const region = parts[1] ? parts[1].toUpperCase() : undefined;

        // Predefined fallback chains for future locales (scripts, etc.)
        const fallbackChains = {
            'pt-BR': ['pt-BR', 'pt-PT', 'pt'],
            'pt-PT': ['pt-PT', 'pt'],
            'zh-HK': ['zh-HK', 'zh-Hant', 'zh'],
            'zh-TW': ['zh-TW', 'zh-Hant', 'zh'],
            'zh-CN': ['zh-CN', 'zh-Hans', 'zh']
        };

        // Build candidate overlays (language-region forms) to attempt
        let overlayCandidates = [];
        if (region) {
            const localeKey = language + '-' + region;
            if (fallbackChains[localeKey]) {
                overlayCandidates = fallbackChains[localeKey];
            } else {
                overlayCandidates = [localeKey];
            }
        }

        // Always end chain with plain language and 'en' as ultimate fallback
        const baseCandidates = [language, 'en'];

        // Load base (first candidate language that exists)
        for (let i = 0; i < baseCandidates.length; i++) {
            const base = baseCandidates[i];
            try {
                this.languageDefault = await $.getJSON('/localisation/' + base + '.json');
                this.resolvedChain.push(base + '.json');
                console.log('Loaded base language file: ' + base + '.json');
                break;
            } catch (e) {
                console.warn('Base language file not found: ' + base + '.json');
            }
        }
        if (!this.languageDefault) {
            console.error('Failed to load any base language file; aborting localisation load.');
            return;
        }

        // Try overlays
        for (let i = 0; i < overlayCandidates.length; i++) {
            const overlay = overlayCandidates[i];
            // Skip synthetic script identifiers like zh-Hant or zh-Hans unless you add files later
            if (overlay.indexOf('zh-Hant') === 0 || overlay.indexOf('zh-Hans') === 0) continue;
            try {
                this.languageOverlay = await $.getJSON('/localisation/' + overlay + '.json');
                this.resolvedChain.push(overlay + '.json');
                console.log('Loaded overlay language file: ' + overlay + '.json');
                break; // first successful overlay only
            } catch (e) {
                // continue to next candidate
                console.warn('Overlay not found: ' + overlay + '.json');
            }
        }
    }

    #replaceTokens(text, substituteArray) {
        let workText = text;
        if (substituteArray) {
            if (Array.isArray(substituteArray)) {
                // handle as an array
                for (let i = 0; i < substituteArray.length; i++) {
                    workText = workText.replace('{' + i + '}', substituteArray[i]);
                }
                return workText;
            } else {
                return text.replace('{0}', substituteArray);
            }
        } else {
            return text;
        }
    }

    setPageElementInnerHTMLLanguage(elementList) {
        for (let i = 0; i < elementList.length; i++) {
            let dataLang = $(elementList[i]).attr('data-lang');
            if (dataLang) {
                let dataLangStr = dataLang.replace(/\W/g, '');
                elementList[i].innerHTML = this.getLang(dataLangStr);
            }
        }
    }

    applyLanguage() {
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h1'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h2'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('h3'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('span'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('p'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('div'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('th'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('td'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('label'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('a'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('button'));
        this.setPageElementInnerHTMLLanguage(document.getElementsByTagName('option'));
    }
}