const DEFAULT_FONT_SIZE = 12;

const elements = {
    sets: document.getElementById('sets'),
    setTitle: document.getElementById('setTitle'),
    cardIndex: document.getElementById('cardIndex'),
    cardTotal: document.getElementById('cardTotal'),
    front: document.getElementById('front'),
    back: document.getElementById('back'),
    addCardBtn: document.getElementById('addCardBtn'),
    deleteCardBtn: document.getElementById('deleteCardBtn'),
    prevCardBtn: document.getElementById('prevCardBtn'),
    nextCardBtn: document.getElementById('nextCardBtn'),
    saveSetBtn: document.getElementById('saveSetBtn'),
    sendToVrBtn: document.getElementById('sendToVrBtn'),
    deleteSetBtn: document.getElementById('deleteSetBtn'),
    newSetBtn: document.getElementById('newSetBtn'),
    frontToolbar: document.getElementById('frontToolbar'),
    backToolbar: document.getElementById('backToolbar'),
    dirtyIndicator: document.getElementById('dirtyIndicator'),
    saveStatus: document.getElementById('saveStatus'),
    vrStatus: document.getElementById('vrStatus'),
};

const editorConfigs = {
    front: {
        field: 'front',
        editor: elements.front,
        toolbar: elements.frontToolbar,
        fontSize: DEFAULT_FONT_SIZE,
        selection: null,
    },
    back: {
        field: 'back',
        editor: elements.back,
        toolbar: elements.backToolbar,
        fontSize: DEFAULT_FONT_SIZE,
        selection: null,
    },
};

let currentSetTitle = '';
let currentCardIdx = 0;
let currentSet = [createEmptyCard()];
let currentSetId = null;
let savedSets = [];
let isDirty = false;

function createEmptyCard() {
    return {
        front: '',
        back: '',
        frontFontSize: DEFAULT_FONT_SIZE,
        backFontSize: DEFAULT_FONT_SIZE,
    };
}

function fontSizeFieldName(field) {
    return field === 'front' ? 'frontFontSize' : 'backFontSize';
}

function normalizeCard(card = {}) {
    return {
        front: typeof card.front === 'string' ? card.front : '',
        back: typeof card.back === 'string' ? card.back : '',
        frontFontSize: clampFontSize(card.frontFontSize),
        backFontSize: clampFontSize(card.backFontSize),
    };
}

function clampFontSize(value) {
    const parsed = Number.parseInt(value, 10);
    if (Number.isNaN(parsed)) {
        return DEFAULT_FONT_SIZE;
    }

    return Math.min(100, Math.max(5, parsed));
}

function normalizeCards(cards) {
    if (!Array.isArray(cards) || cards.length === 0) {
        return [createEmptyCard()];
    }

    return cards.map(normalizeCard);
}

function cloneCards(cards) {
    return normalizeCards(cards).map((card) => ({ ...card }));
}

function ensureCurrentCard() {
    if (!Array.isArray(currentSet) || currentSet.length === 0) {
        currentSet = [createEmptyCard()];
    }

    if (currentCardIdx < 0) {
        currentCardIdx = 0;
    }

    if (currentCardIdx >= currentSet.length) {
        currentCardIdx = currentSet.length - 1;
    }

    if (!currentSet[currentCardIdx]) {
        currentSet[currentCardIdx] = createEmptyCard();
    }
}

function setStatus(element, message, state = 'neutral') {
    element.textContent = message;
    element.dataset.state = state;
}

function markDirty() {
    isDirty = true;
    elements.dirtyIndicator.textContent = 'Unsaved changes';
    elements.dirtyIndicator.dataset.state = 'dirty';
    updateActionButtons();
}

function markClean() {
    isDirty = false;
    elements.dirtyIndicator.textContent = 'All changes saved';
    elements.dirtyIndicator.dataset.state = 'clean';
    updateActionButtons();
}

function setActiveEditorFontSize(field, nextFontSize, options = {}) {
    const config = editorConfigs[field];
    const clamped = clampFontSize(nextFontSize);

    config.fontSize = clamped;
    config.editor.style.fontSize = `${clamped}px`;
    config.toolbar.querySelector('.font-size-input').value = clamped;

    if (options.persist !== false) {
        ensureCurrentCard();
        currentSet[currentCardIdx][fontSizeFieldName(field)] = clamped;
    }

    if (options.markDirty) {
        markDirty();
    }
}

function syncField(field) {
    ensureCurrentCard();
    currentSet[currentCardIdx][field] = elements[field].innerHTML;
    currentSet[currentCardIdx][fontSizeFieldName(field)] = editorConfigs[field].fontSize;
}

function syncCurrentCard() {
    syncField('front');
    syncField('back');
}

