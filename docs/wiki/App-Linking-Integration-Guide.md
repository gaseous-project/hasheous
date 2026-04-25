# App-Linking Integration Guide

This guide explains how developers of client applications (ROM managers, metadata tools, etc.) can implement the **Link to Hasheous** flow so their users can grant the application access to their Hasheous account.

---

## Overview

Hasheous provides an OAuth-style pop-up authorisation flow. When a user clicks your **Link to Hasheous** button your application:

1. Opens a small pop-up window hosted by Hasheous.
2. The user logs in (if not already) and confirms the link.
3. Hasheous sends a per-user API key back to your application via `postMessage`.
4. Your application stores that key and sends it as the `X-API-Key` header on future requests.

No passwords or session tokens are ever shared with your application.

---

## Prerequisites

1. **Register your application** as an `App`-type DataObject in Hasheous (Settings → Apps → New App).
2. **Obtain a Client API Key** for your App DataObject (App detail page → Client API Keys → Create).  
   This is your `clientApiKey` — a long random string you embed in your application.  
   Treat it like a public OAuth `client_id`; it identifies *your app*, not the user.

---

## Step 1 – Add the "Link to Hasheous" button

Display the button wherever users manage integrations in your UI.  
Hasheous provides an official button asset and CSS class you can use, or you can draw your own following the specification below.

### Using the Hasheous-provided asset

The official button SVG is served from:

```
https://<your-hasheous-host>/images/hasheous-link-button.svg
```

It is a 220 × 40 px white button with the Hasheous "H" mark and the label **Link to Hasheous**.

### HTML snippet (plain button)

```html
<button type="button"
        class="hasheous-link-button"
        id="hasheous-link-btn"
        onclick="startHasheousLink()">
  <!-- Inline "H" icon -->
  <svg class="hasheous-link-button-icon"
       xmlns="http://www.w3.org/2000/svg"
       viewBox="0 0 30 30"
       aria-hidden="true">
    <rect x="3"  y="3"  width="6" height="24" rx="1.5" fill="#001638"/>
    <rect x="21" y="3"  width="6" height="24" rx="1.5" fill="#001638"/>
    <rect x="3"  y="12" width="24" height="6"  rx="1.5" fill="#001638"/>
  </svg>
  <span>Link to Hasheous</span>
</button>
```

### CSS (copy from Hasheous or host your own)

The `.hasheous-link-button` class mirrors the Google Material sign-in button style:

```css
.hasheous-link-button {
    -webkit-appearance: none;
    background-color: #ffffff;
    border: 1px solid #747775;
    border-radius: 10px;
    box-sizing: border-box;
    color: #1f1f1f;
    cursor: pointer;
    font-family: "Lato", "Roboto", Arial, sans-serif;
    font-size: 14px;
    font-weight: 600;
    height: 40px;
    letter-spacing: 0.25px;
    outline: none;
    overflow: hidden;
    padding: 0 12px;
    transition: background-color .218s, border-color .218s, box-shadow .218s;
    white-space: nowrap;
    display: inline-flex;
    align-items: center;
    gap: 10px;
}

.hasheous-link-button .hasheous-link-button-icon {
    width: 20px;
    height: 20px;
    flex-shrink: 0;
}

.hasheous-link-button:hover {
    box-shadow: 0 1px 2px 0 rgba(60,64,67,.30),
                0 1px 3px 1px rgba(60,64,67,.15);
}

.hasheous-link-button:disabled {
    cursor: default;
    background-color: #ffffff61;
    border-color: #1f1f1f1f;
    color: rgba(31,31,31,0.38);
}
```

### Dark-mode variant

For dark-themed UIs invert the background and border:

```css
@media (prefers-color-scheme: dark) {
    .hasheous-link-button {
        background-color: #1e1e1e;
        border-color: #8c8c8c;
        color: #e8e8e8;
    }
    .hasheous-link-button .hasheous-link-button-icon rect {
        fill: #7fa8d8; /* lighter navy for dark backgrounds */
    }
}
```

---

## Step 2 – Open the authorisation popup

When the user clicks the button, open the Hasheous link page in a small popup window:

