const express = require('express');
const bodyParser = require('body-parser');
const cors = require('cors');
const dgram = require('dgram');
const path = require('path');
const fs = require('fs');
const { randomUUID } = require('crypto');

const app = express();

app.use(bodyParser.json({ limit: '2mb' }));
app.use(cors());
app.use(express.static(path.join(__dirname, 'public')));

const DB_FILE = process.env.DB_FILE || path.join(__dirname, 'data', 'db.json');
const DEFAULT_FONT_SIZE = 12;
const LIMITS = {
    maxSetNameLength: 120,
    maxCardsPerSet: 100,
    maxCardHtmlLength: 12000,
};
const DISCOVERY_PORT = Number.parseInt(process.env.DISCOVERY_PORT || '41234', 10);
const DISCOVERY_REQUEST_MESSAGE = 'stage-notes-discovery';
const DISCOVERY_RESPONSE_PREFIX = 'stage-notes-discovery-response:';

function createEmptyDB() {
    return {
        sets: [],
        lastUpdated: 0,
        vr: {
            activeSetId: null,
        },
    };
}

function now() {
    return Date.now();
}

function escapeAttribute(value) {
    return String(value)
        .replaceAll('&', '&amp;')
        .replaceAll('"', '&quot;')
        .replaceAll("'", '&#39;')
        .replaceAll('<', '&lt;')
        .replaceAll('>', '&gt;');
}

function normalizeName(name) {
    const value = String(name || '').trim();
    return value.slice(0, LIMITS.maxSetNameLength) || 'Untitled Set';
}

function clampFontSize(value) {
    const parsed = Number.parseInt(value, 10);
    if (Number.isNaN(parsed)) {
        return DEFAULT_FONT_SIZE;
    }

    return Math.min(100, Math.max(5, parsed));
}

function filterAllowedStyles(styleValue) {
    return String(styleValue || '')
        .split(';')
        .map((rule) => rule.trim())
        .filter(Boolean)
        .map((rule) => {
            const [rawName, ...rawValueParts] = rule.split(':');
            if (!rawName || rawValueParts.length === 0) {
                return null;
            }

            const name = rawName.trim().toLowerCase();
            const value = rawValueParts.join(':').trim();

            if (name === 'color' || name === 'text-align') {
                return `${name}: ${value}`;
            }

            return null;
        })
        .filter(Boolean)
        .join('; ');
}

function sanitizeStoredHtml(rawHtml) {
    let html = String(rawHtml || '');

    html = html.replace(/<!--[\s\S]*?-->/g, '');
    html = html.replace(/<(script|style|iframe|object|embed|meta|link)\b[^>]*>[\s\S]*?<\/\1>/gi, '');
    html = html.replace(/<(script|style|iframe|object|embed|meta|link)\b[^>]*\/?>/gi, '');
    html = html.replace(/\son\w+\s*=\s*(".*?"|'.*?'|[^\s>]+)/gi, '');
    html = html.replace(/\shref\s*=\s*("([^"]*)"|'([^']*)'|([^\s>]+))/gi, (match, quoted, doubleQuoted, singleQuoted, bareValue) => {
        const href = (doubleQuoted ?? singleQuoted ?? bareValue ?? '').trim();

        if (/^(javascript:|data:)/i.test(href)) {
            return ' href="#"';
        }

        return ` href="${escapeAttribute(href)}"`;
    });
    html = html.replace(/\sstyle\s*=\s*("([^"]*)"|'([^']*)')/gi, (match, quoted, doubleQuoted, singleQuoted) => {
        const filtered = filterAllowedStyles(doubleQuoted ?? singleQuoted ?? '');
        return filtered ? ` style="${escapeAttribute(filtered)}"` : '';
    });

    return html.trim();
}

function normalizeCard(card = {}) {
    return {
        front: sanitizeStoredHtml(String(card.front || '').slice(0, LIMITS.maxCardHtmlLength)),
        back: sanitizeStoredHtml(String(card.back || '').slice(0, LIMITS.maxCardHtmlLength)),
        frontFontSize: clampFontSize(card.frontFontSize),
        backFontSize: clampFontSize(card.backFontSize),
    };
}

function normalizeCards(cards) {
    if (!Array.isArray(cards) || cards.length === 0) {
        return [{
            front: '',
            back: '',
            frontFontSize: DEFAULT_FONT_SIZE,
            backFontSize: DEFAULT_FONT_SIZE,
        }];
    }

    return cards.map(normalizeCard);
}

function normalizeSet(set = {}) {
    return {
        id: typeof set.id === 'string' && set.id ? set.id : randomUUID(),
        name: normalizeName(set.name),
        cards: normalizeCards(set.cards),
        sentToVr: Boolean(set.sentToVr),
        lastSentToVr: Number.isFinite(Number(set.lastSentToVr)) ? Number(set.lastSentToVr) : 0,
        lastUpdated: Number.isFinite(Number(set.lastUpdated)) ? Number(set.lastUpdated) : 0,
    };
}

