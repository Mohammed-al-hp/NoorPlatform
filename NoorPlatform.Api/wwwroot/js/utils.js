/**
 * منصة نور — أدوات مشتركة
 */
(function (global) {
    'use strict';

    const SURAHS = [
        { n: 1, name: 'الفاتحة', v: 7 }, { n: 2, name: 'البقرة', v: 286 }, { n: 3, name: 'آل عمران', v: 200 },
        { n: 4, name: 'النساء', v: 176 }, { n: 5, name: 'المائدة', v: 120 }, { n: 6, name: 'الأنعام', v: 165 },
        { n: 7, name: 'الأعراف', v: 206 }, { n: 8, name: 'الأنفال', v: 75 }, { n: 9, name: 'التوبة', v: 129 },
        { n: 10, name: 'يونس', v: 109 }, { n: 11, name: 'هود', v: 123 }, { n: 12, name: 'يوسف', v: 111 },
        { n: 13, name: 'الرعد', v: 43 }, { n: 14, name: 'إبراهيم', v: 52 }, { n: 15, name: 'الحجر', v: 99 },
        { n: 16, name: 'النحل', v: 128 }, { n: 17, name: 'الإسراء', v: 111 }, { n: 18, name: 'الكهف', v: 110 },
        { n: 19, name: 'مريم', v: 98 }, { n: 20, name: 'طه', v: 135 }, { n: 21, name: 'الأنبياء', v: 112 },
        { n: 22, name: 'الحج', v: 78 }, { n: 23, name: 'المؤمنون', v: 118 }, { n: 24, name: 'النور', v: 64 },
        { n: 25, name: 'الفرقان', v: 77 }, { n: 26, name: 'الشعراء', v: 227 }, { n: 27, name: 'النمل', v: 93 },
        { n: 28, name: 'القصص', v: 88 }, { n: 29, name: 'العنكبوت', v: 69 }, { n: 30, name: 'الروم', v: 60 },
        { n: 31, name: 'لقمان', v: 34 }, { n: 32, name: 'السجدة', v: 30 }, { n: 33, name: 'الأحزاب', v: 73 },
        { n: 34, name: 'سبأ', v: 54 }, { n: 35, name: 'فاطر', v: 45 }, { n: 36, name: 'يس', v: 83 },
        { n: 37, name: 'الصافات', v: 182 }, { n: 38, name: 'ص', v: 88 }, { n: 39, name: 'الزمر', v: 75 },
        { n: 40, name: 'غافر', v: 85 }, { n: 41, name: 'فصلت', v: 54 }, { n: 42, name: 'الشورى', v: 53 },
        { n: 43, name: 'الزخرف', v: 89 }, { n: 44, name: 'الدخان', v: 59 }, { n: 45, name: 'الجاثية', v: 37 },
        { n: 46, name: 'الأحقاف', v: 35 }, { n: 47, name: 'محمد', v: 38 }, { n: 48, name: 'الفتح', v: 29 },
        { n: 49, name: 'الحجرات', v: 18 }, { n: 50, name: 'ق', v: 45 }, { n: 51, name: 'الذاريات', v: 60 },
        { n: 52, name: 'الطور', v: 49 }, { n: 53, name: 'النجم', v: 62 }, { n: 54, name: 'القمر', v: 55 },
        { n: 55, name: 'الرحمن', v: 78 }, { n: 56, name: 'الواقعة', v: 96 }, { n: 57, name: 'الحديد', v: 29 },
        { n: 58, name: 'المجادلة', v: 22 }, { n: 59, name: 'الحشر', v: 24 }, { n: 60, name: 'الممتحنة', v: 13 },
        { n: 61, name: 'الصف', v: 14 }, { n: 62, name: 'الجمعة', v: 11 }, { n: 63, name: 'المنافقون', v: 11 },
        { n: 64, name: 'التغابن', v: 18 }, { n: 65, name: 'الطلاق', v: 12 }, { n: 66, name: 'التحريم', v: 12 },
        { n: 67, name: 'الملك', v: 30 }, { n: 68, name: 'القلم', v: 52 }, { n: 69, name: 'الحاقة', v: 52 },
        { n: 70, name: 'المعارج', v: 44 }, { n: 71, name: 'نوح', v: 28 }, { n: 72, name: 'الجن', v: 28 },
        { n: 73, name: 'المزمل', v: 20 }, { n: 74, name: 'المدثر', v: 56 }, { n: 75, name: 'القيامة', v: 40 },
        { n: 76, name: 'الإنسان', v: 31 }, { n: 77, name: 'المرسلات', v: 50 }, { n: 78, name: 'النبأ', v: 40 },
        { n: 79, name: 'النازعات', v: 46 }, { n: 80, name: 'عبس', v: 42 }, { n: 81, name: 'التكوير', v: 29 },
        { n: 82, name: 'الانفطار', v: 19 }, { n: 83, name: 'المطففين', v: 36 }, { n: 84, name: 'الانشقاق', v: 25 },
        { n: 85, name: 'البروج', v: 22 }, { n: 86, name: 'الطارق', v: 17 }, { n: 87, name: 'الأعلى', v: 19 },
        { n: 88, name: 'الغاشية', v: 26 }, { n: 89, name: 'الفجر', v: 30 }, { n: 90, name: 'البلد', v: 20 },
        { n: 91, name: 'الشمس', v: 15 }, { n: 92, name: 'الليل', v: 21 }, { n: 93, name: 'الضحى', v: 11 },
        { n: 94, name: 'الشرح', v: 8 }, { n: 95, name: 'التين', v: 8 }, { n: 96, name: 'العلق', v: 19 },
        { n: 97, name: 'القدر', v: 5 }, { n: 98, name: 'البينة', v: 8 }, { n: 99, name: 'الزلزلة', v: 8 },
        { n: 100, name: 'العاديات', v: 11 }, { n: 101, name: 'القارعة', v: 11 }, { n: 102, name: 'التكاثر', v: 8 },
        { n: 103, name: 'العصر', v: 3 }, { n: 104, name: 'الهمزة', v: 9 }, { n: 105, name: 'الفيل', v: 5 },
        { n: 106, name: 'قريش', v: 4 }, { n: 107, name: 'الماعون', v: 7 }, { n: 108, name: 'الكوثر', v: 3 },
        { n: 109, name: 'الكافرون', v: 6 }, { n: 110, name: 'النصر', v: 3 }, { n: 111, name: 'المسد', v: 5 },
        { n: 112, name: 'الإخلاص', v: 4 }, { n: 113, name: 'الفلق', v: 5 }, { n: 114, name: 'الناس', v: 6 }
    ];

    function escapeHtml(str) {
        if (str == null) return '';
        return String(str)
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function formatLocalDateYmd(d) {
        const dt = d instanceof Date ? d : new Date();
        const y = dt.getFullYear();
        const m = String(dt.getMonth() + 1).padStart(2, '0');
        const day = String(dt.getDate()).padStart(2, '0');
        return `${y}-${m}-${day}`;
    }

    /** تواريخ بأرقام إنجليزية (2026) — en-GB */
    function formatDateEnGb(value, options) {
        if (!value) return '—';
        const d = value instanceof Date ? value : new Date(value);
        if (isNaN(d.getTime())) return '—';
        const opts = options || { year: 'numeric', month: 'short', day: 'numeric' };
        return d.toLocaleDateString('en-GB', { ...opts, numberingSystem: 'latn' });
    }

    function formatDateTimeEnGb(value) {
        if (!value) return '—';
        const d = value instanceof Date ? value : new Date(value);
        if (isNaN(d.getTime())) return '—';
        return d.toLocaleString('en-GB', {
            year: 'numeric', month: 'short', day: 'numeric',
            hour: '2-digit', minute: '2-digit', numberingSystem: 'latn'
        });
    }

    function surahOptionsHtml(selectedName) {
        return SURAHS.map(s => {
            const sel = s.name === selectedName ? ' selected' : '';
            return `<option value="${escapeHtml(s.name)}"${sel}>${s.n}. ${escapeHtml(s.name)}</option>`;
        }).join('');
    }

    function getSurahsInRange(fromName, toName) {
        const fromIdx = SURAHS.findIndex(s => s.name === fromName);
        const toIdx = SURAHS.findIndex(s => s.name === toName);
        if (fromIdx < 0 || toIdx < 0) return [];
        const start = Math.min(fromIdx, toIdx);
        const end = Math.max(fromIdx, toIdx);
        return SURAHS.slice(start, end + 1);
    }

    function parseVerseCount(verses) {
        if (!verses) return 0;
        const parts = String(verses).split('-');
        if (parts.length === 2) {
            const from = parseInt(parts[0], 10);
            const to = parseInt(parts[1], 10);
            if (!isNaN(from) && !isNaN(to) && to >= from) return to - from + 1;
        }
        const single = parseInt(verses, 10);
        return isNaN(single) ? 0 : single;
    }

    global.NoorUtils = {
        SURAHS,
        escapeHtml,
        formatLocalDateYmd,
        formatDateEnGb,
        formatDateTimeEnGb,
        surahOptionsHtml,
        getSurahsInRange,
        parseVerseCount
    };

    global.escapeHtml = escapeHtml;
    global.formatLocalDateYmd = formatLocalDateYmd;
    global.formatDateEnGb = formatDateEnGb;
    global.formatDateTimeEnGb = formatDateTimeEnGb;
    global.SURAHS = SURAHS;
})(typeof window !== 'undefined' ? window : global);