function stripHtml(html) {
    const temp = document.createElement('div');
    temp.innerHTML = html;
    return (temp.textContent || temp.innerText || '').trim();
}

function currentDraftHasContent() {
    if (elements.setTitle.value.trim()) {
        return true;
    }

    return currentSet.some((card) => stripHtml(card.front) || stripHtml(card.back));
}

function getCurrentSavedSet() {
    return savedSets.find((set) => set.id === currentSetId) || null;
}

function formatTimestamp(timestamp) {
    if (!timestamp) {
        return '';
    }

    try {
        return new Date(timestamp).toLocaleString();
    } catch {
        return '';
    }
}

function refreshVrStatus() {
    const selectedSet = getCurrentSavedSet();

    if (!selectedSet) {
        setStatus(elements.vrStatus, 'Save a set before sending it to the VR app.', 'neutral');
        return;
    }

    if (selectedSet.sentToVr) {
        const formatted = formatTimestamp(selectedSet.lastSentToVr);
        setStatus(
            elements.vrStatus,
            formatted
                ? `Published to VR. Last sent ${formatted}.`
                : 'Published to VR.',
            'success'
        );
        return;
    }

    setStatus(elements.vrStatus, 'This set has not been sent to the VR app yet.', 'info');
}

function updateActionButtons() {
    elements.deleteSetBtn.disabled = !currentSetId;
    elements.sendToVrBtn.disabled = !currentSetId && !currentDraftHasContent();
}

function getSelectionRangeWithin(editor) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) {
        return null;
    }

    const range = selection.getRangeAt(0);
    if (!editor.contains(range.commonAncestorContainer)) {
        return null;
    }

    return range.cloneRange();
}

function saveSelection(field) {
    const config = editorConfigs[field];
    const range = getSelectionRangeWithin(config.editor);
    if (range) {
        config.selection = range;
    }
}

function restoreSelection(field) {
    const config = editorConfigs[field];
    config.editor.focus();

    if (!config.selection) {
        return false;
    }

    const selection = window.getSelection();
    if (!selection) {
        return false;
    }

    try {
        selection.removeAllRanges();
        selection.addRange(config.selection);
        return true;
    } catch {
        config.selection = null;
        return false;
    }
}

function execEditorCommand(field, command, value = null) {
    restoreSelection(field);

    if (command === 'foreColor') {
        document.execCommand('styleWithCSS', false, true);
    }

    document.execCommand(command, false, value);
    syncField(field);
    saveSelection(field);
    markDirty();
}

function normalizeUrl(url) {
    if (!url) {
        return '';
    }

    if (/^(https?:\/\/|mailto:|tel:)/i.test(url)) {
        return url;
    }

    return `https://${url}`;
}

function escapeHtml(text) {
    return text
        .replaceAll('&', '&amp;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;');
}

function handleCreateLink(field) {
    const urlInput = window.prompt('Enter the link URL.', 'https://');
    if (!urlInput) {
        return;
    }

    const url = normalizeUrl(urlInput.trim());
    if (!url) {
        return;
    }

    restoreSelection(field);
    const selection = window.getSelection();
    const selectedText = selection ? selection.toString().trim() : '';

    if (selectedText) {
        execEditorCommand(field, 'createLink', url);
        return;
    }

    const linkText = window.prompt('Enter the link text.', url);
    if (linkText === null) {
        return;
    }

    const html = `<a href="${escapeHtml(url)}" target="_blank" rel="noopener noreferrer">${escapeHtml(linkText || url)}</a>`;
    execEditorCommand(field, 'insertHTML', html);
}

function handleTextColor(field, button) {
    const currentColor = button.dataset.value || '#111111';
    const chosenColor = window.prompt(
        'Enter a text color value (for example #1f1f1f or red).',
        currentColor
    );

    if (!chosenColor) {
        return;
    }

    const nextColor = chosenColor.trim();
    button.dataset.value = nextColor;
    button.style.color = nextColor;
    execEditorCommand(field, 'foreColor', nextColor);
}

function handleToolbarAction(field, button) {
    const command = button.dataset.cmd;

    switch (command) {
        case 'decreaseFontSize':
            setActiveEditorFontSize(field, editorConfigs[field].fontSize - 1, { markDirty: true });
            return;
        case 'increaseFontSize':
            setActiveEditorFontSize(field, editorConfigs[field].fontSize + 1, { markDirty: true });
            return;
        case 'createLink':
            handleCreateLink(field);
            return;
        case 'foreColor':
            handleTextColor(field, button);
            return;
        default:
            execEditorCommand(field, command);
    }
}

