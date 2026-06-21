/* ═══════════════════════════════════════════════════════════
   Serial Monitor V2 — User Manual Scripts
   Bilingual i18n + sidebar highlighting + smooth scroll
   ═══════════════════════════════════════════════════════════ */

/* ── Bilingual text database ──
   Add `en` values later — empty string = no English yet */
const I18N = {

/* ── Sidebar ── */
'sidebar-title':          { zh: 'Serial Monitor V2', en: '' },
'sidebar-subtitle':       { zh: '用户手册', en: '' },

/* ── Chapter 1: Quick Start ── */
'ch1-title':              { zh: '1. 快速入门', en: '' },
'ch1-p1':                 { zh: '打开软件后，三步即可看到第一条数据：', en: '' },
'ch1-step1':              { zh: '① 选择串口 — 顶栏下拉菜单自动扫描可用串口', en: '' },
'ch1-step2':              { zh: '② 点击连接 — 波特率默认 115200，通常不需要改', en: '' },
'ch1-step3':              { zh: '③ 切换到对应标签页 — 数据会自动路由到正确的面板', en: '' },
'ch1-p2':                 { zh: 'STM32 端发一行 <code>[plot,ch1,25.3]</code>，波形图面板即刻显示曲线。无需任何配置。', en: '' },

/* ── Chapter 2: Panel Guide ── */
'ch2-title':              { zh: '2. 面板指南', en: '' },
'ch2-intro':              { zh: 'Serial Monitor V2 有 10 个功能面板，每个面板对应一类串口数据。协议格式统一为 <code>[type,arg1,arg2,...]</code>，PC 端根据 <code>type</code> 字段自动路由。', en: '' },

/* ── 2.1 Receive Area ── */
'p-receive-title':        { zh: '2.1 接收区', en: '' },
'p-receive-desc':         { zh: '所有串口数据在此显示。AvalonEdit 虚拟化渲染，高频数据不掉帧。', en: '' },
'p-receive-features':     { zh: '功能：彩色日志（系统消息 / 发送回显 / 接收数据 三色区分）、时间戳（三种格式）、智能滚屏锁定、暂停显示 + 缓冲满提醒、Ctrl+F 搜索（关键字 / 正则 / 大小写敏感）、日志导出。', en: '' },

/* ── 2.2 Send Area ── */
'p-send-title':           { zh: '2.2 发送区', en: '' },
'p-send-desc':            { zh: '快捷发送面板，支持 HEX / 文本双模式、多种编码（UTF-8/GBK）。', en: '' },
'p-send-features':        { zh: '功能：chip 按钮快捷发送 + 右键编辑删除、发送历史（最近 20 条去重）、HEX 实时格式化、定时发送、换行符可选（\\r\\n / \\n / \\r / 无）、Enter = 发送 / Shift+Enter = 换行。', en: '' },

/* ── 2.3 Waveform Plot ── */
'p-plot-title':           { zh: '2.3 波形图', en: '' },
'p-plot-desc':            { zh: 'OxyPlot 实时曲线面板。通道名自动成为图例，颜色自动分配。', en: '' },
'p-plot-protocol':        { zh: '协议格式', en: '' },
'p-plot-example':         { zh: '示例', en: '' },
'p-plot-features':        { zh: '功能：滚动 / 扫描双模式、30Hz 刷新限流、数值 HUD 半透明叠加、标点 / 连线可切换、信号分析（频率 / 幅值 / 占空比 / 波形类型识别）、CSV 导出、Y 轴手动 / 自动范围、⏱/📶 时域/频域一键切换。', en: '' },

/* ── 2.4 Tuning Workbench ── */
'p-tuning-title':         { zh: '2.4 调参工作台', en: '' },
'p-tuning-desc':          { zh: '波形图底部可伸缩抽屉。拖动滑杆调整 PID 参数，同时实时观察波形变化。', en: '' },
'p-tuning-protocol':      { zh: '协议格式', en:'' },
'p-tuning-features':      { zh: '滑杆与 Sliders 面板共享 VM，步长/颜色/范围双向即时生效。+/- 按钮精调 PID，步进值取自 TickFrequency。', en: '' },

/* ── 2.5 FFT Spectrum ── */
'p-fft-title':            { zh: '2.5 FFT 频谱', en: '' },
'p-fft-desc':             { zh: 'PC 端自动 FFT：<code>[plot,...]</code> 数据滑窗后实时频谱分析，STM32 端零改动。', en: '' },
'p-fft-protocol':         { zh: '协议格式', en:'' },
'p-fft-features':         { zh: '也可由 STM32 端 CMSIS-DSP 计算 FFT → <code>[fft,name,点数,bin0,...]</code> 协议直发。窗函数：汉宁 / 矩形 / 汉明 / 布莱克曼。频域指标：基频 / 幅度 / THD / SNR / DC 偏置。采样率输入 → X 轴 Bin → Hz。', en: '' },

/* ── 2.6 Sensor Panel ── */
'p-sensor-title':         { zh: '2.6 传感面板', en: '' },
'p-sensor-desc':          { zh: 'STM32 发一行 <code>[sensor,temp,芯片温度,42.5]</code>，PC 端自动建卡——零配置，即插即显。', en: '' },
'p-sensor-protocol':      { zh: '协议格式', en:'' },
'p-sensor-features':      { zh: '8 类卡片：温度 / 湿度 / 气压 / 状态 / 电机 / 电池 / 开关 / 滑杆，每类独立配色 + 定制布局。双向控制：开关卡点一下 → <code>[ctrl,led,蓝色LED,on]</code> 发回 MCU。滑杆卡拖拽实时回控。迷你波形：数值类卡片内嵌 30 点面积图。智能离线检测：2 秒心跳超时自动标红。编辑模式：拖拽排序 + 增删改 + 分组布局。', en: '' },

/* ── 2.7 Keys Panel ── */
'p-keys-title':           { zh: '2.7 按键面板', en: '' },
'p-keys-desc':            { zh: '虚拟按键面板，PC 端按下 → 发送 <code>[key,name,state]</code> → MCU 端响应。', en: '' },
'p-keys-protocol':        { zh: '协议格式', en:'' },
'p-keys-features':        { zh: '6 种键盘布局预设 + 自定义按键。颜色可调（40 色 Material Design 色板 + hex 自定义）。支持批量编辑。', en: '' },

/* ── 2.8 Sliders Panel ── */
'p-sliders-title':        { zh: '2.8 滑杆面板', en: '' },
'p-sliders-desc':         { zh: '拖拽滑杆 → 节流发送 <code>[slider,name,val]</code> → MCU 端实时接收数值。', en: '' },
'p-sliders-protocol':     { zh: '协议格式', en:'' },
'p-sliders-features':     { zh: '自定义颜色轨道 + 拇指。拖拽过程中节流发送，松手时发送最终值。支持预设（全部归零/置中/最大）。', en: '' },

/* ── 2.9 Joystick Panel ── */
'p-joystick-title':       { zh: '2.9 摇杆面板', en: '' },
'p-joystick-desc':        { zh: '双轴摇杆控件 → <code>[joystick,id,x1,y1,x2,y2]</code> → MCU 端控制（如小车/云台）。', en: '' },
'p-joystick-protocol':    { zh: '协议格式', en:'' },
'p-joystick-features':    { zh: '3 种内置风格（手柄/极简/经典）+ 自定义图片素材。底板和拇指可分别替换。支持 ⟲ 全部回中。', en: '' },

/* ── 2.10 OLED Drawing & PC Canvas ── */
'p-oled-title':           { zh: '2.10 OLED 绘图 & PC 画板', en: '' },
'p-oled-desc':            { zh: 'PC 端鼠标绘制图形 → 实时转换为 <code>[draw,...]</code> 协议 → STM32 接收后渲染到物理 OLED/LCD 屏幕。', en: '' },
'p-oled-protocol':        { zh: '协议格式', en:'' },
'p-oled-features':        { zh: '10 种绘图指令：点/线/矩形/圆角矩形/圆/椭圆/三角/弧/文字/清屏。旋转支持：矩形/椭圆旋转，<code>a&lt;angle&gt;</code> 协议后缀。F5 增量同步：拖动图形时每次只发 1 帧，MCU 本地维护图形数组。双屏支持：OLED 128×64 + LCD 240×280，注释一行代码即可切换。PC 画板：类似画图软件，鼠标拖拽绘制 → 实时同步到 STM32 物理屏幕。', en: '' },

/* ── Chapter 3: Protocol Reference ── */
'ch3-title':              { zh: '3. 协议速查', en: '' },
'ch3-intro':              { zh: '全部协议格式、参数、方向、示例。方向：📤 = MCU → PC，📥 = PC → MCU，🔄 = 双向。', en: '' },
'ch3-tbl-type':           { zh: 'Type', en: '' },
'ch3-tbl-format':         { zh: '格式', en: '' },
'ch3-tbl-dir':            { zh: '方向', en: '' },
'ch3-tbl-example':        { zh: '示例', en: '' },
'ch3-tbl-note':           { zh: '说明', en: '' },

/* ── Chapter 4: MCU Integration ── */
'ch4-title':              { zh: '4. MCU 对接', en: '' },
'ch4-intro':              { zh: 'MCU 端只需 <code>Serial.c</code> + <code>Serial.h</code> 两个文件，扔进 CubeMX 工程即可。零依赖、零 malloc。', en: '' },
'ch4-step1-title':        { zh: '4.1 导入文件', en: '' },
'ch4-step1-p1':           { zh: '将 <code>Serial.c</code> 和 <code>Serial.h</code> 复制到 CubeMX 工程的 <code>Core/Src/</code> 和 <code>Core/Inc/</code> 目录。', en: '' },
'ch4-step2-title':        { zh: '4.2 开启串口中断（必须）', en: '' },
'ch4-step2-p1':           { zh: '在 CubeMX 中，对每个要用的 USART：<strong>NVIC Settings</strong> → 勾选 <code>USARTx global interrupt</code> ✅。不勾中断 → 接收功能静默失效。', en: '' },
'ch4-step3-title':        { zh: '4.3 三行初始化', en: '' },
'ch4-step3-p1':           { zh: '在 <code>main()</code> 中加一行：', en: '' },
'ch4-step4-title':        { zh: '4.4 发送数据到 PC', en: '' },
'ch4-step4-p1':           { zh: '6 个发送函数，最常用 <code>Serial_Printf</code>：', en: '' },
'ch4-step5-title':        { zh: '4.5 接收 PC 指令', en: '' },
'ch4-step5-p1':           { zh: '主循环中轮询接收，用 <code>GetField</code> 拆字段，按 <code>type</code> 路由：', en: '' },
'ch4-step6-title':        { zh: '4.6 传感数据上报', en: '' },
'ch4-step6-p1':           { zh: '定时发送传感器读数，PC 端自动建卡。心跳状态卡保持 500ms 间隔，超 2s PC 端自动标红离线：', en: '' },
'ch4-step7-title':        { zh: '4.7 路由分发模式（推荐）', en: '' },
'ch4-step7-p1':           { zh: '协议类型超过 3 种时，建议用路由分发——每个 type 一个 handler，主循环只调 <code>RouteMessage(raw)</code>：', en: '' },

/* ── Chapter 5: FAQ ── */
'ch5-title':              { zh: '5. 常见问题', en: '' },
'faq-q1':                 { zh: 'Q: 接收不到数据，但发送正常？', en: '' },
'faq-a1':                 { zh: '99% 是没在 CubeMX 里开串口中断。NVIC 页签 → <code>USARTx global interrupt</code> → 勾上 ✅ → 重新编译。', en: '' },
'faq-q2':                 { zh: 'Q: 串口列表为空？', en: '' },
'faq-a2':                 { zh: '确认设备已连接、驱动已安装。部分 USB 转串口芯片（CH340/CP2102）需手动安装驱动。', en: '' },
'faq-q3':                 { zh: 'Q: 波形图卡顿/数据丢失？', en: '' },
'faq-a3':                 { zh: '波特率太低——115200 是推荐起点。不要在 <code>while(1)</code> 里放 <code>HAL_Delay(500)</code> 然后发大量数据。', en: '' },
'faq-q4':                 { zh: 'Q: 传感面板卡片不更新？', en: '' },
'faq-a4':                 { zh: '检查协议格式——必须是 <code>[sensor,类型,卡片名,值]</code>。<code>值</code> 不能为空（除非主动留空用双逗号 <code>,,</code>）。状态卡 2 秒无心跳会自动标红——固件挂了或串口断了。', en: '' },
'faq-q5':                 { zh: 'Q: 支持哪些 STM32 系列？', en: '' },
'faq-a5':                 { zh: '只要用 HAL 库就支持。已在 STM32F407 和 STM32H743 上验证，F1/G0/L4 理论兼容。', en: '' },
'faq-q6':                 { zh: 'Q: 如何反馈问题？', en: '' },
'faq-a6':                 { zh: '请在 <a href="https://github.com/Encaron/SerialMonitor/issues">GitHub Issues</a> 提交，附上串口参数、操作步骤和截图。', en: '' },

/* ── Shared ── */
'lang-zh':                { zh: '中', en: '' },
'lang-en':                { zh: 'EN', en: '' },
'toc-panels':             { zh: '面板', en: '' },
};

