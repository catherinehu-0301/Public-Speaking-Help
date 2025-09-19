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
    saveCardBtn: document.getElementById('saveCardBtn'),
    deleteSetBtn: document.getElementById('deleteSetBtn'),
    newSetBtn: document.getElementById('newSetBtn'),
    frontToolbar: document.getElementById('frontToolbar'),
    backToolbar: document.getElementById('backToolbar'),
}

// Front Toolbar Logic'
let frontFontSize = 12;

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