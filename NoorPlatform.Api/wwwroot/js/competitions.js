/**
 * منصة نور — إدارة المسابقات القرآنية
 */
(function (global) {
    'use strict';

    const NoorCompetitions = {
        fetchCompetitions: async function () {
            try {
                const data = await apiFetch('/competitions');
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
                const d = new Date(comp.date);
                const today = new Date(); today.setHours(0, 0, 0, 0);
                const isActive = d.toDateString() === today.toDateString();
                const isUpcoming = d > today;
                
                let statusBadge = isActive ? '<span class="status-badge status-excellent">جارية الآن</span>' : 
                                 isUpcoming ? '<span class="status-badge status-good">قادمة</span>' : 
                                 '<span class="status-badge status-absent">منتهية</span>';

                const levelNames = { Internal: 'داخلية', Regional: 'إقليمية', National: 'وطنية' };
                const levelText = levelNames[comp.level] || comp.level || 'عام';

                return `
                <div class="stat-card" style="flex-direction:column;align-items:stretch;gap:12px;padding:20px;border-radius:16px;">
                    <div style="display:flex;justify-content:space-between;align-items:flex-start">
                        <div class="stat-icon" style="margin-bottom:0;background:var(--gradient);color:white;width:48px;height:48px;">
                            ${window.Icon ? window.Icon('award', { size: 24 }) : ''}
                        </div>
                        ${statusBadge}
                    </div>
                    <div style="margin-top:10px;">
                        <h3 style="font-size:18px;font-weight:800;margin-bottom:4px">${escapeHtml(comp.title) }</h3>
                        <p style="font-size:12px;color:var(--text-muted)">مستوى: ${escapeHtml(levelText)}</p>
                    </div>
                                        <div style="font-size:12px;background:var(--bg);padding:10px;border-radius:8px;display:flex;justify-content:space-between">
                        <span>${formatDateEnGb(comp.date)}</span>
                        <span>${comp.participantsCount} مشارك</span>
                    </div>
                    <div style="display:flex;gap:8px;margin-top:auto;padding-top:10px;">
                        <button class="btn btn-primary" style="flex:1;padding:8px;font-size:13px;" onclick="NoorCompetitions.showLeaderboard(${comp.id}, ${jsArg(comp.title || comp.name)})">
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
            const today = NoorUtils.formatLocalDateYmd(new Date());
            document.getElementById('compStartDate').value = today;
            openModal('addCompetitionModal');
        },

        saveCompetition: async function (e) {
            e.preventDefault();
            const name = document.getElementById('compName').value.trim();
            const startDate = document.getElementById('compStartDate').value;
            const level = document.getElementById('compLevel').value;
            const description = document.getElementById('compDesc').value.trim();
            const btn = document.querySelector('#addCompetitionModal .btn-primary');

            if (!name || !startDate) {
                showToast('يرجى تعبئة اسم المسابقة وتواريخها', 'warning');
                return;
            }

            try {
                if (global.setBtnLoading) global.setBtnLoading(btn, true);
                
                await apiFetch('/competitions', 'POST', {
                    title: name,
                    description: description || '',
                    date: startDate,
                    level: level
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
                const lb = await apiFetch(`/competitions/${id}/leaderboard`);
                const results = lb.leaderboard || [];
                
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
                                    <th>الحلقة</th>
                                    <th>الدرجة</th>
                                </tr>
                            </thead>
                            <tbody>
                                ${results.map((r, index) => {
                                    let rankTrophy = '';
                                    let rowStyle = '';
                                    
                                    if (index === 0) {
                                        rankTrophy = '1';
                                        rowStyle = 'background:rgba(251, 191, 36, 0.05);';
                                    } else if (index === 1) {
                                        rankTrophy = '2';
                                    } else if (index === 2) {
                                        rankTrophy = '3';
                                    }

                                    return `
                                    <tr style="${rowStyle}">
                                        <td>
                                            <div style="display:flex;align-items:center;gap:8px;font-weight:800;font-size:16px;">
                                                ${index + 1}
                                            </div>
                                        </td>
                                        <td><strong>${escapeHtml(r.studentName)}</strong></td>
                                        <td>${escapeHtml(r.circleName || '—') }</td>
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
