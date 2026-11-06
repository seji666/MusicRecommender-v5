(() => {
    // ========== Глобален плейър ==========
    // Един Audio обект за цялото приложение => винаги звучи само една песен,
    // а нивото на звука е общо и се пази в localStorage.
    const VOL_KEY = 'mr_volume';
    const audio = new Audio();
    audio.preload = 'none';

    let savedVol = 0.7;
    try {
        const v = parseFloat(localStorage.getItem(VOL_KEY));
        if (!isNaN(v)) savedVol = Math.min(1, Math.max(0, v));
    } catch { /* localStorage може да е недостъпен */ }
    audio.volume = savedVol;

    const $ = id => document.getElementById(id);
    const bar = $('player'), cover = $('pl-cover'), title = $('pl-title'), artist = $('pl-artist');
    const toggle = $('pl-toggle'), progress = $('pl-progress'), time = $('pl-time');
    const volume = $('pl-volume'), volIcon = $('pl-vol-icon');
    let currentSrc = null;

    volume.value = audio.volume;
    updateVolIcon();

    const fmt = s => {
        if (!isFinite(s)) return '0:00';
        const m = Math.floor(s / 60), r = Math.floor(s % 60);
        return m + ':' + String(r).padStart(2, '0');
    };

    function updateVolIcon() {
        const v = audio.volume;
        volIcon.className = 'pl-vol-icon bi ' +
            (v === 0 ? 'bi-volume-mute' : v < 0.5 ? 'bi-volume-down' : 'bi-volume-up');
    }

    function syncButtons() {
        const playing = !audio.paused;
        document.querySelectorAll('.play-btn').forEach(btn => {
            const active = playing && btn.dataset.src === currentSrc;
            btn.classList.toggle('is-playing', active);
            btn.querySelector('i').className = 'bi ' + (active ? 'bi-pause-fill' : 'bi-play-fill');
        });
        toggle.querySelector('i').className = 'bi ' + (playing ? 'bi-pause-fill' : 'bi-play-fill');
    }

    document.addEventListener('click', e => {
        const btn = e.target.closest('.play-btn');
        if (!btn) return;

        if (btn.dataset.src === currentSrc) {          // същата песен -> пауза/пусни
            audio.paused ? audio.play() : audio.pause();
            return;
        }
        audio.pause();                                  // спира предишната песен
        currentSrc = btn.dataset.src;
        audio.src = currentSrc;
        audio.volume = parseFloat(volume.value);        // еднакво ниво на звука за всички песни
        audio.play().catch(() => { /* блокирано от браузъра */ });

        title.textContent = btn.dataset.title || '';
        artist.textContent = btn.dataset.artist || '';
        if (btn.dataset.cover) { cover.src = btn.dataset.cover; cover.style.visibility = 'visible'; }
        else cover.style.visibility = 'hidden';
        bar.classList.remove('d-none');
    });

    toggle.addEventListener('click', () => { audio.paused ? audio.play() : audio.pause(); });
    audio.addEventListener('play', syncButtons);
    audio.addEventListener('pause', syncButtons);
    audio.addEventListener('ended', () => { progress.value = 0; syncButtons(); });
    audio.addEventListener('timeupdate', () => {
        if (audio.duration) progress.value = (audio.currentTime / audio.duration) * 1000;
        time.textContent = fmt(audio.currentTime);
    });
    progress.addEventListener('input', () => {
        if (audio.duration) audio.currentTime = (progress.value / 1000) * audio.duration;
    });

    volume.addEventListener('input', () => {
        audio.volume = parseFloat(volume.value);
        try { localStorage.setItem(VOL_KEY, String(audio.volume)); } catch { }
        updateVolIcon();
    });
    let lastVol = savedVol || 0.7;
    volIcon.addEventListener('click', () => {            // mute / unmute
        if (audio.volume > 0) { lastVol = audio.volume; volume.value = 0; }
        else volume.value = lastVol;
        volume.dispatchEvent(new Event('input'));
    });

    // ========== Динамичен плейър при скролване ==========
    // Скрол надолу -> свива се до мини версия; скрол нагоре / hover / фокус / край на страницата -> разгъва се.
    let lastY = window.scrollY, ticking = false, hovered = false;
    const setCompact = c => bar.classList.toggle('is-compact', c);
    bar.addEventListener('mouseenter', () => { hovered = true; setCompact(false); });
    bar.addEventListener('mouseleave', () => { hovered = false; });
    bar.addEventListener('focusin', () => setCompact(false));
    bar.addEventListener('click', e => {                 // клик върху мини версията я разгъва
        if (bar.classList.contains('is-compact') && !e.target.closest('#pl-toggle')) setCompact(false);
    });
    window.addEventListener('scroll', () => {
        if (ticking) return;
        ticking = true;
        requestAnimationFrame(() => {
            const y = window.scrollY, dy = y - lastY;
            const atBottom = window.innerHeight + y >= document.documentElement.scrollHeight - 80;
            if (!hovered && !bar.contains(document.activeElement)) {
                if (y < 80 || atBottom || dy < -6) setCompact(false);
                else if (dy > 6) setCompact(true);
            }
            lastY = y;
            ticking = false;
        });
    }, { passive: true });

    // ========== Добавяне към любими без презареждане ==========
    document.addEventListener('submit', async e => {
        const f = e.target;
        if (f.classList.contains('ajax-add')) {
            e.preventDefault();
            const btn = f.querySelector('button');
            btn.disabled = true;
            try {
                const r = await fetch(f.action, {
                    method: 'POST',
                    body: new FormData(f),
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                btn.innerHTML = r.ok ? '<i class="bi bi-heart-fill"></i> Добавена' : 'Грешка';
                if (!r.ok) btn.disabled = false;
            } catch { btn.textContent = 'Грешка'; btn.disabled = false; }
        }
        // Индикатор за зареждане при AI заявка
        if (f.classList.contains('loading-form') && f.checkValidity()) {
            const b = f.querySelector('.submit-btn');
            b.querySelector('.btn-label').classList.add('d-none');
            b.querySelector('.btn-loading').classList.remove('d-none');
            setTimeout(() => b.disabled = true, 0);
        }
    });
})();

// ========== Добавяне в плейлист (модален прозорец) ==========
(() => {
    const modalEl = document.getElementById('plModal');
    if (!modalEl || typeof bootstrap === 'undefined') return;

    const modal = new bootstrap.Modal(modalEl);
    const listEl = document.getElementById('plm-list');
    const statusEl = document.getElementById('plm-status');
    const trackEl = document.getElementById('plm-track');
    const newForm = document.getElementById('plm-new');
    const newName = document.getElementById('plm-new-name');
    const token = () => modalEl.querySelector('input[name="__RequestVerificationToken"]')?.value ?? '';
    let track = null;           // песента, която се добавя в момента

    const say = (msg, ok) => {
        statusEl.textContent = msg || '';
        statusEl.className = 'small mb-2 ' + (msg ? (ok ? 'text-success' : 'text-danger') : 'd-none');
    };

    function row(p) {
        const b = document.createElement('button');
        b.type = 'button';
        b.className = 'pl-pick';
        b.dataset.id = p.id;

        const icon = document.createElement('i');
        const name = document.createElement('span');
        name.className = 'pl-pick-name';
        name.textContent = p.name;
        const count = document.createElement('span');
        count.className = 'pl-pick-count';
        count.textContent = p.count + ' бр.';

        const mark = done => {
            icon.className = 'bi ' + (done ? 'bi-check-circle-fill' : 'bi-plus-circle');
            b.disabled = done;
        };
        mark(p.contains);
        b.append(icon, name, count);
        b.addEventListener('click', async () => {
            b.disabled = true;
            if (await add(p.id, null)) { mark(true); count.textContent = (p.count + 1) + ' бр.'; }
            else b.disabled = false;
        });
        return b;
    }

    async function load() {
        listEl.textContent = '';
        say('');
        try {
            const r = await fetch(modalEl.dataset.listUrl + '?deezerId=' + encodeURIComponent(track.id));
            if (!r.ok) throw new Error();
            const items = await r.json();
            if (items.length === 0) {
                const p = document.createElement('div');
                p.className = 'text-muted small';
                p.textContent = 'Още нямате плейлисти — създайте първия по-долу.';
                listEl.append(p);
            }
            items.forEach(p => listEl.append(row(p)));
        } catch { say('Плейлистите не можаха да се заредят.', false); }
    }

    // Връща true при успех. playlistId = 0 + name => нов плейлист
    async function add(playlistId, name) {
        const fd = new FormData();
        fd.append('__RequestVerificationToken', token());
        fd.append('playlistId', playlistId);
        if (name) fd.append('newName', name);
        fd.append('DeezerId', track.id);
        fd.append('Title', track.title);
        fd.append('Artist', track.artist);
        fd.append('Album', track.album);
        fd.append('CoverUrl', track.cover);
        fd.append('PreviewUrl', track.preview);
        fd.append('Link', track.link);
        try {
            const r = await fetch(modalEl.dataset.addUrl, {
                method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest' }
            });
            const data = await r.json().catch(() => ({}));
            if (!r.ok) { say(data.error || 'Грешка при добавянето.', false); return false; }
            say(data.added ? 'Добавена в „' + data.name + '“.' : 'Вече е в „' + data.name + '“.', true);
            return true;
        } catch { say('Грешка при връзка със сървъра.', false); return false; }
    }

    document.addEventListener('click', e => {
        const btn = e.target.closest('.add-pl-btn');
        if (!btn) return;
        const d = btn.dataset;
        track = { id: d.id, title: d.title, artist: d.artist, album: d.album ?? '',
                  cover: d.cover ?? '', preview: d.preview ?? '', link: d.link ?? '' };
        trackEl.textContent = track.title + ' · ' + track.artist;
        newName.value = '';
        load();
        modal.show();
    });

    newForm.addEventListener('submit', async e => {
        e.preventDefault();
        const name = newName.value.trim();
        if (!name) { say('Въведете име на плейлиста.', false); return; }
        if (await add(0, name)) { newName.value = ''; load().then(() => say('Плейлистът е създаден и песента е добавена.', true)); }
    });
})();
