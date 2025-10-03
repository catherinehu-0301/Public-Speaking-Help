const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const path = require('path');
const fs = require('fs');
const { randomUUID } = require('crypto');
const { ok } = require('assert');

const app = express();
app.use(bodyParser.json({ limit: '2mb' }));
app.use(cors());
app.use(express.static(path.join(__dirname, 'public')));

const DB_FILE = path.join(__dirname, 'data', 'db.json');

// TODO: database functions
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

let db = loadDB();
let globalETag = String(db.lastUpdated || Date.now());
const now = () => Date.now();
const update = () => {
    db.lastUpdated = now();
    globalETag = String(db.lastUpdated);
    saveDB(db);
}
const findSetById = (id) => db.sets.find(s => s.id === id);

// API Routes
app.get('/api/sets', (req, res) => { // Get all sets
    // res.set('ETag', globalETag);
    res.json({ lastUpdated: db.lastUpdated,sets: db.sets});
});

app.post('/api/sets', (req, res) =>{ // Create a new set
    const name = String(req.body?.name || 'Untitled Set').trim();
    const id = randomUUID();
    const set = { id, name, cards: [], lastUpdated: now() };
    db.sets.push(set);
    update();
    res.json({ok: true, set});
});

app.put('/api/sets/:id', (req, res) => { // Update a set
    const set = findSetById(req.params.id);
    if (!set) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    const name = req.body.name;
    set.lastUpdated = now();
    update();
    res.json({ ok: true, set });
});

app.delete('/api/sets/:id', (req, res) => {
    const before = db.sets.length;
    db.sets = db.sets.filter(s => s.id !== req.params.id);
    if (db.sets.length < before) {
        update();
        res.json({ ok: true });
    } else {
        res.status(404).json({ ok: false, error: 'Set not found' });
    }
});

// TODO: Card formatters

const PORT = process.env.PORT || 3000;
app.listen(PORT, () => {
    console.log(`Server is running on port ${PORT}`);
});