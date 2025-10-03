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
}

// Saved Sets

// Current Set
let currentSetTitle = '';
let currentCardIdx = 0;

// A given card looks like this:
// { front: 'Front text', back: 'Back text' }
let currentSet = [
    { front: '', back: '' }
];
let currentSetId = null;
let savedSets = [];

// Front Toolbar Logic'
let frontFontSize = 12;
let backFontSize = 12;
// store which part is currently selected
let frontSelectionStart = null;
let frontSelectionEnd = null;
// Bolding requirements
// - only toggle bold for the highlighted text
// - if no text is selected, make every new letter bold until toggled off
let isFrontBold = false;
// store which parts are bolded
let frontBoldRanges = [];
// Italics
let isFrontItalic = false;
let frontItalicRanges = [];
// Underline
let isFrontUnderline = false;
let frontUnderlineRanges = [];

// React to mouse down and up to track selection
elements.front.addEventListener('mousedown', () => {
    // reset selection
    frontSelectionStart = null;
    frontSelectionEnd = null;
    // set start of selection
    frontSelectionStart = elements.front.selectionStart;
});
elements.front.addEventListener('mouseup', () => {
    frontSelectionEnd = elements.front.selectionEnd;
});

// React to keyboard inputs to update the text of the current card
elements.front.addEventListener('input', () => {
    currentSet[currentCardIdx].front = elements.front.innerHTML;
});
elements.back.addEventListener('input', () => {
    currentSet[currentCardIdx].back = elements.back.innerHTML;
});

// Decrease Font Size
elements.frontToolbar.querySelector('[data-cmd="decreaseFontSize"]').addEventListener('click', () => {
    frontFontSize = Math.max(5, frontFontSize - 1);
    elements.front.style.fontSize = frontFontSize + 'px';
    elements.frontToolbar.querySelector('#fontSizeInput').value = frontFontSize;
});

// Increase Font Size
elements.frontToolbar.querySelector('[data-cmd="increaseFontSize"]').addEventListener('click', () => {
    frontFontSize = Math.min(100, frontFontSize + 1);
    elements.front.style.fontSize = frontFontSize + 'px';
    elements.frontToolbar.querySelector('#fontSizeInput').value = frontFontSize;
});

// function applyTextStyles(textarea, ranges, style, remove = false) {
//     let text = textarea.value;
//     let styledText = '';
//     let lastIndex = 0;
//     ranges.sort((a, b) => a[0] - b[0]); // Sort ranges by start index

//     console.log(ranges);
//     for (let range of ranges) {
//         // Add text before the range
//         styledText += text.substring(lastIndex, range[0]);
//         // Add styled text
//         if (!remove) {
//             if (style === 'bold') {
//                 styledText += '<b>' + text.substring(range[0], range[1]) + '</b>';
//             }
//         } else {
//             styledText += text.substring(range[0], range[1]);
//         }
//     }
//     // Add remaining text
//     styledText += text.substring(lastIndex);
//     console.log(styledText);
//     textarea.innerHTML = styledText;
// }

// Toggle Bold
elements.frontToolbar.querySelector('[data-cmd="bold"]').addEventListener('click', () => {
    
    elements.front.focus();
    document.execCommand('bold');

    // isFrontBold = !isFrontBold;
    // if (frontSelectionStart !== null && frontSelectionEnd !== null && frontSelectionStart !== frontSelectionEnd) {
    //     // If text is selected, toggle bold for the selected range
    //     if (isFrontBold) {
    //         frontBoldRanges.push([frontSelectionStart, frontSelectionEnd]);
    //         applyTextStyles(elements.front, frontBoldRanges, 'bold');
    //     } else {
    //         frontBoldRanges = frontBoldRanges.filter(range => !(range[0] === frontSelectionStart && range[1] === frontSelectionEnd));
    //         // unbold the selected range
    //         applyTextStyles(elements.front, frontBoldRanges, 'bold', true);
    //     }
    // }
});


// Toggle Italic
elements.frontToolbar.querySelector('[data-cmd="italic"]').addEventListener('click', () => {
    elements.front.focus();
    document.execCommand('italic');
});

// Toggle Underline
elements.frontToolbar.querySelector('[data-cmd="underline"]').addEventListener('click', () => {
    elements.front.focus();
    document.execCommand('underline');
});


elements.backToolbar.querySelector('[data-cmd="decreaseFontSize"]').addEventListener('click', () => {
    backFontSize = Math.max(5, backFontSize - 1);
    elements.back.style.fontSize = backFontSize + 'px';
    elements.backToolbar.querySelector('#fontSizeInput').value = backFontSize;
});

elements.backToolbar.querySelector('[data-cmd="increaseFontSize"]').addEventListener('click', () => {
    backFontSize = Math.min(100, backFontSize + 1);
    elements.back.style.fontSize = backFontSize + 'px';
    elements.backToolbar.querySelector('#fontSizeInput').value = backFontSize;
});

elements.backToolbar.querySelector('[data-cmd="bold"]').addEventListener('click', () => {
    elements.back.focus();
    document.execCommand('bold');
});

elements.backToolbar.querySelector('[data-cmd="italic"]').addEventListener('click', () => {
    elements.back.focus();
    document.execCommand('italic');
});

