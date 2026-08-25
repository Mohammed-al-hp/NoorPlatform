/**
 * منصة نور — جلب نص القرآن الكريم (رواية قالون)
 * المصدر: مجمع الملك فهد لطباعة المصحف الشريف، عبر jsDelivr CDN
 * https://github.com/fawazahmed0/quran-api
 */
(function (global) {
    'use strict';

    const BASE_URL = 'https://cdn.jsdelivr.net/gh/fawazahmed0/quran-api@1/editions/ara-quranqaloon';

    /**
     * تحويل اسم السورة (كما يُخزَّن بحقول select في hifz.js، مثل "الفاتحة")
     * إلى رقمها (1-114)، بالاعتماد على مصفوفة SURAHS الموجودة أصلاً بـ utils.js.
     */
    function getSurahNumberByName(surahName) {
        const list = global.SURAHS || (global.NoorUtils && global.NoorUtils.SURAHS) || [];
        const found = list.find(s => s.name === surahName);
        return found ? found.n : null;
    }

    // تخزين مؤقت بالذاكرة لكل سورة تم جلبها بهذه الجلسة، لتفادي إعادة الطلب لنفس السورة
    const _surahCache = {};

    /**
     * جلب كل آيات سورة معيّنة برواية قالون.
     * @param {number} surahNumber رقم السورة (1-114)
     * @returns {Promise<Array<{number:number, text:string}>>}
     */
    async function fetchSurahVerses(surahNumber) {
        const num = parseInt(surahNumber, 10);
        if (!num || num < 1 || num > 114) return [];

        if (_surahCache[num]) return _surahCache[num];

        try {
            const res = await fetch(`${BASE_URL}/${num}.min.json`);
            if (!res.ok) throw new Error('HTTP ' + res.status);
            const data = await res.json();
            // بنية الاستجابة المؤكدة فعليًا (تم التحقق عبر اختبار مباشر):
            // { "chapter": [ { "chapter": 1, "verse": 1, "text": "..." }, ... ] }
            const verses = (data.chapter || []).map(v => ({
                number: v.verse,
                text: v.text
            }));
            _surahCache[num] = verses;
            return verses;
        } catch (e) {
            console.warn('تعذر جلب نص السورة من مصدر القرآن (قالون):', e);
            return [];
        }
    }

    /**
     * جلب نص آية واحدة محددة.
     * @param {number} surahNumber
     * @param {number} verseNumber
     * @returns {Promise<string>} نص الآية، أو سلسلة فارغة إن تعذر الجلب
     */
    async function fetchVerseText(surahNumber, verseNumber) {
        const verses = await fetchSurahVerses(surahNumber);
        const v = verses.find(x => x.number === parseInt(verseNumber, 10));
        return v ? v.text : '';
    }

    /**
     * جلب نص آية بالاعتماد على اسم السورة (كما هو مخزن بحقول hifz.js) بدل رقمها.
     * @param {string} surahName اسم السورة كما بمصفوفة SURAHS (مثال: "الفاتحة")
     * @param {number} verseNumber
     * @returns {Promise<string>}
     */
    async function fetchVerseTextByName(surahName, verseNumber) {
        const num = getSurahNumberByName(surahName);
        if (!num) return '';
        return fetchVerseText(num, verseNumber);
    }

    /**
     * جلب نص نطاق من الآيات (من...إلى) كنص واحد متصل، جاهز للعرض في حقل نصي.
     * @param {string} surahName
     * @param {number} fromVerse
     * @param {number} toVerse
     * @returns {Promise<string>}
     */
    async function fetchVerseRangeTextByName(surahName, fromVerse, toVerse) {
        const num = getSurahNumberByName(surahName);
        if (!num) return '';
        const verses = await fetchSurahVerses(num);
        const from = parseInt(fromVerse, 10);
        const to = parseInt(toVerse, 10) || from;
        const selected = verses.filter(v => v.number >= from && v.number <= to);
        return selected.map(v => v.text).join(' ');
    }

    global.NoorQuranText = {
        fetchSurahVerses,
        fetchVerseText,
        fetchVerseTextByName,
        fetchVerseRangeTextByName,
        getSurahNumberByName
    };
})(window);
