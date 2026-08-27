/**
 * منصة نور — إدارة المسابقات القرآنية
 */
(function (global) {
    'use strict';

    const NoorCompetitions = {
        fetchCompetitions: async function () {
            try {
                const data = await apiFetch('/competitions?pageNumber=1&pageSize=100');
                this.renderCompetitions(data.items || data);
            } catch (err) {
                console.error(err);
                if (typeof showToast === 'function') showToast('حدث خطأ أثناء جلب المسابقات', 'error');
            }
        },

        renderCompetitions: function (competitions) {
            const container = document.getElementById('competitionsGrid');
            if (!container) return;

            if (!competitions || competitions.length === 0) {
                container.innerHTML = '<p style="text-align:center;color:var(--text-muted);width:100%;grid-column:1/-1;">لا توجد مسابقات مسجلة حالياً</p>';
                return;
            }

            container.innerHTML = competitions.map(comp => {
                const isActive = new Date(comp.endDate) >= new Date() && new Date(comp.startDate) <= new Date();
                const isUpcoming = new Date(comp.startDate) > new Date();
                
                let statusBadge = isActive ? '<span class="status-badge status-excellent">جارية الآن</span>' : 
                                 isUpcoming ? '<span class="status-badge status-good">قادمة</span>' : 
                                 '<span class="status-badge status-absent">منتهية</span>';

                let levelText = 'عام';
                switch(comp.level) {
                    case 'Internal': 
                    case 0: levelText = 'داخلية'; break;
                    case 'Local': 
                    case 1: levelText = 'محلية'; break;
                    case 'National': 
                    case 2: levelText = 'وطنية'; break;
                    case 'International': 
                    case 3: levelText = 'دولية'; break;
                    default: if (typeof comp.level === 'string') levelText = comp.level;
                }

                return `
                <div class="stat-card" style="flex-direction:column;align-items:stretch;gap:12px;padding:20px;border-radius:16px;">
                    <div style="display:flex;justify-content:space-between;align-items:flex-start">
                        <div class="stat-icon" style="margin-bottom:0;background:var(--gradient);color:white;width:48px;height:48px;">
                            ${window.Icon ? window.Icon('award', { size: 24 }) : ''}
                        </div>
                        ${statusBadge}
                    </div>
                    <div style="margin-top:10px;">
                        <h3 style="font-size:18px;font-weight:800;margin-bottom:4px">${escapeHtml(comp.name)}</h3>
                        <p style="font-size:12px;color:var(--text-muted)">مستوى: ${escapeHtml(levelText)}</p>
                    </div>
                    <div style="display:flex;justify-content:space-between;font-size:12px;color:var(--text);background:var(--bg);padding:10px;border-radius:8px;">
                        <div>
                            <div style="color:var(--text-muted);margin-bottom:2px;font-size:10px">تاريخ البدء</div>
                            <div style="font-weight:700">${formatDateEnGb(comp.startDate)}</div>
                        </div>
                        <div style="text-align:left">
                            <div style="color:var(--text-muted);margin-bottom:2px;font-size:10px">تاريخ الانتهاء</div>
                            <div style="font-weight:700">${formatDateEnGb(comp.endDate)}</div>
                        </div>
                    </div>
                    <div style="display:flex;gap:8px;margin-top:auto;padding-top:10px;">
                        <button class="btn btn-primary" style="flex:1;padding:8px;font-size:13px;" onclick="NoorCompetitions.showLeaderboard(${comp.id}, '${escapeHtml(comp.name).replace(/'/g, "\\'")}')">
                            ${window.Icon ? window.Icon('bar-chart-2', { size: 14 }) : ''} التراتيب
                        </button>
                        ${global.USER?.role === 'Admin' ? 
                        `<button class="btn btn-outline btn-delete" style="padding:8px 12px" onclick="NoorCompetitions.deleteCompetition(${comp.id})">
                            ${window.Icon ? window.Icon('trash-2', { size: 14 }) : ''}
                        </button>` : ''}
                    </div>
                </div>`;
            }).join('');
        },

        openAddModal: function () {
            document.getElementById('addCompForm').reset();
            const today = new Date().toISOString().split('T')[0];
            document.getElementById('compStartDate').value = today;
            document.getElementById('compEndDate').value = today;
            openModal('addCompetitionModal');
        },

        saveCompetition: async function (e) {
            e.preventDefault();
            const name = document.getElementById('compName').value.trim();
            const startDate = document.getElementById('compStartDate').value;
            const endDate = document.getElementById('compEndDate').value;
            const level = document.getElementById('compLevel').value;
            const description = document.getElementById('compDesc').value.trim();
            const btn = document.querySelector('#addCompetitionModal .btn-primary');

            if (!name || !startDate || !endDate) {
                showToast('يرجى تعبئة اسم المسابقة وتواريخها', 'warning');
                return;
            }

            try {
                if (global.setBtnLoading) global.setBtnLoading(btn, true);
                
                await apiFetch('/competitions', 'POST', {
                    name: name,
                    description: description || null,
                    startDate: startDate,
                    endDate: endDate,
                    level: parseInt(level, 10)
                });
                
                closeModal('addCompetitionModal');
                showToast('تم إنشاء المسابقة بنجاح', 'success');
                NoorCompetitions.fetchCompetitions();
            } catch (err) {
                showToast(err.message || 'فشل حفظ المسابقة', 'error');
            } finally {
                if (global.setBtnLoading) global.setBtnLoading(btn, false);
            }
        },

        deleteCompetition: function (id) {
            global.confirmDelete('هل أنت متأكد من حذف هذه المسابقة نهائياً مع نتائجها؟', async () => {
                try {
                    await apiFetch(`/competitions/${id}`, 'DELETE');
                    showToast('تم حذف المسابقة بنجاح', 'success');
                    NoorCompetitions.fetchCompetitions();
                } catch (err) {
                    showToast(err.message, 'error');
                }
            });
        },

        showLeaderboard: async function (id, name) {
            try {
                const results = await apiFetch(`/competitions/${id}/leaderboard`);
                
                let html = '';
                if (!results || results.length === 0) {
                    html = '<div style="text-align:center;padding:40px;color:var(--text-muted)">لا توجد نتائج مسجلة لهذه المسابقة بعد</div>';
                } else {
                    html = `
                    <div style="background:var(--card);border-radius:var(--radius);overflow:hidden;border:1px solid var(--border)">
                        <table style="width:100%;border-collapse:collapse;">
                            <thead>
                                <tr>
                                    <th>الترتيب</th>
                                    <th>الطالب</th>
                                    <th>المحفظ</th>
                                    <th>الدرجة</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${results.map((r, index) => {
                                    let rankTrophy = '';
                                    let rowStyle = '';
                                    
                                    if (index === 0) {
                                        rankTrophy = '<span style="color:#fbbf24;font-size:18px">🏆</span>';
                                        rowStyle = 'background:rgba(251, 191, 36, 0.05);';
                                    } else if (index === 1) {
                                        rankTrophy = '<span style="color:#94a3b8;font-size:18px">🥈</span>';
                                    } else if (index === 2) {
                                        rankTrophy = '<span style="color:#b45309;font-size:18px">🥉</span>';
                                    }

                                    return `
                                    <tr style="${rowStyle}">
                                        <td>
                                            <div style="display:flex;align-items:center;gap:8px;font-weight:800;font-size:16px;">
                                                ${index + 1} ${rankTrophy}
                                            </div>
                                        </td>
                                        <td><strong>${escapeHtml(r.studentName)}</strong></td>
                                        <td>${escapeHtml(r.teacherName || '—')}</td>
                                        <td>
                                            <span class="status-badge status-excellent" style="font-size:14px;padding:4px 12px">${r.totalScore.toFixed(2)}</span>
                                        </td>
                                    </tr>`;
                                }).join('')}
                            </tbody>
                        </table>
                    </div>`;
                }

                if (setModalBody('leaderboardBody', html, (window.Icon ? window.Icon('award', { size: 20 }) : '') + ' تراتيب: ' + escapeHtml(name))) {
                    openModal('leaderboardModal');
                }
            } catch (err) {
                console.error(err);
                showToast('حدث خطأ في جلب التراتيب', 'error');
            }
        }
    };

    global.NoorCompetitions = NoorCompetitions;

})(window);
