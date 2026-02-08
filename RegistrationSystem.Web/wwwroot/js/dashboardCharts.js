// ═══════════════════════════════════════════════════════════════════════════════
// Dashboard Bar Charts
// Renders responsive bar charts on canvas elements that fill their container
// ═══════════════════════════════════════════════════════════════════════════════

window.dashboardCharts = {
    /**
     * Render a bar chart on a canvas element.
     * @param {string} canvasId - The id of the canvas element
     * @param {Array} data - Array of { label: string, value: number }
     * @param {string} color - Fill color for bars (hex)
     * @param {string} hoverColor - Hover fill color (hex)
     */
    renderBarChart: function (canvasId, data, color, hoverColor) {
        const canvas = document.getElementById(canvasId);
        if (!canvas || !data || data.length === 0) return;

        const container = canvas.parentElement;
        const dpr = window.devicePixelRatio || 1;

        // Size canvas to fill container
        const width = container.clientWidth;
        const height = 180;
        canvas.width = width * dpr;
        canvas.height = height * dpr;
        canvas.style.width = width + 'px';
        canvas.style.height = height + 'px';

        const ctx = canvas.getContext('2d');
        ctx.scale(dpr, dpr);

        const maxVal = Math.max(...data.map(d => d.value), 1);
        const gap = Math.max(1, Math.min(3, Math.floor(width / data.length * 0.1)));
        const barW = Math.max(2, (width - gap * (data.length - 1)) / data.length);
        const chartH = height - 24; // leave room for date labels

        // Store bar rects for tooltip
        const bars = [];

        // Draw bars
        ctx.fillStyle = color;
        for (let i = 0; i < data.length; i++) {
            const x = i * (barW + gap);
            const barH = maxVal > 0 ? (data[i].value / maxVal) * chartH : 0;
            const y = chartH - barH;
            const radius = Math.min(3, barW / 2);

            // Rounded top rect
            ctx.beginPath();
            if (barH > radius * 2) {
                ctx.moveTo(x, y + radius);
                ctx.arcTo(x, y, x + barW, y, radius);
                ctx.arcTo(x + barW, y, x + barW, y + barH, radius);
                ctx.lineTo(x + barW, chartH);
                ctx.lineTo(x, chartH);
            } else if (barH > 0) {
                ctx.rect(x, y, barW, barH);
            }
            ctx.closePath();
            ctx.fill();

            bars.push({ x, y, w: barW, h: barH, label: data[i].label, value: data[i].value });
        }

        // Draw date labels (first and last)
        ctx.fillStyle = '#94a3b8'; // slate-400
        ctx.font = '11px system-ui, -apple-system, sans-serif';
        ctx.textBaseline = 'top';
        if (data.length > 0) {
            ctx.textAlign = 'left';
            ctx.fillText(data[0].label, 0, chartH + 6);
            ctx.textAlign = 'right';
            ctx.fillText(data[data.length - 1].label, width, chartH + 6);
        }

        // Tooltip on hover
        let tooltip = null;
        const showTooltip = (e) => {
            const rect = canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            const my = e.clientY - rect.top;

            let hit = null;
            for (const bar of bars) {
                if (mx >= bar.x && mx <= bar.x + bar.w && my >= bar.y && my <= chartH) {
                    hit = bar;
                    break;
                }
            }

            if (!tooltip) {
                tooltip = document.createElement('div');
                tooltip.className = 'fixed z-50 px-2.5 py-1.5 bg-slate-800 text-white text-xs rounded-lg shadow-lg pointer-events-none whitespace-nowrap';
                document.body.appendChild(tooltip);
            }

            if (hit) {
                canvas.style.cursor = 'pointer';
                tooltip.textContent = `${hit.label}: ${hit.value}`;
                tooltip.style.display = 'block';
                tooltip.style.left = (e.clientX + 12) + 'px';
                tooltip.style.top = (e.clientY - 30) + 'px';

                // Redraw with highlight
                ctx.clearRect(0, 0, width, chartH);
                for (const bar of bars) {
                    ctx.fillStyle = bar === hit ? hoverColor : color;
                    ctx.beginPath();
                    const radius = Math.min(3, bar.w / 2);
                    if (bar.h > radius * 2) {
                        ctx.moveTo(bar.x, bar.y + radius);
                        ctx.arcTo(bar.x, bar.y, bar.x + bar.w, bar.y, radius);
                        ctx.arcTo(bar.x + bar.w, bar.y, bar.x + bar.w, bar.y + bar.h, radius);
                        ctx.lineTo(bar.x + bar.w, chartH);
                        ctx.lineTo(bar.x, chartH);
                    } else if (bar.h > 0) {
                        ctx.rect(bar.x, bar.y, bar.w, bar.h);
                    }
                    ctx.closePath();
                    ctx.fill();
                }
            } else {
                canvas.style.cursor = 'default';
                tooltip.style.display = 'none';
            }
        };

        const hideTooltip = () => {
            if (tooltip) {
                tooltip.style.display = 'none';
            }
            canvas.style.cursor = 'default';
            // Redraw without highlight
            ctx.clearRect(0, 0, width, chartH);
            ctx.fillStyle = color;
            for (const bar of bars) {
                ctx.beginPath();
                const radius = Math.min(3, bar.w / 2);
                if (bar.h > radius * 2) {
                    ctx.moveTo(bar.x, bar.y + radius);
                    ctx.arcTo(bar.x, bar.y, bar.x + bar.w, bar.y, radius);
                    ctx.arcTo(bar.x + bar.w, bar.y, bar.x + bar.w, bar.y + bar.h, radius);
                    ctx.lineTo(bar.x + bar.w, chartH);
                    ctx.lineTo(bar.x, chartH);
                } else if (bar.h > 0) {
                    ctx.rect(bar.x, bar.y, bar.w, bar.h);
                }
                ctx.closePath();
                ctx.fill();
            }
        };

        // Clean up old listeners
        canvas._cleanup?.();
        canvas.addEventListener('mousemove', showTooltip);
        canvas.addEventListener('mouseleave', hideTooltip);
        canvas._cleanup = () => {
            canvas.removeEventListener('mousemove', showTooltip);
            canvas.removeEventListener('mouseleave', hideTooltip);
            if (tooltip && tooltip.parentElement) {
                tooltip.parentElement.removeChild(tooltip);
            }
        };
    }
};
