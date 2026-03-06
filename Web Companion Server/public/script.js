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
    deleteSetBtn: document.getElementById('deleteSetBtn'),
    newSetBtn: document.getElementById('newSetBtn'),
    frontToolbar: document.getElementById('frontToolbar'),
    backToolbar: document.getElementById('backToolbar'),
};

const DEFAULT_FONT_SIZE = 12;

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

function createEmptyCard() {
    return { front: '', back: '' };
}

function normalizeCard(card = {}) {
    return {
        front: typeof card.front === 'string' ? card.front : '',
        back: typeof card.back === 'string' ? card.back : '',
    };
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

function syncField(field) {
    ensureCurrentCard();
    currentSet[currentCardIdx][field] = elements[field].innerHTML;
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

function setActiveEditorFontSize(field, nextFontSize) {
    const config = editorConfigs[field];
    const parsed = Number.parseInt(nextFontSize, 10);

    if (Number.isNaN(parsed)) {
        return;
    }

    const clamped = Math.min(100, Math.max(5, parsed));
    config.fontSize = clamped;
    config.editor.style.fontSize = `${clamped}px`;
    config.toolbar.querySelector('.font-size-input').value = clamped;
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
            setActiveEditorFontSize(field, editorConfigs[field].fontSize - 1);
            return;
        case 'increaseFontSize':
            setActiveEditorFontSize(field, editorConfigs[field].fontSize + 1);
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

    setActiveEditorFontSize(field, DEFAULT_FONT_SIZE);

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
        setActiveEditorFontSize(field, event.target.value);
    });

    ['mouseup', 'keyup', 'focus'].forEach((eventName) => {
        config.editor.addEventListener(eventName, () => {
            saveSelection(field);
        });
    });

    config.editor.addEventListener('input', () => {
        syncField(field);
        saveSelection(field);
    });
}

function updateCardDisplay() {
    ensureCurrentCard();
    elements.cardIndex.textContent = currentCardIdx + 1;
    elements.cardTotal.textContent = currentSet.length;
    elements.setTitle.value = currentSetTitle;
    elements.front.innerHTML = currentSet[currentCardIdx].front;
    elements.back.innerHTML = currentSet[currentCardIdx].back;
    editorConfigs.front.selection = null;
    editorConfigs.back.selection = null;
}

function resetEditorState() {
    currentSetId = null;
    currentSetTitle = '';
    currentSet = [createEmptyCard()];
    currentCardIdx = 0;
    updateSetDisplay();
    updateCardDisplay();
}

function updateSetDisplay() {
    elements.sets.innerHTML = '';

    savedSets.forEach((set) => {
        const button = document.createElement('button');
        button.type = 'button';
        button.classList.add('set-button');
        button.textContent = set.name;
        button.dataset.id = set.id;

        if (set.id === currentSetId) {
            button.classList.add('active');
        }

        button.addEventListener('click', () => {
            loadSet(set.id);
        });

        elements.sets.appendChild(button);
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
    updateSetDisplay();
    updateCardDisplay();
}

async function loadSets() {
    try {
        const response = await fetch('/api/sets');
        const data = await response.json();
        return Array.isArray(data.sets) ? data.sets : [];
    } catch (error) {
        console.error('Error loading sets:', error);
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

        const data = await response.json();
        return data;
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

        const data = await response.json();
        return data;
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

        const data = await response.json();
        return data;
    } catch (error) {
        console.error('Error deleting set:', error);
        return { ok: false, error: error.message };
    }
}

async function refreshSets() {
    savedSets = await loadSets();
    updateSetDisplay();
}

async function initializePage() {
    setupEditor('front');
    setupEditor('back');

    savedSets = await loadSets();

    if (savedSets.length > 0) {
        loadSet(savedSets[0].id);
    } else {
        resetEditorState();
    }
}

elements.setTitle.addEventListener('input', () => {
    currentSetTitle = elements.setTitle.value;
});

elements.addCardBtn.addEventListener('click', () => {
    syncCurrentCard();
    currentSet.push(createEmptyCard());
    currentCardIdx = currentSet.length - 1;
    updateCardDisplay();
});

elements.deleteCardBtn.addEventListener('click', () => {
    syncCurrentCard();

    if (currentSet.length <= 1) {
        currentSet = [createEmptyCard()];
        currentCardIdx = 0;
        updateCardDisplay();
        return;
    }

    currentSet.splice(currentCardIdx, 1);

    if (currentCardIdx >= currentSet.length) {
        currentCardIdx = currentSet.length - 1;
    }

    updateCardDisplay();
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

    if (currentDraftHasContent()) {
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
    syncCurrentCard();

    const title = elements.setTitle.value.trim() || 'Untitled Set';
    let result;

    if (currentSetId) {
        result = await updateSet(currentSetId, title, currentSet);
    } else {
        result = await createSet(title, currentSet);
    }

    if (!result || !result.ok) {
        window.alert(`Error saving set: ${result?.error || 'Unknown error'}`);
        return;
    }

    await refreshSets();
    loadSet(result.set.id);
    window.alert('Set saved successfully.');
});

elements.deleteSetBtn.addEventListener('click', async () => {
    if (!currentSetId) {
        window.alert('No saved set is currently selected.');
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
        window.alert(`Error deleting set: ${result.error}`);
        return;
    }

    await refreshSets();

    if (savedSets.length > 0) {
        loadSet(savedSets[0].id);
    } else {
        resetEditorState();
    }

    window.alert('Set deleted successfully.');
});

document.addEventListener('DOMContentLoaded', initializePage);
