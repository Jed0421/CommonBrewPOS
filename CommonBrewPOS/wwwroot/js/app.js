// ── AI Chatbot Panel ──────────────────────────────────────────
const aiPanel   = document.getElementById('aiPanel');
const aiOverlay = document.getElementById('aiOverlay');
const aiToggle  = document.getElementById('aiToggle');
const aiClose   = document.getElementById('aiClose');
const aiInput   = document.getElementById('aiInput');
const aiSend    = document.getElementById('aiSend');
const aiMessages = document.getElementById('aiMessages');

function openAI() {
    if (!aiPanel) return;
    aiPanel.classList.add('open');
    aiOverlay.classList.add('show');
    document.body.style.overflow = 'hidden';
}
function closeAI() {
    if (!aiPanel) return;
    aiPanel.classList.remove('open');
    aiOverlay.classList.remove('show');
    document.body.style.overflow = '';
}

if (aiToggle)  aiToggle.addEventListener('click', openAI);
if (aiClose)   aiClose.addEventListener('click', closeAI);
if (aiOverlay) aiOverlay.addEventListener('click', closeAI);

function sendAiQuestion(question) {
    openAI();
    if (!aiInput) return;
    aiInput.value = question;
    handleAiSend();
}

async function handleAiSend() {
    if (!aiInput || !aiMessages) return;
    const q = aiInput.value.trim();
    if (!q) return;

    // User bubble
    addMessage(q, 'user');
    aiInput.value = '';

    // Loading indicator
    const loadId = 'load-' + Date.now();
    aiMessages.insertAdjacentHTML('beforeend',
        `<div class="ai-msg bot msg-loading" id="${loadId}">
            <div class="msg-bubble">✨ Thinking...</div>
         </div>`);
    aiMessages.scrollTop = aiMessages.scrollHeight;

    try {
        const resp = await fetch('/api/chatbot/ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question: q })
        });
        const data = await resp.json();
        document.getElementById(loadId)?.remove();
        addMessage(data.reply || 'No response.', 'bot');
    } catch (e) {
        document.getElementById(loadId)?.remove();
        addMessage('Sorry, I could not connect. Please try again.', 'bot');
    }
}

function addMessage(text, role) {
    if (!aiMessages) return;
    const div = document.createElement('div');
    div.className = `ai-msg ${role}`;
    div.innerHTML = `<div class="msg-bubble">${escapeHtml(text).replace(/\n/g,'<br>')}</div>`;
    aiMessages.appendChild(div);
    aiMessages.scrollTop = aiMessages.scrollHeight;
}

function escapeHtml(str) {
    return str.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;');
}

if (aiSend)  aiSend.addEventListener('click', handleAiSend);
if (aiInput) aiInput.addEventListener('keydown', e => { if (e.key === 'Enter') handleAiSend(); });

// ── Auto-dismiss alerts ───────────────────────────────────────
document.querySelectorAll('.alert-success, .alert-error').forEach(el => {
    setTimeout(() => {
        el.style.opacity = '0';
        el.style.transition = 'opacity .5s';
        setTimeout(() => el.remove(), 500);
    }, 4000);
});

// ── Add CSRF token to all fetch requests ──────────────────────
const csrfToken = document.querySelector('input[name=__RequestVerificationToken]')?.value;
const origFetch = window.fetch;
window.fetch = function(url, opts = {}) {
    if (opts.method && opts.method.toUpperCase() !== 'GET' && csrfToken) {
        opts.headers = opts.headers || {};
        opts.headers['RequestVerificationToken'] = csrfToken;
    }
    return origFetch(url, opts);
};
