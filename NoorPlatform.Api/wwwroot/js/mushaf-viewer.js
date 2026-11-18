/**
 * منصة نور — عارض «مصحف أمانة التعليم» (رواية قالون) كصور صفحات، بإذن الجهة الناشرة.
 * الصفحات في /mushaf/{رقم}.jpg (576 صفحة: القرآن من 3 إلى 573) والفهرس في /data/mushaf-index.json
 * (صفحة بداية كل سورة، مُتحقَّق منها صفحةً صفحة). صفحة آية داخل السورة تقديرية بنسبة موضعها.
 * الاستعمال: NoorMushaf.open({ surah: 78, verse: 12 }) أو NoorMushaf.open({ page: 545 }).
 */
(function (global) {
    'use strict';

    const INDEX_URL = '/data/mushaf-index.json?v=1';
    let index = null;
    let page = 3;
    let root = null;
    let touchX = null;

    const ic = (n, s) => (global.Icon ? global.Icon(n, { size: s || 16 }) : '');
    const surahs = () => global.SURAHS || (global.NoorUtils && global.NoorUtils.SURAHS) || [];

    async function loadIndex() {
        if (index) return index;
        const res = await fetch(INDEX_URL);
        if (!res.ok) throw new Error('HTTP ' + res.status);
        index = await res.json();
        return index;
    }

    /** الصفحة التي تقع فيها الآية (تقدير داخل السورة بنسبة موضع الآية). */
    function pageFor(surahNumber, verse) {
        const starts = index.surahStartPage;
        const n = Math.min(Math.max(surahNumber, 1), 114);
        const start = starts[n - 1];
        const end = n < 114 ? starts[n] : index.lastQuranPage;
        const count = index.verseCounts[n - 1] || 1;
        const v = Math.min(Math.max(verse || 1, 1), count);
        if (end <= start) return start;
        return Math.min(end, start + Math.floor((v - 1) / count * (end - start + 1)));
    }

    /** أسماء السور الظاهرة في الصفحة. */
    function surahsOnPage(p) {
        const starts = index.surahStartPage;
        const list = [];
        for (let i = 0; i < 114; i++) {
            const start = starts[i];
            const end = i < 113 ? starts[i + 1] : index.lastQuranPage;
            if (start <= p && p <= end && (start === p || end > p || i === 113)) list.push(i + 1);
        }
        // السورة التي تستمر في الصفحة دون أن تبدأ فيها
        if (!list.length) {
            for (let i = 113; i >= 0; i--) if (starts[i] <= p) { list.push(i + 1); break; }
        }
        return list;
    }

    function build() {
        root = document.createElement('div');
        root.className = 'mushaf-viewer';
        root.id = 'mushafViewer';
        root.setAttribute('role', 'dialog');
        root.setAttribute('aria-modal', 'true');
        root.setAttribute('aria-label', 'المصحف');
        root.innerHTML = `
            <div class="mushaf-bar">
                <strong class="mushaf-title">${ic('book-open', 18)} المصحف</strong>
                <select class="form-input mushaf-surah" aria-label="الانتقال إلى سورة"></select>
                <label class="mushaf-page-jump">صفحة
                    <input class="form-input" type="number" min="1" max="576" aria-label="رقم الصفحة">
                </label>
                <button type="button" class="mushaf-close" aria-label="إغلاق">${ic('x', 20)}</button>
            </div>
            <div class="mushaf-stage">
                <button type="button" class="mushaf-nav mushaf-next" aria-label="الصفحة التالية">${ic('chevron-left', 28)}</button>
                <div class="mushaf-page-wrap">
                    <img class="mushaf-page" alt="صفحة المصحف" draggable="false">
                    <div class="mushaf-loading">جاري تحميل الصفحة...</div>
                </div>
                <button type="button" class="mushaf-nav mushaf-prev" aria-label="الصفحة السابقة">${ic('chevron-right', 28)}</button>
            </div>
            <div class="mushaf-foot"><span class="mushaf-info"></span></div>`;
        document.body.appendChild(root);

        const sel = root.querySelector('.mushaf-surah');
        sel.innerHTML = surahs().map(s => `<option value="${s.n}">${s.n}. ${s.name}</option>`).join('');
        sel.addEventListener('change', () => show(index.surahStartPage[parseInt(sel.value, 10) - 1]));
        const pageInput = root.querySelector('.mushaf-page-jump input');
        pageInput.addEventListener('change', () => show(parseInt(pageInput.value, 10)));
        root.querySelector('.mushaf-close').addEventListener('click', close);
        // الاتجاه العربي: الصفحة التالية إلى اليسار
        root.querySelector('.mushaf-next').addEventListener('click', () => show(page + 1));
        root.querySelector('.mushaf-prev').addEventListener('click', () => show(page - 1));
        const img = root.querySelector('.mushaf-page');
        img.addEventListener('load', () => root.classList.remove('is-loading'));
        img.addEventListener('error', () => {
            root.classList.remove('is-loading');
            root.querySelector('.mushaf-info').textContent = 'تعذر تحميل الصفحة — تحقق من الاتصال';
        });
        const stage = root.querySelector('.mushaf-stage');
        stage.addEventListener('touchstart', e => { touchX = e.touches[0].clientX; }, { passive: true });
        stage.addEventListener('touchend', e => {
            if (touchX == null) return;
            const dx = e.changedTouches[0].clientX - touchX;
            touchX = null;
            if (Math.abs(dx) < 50) return;
            show(dx > 0 ? page + 1 : page - 1); // السحب نحو اليمين يقلب للصفحة التالية كالمصحف الورقي
        });
        document.addEventListener('keydown', onKey);
    }

    function onKey(e) {
        if (!root || !root.classList.contains('open')) return;
        if (e.key === 'Escape') close();
        else if (e.key === 'ArrowLeft') show(page + 1);
        else if (e.key === 'ArrowRight') show(page - 1);
    }

    function show(p) {
        if (!index || !Number.isFinite(p)) return;
        page = Math.min(Math.max(p, 1), index.totalPages);
        root.classList.add('is-loading');
        root.querySelector('.mushaf-page').src = `/mushaf/${page}.jpg`;
        root.querySelector('.mushaf-page-jump input').value = page;
        const list = page >= index.firstQuranPage && page <= index.lastQuranPage ? surahsOnPage(page) : [];
        if (list.length) root.querySelector('.mushaf-surah').value = String(list[list.length - 1]);
        const names = list.map(n => (surahs().find(s => s.n === n) || {}).name).filter(Boolean);
        root.querySelector('.mushaf-info').textContent =
            (names.length ? 'سورة ' + names.join(' • ') + ' — ' : '') + `صفحة ${page} من ${index.totalPages}`;
        root.querySelector('.mushaf-next').disabled = page >= index.totalPages;
        root.querySelector('.mushaf-prev').disabled = page <= 1;
        // تحميل مسبق للصفحتين المجاورتين لتقليب سريع
        [page + 1, page - 1].forEach(n => { if (n >= 1 && n <= index.totalPages) { const i = new Image(); i.src = `/mushaf/${n}.jpg`; } });
    }

    async function open(opts) {
        const o = opts || {};
        try {
            await loadIndex();
        } catch (e) {
            global.showToast && global.showToast('تعذر تحميل فهرس المصحف', 'error');
            return;
        }
        if (!root) build();
        root.classList.add('open');
        document.body.classList.add('mushaf-open');
        show(o.page || (o.surah ? pageFor(o.surah, o.verse) : page));
        root.querySelector('.mushaf-close').focus();
    }

    function close() {
        if (!root) return;
        root.classList.remove('open');
        document.body.classList.remove('mushaf-open');
    }

    global.NoorMushaf = { open, close, pageFor: (s, v) => (index ? pageFor(s, v) : null) };
})(window);