/* ── Current language ── */
let currentLang = 'zh';

/* ── Apply language to page ── */
function applyLang(lang) {
    currentLang = lang;
    document.documentElement.lang = lang === 'zh' ? 'zh-CN' : 'en';

    /* Update all [data-i18n] elements */
    document.querySelectorAll('[data-i18n]').forEach(el => {
        const key = el.getAttribute('data-i18n');
        const text = I18N[key] && I18N[key][lang];
        if (text) {
            /* If the element contains only text (no children), set textContent.
               If it has children (like <code> tags), only set top-level text nodes.
               For simplicity: if the stored text contains HTML, use innerHTML;
               otherwise use textContent. */
            if (text.indexOf('<') !== -1) {
                el.innerHTML = text;
            } else {
                el.textContent = text;
            }
        }
    });

    /* Update language toggle button */
    const btnZh = document.getElementById('btn-lang-zh');
    const btnEn = document.getElementById('btn-lang-en');
    if (btnZh && btnEn) {
        btnZh.style.fontWeight = lang === 'zh' ? 'bold' : 'normal';
        btnEn.style.fontWeight = lang === 'en' ? 'bold' : 'normal';
    }
}

/* ── Scroll progress bar ── */
function updateScrollProgress() {
    const bar = document.getElementById('scroll-progress');
    if (!bar) return;
    const scrollTop = document.documentElement.scrollTop || document.body.scrollTop;
    const scrollHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
    bar.style.width = scrollHeight > 0 ? (scrollTop / scrollHeight * 100) + '%' : '0%';
}

