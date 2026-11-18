/**
 * منصة نور — نص القرآن الكريم برواية قالون عن نافع (يعمل دون إنترنت)
 * المصدر: مجمع الملك فهد لطباعة المصحف الشريف — مصحف المدينة برواية قالون (UthmanicQaloon v10)،
 * بالعدّ المدني الأول (6214 آية) المطابق لترقيم مصاحف قالون في ليبيا.
 * الملف /data/quran-qaloun.json يُحمَّل مرة واحدة ويحفظه المتصفح (Service Worker) للعمل دون اتصال.
 * يُعرض النص بخط «UthmanicQaloun» لأن بعض علامات الضبط فيه خاصة برسم المجمع.
 */
(function (global) {
    'use strict';

    const DATA_URL = '/data/quran-qaloun.json?v=1';
    let _loading = null;
    let _surahs = null;

    /** تحميل نص المصحف كاملاً (مرة واحدة لكل جلسة). */
    function load() {
        if (_surahs) return Promise.resolve(_surahs);
        if (!_loading) {
            _loading = fetch(DATA_URL)
                .then(res => { if (!res.ok) throw new Error('HTTP ' + res.status); return res.json(); })
                .then(data => { _surahs = data.surahs || []; return _surahs; })
                .catch(e => { _loading = null; console.warn('تعذر تحميل نص المصحف:', e); return []; });
        }
        return _loading;
    }

    function getSurahNumberByName(surahName) {
        const list = global.SURAHS || (global.NoorUtils && global.NoorUtils.SURAHS) || [];
        const found = list.find(s => s.name === surahName);
        return found ? found.n : null;
    }

    /** آيات سورة: [{ number, text }]. */
    async function fetchSurahVerses(surahNumber) {
        const num = parseInt(surahNumber, 10);
        if (!num || num < 1 || num > 114) return [];
        const surahs = await load();
        return (surahs[num - 1] || []).map((text, i) => ({ number: i + 1, text }));
    }

    async function fetchVerseText(surahNumber, verseNumber) {
        const num = parseInt(surahNumber, 10);
        const v = parseInt(verseNumber, 10);
        const surahs = await load();
        return (surahs[num - 1] || [])[v - 1] || '';
    }

    async function fetchVerseTextByName(surahName, verseNumber) {
        const num = getSurahNumberByName(surahName);
        return num ? fetchVerseText(num, verseNumber) : '';
    }

    async function fetchVerseRangeTextByName(surahName, fromVerse, toVerse) {
        const num = getSurahNumberByName(surahName);
        if (!num) return '';
        const verses = await fetchSurahVerses(num);
        const from = parseInt(fromVerse, 10);
        const to = parseInt(toVerse, 10) || from;
        return verses.filter(v => v.number >= from && v.number <= to).map(v => v.text).join(' ');
    }

    /** توحيد النص للبحث: إزالة التشكيل وعلامات الضبط والمدّ وتوحيد الهمزات. */
    function normalizeForSearch(s) {
        return String(s || '')
            .replace(/[ؐ-ًؚ-ٰٟۖ-ۭ࣓-ࣿـ]/g, '')
            .replace(/[إأآٱا]/g, 'ا').replace(/[ىے]/g, 'ي').replace(/ة/g, 'ه').replace(/ؤ/g, 'و').replace(/ئ/g, 'ي')
            // الرسم العثماني يحذف الألف في كلمات كثيرة (الصرط، الكتب...)؛ نتجاهل الألف في الطرفين عند المطابقة
            .replace(/ا/g, '')
            .replace(/\s+/g, ' ').trim();
    }

    /** بداية الآية مختصرة للعرض في القوائم (أول كلمات). */
    function verseLead(text, words) {
        const parts = String(text || '').split(/\s+/);
        const n = words || 6;
        return parts.slice(0, n).join(' ') + (parts.length > n ? ' …' : '');
    }

    /** خيارات قائمة الآيات: «12 — أول كلمات الآية». */
    async function verseOptionsHtml(surahName, selected) {
        const num = getSurahNumberByName(surahName);
        if (!num) return '';
        const verses = await fetchSurahVerses(num);
        const esc = global.escapeHtml || (s => String(s));
        return verses.map(v =>
            `<option value="${v.number}"${String(v.number) === String(selected) ? ' selected' : ''}>${v.number} — ${esc(verseLead(v.text))}</option>`
        ).join('');
    }

    /** البحث عن آيات في سورة تحتوي الكلمة (دون اعتبار للتشكيل). */
    async function searchInSurah(surahName, query) {
        const num = getSurahNumberByName(surahName);
        const q = normalizeForSearch(query);
        if (!num || !q) return [];
        const verses = await fetchSurahVerses(num);
        return verses.filter(v => normalizeForSearch(v.text).includes(q));
    }

    global.NoorQuranText = {
        load,
        fetchSurahVerses,
        fetchVerseText,
        fetchVerseTextByName,
        fetchVerseRangeTextByName,
        getSurahNumberByName,
        verseOptionsHtml,
        searchInSurah,
        normalizeForSearch,
        verseLead
    };
})(window);