function normalizeDB(data) {
    const base = createEmptyDB();
    const next = data && typeof data === 'object' ? data : {};
    const sets = Array.isArray(next.sets) ? next.sets.map(normalizeSet) : [];
    const activeSetId = typeof next?.vr?.activeSetId === 'string' ? next.vr.activeSetId : null;

    return {
        sets,
        lastUpdated: Number.isFinite(Number(next.lastUpdated)) ? Number(next.lastUpdated) : base.lastUpdated,
        vr: {
            activeSetId: sets.some((set) => set.id === activeSetId) ? activeSetId : null,
        },
    };
}

function loadDB() {
    try {
        const parsed = JSON.parse(fs.readFileSync(DB_FILE, 'utf8'));
        return normalizeDB(parsed);
    } catch {
        return createEmptyDB();
    }
}

function saveDB(data) {
    fs.writeFileSync(DB_FILE, JSON.stringify(data, null, 2));
}

let db = loadDB();

function persistDB() {
    db.lastUpdated = now();
    saveDB(db);
}

function findSetById(id) {
    return db.sets.find((set) => set.id === id);
}

function validateSetPayload(body, options = {}) {
    const errors = [];
    const allowMissingCards = Boolean(options.allowMissingCards);
    const hasCards = Array.isArray(body?.cards);
    const hasName = body && Object.prototype.hasOwnProperty.call(body, 'name');

    const rawName = hasName ? String(body?.name || '').trim() : '';
    if (hasName && rawName.length > LIMITS.maxSetNameLength) {
        errors.push(`Set name cannot exceed ${LIMITS.maxSetNameLength} characters.`);
    }

    if (!allowMissingCards && !hasCards) {
        errors.push('A set must include a cards array.');
    }

    if (hasCards) {
        if (body.cards.length > LIMITS.maxCardsPerSet) {
            errors.push(`A set cannot contain more than ${LIMITS.maxCardsPerSet} cards.`);
        }

        body.cards.forEach((card, index) => {
            const frontLength = String(card?.front || '').length;
            const backLength = String(card?.back || '').length;

            if (frontLength > LIMITS.maxCardHtmlLength) {
                errors.push(`Card ${index + 1} front text is too long.`);
            }

            if (backLength > LIMITS.maxCardHtmlLength) {
                errors.push(`Card ${index + 1} back text is too long.`);
            }
        });
    }

    if (errors.length > 0) {
        return { ok: false, error: errors.join(' ') };
    }

    return {
        ok: true,
        value: {
            name: hasName ? normalizeName(body?.name) : undefined,
            cards: hasCards ? normalizeCards(body.cards) : undefined,
        },
    };
}