```js
let _hasheousPopup = null;

function startHasheousLink() {
    const HASHEOUS_HOST = 'https://hasheous.org'; // replace with your Hasheous server URL
    const CLIENT_API_KEY = 'YOUR_CLIENT_API_KEY';  // replace with your app's client key
    const targetOrigin   = encodeURIComponent(window.location.origin);

    const url = `${HASHEOUS_HOST}/pages/link-app.html`
              + `?clientApiKey=${encodeURIComponent(CLIENT_API_KEY)}`
              + `&targetOrigin=${targetOrigin}`;

    _hasheousPopup = window.open(url, 'hasheousLink', 'width=480,height=640');

    if (!_hasheousPopup) {
        // Popup was blocked – inform the user
        alert('Please allow pop-ups for this site to link your Hasheous account.');
    }
}
```

> **Security note**: Always pass `targetOrigin` as your own application's origin.  
> Hasheous will restrict `postMessage` to that origin so the API key cannot be intercepted by other frames.

---

## Step 3 – Listen for the result

Add a `message` listener **once** at page load (not inside the click handler):

```js
window.addEventListener('message', function onHasheousMessage(event) {
    // Safety: ignore messages not from Hasheous
    const HASHEOUS_HOST = 'https://hasheous.org';
    if (event.origin !== HASHEOUS_HOST) return;

    const data = event.data;
    if (!data || data.type !== 'hasheous-link') return;

    if (data.cancelled) {
        // User closed the popup without confirming
        console.log('Hasheous link cancelled by user.');
        return;
    }

    if (data.hasheousApiKey) {
        // Store the key securely (e.g. encrypted local storage, keychain, etc.)
        storeHasheousApiKey(data.hasheousApiKey);
        console.log('Hasheous account linked successfully.');
    }
});
```

---

## Step 4 – Use the API key

Pass the stored key as the `X-API-Key` header on every authenticated request to Hasheous:

```js
async function hasheousLookup(md5Hash) {
    const response = await fetch(`https://hasheous.org/api/v1/Lookup/ByHash/md5/${md5Hash}`, {
        headers: {
            'X-API-Key': getStoredHasheousApiKey(),
            'X-Client-API-Key': 'YOUR_CLIENT_API_KEY'
        }
    });
    return response.json();
}
```

---

## Full working example

```html
<!DOCTYPE html>
<html>
<head>
  <meta charset="utf-8"/>
  <title>My App – Settings</title>
  <style>
    /* ── Hasheous Link button ─────────────────────────────── */
    .hasheous-link-button {
        -webkit-appearance: none;
        background-color: #ffffff;
        border: 1px solid #747775;
        border-radius: 10px;
        box-sizing: border-box;
        color: #1f1f1f;
        cursor: pointer;
        font-family: "Lato", "Roboto", Arial, sans-serif;
        font-size: 14px;
        font-weight: 600;
        height: 40px;
        letter-spacing: 0.25px;
        outline: none;
        padding: 0 12px;
        transition: box-shadow .218s;
        display: inline-flex;
        align-items: center;
        gap: 10px;
    }
    .hasheous-link-button .hasheous-link-button-icon { width:20px; height:20px; flex-shrink:0; }
    .hasheous-link-button:hover {
        box-shadow: 0 1px 2px 0 rgba(60,64,67,.30), 0 1px 3px 1px rgba(60,64,67,.15);
    }
  </style>
