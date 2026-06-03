/**
 * تحميل المكتبات الخارجية مع fallback محلي عند انقطاع الإنترنت
 */
(function (global) {
    function loadScript(src, opts) {
        opts = opts || {};
        return new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = src;
            if (opts.defer) s.defer = true;
            s.onload = function () { resolve(src); };
            s.onerror = function () { reject(new Error('Failed: ' + src)); };
            document.head.appendChild(s);
        });
    }

    global.loadHtml2Pdf = function () {
        if (typeof global.html2pdf !== 'undefined') {
            return Promise.resolve();
        }
        return loadScript('https://cdnjs.cloudflare.com/ajax/libs/html2pdf.js/0.10.1/html2pdf.bundle.min.js', { defer: true })
            .catch(function () {
                return loadScript('/lib/html2pdf.bundle.min.js', { defer: true });
            });
    };

    global.ensureHtml2Pdf = async function () {
        if (typeof global.html2pdf !== 'undefined') return true;
        try {
            await global.loadHtml2Pdf();
            await new Promise(function (r) {
                if (typeof global.html2pdf !== 'undefined') return r();
                var n = 0;
                var t = setInterval(function () {
                    n++;
                    if (typeof global.html2pdf !== 'undefined' || n > 50) {
                        clearInterval(t);
                        r();
                    }
                }, 100);
            });
            return typeof global.html2pdf !== 'undefined';
        } catch (e) {
            console.warn('html2pdf unavailable', e);
            return false;
        }
    };
})(window);