function decodeHtmlEntities(text) {
    return String(text || '')
        .replace(/&#x([0-9a-f]+);/gi, (_, value) => String.fromCodePoint(Number.parseInt(value, 16)))
        .replace(/&#([0-9]+);/g, (_, value) => String.fromCodePoint(Number.parseInt(value, 10)))
        .replace(/&nbsp;/gi, ' ')
        .replace(/&quot;/g, '"')
        .replace(/&#39;/g, "'")
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&amp;/g, '&');
}

function extractAttribute(tagToken, attributeName) {
    const regex = new RegExp(`${attributeName}\\s*=\\s*("([^"]*)"|'([^']*)'|([^\\s>]+))`, 'i');
    const match = tagToken.match(regex);
    return match?.[2] ?? match?.[3] ?? match?.[4] ?? '';
}

function extractStyleProperty(tagToken, propertyName) {
    const style = extractAttribute(tagToken, 'style');
    if (!style) {
        return '';
    }

    const properties = style.split(';');
    for (const property of properties) {
        const [rawName, ...rawValueParts] = property.split(':');
        if (!rawName || rawValueParts.length === 0) {
            continue;
        }

        if (rawName.trim().toLowerCase() === propertyName.toLowerCase()) {
            return rawValueParts.join(':').trim();
        }
    }

    return '';
}

function normalizeColorValue(color) {
    const value = String(color || '').trim();
    if (!value) {
        return '';
    }

    const rgbMatch = value.match(/^rgba?\((\d+),\s*(\d+),\s*(\d+)/i);
    if (!rgbMatch) {
        return value;
    }

    const hex = rgbMatch
        .slice(1, 4)
        .map((component) => {
            const clamped = Math.min(255, Math.max(0, Number.parseInt(component, 10)));
            return clamped.toString(16).padStart(2, '0');
        })
        .join('');

    return `#${hex}`;
}

function extractAlignment(tagToken) {
    const fromStyle = extractStyleProperty(tagToken, 'text-align');
    const fromAttr = extractAttribute(tagToken, 'align');
    const alignment = String(fromStyle || fromAttr || '').trim().toLowerCase();

    if (alignment === 'left' || alignment === 'center' || alignment === 'right') {
        return alignment;
    }

    return '';
}

function appendNewline(output) {
    return output.endsWith('\n') ? output : `${output}\n`;
}

function closeTag(stack, output, tagName) {
    const index = stack.map((entry) => entry.tag).lastIndexOf(tagName);
    if (index === -1) {
        return output;
    }

    for (let cursor = stack.length - 1; cursor >= index; cursor -= 1) {
        const entry = stack.pop();
        output += entry.close;
        if (entry.newline) {
            output = appendNewline(output);
        }
    }

    return output;
}

function htmlToUnityRichText(html) {
    const sanitized = sanitizeStoredHtml(html);
    const tokens = sanitized.match(/<\/?[^>]+>|[^<]+/g) || [];
    const stack = [];
    const listStack = [];
    let output = '';

    for (const token of tokens) {
        if (!token.startsWith('<')) {
            output += decodeHtmlEntities(token);
            continue;
        }

        const isClosing = /^<\//.test(token);
        const tagMatch = token.match(/^<\/?\s*([a-z0-9]+)/i);
        const tagName = tagMatch ? tagMatch[1].toLowerCase() : '';

        if (!tagName) {
            continue;
        }

        if (isClosing) {
            if (tagName === 'ul' || tagName === 'ol') {
                listStack.pop();
            }

            output = closeTag(stack, output, tagName);
            continue;
        }

        switch (tagName) {
            case 'br':
                output = appendNewline(output);
                break;
            case 'b':
            case 'strong':
                output += '<b>';
                stack.push({ tag: tagName, close: '</b>', newline: false });
                break;
            case 'i':
            case 'em':
                output += '<i>';
                stack.push({ tag: tagName, close: '</i>', newline: false });
                break;
            case 'u':
                output += '<u>';
                stack.push({ tag: tagName, close: '</u>', newline: false });
                break;
            case 'font':
            case 'span': {
                const color = normalizeColorValue(extractAttribute(token, 'color') || extractStyleProperty(token, 'color'));
                if (color) {
                    output += `<color=${color}>`;
                    stack.push({ tag: tagName, close: '</color>', newline: false });
                } else {
                    stack.push({ tag: tagName, close: '', newline: false });
                }
                break;
            }
            case 'div':
            case 'p': {
                const alignment = extractAlignment(token);
                if (output && !output.endsWith('\n')) {
                    output = appendNewline(output);
                }

                if (alignment) {
                    output += `<align="${alignment}">`;
                }

                stack.push({
                    tag: tagName,
                    close: alignment ? '</align>' : '',
                    newline: true,
                });
                break;
            }
            case 'ul':
                if (output && !output.endsWith('\n')) {
                    output = appendNewline(output);
                }

                listStack.push({ type: 'ul', index: 0 });
                stack.push({ tag: 'ul', close: '', newline: true });
                break;
            case 'ol':
                if (output && !output.endsWith('\n')) {
                    output = appendNewline(output);
                }

                listStack.push({ type: 'ol', index: 0 });
                stack.push({ tag: 'ol', close: '', newline: true });
                break;
            case 'li': {
                const listContext = listStack[listStack.length - 1];
                if (output && !output.endsWith('\n')) {
                    output = appendNewline(output);
                }

                if (listContext?.type === 'ol') {
                    listContext.index += 1;
                    output += `${listContext.index}. `;
                } else {
                    output += '• ';
                }

                stack.push({ tag: 'li', close: '', newline: false });
                break;
            }
            case 'a':
                stack.push({ tag: 'a', close: '', newline: false });
                break;
            default:
                stack.push({ tag: tagName, close: '', newline: false });
        }
    }

    while (stack.length > 0) {
        const entry = stack.pop();
        output += entry.close;
        if (entry.newline) {
            output = appendNewline(output);
        }
    }

    return output
        .replace(/\n{3,}/g, '\n\n')
        .replace(/[ \t]+\n/g, '\n')
        .trim();
}

function plainTextFromUnityRichText(richText) {
    return String(richText || '')
        .replace(/<[^>]+>/g, '')
        .replace(/\n{3,}/g, '\n\n')
        .trim();
}

function wrapWithSizeTag(richText, fontSize) {
    if (!richText) {
        return '';
    }

    const normalizedFontSize = clampFontSize(fontSize);
    return normalizedFontSize === DEFAULT_FONT_SIZE
        ? richText
        : `<size=${normalizedFontSize}>${richText}</size>`;
}

function exportUnityCard(card) {
    const frontRichText = wrapWithSizeTag(
        htmlToUnityRichText(card.front),
        card.frontFontSize
    );
    const backRichText = wrapWithSizeTag(
        htmlToUnityRichText(card.back),
        card.backFontSize
    );

    return {
        frontRichText,
        backRichText,
        frontPlainText: plainTextFromUnityRichText(frontRichText),
        backPlainText: plainTextFromUnityRichText(backRichText),
        frontFontSize: clampFontSize(card.frontFontSize),
        backFontSize: clampFontSize(card.backFontSize),
    };
}

function exportUnitySet(set) {
    return {
        schemaVersion: 1,
        id: set.id,
        name: set.name,
        lastUpdated: set.lastUpdated,
        lastSentToVr: set.lastSentToVr,
        cards: set.cards.map(exportUnityCard),
    };
}

function startDiscoveryResponder(httpPort) {
    const socket = dgram.createSocket('udp4');

    socket.on('error', (error) => {
        console.warn(`LAN discovery responder error: ${error.message}`);
    });

    socket.on('message', (message, remoteInfo) => {
        if (String(message).trim() !== DISCOVERY_REQUEST_MESSAGE) {
            return;
        }

        const payload = Buffer.from(`${DISCOVERY_RESPONSE_PREFIX}${httpPort}`);
        socket.send(payload, remoteInfo.port, remoteInfo.address, (error) => {
            if (error) {
                console.warn(`LAN discovery response failed: ${error.message}`);
            }
        });
    });

    socket.bind(DISCOVERY_PORT, () => {
        console.log(`LAN discovery responder listening on udp://0.0.0.0:${DISCOVERY_PORT}`);
    });
}

app.get('/api/health', (req, res) => {
    res.json({
        ok: true,
        lastUpdated: db.lastUpdated,
        setCount: db.sets.length,
        publishedSetCount: db.sets.filter((set) => set.sentToVr).length,
    });
});

app.get('/api/sets', (req, res) => {
    res.json({
        lastUpdated: db.lastUpdated,
        vr: db.vr,
        sets: db.sets,
    });
});

app.post('/api/sets', (req, res) => {
    const validation = validateSetPayload(req.body);
    if (!validation.ok) {
        return res.status(400).json({ ok: false, error: validation.error });
    }

    const set = {
        id: randomUUID(),
        name: validation.value.name || 'Untitled Set',
        cards: validation.value.cards,
        sentToVr: false,
        lastSentToVr: 0,
        lastUpdated: now(),
    };

    db.sets.push(set);
    persistDB();
    return res.json({ ok: true, set });
});

app.put('/api/sets/:id', (req, res) => {
    const set = findSetById(req.params.id);
    if (!set) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    const validation = validateSetPayload(req.body, { allowMissingCards: true });
    if (!validation.ok) {
        return res.status(400).json({ ok: false, error: validation.error });
    }

    if (validation.value.name !== undefined) {
        set.name = validation.value.name;
    }
    if (validation.value.cards) {
        set.cards = validation.value.cards;
    }

    set.lastUpdated = now();
    persistDB();
    return res.json({ ok: true, set });
});

app.post('/api/sets/:id/send-to-vr', (req, res) => {
    const set = findSetById(req.params.id);
    if (!set) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    set.sentToVr = true;
    set.lastSentToVr = now();
    set.lastUpdated = now();
    db.vr.activeSetId = set.id;
    persistDB();

    return res.json({
        ok: true,
        set,
        vr: {
            activeSetId: db.vr.activeSetId,
            exportedSet: exportUnitySet(set),
        },
    });
});

app.get('/api/vr/sets', (req, res) => {
    const publishedSets = db.sets
        .filter((set) => set.sentToVr)
        .map(exportUnitySet);

    res.json({
        schemaVersion: 1,
        generatedAt: now(),
        activeSetId: db.vr.activeSetId,
        sets: publishedSets,
    });
});

app.get('/api/vr/active-set', (req, res) => {
    const set = db.vr.activeSetId ? findSetById(db.vr.activeSetId) : null;
    if (!set || !set.sentToVr) {
        return res.status(404).json({ ok: false, error: 'No active VR set found' });
    }

    return res.json({
        ok: true,
        activeSetId: set.id,
        set: exportUnitySet(set),
    });
});

app.delete('/api/sets/:id', (req, res) => {
    const before = db.sets.length;
    db.sets = db.sets.filter((set) => set.id !== req.params.id);

    if (db.sets.length === before) {
        return res.status(404).json({ ok: false, error: 'Set not found' });
    }

    if (db.vr.activeSetId === req.params.id) {
        db.vr.activeSetId = null;
    }

    persistDB();
    return res.json({ ok: true });
});

if (require.main === module) {
    const PORT = Number.parseInt(process.env.PORT || '3000', 10);
    app.listen(PORT, '0.0.0.0', () => {
        console.log(`Server is running on port ${PORT}`);
        startDiscoveryResponder(PORT);
    });
}

module.exports = { app };