elements.backToolbar.querySelector('[data-cmd="underline"]').addEventListener('click', () => {
    elements.back.focus();
    document.execCommand('underline');
});

function updateCardDisplay() {
    elements.cardIndex.textContent = currentCardIdx + 1;
    elements.cardTotal.textContent = currentSet.length;
    elements.front.innerHTML = currentSet[currentCardIdx].front;
    elements.back.innerHTML = currentSet[currentCardIdx].back;
}

// add new card button
elements.addCardBtn.addEventListener('click', () => {
    currentSet.push({ front: '', back: '' });
    currentCardIdx = currentSet.length - 1;
    updateCardDisplay();
});

// delete current card button
elements.deleteCardBtn.addEventListener('click', () => {
    if (currentSet.length > 1) {
        currentSet.splice(currentCardIdx, 1);
        // if possible, go to next card, else go to previous card
        if (currentCardIdx >= currentSet.length) {
            currentCardIdx = currentSet.length - 1;
        }
        updateCardDisplay();
    }
});

// previous card button
elements.prevCardBtn.addEventListener('click', () => {
    console.log(currentCardIdx);
    if (currentCardIdx > 0) {
        currentCardIdx--;
    }
    updateCardDisplay();
});

// next card button
elements.nextCardBtn.addEventListener('click', () => {
    console.log(currentCardIdx);
    if (currentCardIdx < currentSet.length - 1) {
        currentCardIdx++;
    }
    updateCardDisplay();
});


// save set button
elements.saveSetBtn.addEventListener('click', () => {
    let title = elements.setTitle.value.trim();
    if (!title) {
        title = 'Untitled Set';
    }
    
});


// API functions
async function loadSets() {
    try {
        const response = await fetch('/api/sets');
        const data = await response.json();
        return data.sets;
    } catch (error) {
        console.error('Error loading sets:', error);
        return [];
    }
}

async function saveSet(name, cards) {
    try {
        const response = await fetch('/api/sets', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, cards })
        });
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('Error saving set:', error);
        return { ok: false, error: error.message };
    }
}

async function updateSet(id, name, cards) {
    try {
        const response = await fetch(`/api/sets/${id}`, {
            method: 'PUT',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ name, cards })
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
            headers: { 'Content-Type': 'application/json' }
        });
        const data = await response.json();
        return data;
    } catch (error) {
        console.error('Error deleting set:', error);
        return { ok: false, error: error.message };
    }
}


// Initial load
async function initializePage() {
    savedSets = await loadSets();
    updateSetDisplay();
    updateCardDisplay();
}

function updateSetDisplay() {
    const setsContainer = elements.sets;
    setsContainer.innerHTML = '';
    savedSets.forEach(set => {
        const button = document.createElement('button');
        button.classList.add('set');
        button.textContent = set.name;
        button.dataset.id = set.id;
        button.addEventListener('click', () => loadSet(set.id));

        if (set.id === currentSetId) {
            button.classList.add('active');
        }

        setsContainer.appendChild(button);
    });
}

function loadSet(setId) {
    const selectedSet = savedSets.find(s => s.id === setId);
    if (selectedSet) {
        currentSetId = selectedSet.id;
        currentSetTitle = selectedSet.name;
        currentSet = selectedSet.cards.length > 0 ? selectedSet.cards : [{ front: '', back: '' }];
        currentCardIdx = 0;
        elements.setTitle.value = currentSetTitle;
        updateSetDisplay();
        updateCardDisplay();
    }
}

elements.saveSetBtn.addEventListener('click', async () => {
    let title = elements.setTitle.value.trim();
    if (!title) {
        title = 'Untitled Set';
    }
    let result;
    if (currentSetId) {
        result = await updateSet(currentSetId, title, currentSet);
    } else {
        alert('Please select a set to save changes.');
    }

    if (result.ok) {
        currentSetTitle = title;
        savedSets = await loadSets();
        updateSetDisplay();
        alert('Set saved successfully!');
    } else {
        alert('Error saving set: ' + result.error);
    }
});


elements.deleteSetBtn.addEventListener('click', async () => {
    if (currentSetId) {
        const confirmDelete = confirm('Are you sure you want to delete this set? This action cannot be undone.');
        if (confirmDelete) {
            const result = await deleteSet(currentSetId);
            if (result.ok) {
                currentSetId = null;
                currentSetTitle = '';
                currentSet = [{ front: '', back: '' }];
                currentCardIdx = 0;
                elements.setTitle.value = '';
                savedSets = await loadSets();
                updateSetDisplay();
                updateCardDisplay();
                alert('Set deleted successfully!');
            } else {
                alert('Error deleting set: ' + result.error);
            }
        }
    } else {
        alert('No set selected to delete.');
    }
});

elements.newSetBtn.addEventListener('click', () => {
    if (currentSetId && confirm("Create a new set? Any unsaved changes will be lost.")) {
        currentSetId = null;
        currentSetTitle = '';
        currentSet = [{ front: '', back: '' }];
        currentCardIdx = 0;
        elements.setTitle.value = '';
        updateSetDisplay();
        updateCardDisplay();
    }

    // Create a new set that can be shown on the list
    const newSet = { id: Date.now(), name: currentSetTitle, cards: currentSet };
    savedSets.push(newSet);
    updateSetDisplay();
});

document.addEventListener('DOMContentLoaded', initializePage);