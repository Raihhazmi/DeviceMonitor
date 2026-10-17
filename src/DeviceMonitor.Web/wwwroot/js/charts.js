/* ==========================================================================
   charts.js - engine grafik canvas ringan (tanpa library eksternal)
   Mendukung: bar, hbar, donut, line. Sadar tema (dark/light), responsif,
   tooltip interaktif, dan dirender ulang saat tema atau ukuran berubah.
   ========================================================================== */
(function () {
  'use strict';

  var instances = [];

  function themeVar(name, fallback) {
    var v = getComputedStyle(document.documentElement).getPropertyValue(name);
    return (v && v.trim()) || fallback;
  }

  function palette(i) {
    var colors = ['#4f8cff', '#22c9a5', '#f5a524', '#ef4d6f', '#a06bff', '#37c6f4',
      '#f47b3b', '#8bd450', '#e86bd0', '#6b7fd7', '#00b8a9', '#ff7a59',
      '#9aa5b1', '#c9a227', '#5d8bf4'];
    return colors[Math.abs(i) % colors.length];
  }

  function fmtNum(v, suffix) {
    if (v === null || v === undefined || isNaN(v)) return '-';
    var n = Math.abs(v) >= 1000 ? v.toLocaleString('id-ID', { maximumFractionDigits: 0 })
      : v.toLocaleString('id-ID', { maximumFractionDigits: 2 });
    return n + (suffix || '');
  }

  function niceMax(v) {
    if (!v || v <= 0) return 1;
    var exp = Math.pow(10, Math.floor(Math.log10(v)));
    var f = v / exp;
    var nice = f <= 1 ? 1 : f <= 1.5 ? 1.5 : f <= 2 ? 2 : f <= 2.5 ? 2.5 : f <= 3 ? 3 : f <= 4 ? 4 : f <= 5 ? 5 : f <= 7.5 ? 7.5 : 10;
    return nice * exp;
  }

  function truncate(s, max) {
    s = String(s == null ? '' : s);
    return s.length > max ? s.slice(0, max - 1) + '\u2026' : s;
  }

  /* ------------------------------------------------------------------ Chart */

  function Chart(el, cfg) {
    this.el = el;
    this.cfg = cfg || {};
    this.type = (cfg.type || 'bar').toLowerCase();
    this.canvas = document.createElement('canvas');
    this.canvas.style.width = '100%';
    this.canvas.style.height = (cfg.height || 230) + 'px';
    el.innerHTML = '';
    el.appendChild(this.canvas);
    this.legend = document.createElement('div');
    this.legend.className = 'chart-legend';
    el.appendChild(this.legend);
    this.ctx = this.canvas.getContext('2d');
    this.hit = [];
    this.attachEvents();
    this.render();
  }

  Chart.prototype.data = function () {
    var c = this.cfg;
    var labels = c.labels || [];
    var series = (c.series && c.series.length ? c.series : [{ name: 'Nilai', data: [] }]);
    var colors = c.sliceColors && c.sliceColors.length ? c.sliceColors : null;
    return { labels: labels, series: series, colors: colors, meta: c.meta || [] };
  };

  Chart.prototype.isEmpty = function () {
    var d = this.data();
    var total = d.series.reduce(function (a, s) { return a + (s.data || []).reduce(function (x, y) { return x + (y || 0); }, 0); }, 0);
    return d.labels.length === 0 || total === 0;
  };

  Chart.prototype.render = function () {
    var self = this;
    var el = this.el;
    if (this.isEmpty()) {
      el.innerHTML = '<div class="chart-empty">Belum ada data untuk ditampilkan.</div>';
      return;
    }
    if (!el.contains(this.canvas)) {
      el.innerHTML = '';
      el.appendChild(this.canvas);
      el.appendChild(this.legend);
    }

    var dpr = window.devicePixelRatio || 1;
    var w = el.clientWidth || 600;
    var h = this.cfg.height || 230;
    this.canvas.width = Math.max(1, Math.round(w * dpr));
    this.canvas.height = Math.max(1, Math.round(h * dpr));
    this.canvas.style.height = h + 'px';

    var ctx = this.ctx;
    ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    ctx.clearRect(0, 0, w, h);

    this.colors = {
      text: themeVar('--text-muted', '#93a0b8'),
      dim: themeVar('--text-dim', '#6b7891'),
      grid: themeVar('--grid-line', 'rgba(255,255,255,.06)'),
      surface: themeVar('--surface-3', '#1e2941'),
      accent: themeVar('--accent', '#4f8cff'),
      good: themeVar('--good', '#22c9a5'),
      warn: themeVar('--warn', '#f5a524'),
      bad: themeVar('--bad', '#ef4d6f')
    };

    this.hit = [];
    if (this.type === 'donut' || this.type === 'pie') this.drawDonut(ctx, w, h);
    else if (this.type === 'hbar') this.drawHBar(ctx, w, h);
    else if (this.type === 'line' || this.type === 'area') this.drawLine(ctx, w, h);
    else this.drawBar(ctx, w, h);

    this.drawLegend();
  };

  /* --------------------------------------------------------------- bar */

  Chart.prototype.drawBar = function (ctx, w, h) {
    var d = this.data(), c = this.colors, suffix = this.cfg.valueSuffix || '';
    var padL = 42, padR = 10, padT = 12, padB = this.cfg.labelAngle === false ? 22 : 52;
    var labels = d.labels, values = (d.series[0] && d.series[0].data) || [];
    var max = niceMax(Math.max.apply(null, values.concat([1])) * 1.08);
    var plotW = w - padL - padR, plotH = h - padT - padB;

    // grid + axis Y
    ctx.font = '10.5px ' + monoFont();
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    for (var i = 0; i <= 4; i++) {
      var y = padT + plotH - (plotH * i / 4);
      ctx.strokeStyle = c.grid; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(padL, y); ctx.lineTo(w - padR, y); ctx.stroke();
      ctx.fillStyle = c.dim;
      ctx.fillText(shortNum(max * i / 4), padL - 7, y);
    }

    var n = labels.length;
    var slot = plotW / Math.max(1, n);
    var bw = Math.min(this.cfg.barMax || 46, slot * 0.62);

    ctx.textAlign = 'center'; ctx.textBaseline = 'top';
    for (var j = 0; j < n; j++) {
      var v = values[j] || 0;
      var bh = plotH * (v / max);
      var x = padL + slot * j + (slot - bw) / 2;
      var yb = padT + plotH - bh;
      var color = (d.colors && d.colors[j]) || palette(j);

      var g = ctx.createLinearGradient(0, yb, 0, padT + plotH);
      g.addColorStop(0, color);
      g.addColorStop(1, hexA(color, .55));
      ctx.fillStyle = g;
      roundRect(ctx, x, yb, bw, Math.max(bh, 1.5), Math.min(5, bw / 3));
      ctx.fill();

      if (bh > 16) {
        ctx.fillStyle = c.text; ctx.font = '600 10.5px ' + monoFont();
        ctx.fillText(shortNum(v), x + bw / 2, yb - 14);
      }

      // label X (miring bila sempit)
      ctx.font = '10.5px ' + baseFont();
      ctx.fillStyle = c.dim;
      var lx = x + bw / 2, ly = padT + plotH + 8;
      if (slot < 58) {
        ctx.save(); ctx.translate(lx, ly); ctx.rotate(-Math.PI / 5.2);
        ctx.textAlign = 'right'; ctx.fillText(truncate(labels[j], 18), 0, 0); ctx.restore();
        ctx.textAlign = 'center';
      } else {
        wrapText(ctx, truncate(labels[j], 20), lx, ly, slot - 4, 12);
      }

      this.hit.push({ x: x - 3, y: padT, w: bw + 6, h: plotH, title: labels[j], value: fmtNum(v, suffix), meta: (d.meta && d.meta[j]) || '' });
    }
  };

  /* --------------------------------------------------------------- hbar */

  Chart.prototype.drawHBar = function (ctx, w, h) {
    var d = this.data(), c = this.colors, suffix = this.cfg.valueSuffix || '';
    var labels = d.labels, values = (d.series[0] && d.series[0].data) || [];
    var max = Math.max.apply(null, values.concat([1]));

    ctx.font = '11px ' + baseFont();
    var labelW = 0;
    labels.forEach(function (l) { labelW = Math.max(labelW, ctx.measureText(truncate(l, 24)).width); });
    labelW = Math.min(Math.max(labelW + 12, 78), w * 0.44);
    ctx.font = '600 11px ' + monoFont();
    var valueW = 0;
    values.forEach(function (v) { valueW = Math.max(valueW, ctx.measureText(shortNum(v) + suffix).width); });
    var padR = Math.min(Math.max(valueW + 14, 46), w * 0.3), rowH = h / Math.max(1, labels.length);
    var barH = Math.min(15, rowH * 0.56);

    for (var i = 0; i < labels.length; i++) {
      var y = rowH * i + (rowH - barH) / 2;
      var v = values[i] || 0;
      var bw = Math.max(2, (w - labelW - padR) * (v / max));
      var color = (d.colors && d.colors[i]) || palette(i);

      ctx.fillStyle = c.text; ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
      ctx.font = '11px ' + baseFont();
      ctx.fillText(truncate(labels[i], 26), labelW - 10, y + barH / 2);

      ctx.fillStyle = c.surface;
      roundRect(ctx, labelW, y, w - labelW - padR, barH, barH / 2); ctx.fill();

      var g = ctx.createLinearGradient(labelW, 0, labelW + bw, 0);
      g.addColorStop(0, hexA(color, .65)); g.addColorStop(1, color);
      ctx.fillStyle = g;
      roundRect(ctx, labelW, y, bw, barH, barH / 2); ctx.fill();

      ctx.fillStyle = c.text; ctx.textAlign = 'left'; ctx.font = '600 11px ' + monoFont();
      ctx.fillText(shortNum(v) + suffix, labelW + bw + 8, y + barH / 2);

      this.hit.push({ x: labelW, y: rowH * i, w: w - labelW - padR, h: rowH, title: labels[i], value: fmtNum(v, suffix), meta: (d.meta && d.meta[i]) || '' });
    }
  };

  /* --------------------------------------------------------------- donut */

  Chart.prototype.drawDonut = function (ctx, w, h) {
    var d = this.data(), c = this.colors, suffix = this.cfg.valueSuffix || '';
    var values = (d.series[0] && d.series[0].data) || [];
    var total = values.reduce(function (a, b) { return a + b; }, 0);
    var legendW = this.cfg.legend === false ? 0 : Math.min(210, w * 0.42);
    var cx = (w - legendW) / 2, cy = h / 2;
    var R = Math.max(28, Math.min((h - 16) / 2, (w - legendW) / 2 - 8));
    var r = R * 0.615;
    var a0 = -Math.PI / 2;

    for (var i = 0; i < values.length; i++) {
      var frac = total > 0 ? values[i] / total : 0;
      var a1 = a0 + frac * Math.PI * 2;
      var color = (d.colors && d.colors[i]) || palette(i);
      ctx.beginPath();
      ctx.moveTo(cx, cy);
      ctx.arc(cx, cy, R, a0, a1);
      ctx.closePath();
      ctx.fillStyle = color;
      ctx.fill();
      ctx.strokeStyle = themeVar('--surface', '#131c31');
      ctx.lineWidth = 2;
      ctx.stroke();

      var mid = (a0 + a1) / 2;
      this.hit.push({
        arc: true, cx: cx, cy: cy, r0: r, r1: R, a0: a0, a1: a1,
        title: d.labels[i], value: fmtNum(values[i], suffix),
        meta: (total > 0 ? (100 * values[i] / total).toFixed(1) : '0') + '% dari total'
      });
      a0 = a1;
    }

    // lubang tengah
    ctx.beginPath(); ctx.arc(cx, cy, r, 0, Math.PI * 2);
    ctx.fillStyle = themeVar('--surface', '#131c31'); ctx.fill();

    ctx.textAlign = 'center';
    ctx.fillStyle = c.text; ctx.font = '700 19px ' + baseFont();
    ctx.textBaseline = 'alphabetic';
    ctx.fillText(shortNum(total), cx, cy + 1);
    ctx.fillStyle = c.dim; ctx.font = '10.5px ' + baseFont();
    ctx.fillText(this.cfg.centerLabel || 'TOTAL', cx, cy + 17);

    // legenda kanan
    if (legendW > 0) {
      var lx = w - legendW + 6;
      var lh = Math.min(19, (h - 10) / Math.max(1, values.length));
      var startY = cy - (values.length * lh) / 2 + lh / 2;
      ctx.textAlign = 'left'; ctx.textBaseline = 'middle';
      for (var k = 0; k < values.length; k++) {
        var yy = startY + k * lh;
        if (k > 11) { ctx.fillStyle = c.dim; ctx.font = '10.5px ' + baseFont(); ctx.fillText('+' + (values.length - k) + ' lainnya', lx, yy); break; }
        ctx.fillStyle = (d.colors && d.colors[k]) || palette(k);
        roundRect(ctx, lx, yy - 4, 8, 8, 2); ctx.fill();
        ctx.fillStyle = c.text; ctx.font = '11px ' + baseFont();
        ctx.fillText(truncate(d.labels[k], 21), lx + 14, yy);
        ctx.fillStyle = c.dim; ctx.font = '600 10.5px ' + monoFont();
        ctx.textAlign = 'right';
        ctx.fillText(shortNum(values[k]), w - 4, yy);
        ctx.textAlign = 'left';
      }
    }
  };

  /* --------------------------------------------------------------- line */

  Chart.prototype.drawLine = function (ctx, w, h) {
    var d = this.data(), c = this.colors;
    var padL = 44, padR = 12, padT = 14, padB = 30;
    var plotW = w - padL - padR, plotH = h - padT - padB;
    var n = d.labels.length;
    var max = 0;
    d.series.forEach(function (s) { (s.data || []).forEach(function (v) { if (v > max) max = v; }); });
    max = niceMax(max * 1.12 || 1);

    ctx.font = '10.5px ' + monoFont();
    ctx.textAlign = 'right'; ctx.textBaseline = 'middle';
    for (var i = 0; i <= 4; i++) {
      var y = padT + plotH - plotH * i / 4;
      ctx.strokeStyle = c.grid; ctx.lineWidth = 1;
      ctx.beginPath(); ctx.moveTo(padL, y); ctx.lineTo(w - padR, y); ctx.stroke();
      ctx.fillStyle = c.dim; ctx.fillText(shortNum(max * i / 4), padL - 7, y);
    }

    ctx.textAlign = 'center'; ctx.textBaseline = 'top'; ctx.font = '10.5px ' + baseFont();
    var stepLbl = Math.max(1, Math.ceil(n / Math.max(3, Math.floor(plotW / 58))));
    for (var j = 0; j < n; j++) {
      if (j % stepLbl !== 0 && j !== n - 1) continue;
      ctx.fillStyle = c.dim;
      ctx.fillText(truncate(d.labels[j], 8), padL + (plotW * j / Math.max(1, n - 1)), padT + plotH + 8);
    }

    var self = this;
    d.series.forEach(function (s, si) {
      var color = s.color || palette(si);
      var pts = (s.data || []).map(function (v, idx) {
        return {
          x: padL + (plotW * idx / Math.max(1, n - 1 || 1)),
          y: padT + plotH - plotH * ((v || 0) / max),
          v: v || 0
        };
      });

      if (s.fill) {
        var g = ctx.createLinearGradient(0, padT, 0, padT + plotH);
        g.addColorStop(0, hexA(color, .34)); g.addColorStop(1, hexA(color, .02));
        ctx.beginPath();
        ctx.moveTo(pts.length ? pts[0].x : padL, padT + plotH);
        pts.forEach(function (p) { ctx.lineTo(p.x, p.y); });
        ctx.lineTo(pts.length ? pts[pts.length - 1].x : padL, padT + plotH);
        ctx.closePath(); ctx.fillStyle = g; ctx.fill();
      }

      ctx.beginPath();
      pts.forEach(function (p, idx) { idx === 0 ? ctx.moveTo(p.x, p.y) : ctx.lineTo(p.x, p.y); });
      ctx.strokeStyle = color; ctx.lineWidth = 2; ctx.lineJoin = 'round'; ctx.lineCap = 'round';
      ctx.stroke();

      if (pts.length <= 40) {
        pts.forEach(function (p) {
          ctx.beginPath(); ctx.arc(p.x, p.y, 2.4, 0, Math.PI * 2);
          ctx.fillStyle = color; ctx.fill();
        });
      }

      pts.forEach(function (p, idx) {
        self.hit.push({ x: p.x - 6, y: padT, w: 12, h: plotH, title: d.labels[idx], value: fmtNum(p.v, self.cfg.valueSuffix || ''), meta: s.name, seriesIndex: si });
      });
    });
  };

  /* --------------------------------------------------------------- legend */

  Chart.prototype.drawLegend = function () {
    var d = this.data();
    var html = '';
    if (this.type === 'donut' || this.type === 'pie') {
      if (this.cfg.legend !== false) this.legend.innerHTML = '';
      return;
    }
    if (this.type === 'line' || this.type === 'area') {
      d.series.forEach(function (s, i) {
        html += '<span><i style="background:' + (s.color || palette(i)) + '"></i>' + escapeHtml(s.name) + '</span>';
      });
    } else if (this.type === 'bar' && d.labels.length <= 12) {
      d.labels.forEach(function (l, i) {
        html += '<span><i style="background:' + ((d.colors && d.colors[i]) || palette(i)) + '"></i>' + escapeHtml(truncate(l, 22)) + '</span>';
      });
    }
    this.legend.innerHTML = html;
  };

  /* --------------------------------------------------------------- events */

  Chart.prototype.attachEvents = function () {
    var self = this;
    var tip = ensureTip();

    this.canvas.addEventListener('mousemove', function (e) {
      var rect = self.canvas.getBoundingClientRect();
      var mx = e.clientX - rect.left, my = e.clientY - rect.top;
      var found = null;
      for (var i = 0; i < self.hit.length; i++) {
        var h = self.hit[i];
        if (h.arc) {
          var dx = mx - h.cx, dy = my - h.cy;
          var dist = Math.sqrt(dx * dx + dy * dy);
          if (dist < h.r0 || dist > h.r1) continue;
          var ang = Math.atan2(dy, dx);
          while (ang < -Math.PI / 2) ang += Math.PI * 2;
          if (ang >= h.a0 && ang <= h.a1) { found = h; break; }
        } else if (mx >= h.x && mx <= h.x + h.w && my >= h.y && my <= h.y + h.h) { found = h; break; }
      }
      if (found) {
        tip.innerHTML = '<b>' + escapeHtml(found.title) + '</b>' + escapeHtml(found.value) +
          (found.meta ? '<span style="opacity:.7"> · ' + escapeHtml(found.meta) + '</span>' : '');
        tip.style.opacity = '1';
        var tx = e.clientX + 14, ty = e.clientY + 14;
        if (tx + tip.offsetWidth > window.innerWidth - 8) tx = e.clientX - tip.offsetWidth - 12;
        if (ty + tip.offsetHeight > window.innerHeight - 8) ty = e.clientY - tip.offsetHeight - 12;
        tip.style.left = tx + 'px'; tip.style.top = ty + 'px';
      } else {
        tip.style.opacity = '0';
      }
    });

    this.canvas.addEventListener('mouseleave', function () { tip.style.opacity = '0'; });
  };

  var tipEl = null;
  function ensureTip() {
    if (!tipEl) {
      tipEl = document.createElement('div');
      tipEl.className = 'chart-tip';
      document.body.appendChild(tipEl);
    }
    return tipEl;
  }

  /* --------------------------------------------------------------- helpers */

  function baseFont() { return '"Segoe UI", system-ui, -apple-system, Arial, sans-serif'; }
  function monoFont() { return '"Cascadia Mono", Consolas, "SF Mono", Menlo, monospace'; }

  function shortNum(v) {
    v = Number(v) || 0;
    var a = Math.abs(v);
    if (a >= 1e9) return (v / 1e9).toFixed(1) + 'B';
    if (a >= 1e6) return (v / 1e6).toFixed(1) + 'M';
    if (a >= 1e4) return (v / 1e3).toFixed(1) + 'k';
    if (a >= 100) return Math.round(v).toString();
    if (a >= 1) return (Math.round(v * 10) / 10).toString();
    return (Math.round(v * 100) / 100).toString();
  }

  function hexA(hex, alpha) {
    if (!hex || hex.charAt(0) !== '#') return 'rgba(79,140,255,' + alpha + ')';
    var h = hex.replace('#', '');
    if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
    var r = parseInt(h.slice(0, 2), 16), g = parseInt(h.slice(2, 4), 16), b = parseInt(h.slice(4, 6), 16);
    return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
  }

  function roundRect(ctx, x, y, w, h, r) {
    r = Math.min(r, Math.abs(w) / 2, Math.abs(h) / 2);
    ctx.beginPath();
    ctx.moveTo(x + r, y);
    ctx.lineTo(x + w - r, y); ctx.quadraticCurveTo(x + w, y, x + w, y + r);
    ctx.lineTo(x + w, y + h - r); ctx.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
    ctx.lineTo(x + r, y + h); ctx.quadraticCurveTo(x, y + h, x, y + h - r);
    ctx.lineTo(x, y + r); ctx.quadraticCurveTo(x, y, x + r, y);
    ctx.closePath();
  }

  function wrapText(ctx, text, x, y, maxW, lineH) {
    var words = String(text).split(/\s+/), line = '', lines = [];
    words.forEach(function (wd) {
      var test = line ? line + ' ' + wd : wd;
      if (ctx.measureText(test).width > maxW && line) { lines.push(line); line = wd; }
      else line = test;
    });
    if (line) lines.push(line);
    lines.slice(0, 2).forEach(function (l, i) { ctx.fillText(l, x, y + i * lineH); });
  }

  function escapeHtml(s) {
    return String(s == null ? '' : s).replace(/[&<>"']/g, function (m) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[m];
    });
  }

  /* --------------------------------------------------------------- boot */

  function initAll() {
    document.querySelectorAll('[data-chart]').forEach(function (el) {
      if (el.__chart) { el.__chart.render(); return; }
      var node = document.getElementById(el.getAttribute('data-chart'));
      if (!node) return;
      var cfg;
      try { cfg = JSON.parse(node.textContent); } catch (err) { console.error('chart config invalid', el, err); return; }
      cfg.height = cfg.height || Number(el.getAttribute('data-height')) || 230;
      cfg.type = cfg.type || el.getAttribute('data-type') || 'bar';
      cfg.centerLabel = cfg.centerLabel || el.getAttribute('data-center');
      var ch = new Chart(el, cfg);
      el.__chart = ch;
      instances.push(ch);
    });
  }

  function renderAll() { instances.forEach(function (c) { try { c.render(); } catch (e) { } }); }

  var rt;
  window.addEventListener('resize', function () { clearTimeout(rt); rt = setTimeout(renderAll, 140); });

  /* --------------------------------------------------------------- theme */

  function applyTheme(t) {
    document.documentElement.setAttribute('data-theme', t);
    try { localStorage.setItem('dm-theme', t); } catch (e) { }
    document.querySelectorAll('[data-theme-btn]').forEach(function (b) {
      b.classList.toggle('on', b.getAttribute('data-theme-btn') === t);
    });
    requestAnimationFrame(renderAll);
  }

  function initTheme() {
    var saved = null;
    try { saved = localStorage.getItem('dm-theme'); } catch (e) { }
    applyTheme(saved === 'light' ? 'light' : (saved === 'dark' ? 'dark' : 'dark'));
    document.querySelectorAll('[data-theme-btn]').forEach(function (b) {
      b.addEventListener('click', function () { applyTheme(b.getAttribute('data-theme-btn')); });
    });
  }

  /* --------------------------------------------------------------- app ui */

  function initUi() {
    // sidebar mobile
    var burger = document.querySelector('[data-burger]');
    var sidebar = document.querySelector('.sidebar');
    if (burger && sidebar) burger.addEventListener('click', function () { sidebar.classList.toggle('open'); });

    // filter tabel client-side
    document.querySelectorAll('[data-table-filter]').forEach(function (input) {
      var table = document.querySelector(input.getAttribute('data-table-filter'));
      if (!table) return;
      input.addEventListener('input', function () {
        var q = input.value.toLowerCase().trim();
        table.querySelectorAll('tbody tr').forEach(function (tr) {
          tr.style.display = !q || tr.textContent.toLowerCase().indexOf(q) >= 0 ? '' : 'none';
        });
        var shown = Array.prototype.filter.call(table.querySelectorAll('tbody tr'), function (tr) { return tr.style.display !== 'none'; }).length;
        var counter = document.querySelector(input.getAttribute('data-table-counter') || '');
        if (counter) counter.textContent = shown + ' baris';
      });
    });

    // sorting tabel
    document.querySelectorAll('table[data-sortable] th[data-sort]').forEach(function (th) {
      th.style.cursor = 'pointer';
      th.title = 'Klik untuk urutkan';
      th.addEventListener('click', function () {
        var table = th.closest('table');
        var idx = Array.prototype.indexOf.call(th.parentNode.children, th);
        var asc = th.getAttribute('data-dir') !== 'asc';
        table.querySelectorAll('th[data-sort]').forEach(function (o) { o.removeAttribute('data-dir'); o.classList.remove('sort-asc', 'sort-desc'); });
        th.setAttribute('data-dir', asc ? 'asc' : 'desc');
        th.classList.add(asc ? 'sort-asc' : 'sort-desc');
        var rows = Array.prototype.slice.call(table.querySelectorAll('tbody tr'));
        rows.sort(function (a, b) {
          var x = a.children[idx], y = b.children[idx];
          if (!x || !y) return 0;
          var vx = (x.getAttribute('data-v') !== null ? x.getAttribute('data-v') : x.textContent).trim();
          var vy = (y.getAttribute('data-v') !== null ? y.getAttribute('data-v') : y.textContent).trim();
          var nx = parseFloat(String(vx).replace(/[^0-9.\-]/g, '')), ny = parseFloat(String(vy).replace(/[^0-9.\-]/g, ''));
          var bothNum = !isNaN(nx) && !isNaN(ny) && String(vx).length && String(vy).length;
          var r = bothNum ? nx - ny : String(vx).localeCompare(String(vy), 'id');
          return asc ? r : -r;
        });
        var tb = table.querySelector('tbody');
        rows.forEach(function (r) { tb.appendChild(r); });
      });
    });

    // auto refresh
    var auto = document.querySelector('[data-auto-refresh]');
    if (auto) {
      var on = false;
      try { on = localStorage.getItem('dm-auto') === '1'; } catch (e) { }
      auto.checked = on;
      var timer = null;
      var tick = function () {
        if (!auto.checked) return;
        var y = window.scrollY;
        window.location.reload();
        setTimeout(function () { window.scrollTo(0, y); }, 40);
      };
      var sync = function () {
        clearInterval(timer);
        try { localStorage.setItem('dm-auto', auto.checked ? '1' : '0'); } catch (e) { }
        if (auto.checked) timer = setInterval(tick, Number(auto.getAttribute('data-interval') || 60000));
      };
      auto.addEventListener('change', sync);
      sync();
    }

    // ringkasan live di topbar
    var sumEl = document.querySelector('[data-summary]');
    if (sumEl) {
      var load = function () {
        fetch(sumEl.getAttribute('data-url') || '/api/summary')
          .then(function (r) { return r.ok ? r.json() : null; })
          .then(function (d) {
            if (!d) return;
            sumEl.querySelectorAll('[data-f]').forEach(function (n) {
              if (d[n.getAttribute('data-f')] !== undefined) n.textContent = d[n.getAttribute('data-f')];
            });
            var t = sumEl.querySelector('[data-time]');
            if (t && d.generatedAtUtc) {
              var dt = new Date(d.generatedAtUtc);
              t.textContent = dt.toLocaleTimeString('id-ID', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
            }
          }).catch(function () { });
      };
      load();
      setInterval(load, 30000);
    }

    // tombol salin
    document.querySelectorAll('[data-copy]').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var target = document.querySelector(btn.getAttribute('data-copy'));
        if (!target) return;
        var text = target.innerText;
        var done = function () {
          var old = btn.textContent; btn.textContent = 'Tersalin ✓';
          setTimeout(function () { btn.textContent = old; }, 1400);
        };
        if (navigator.clipboard) navigator.clipboard.writeText(text).then(done, done);
        else {
          var ta = document.createElement('textarea');
          ta.value = text; document.body.appendChild(ta); ta.select();
          try { document.execCommand('copy'); } catch (e) { }
          document.body.removeChild(ta); done();
        }
      });
    });

    // konfirmasi aksi berbahaya
    document.querySelectorAll('form[data-confirm]').forEach(function (f) {
      f.addEventListener('submit', function (e) {
        if (!window.confirm(f.getAttribute('data-confirm'))) e.preventDefault();
      });
    });
  }

  document.addEventListener('DOMContentLoaded', function () {
    initTheme();
    initAll();
    initUi();
  });

  window.DM = { charts: { init: initAll, render: renderAll, Chart: Chart }, applyTheme: applyTheme };
})();
