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