/* ── Back to top ── */
function updateBackToTop() {
    const btn = document.getElementById('back-to-top');
    if (!btn) return;
    const scrollTop = document.documentElement.scrollTop || document.body.scrollTop;
    btn.classList.toggle('visible', scrollTop > 400);
}

/* ── Syntax highlighting (C) ── */
const C_KEYWORDS = new Set([
    'auto','break','case','char','const','continue','default','do','double',
    'else','enum','extern','float','for','goto','if','inline','int','long',
    'register','return','short','signed','sizeof','static','struct','switch',
    'typedef','union','unsigned','void','volatile','while',
    'uint8_t','uint16_t','uint32_t','int8_t','int16_t','int32_t','size_t',
    'GPIO_PIN_SET','GPIO_PIN_RESET','GPIO_PIN_0','GPIO_PIN_5','GPIO_PIN_12',
    'GPIOA','GPIOB','GPIOD','OLED_FILLED','OLED_UNFILLED','NULL','true','false',
]);

function highlightCode(el) {
    let html = el.textContent;
    /* Must escape HTML entities in the source first */
    html = html.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
    /* Also escape quotes so the string regex matches */
    html = html.replace(/"/g, '&quot;');

    /* Comments — /* ... *​/ and // ... */
    html = html.replace(/(\/\*[\s\S]*?\*\/)/g, '<span class="hl-cmt">$1</span>');
    html = html.replace(/(\/\/[^\n]*)/g, '<span class="hl-cmt">$1</span>');

    /* Preprocessor */
    html = html.replace(/^(#\s*\w+.*)$/gm, '<span class="hl-pp">$1</span>');

    /* Strings — "..." */
    html = html.replace(/(&quot;[^&]*&quot;)/g, '<span class="hl-str">$1</span>');

    /* Numbers */
    html = html.replace(/\b(\d+\.?\d*f?)\b/g, '<span class="hl-num">$1</span>');

    /* Keywords + types */
    C_KEYWORDS.forEach(kw => {
        const re = new RegExp('\\b(' + kw + ')\\b', 'g');
        html = html.replace(re, '<span class="hl-kw">$1</span>');
    });

    /* Function calls: word then ( — skip already-highlighted spans */
    html = html.replace(/([^"=])(\b[A-Za-z_]\w*)\s*\(/g, function(m, before, name) {
        return before + '<span class="hl-fn">' + name + '</span>(';
    });

    el.innerHTML = html;
}

function initSyntaxHighlight() {
    document.querySelectorAll('pre code').forEach(code => {
        highlightCode(code);
    });
}

/* ── Code block copy buttons ── */
function initCopyButtons() {
    document.querySelectorAll('.content-section pre').forEach(pre => {
        const btn = document.createElement('button');
        btn.className = 'copy-btn';
        btn.textContent = '📋';
        btn.title = '复制代码';
        btn.addEventListener('click', async () => {
            const code = pre.querySelector('code') || pre;
            await navigator.clipboard.writeText(code.textContent);
            btn.textContent = '✓';
            btn.classList.add('copied');
            setTimeout(() => { btn.textContent = '📋'; btn.classList.remove('copied'); }, 1800);
        });
        pre.appendChild(btn);
    });
}

/* ── Sidebar active section tracking + auto-expand ── */
function updateActiveNav() {
    const sections = document.querySelectorAll('.doc-content section[id], .doc-content h4[id]');
    const navLinks = document.querySelectorAll('.doc-sidebar a[href^="#"]');
    if (!sections.length || !navLinks.length) return;

    let currentId = '';
    sections.forEach(sec => {
        const rect = sec.getBoundingClientRect();
        if (rect.top <= 120) {
            currentId = sec.id;
        }
    });

    navLinks.forEach(link => {
        link.classList.toggle('active', link.getAttribute('href') === '#' + currentId);
    });

    /* Auto-expand collapsed group + scroll active into view */
    if (currentId) {
        const activeLink = document.querySelector(`.doc-sidebar a[href="#${currentId}"]`);
        if (activeLink) {
            const group = activeLink.closest('.sect-group');
            if (group && group.classList.contains('collapsed')) {
                group.classList.remove('collapsed');
            }
            activeLink.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
        }
    }
}

/* ── Sidebar section collapse ── */
function initSidebarCollapse() {
    document.querySelectorAll('.doc-sidebar .sect-title').forEach(title => {
        title.addEventListener('click', () => {
            title.closest('.sect-group').classList.toggle('collapsed');
        });
    });
}

/* ── Hero binary rain (index.html only) ── */
function initHeroBinaryRain() {
    const canvas = document.getElementById('hero-bg-canvas');
    if (!canvas) return;
    const ctx = canvas.getContext('2d');
    const hero = document.getElementById('hero');

    function resize() {
        canvas.width = hero.offsetWidth;
        canvas.height = hero.offsetHeight;
    }
    resize();
    window.addEventListener('resize', resize);

    const fontSize = 14;
    function cols() { return Math.floor(canvas.width / fontSize) || 1; }
    let drops = Array(cols()).fill(0).map(() => Math.floor(Math.random() * 20));

    function isDark() {
        return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    function draw() {
        /* Faint trail — fade previous frame */
        ctx.fillStyle = isDark() ? 'rgba(13,17,23,0.06)' : 'rgba(246,248,250,0.06)';
        ctx.fillRect(0, 0, canvas.width, canvas.height);

        /* Binary characters — visible but not loud */
        ctx.fillStyle = isDark() ? 'rgba(88,166,255,0.18)' : 'rgba(9,105,218,0.14)';
        ctx.font = fontSize + 'px "Cascadia Code", "Fira Code", monospace';

        const n = cols();
        if (drops.length !== n) {
            drops = Array(n).fill(0).map(() => Math.floor(Math.random() * 15));
        }

        for (let i = 0; i < n; i++) {
            const ch = Math.random() > 0.5 ? '0' : '1';
            ctx.fillText(ch, i * fontSize, drops[i] * fontSize);
            if (drops[i] * fontSize > canvas.height && Math.random() > 0.975)
                drops[i] = 0;
            drops[i]++;
        }
    }

    setInterval(draw, 100);
}

/* ── Combined scroll handler (throttled) ── */
let scrollTicking = false;
function onScroll() {
    if (!scrollTicking) {
        requestAnimationFrame(() => {
            updateScrollProgress();
            updateBackToTop();
            updateActiveNav();
            scrollTicking = false;
        });
        scrollTicking = true;
    }
}

/* ── Lightbox ── */
function initLightbox() {
    const lb = document.getElementById('lightbox');
    if (!lb) return;
    const lbImg = lb.querySelector('img');

    /* Click any content image → open lightbox */
    document.querySelectorAll('.content-section img, .design-card img, .showcase-grid img').forEach(img => {
        img.style.cursor = 'zoom-in';
        img.addEventListener('click', () => {
            lbImg.src = img.src;
            lbImg.alt = img.alt;
            lb.classList.add('open');
        });
    });

    /* Click overlay → close */
    lb.addEventListener('click', () => lb.classList.remove('open'));

    /* Esc → close */
    document.addEventListener('keydown', (e) => {
        if (e.key === 'Escape') lb.classList.remove('open');
    });
}

/* ── Init on DOM ready ── */
document.addEventListener('DOMContentLoaded', () => {
    applyLang(currentLang);
    initHeroBinaryRain();
    initLightbox();

    window.addEventListener('scroll', onScroll, { passive: true });

    /* Back to top click */
    document.getElementById('back-to-top')?.addEventListener('click', () => {
        window.scrollTo({ top: 0, behavior: 'smooth' });
    });

    /* Sidebar collapse */
    initSidebarCollapse();

    /* Syntax highlight + copy buttons */
    initSyntaxHighlight();
    initCopyButtons();

    /* Language toggle */
    document.getElementById('btn-lang-zh')?.addEventListener('click', (e) => { e.preventDefault(); applyLang('zh'); });
    document.getElementById('btn-lang-en')?.addEventListener('click', (e) => { e.preventDefault(); applyLang('en'); });
});