</head>
<body>

  <h2>Hasheous Integration</h2>
  <p id="link-status">Not linked.</p>

  <button type="button" class="hasheous-link-button" onclick="startHasheousLink()">
    <svg class="hasheous-link-button-icon" xmlns="http://www.w3.org/2000/svg" viewBox="0 0 30 30" aria-hidden="true">
      <rect x="3"  y="3"  width="6" height="24" rx="1.5" fill="#001638"/>
      <rect x="21" y="3"  width="6" height="24" rx="1.5" fill="#001638"/>
      <rect x="3"  y="12" width="24" height="6"  rx="1.5" fill="#001638"/>
    </svg>
    <span>Link to Hasheous</span>
  </button>

  <script>
    const HASHEOUS_HOST   = 'https://hasheous.org';
    const CLIENT_API_KEY  = 'YOUR_CLIENT_API_KEY';

    // Listen for popup result (registered once at page load)
    window.addEventListener('message', function (event) {
        if (event.origin !== HASHEOUS_HOST) return;
        const data = event.data;
        if (!data || data.type !== 'hasheous-link') return;

        if (data.cancelled) {
            document.getElementById('link-status').textContent = 'Linking cancelled.';
            return;
        }
        if (data.hasheousApiKey) {
            localStorage.setItem('hasheousApiKey', data.hasheousApiKey);
            document.getElementById('link-status').textContent = '✓ Linked to Hasheous!';
        }
    });

    function startHasheousLink() {
        const targetOrigin = encodeURIComponent(window.location.origin);
        const url = `${HASHEOUS_HOST}/pages/link-app.html`
                  + `?clientApiKey=${encodeURIComponent(CLIENT_API_KEY)}`
                  + `&targetOrigin=${targetOrigin}`;

        const popup = window.open(url, 'hasheousLink', 'width=480,height=640');
        if (!popup) alert('Please allow pop-ups to link your Hasheous account.');
    }
  </script>

</body>
</html>
```

---

## Button visual specification

Use these values when designing a native-platform variant (desktop apps, mobile, etc.):

| Property | Value |
|---|---|
| Background | `#ffffff` (white) / `#1e1e1e` (dark mode) |
| Border | `1px solid #747775` / `1px solid #8c8c8c` (dark) |
| Border radius | `10px` |
| Height | `40px` |
| Padding | `0 12px` |
| Icon | Hasheous "H" mark, `20 × 20px`, colour `#001638` / `#7fa8d8` (dark) |
| Icon–label gap | `10px` |
| Font | Lato Bold (fallback: Roboto, Arial) |
| Font size | `14px` |
| Font weight | `600` |
| Label colour | `#1f1f1f` / `#e8e8e8` (dark) |
| Label text | **Link to Hasheous** |
| Hover shadow | `0 1px 2px rgba(60,64,67,.30), 0 1px 3px 1px rgba(60,64,67,.15)` |
| Disabled opacity | `38%` |

The Hasheous "H" icon is defined by three rectangles:

```
Left bar:   x=3  y=3  w=6  h=24  rx=1.5
Right bar:  x=21 y=3  w=6  h=24  rx=1.5
Crossbar:   x=3  y=12 w=24 h=6   rx=1.5
```

(Coordinates within a `30 × 30` viewBox.)

---

## Error handling

| Situation | What happens |
|---|---|
| Unrecognised / revoked `clientApiKey` | Hasheous shows "Application Not Recognised" and no key is issued. `message` event is never fired. |
| User cancels or closes popup | `event.data.cancelled === true` |
| Popup blocked by browser | `window.open()` returns `null` — show a prompt asking the user to allow pop-ups. |
| User already linked this app | The existing key is returned (idempotent). You can call the flow again at any time to retrieve it. |
| Key revoked by user | Future API calls with that key return `401`. Repeat the linking flow to obtain a fresh key. |

---

## Revoking access

Users can revoke your application's access at any time from their **Account → Linked Applications** page on the Hasheous website. Your application should handle `401` responses gracefully and prompt the user to re-link.

---

## API reference

| Endpoint | Auth | Description |
|---|---|---|
| `GET /api/v1/AppLink/AppInfo?clientApiKey=xxx` | Anonymous | Returns `{ dataObjectId, name, logoUrl }` for the app. |
| `POST /api/v1/AppLink/Authorize` | Cookie (user) | Body: `{ clientApiKey }`. Returns the raw API key string. |
| `GET /api/v1/Account/AppLinks` | Cookie (user) | Lists all apps the user has linked. |
| `DELETE /api/v1/Account/AppLinks/{id}` | Cookie (user) | Revokes a specific link. |

All other Hasheous API endpoints that require user authentication accept the `X-API-Key` header with the key obtained above.

---

## Support

- GitHub Issues: <https://github.com/gaseous-project/hasheous>
- Discord: <https://discord.gg/Nhu7wpT3k4>
