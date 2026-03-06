const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const { randomUUID } = require('crypto');

const app = express();

app.use(bodyParser.json({ limit: '2mb' }));
app.use(cors());
app.use(express.static(path.join(__dirname, 'public')));

const DB_FILE = process.env.DB_FILE || path.join(__dirname, 'data', 'db.json');

function loadDB() {
    try {
        return JSON.parse(fs.readFileSync(DB_FILE, 'utf8'));
    } catch {
        return { sets: [], lastUpdated: 0 };
    }
}

function saveDB(data) {
    fs.writeFileSync(DB_FILE, JSON.stringify(data, null, 2));
}

function normalizeName(name) {
    const value = String(name || '').trim();
    return value || 'Untitled Set';
}

function normalizeCards(cards) {
    if (!Array.isArray(cards)) {
        return [];
    }

    return cards.map((card) => ({
        front: String(card?.front || ''),
        back: String(card?.back || ''),
    }));
}

let db = loadDB();

const now = () => Date.now();

function persistDB() {
    db.lastUpdated = now();
    saveDB(db);
}

function findSetById(id) {
    return db.sets.find((set) => set.id === id);
}

app.get('/api/sets', (req, res) => {
    res.json({ lastUpdated: db.lastUpdated, sets: db.sets });
});

app.post('/api/sets', (req, res) => {
    const set = {
        id: randomUUID(),
        name: normalizeName(req.body?.name),
        cards: normalizeCards(req.body?.cards),
        lastUpdated: now(),
    };

    db.sets.push(set);
    persistDB();
    res.json({ ok: true, set });
});

app.put('/api/sets/:id', (req, res) => {
    const set = findSetById(req.params.id);

    if (!set) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    set.name = normalizeName(req.body?.name ?? set.name);

    if (Array.isArray(req.body?.cards)) {
        set.cards = normalizeCards(req.body.cards);
    }

    set.lastUpdated = now();
    persistDB();
    return res.json({ ok: true, set });
});

app.delete('/api/sets/:id', (req, res) => {
    const before = db.sets.length;
    db.sets = db.sets.filter((set) => set.id !== req.params.id);

    if (db.sets.length === before) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    persistDB();
    return res.json({ ok: true });
});

if (require.main === module) {
    const PORT = process.env.PORT || 3000;
    app.listen(PORT, () => {
        console.log(`Server is running on port ${PORT}`);
    });
}

module.exports = { app };