function setupEditor(field) {
    const config = editorConfigs[field];
    const toolbarButtons = config.toolbar.querySelectorAll('button.tool');
    const fontSizeInput = config.toolbar.querySelector('.font-size-input');

    setActiveEditorFontSize(field, DEFAULT_FONT_SIZE, { persist: false, markDirty: false });

    toolbarButtons.forEach((button) => {
        button.type = 'button';

        if (button.dataset.cmd === 'foreColor') {
            button.style.color = button.dataset.value || '#111111';
        }

        button.addEventListener('mousedown', (event) => {
            event.preventDefault();
        });

        button.addEventListener('click', () => {
            handleToolbarAction(field, button);
        });
    });

    fontSizeInput.addEventListener('input', (event) => {
        setActiveEditorFontSize(field, event.target.value, { markDirty: true });
    });

    ['mouseup', 'keyup', 'focus'].forEach((eventName) => {
        config.editor.addEventListener(eventName, () => {
            saveSelection(field);
        });
    });

    config.editor.addEventListener('input', () => {
        syncField(field);
        saveSelection(field);
        markDirty();
    });
}

function updateCardDisplay() {
    ensureCurrentCard();
    elements.cardIndex.textContent = currentCardIdx + 1;
    elements.cardTotal.textContent = currentSet.length;
    elements.setTitle.value = currentSetTitle;
    elements.front.innerHTML = currentSet[currentCardIdx].front;
    elements.back.innerHTML = currentSet[currentCardIdx].back;
    setActiveEditorFontSize('front', currentSet[currentCardIdx].frontFontSize, { persist: false, markDirty: false });
    setActiveEditorFontSize('back', currentSet[currentCardIdx].backFontSize, { persist: false, markDirty: false });
    editorConfigs.front.selection = null;
    editorConfigs.back.selection = null;
    updateActionButtons();
}

function resetEditorState() {
    currentSetId = null;
    currentSetTitle = '';
    currentSet = [createEmptyCard()];
    currentCardIdx = 0;
    markClean();
    updateSetDisplay();
    updateCardDisplay();
    setStatus(elements.saveStatus, 'Started a new draft.', 'info');
    refreshVrStatus();
}

function createSetButton(set) {
    const button = document.createElement('button');
    const name = document.createElement('span');

    button.type = 'button';
    button.classList.add('set-button');
    name.classList.add('set-name');
    name.textContent = set.name;
    button.appendChild(name);

    if (set.sentToVr) {
        button.classList.add('is-published');
        const badge = document.createElement('span');
        badge.classList.add('set-badge');
        badge.textContent = 'VR';
        button.appendChild(badge);
    }

    if (set.id === currentSetId) {
        button.classList.add('active');
    }

    button.addEventListener('click', () => {
        loadSet(set.id);
    });

    return button;
}

function updateSetDisplay() {
    elements.sets.innerHTML = '';

    if (savedSets.length === 0) {
        const emptyState = document.createElement('div');
        emptyState.classList.add('empty-state');
        emptyState.textContent = 'No saved sets yet. Create one, save it, then publish it to VR.';
        elements.sets.appendChild(emptyState);
        return;
    }

    savedSets.forEach((set) => {
        elements.sets.appendChild(createSetButton(set));
    });
}

function loadSet(setId) {
    const selectedSet = savedSets.find((set) => set.id === setId);
    if (!selectedSet) {
        return;
    }

    currentSetId = selectedSet.id;
    currentSetTitle = selectedSet.name;
    currentSet = cloneCards(selectedSet.cards);
    currentCardIdx = 0;
    markClean();
    updateSetDisplay();
    updateCardDisplay();
    setStatus(elements.saveStatus, `Loaded "${selectedSet.name}".`, 'info');
    refreshVrStatus();
}

async function loadSets() {
    try {
        const response = await fetch('/api/sets');
        const data = await response.json();
        return Array.isArray(data.sets) ? data.sets : [];
    } catch (error) {
        console.error('Error loading sets:', error);
        setStatus(elements.saveStatus, 'Could not load saved sets from the server.', 'error');
        return [];
    }
}

async function createSet(name, cards) {
    try {
        const response = await fetch('/api/sets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, cards }),
        });

        return await response.json();
    } catch (error) {
        console.error('Error creating set:', error);
        return { ok: false, error: error.message };
    }
}

