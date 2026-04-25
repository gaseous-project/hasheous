/**
 * link-app.js
 *
 * Drives the Hasheous App-Link popup flow:
 *
 * 1. Reads ?clientApiKey=xxx from the query string.
 * 2. Calls GET /api/v1/AppLink/AppInfo to validate the app and get its display info.
 * 3. Checks login state via GET /api/v1/Account/ProfileBasic.
 * 4. If not logged in, shows a login form; once authenticated, falls through to step 5.
 * 5. Shows an authorisation confirmation with the app name and logo.
 * 6. On Confirm: calls POST /api/v1/AppLink/Authorize, sends the returned key to the
 *    opener via postMessage, and closes the popup.
 * 7. On Cancel: sends a cancellation postMessage and closes the popup.
 *
 * postMessage shape (sent to window.opener):
 *   { type: 'hasheous-link', hasheousApiKey: '<key>' }   – on success
 *   { type: 'hasheous-link', cancelled: true }            – on cancel
 */

(function () {
    'use strict';

    // ── Helpers ──────────────────────────────────────────────────────────────

    function getQueryParam(name) {
        return new URLSearchParams(window.location.search).get(name);
    }

    function showSection(id) {
        ['error-section', 'login-section', 'confirm-section', 'success-section'].forEach(s => {
            document.getElementById(s).style.display = (s === id) ? 'block' : 'none';
        });
    }

    function sendToOpener(data) {
        if (window.opener && !window.opener.closed) {
            window.opener.postMessage(Object.assign({ type: 'hasheous-link' }, data), '*');
        }
    }

    function cancelAndClose() {
        sendToOpener({ cancelled: true });
        window.close();
    }

    // ── State ─────────────────────────────────────────────────────────────────

    const clientApiKey = getQueryParam('clientApiKey');
    let appInfo = null; // populated after AppInfo call

    // ── Localisation helper (falls back gracefully if lang is not loaded) ─────

    function t(key, fallback) {
        if (typeof lang !== 'undefined' && lang.getLang) {
            const v = lang.getLang(key);
            return (v && v !== key) ? v : (fallback || key);
        }
        return fallback || key;
    }

    // ── Step 1: validate app ──────────────────────────────────────────────────

    async function loadAppInfo() {
        if (!clientApiKey) {
            showError(t('linkapp_error_body', 'This application could not be identified. Please contact the application developer.'));
            return;
        }

        try {
            const resp = await fetch('/api/v1/AppLink/AppInfo?clientApiKey=' + encodeURIComponent(clientApiKey));
            if (!resp.ok) {
                const body = await resp.json().catch(() => ({}));
                showError(body.message || t('linkapp_error_body', 'This application could not be identified. Please contact the application developer.'));
                return;
            }
            appInfo = await resp.json();
        } catch {
            showError(t('linkapp_error_body', 'This application could not be identified. Please contact the application developer.'));
            return;
        }

        // Step 2: check login state
        await checkLogin();
    }

    function showError(message) {
        document.getElementById('error-message').textContent = message;
        showSection('error-section');
    }

    // ── Step 2: check login ───────────────────────────────────────────────────

    async function checkLogin() {
        try {
            const resp = await fetch('/api/v1/Account/ProfileBasic', { credentials: 'include' });
            if (resp.ok) {
                // User is logged in – go straight to confirmation
                showConfirm();
                return;
            }
        } catch { /* fall through to login */ }
        showLoginForm();
    }

    // ── Step 3: login form ────────────────────────────────────────────────────

    function showLoginForm() {
        // Wire register link to open in a new tab so the popup stays alive
        document.getElementById('register-link').href = '/index.html?page=register';
        document.getElementById('register-link').target = '_blank';

        document.getElementById('login-btn').addEventListener('click', doLogin);
        document.getElementById('cancel-btn-login').addEventListener('click', cancelAndClose);

        // Allow Enter key in password field
        document.getElementById('login_password').addEventListener('keyup', function (e) {
            if (e.key === 'Enter') doLogin();
        });

        showSection('login-section');
    }

    async function doLogin() {
        const email = document.getElementById('login_email').value;
        const password = document.getElementById('login_password').value;
        const rememberMe = document.getElementById('login_rememberme').checked;
        const errorLabel = document.getElementById('login_errorlabel');
        errorLabel.textContent = '';

        try {
            // Fetch antiforgery token first
            const tokenResp = await fetch('/api/v1/Account/antiforgery-token', { credentials: 'include' });
            const tokenData = await tokenResp.json();

            const resp = await fetch('/api/v1/Account/Login', {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': tokenData.token
                },
                body: JSON.stringify({ Email: email, Password: password, RememberMe: rememberMe })
            });

            if (resp.ok) {
                showConfirm();
            } else {
                errorLabel.textContent = t('incorrectpassword', 'Incorrect email address or password');
            }
        } catch {
            errorLabel.textContent = t('incorrectpassword', 'Incorrect email address or password');
        }
    }

    // ── Step 4: confirmation ──────────────────────────────────────────────────

    function showConfirm() {
        if (!appInfo) { showError(t('linkapp_error_body', 'Application information is unavailable.')); return; }

        const logoEl = document.getElementById('app-logo');
        if (appInfo.logoUrl) {
            logoEl.src = appInfo.logoUrl;
            logoEl.style.display = 'block';
        } else {
            logoEl.style.display = 'none';
        }

        document.getElementById('app-name').textContent = appInfo.name;

        // Build confirm message with app name interpolated
        const msgTemplate = t('linkapp_confirm_body', 'Allow {0} to access your Hasheous account?');
        document.getElementById('confirm-message').textContent = msgTemplate.replace('{0}', appInfo.name);

        document.getElementById('authorise-btn').addEventListener('click', doAuthorise);
        document.getElementById('cancel-btn-confirm').addEventListener('click', cancelAndClose);

        showSection('confirm-section');
    }

    // ── Step 5: authorise ─────────────────────────────────────────────────────

    async function doAuthorise() {
        document.getElementById('authorise-btn').disabled = true;

        try {
            // Fetch antiforgery token
            const tokenResp = await fetch('/api/v1/Account/antiforgery-token', { credentials: 'include' });
            const tokenData = await tokenResp.json();

            const resp = await fetch('/api/v1/AppLink/Authorize', {
                method: 'POST',
                credentials: 'include',
                headers: {
                    'Content-Type': 'application/json',
                    'X-CSRF-TOKEN': tokenData.token
                },
                body: JSON.stringify({ ClientApiKey: clientApiKey })
            });

            if (resp.ok) {
                const apiKey = await resp.text();
                showSection('success-section');
                sendToOpener({ hasheousApiKey: apiKey });
                // Close after a brief pause so the user sees the success message
                setTimeout(() => window.close(), 1500);
            } else {
                document.getElementById('authorise-btn').disabled = false;
                showError(t('linkapp_error_body', 'Authorisation failed. Please try again.'));
            }
        } catch {
            document.getElementById('authorise-btn').disabled = false;
            showError(t('linkapp_error_body', 'Authorisation failed. Please try again.'));
        }
    }

    // ── Boot ──────────────────────────────────────────────────────────────────

    // Apply localisation strings once the DOM is ready
    document.addEventListener('DOMContentLoaded', function () {
        if (typeof lang !== 'undefined') {
            lang.applyLanguage();
        }
        loadAppInfo();
    });
})();