async function updateSet(id, name, cards) {
    try {
        const response = await fetch(`/api/sets/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, cards }),
        });

        return await response.json();
    } catch (error) {
        console.error('Error updating set:', error);
        return { ok: false, error: error.message };
    }
}

async function deleteSet(id) {
    try {
        const response = await fetch(`/api/sets/${id}`, {
            method: 'DELETE',
            headers: { 'Content-Type': 'application/json' },
        });

        return await response.json();
    } catch (error) {
        console.error('Error deleting set:', error);
        return { ok: false, error: error.message };
    }
}

async function publishSetToVr(id) {
    try {
        const response = await fetch(`/api/sets/${id}/send-to-vr`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
        });

        return await response.json();
    } catch (error) {
        console.error('Error publishing set to VR:', error);
        return { ok: false, error: error.message };
    }
}

async function refreshSets() {
    savedSets = await loadSets();
    updateSetDisplay();
}

async function persistCurrentSet(options = {}) {
    syncCurrentCard();

    const title = elements.setTitle.value.trim() || 'Untitled Set';
    const result = currentSetId
        ? await updateSet(currentSetId, title, currentSet)
        : await createSet(title, currentSet);

    if (!result || !result.ok) {
        setStatus(
            elements.saveStatus,
            `Save failed: ${result?.error || 'Unknown error.'}`,
            'error'
        );
        return null;
    }

    await refreshSets();
    loadSet(result.set.id);
    markClean();

    if (!options.silent) {
        setStatus(elements.saveStatus, 'Changes saved.', 'success');
    }

    return result.set;
}

async function initializePage() {
    setupEditor('front');
    setupEditor('back');

    savedSets = await loadSets();
    updateSetDisplay();

    if (savedSets.length > 0) {
        loadSet(savedSets[0].id);
    } else {
        resetEditorState();
    }

    setStatus(elements.saveStatus, 'Dashboard ready.', 'info');
}

elements.setTitle.addEventListener('input', () => {
    currentSetTitle = elements.setTitle.value;
    markDirty();
});

elements.addCardBtn.addEventListener('click', () => {
    syncCurrentCard();
    currentSet.push(createEmptyCard());
    currentCardIdx = currentSet.length - 1;
    updateCardDisplay();
    markDirty();
});

elements.deleteCardBtn.addEventListener('click', () => {
    syncCurrentCard();

    if (currentSet.length <= 1) {
        currentSet = [createEmptyCard()];
        currentCardIdx = 0;
        updateCardDisplay();
        markDirty();
        return;
    }

    currentSet.splice(currentCardIdx, 1);

    if (currentCardIdx >= currentSet.length) {
        currentCardIdx = currentSet.length - 1;
    }

    updateCardDisplay();
    markDirty();
});

elements.prevCardBtn.addEventListener('click', () => {
    syncCurrentCard();

    if (currentCardIdx > 0) {
        currentCardIdx -= 1;
    }

    updateCardDisplay();
});

elements.nextCardBtn.addEventListener('click', () => {
    syncCurrentCard();

    if (currentCardIdx < currentSet.length - 1) {
        currentCardIdx += 1;
    }

    updateCardDisplay();
});

elements.newSetBtn.addEventListener('click', () => {
    syncCurrentCard();

    if (isDirty && currentDraftHasContent()) {
        const confirmed = window.confirm(
            'Start a new set? Any unsaved changes in the current editor will be lost.'
        );

        if (!confirmed) {
            return;
        }
    }

    resetEditorState();
});

elements.saveSetBtn.addEventListener('click', async () => {
    await persistCurrentSet();
});

elements.sendToVrBtn.addEventListener('click', async () => {
    let setToPublish = getCurrentSavedSet();

    if (!setToPublish || isDirty) {
        setStatus(elements.saveStatus, 'Saving changes before publishing to VR...', 'info');
        setToPublish = await persistCurrentSet({ silent: true });
    }

    if (!setToPublish) {
        return;
    }

    const result = await publishSetToVr(setToPublish.id);
    if (!result || !result.ok) {
        setStatus(
            elements.vrStatus,
            `Could not send the set to VR: ${result?.error || 'Unknown error.'}`,
            'error'
        );
        return;
    }

    await refreshSets();
    loadSet(result.set.id);
    setStatus(
        elements.vrStatus,
        `Sent "${result.set.name}" to the VR app.`,
        'success'
    );
});

elements.deleteSetBtn.addEventListener('click', async () => {
    if (!currentSetId) {
        setStatus(elements.saveStatus, 'Select a saved set before deleting.', 'error');
        return;
    }

    const confirmed = window.confirm(
        'Delete this set? This action cannot be undone.'
    );

    if (!confirmed) {
        return;
    }

    const result = await deleteSet(currentSetId);
    if (!result.ok) {
        setStatus(
            elements.saveStatus,
            `Delete failed: ${result.error}`,
            'error'
        );
        return;
    }

    await refreshSets();

    if (savedSets.length > 0) {
        loadSet(savedSets[0].id);
    } else {
        resetEditorState();
    }

    setStatus(elements.saveStatus, 'Set deleted.', 'success');
});

document.addEventListener('DOMContentLoaded', initializePage);
