/* ═══════════════════════════════════════════════════════════
   Serial Monitor V2 — User Manual Scripts
   Bilingual i18n + sidebar highlighting + smooth scroll
   ═══════════════════════════════════════════════════════════ */


/* ── Bilingual text database ──
   ~550 translations — 457 keys for all 3 pages */
const I18N = {

    /* Shared */
    'lang-en': { zh: 'EN', en: 'EN' },
    'lang-zh': { zh: '中', en: 'CN' },

    /* Index Page (idx-*) */
    'idx-a11y-back-to-top': { zh: '↑', en: '↑' },
    'idx-cta-btn-download': { zh: '⬇ 下载软件', en: '⬇ Download' },
    'idx-cta-btn-read-manual': { zh: '📖 阅读使用说明', en: '📖 Read User Manual' },
    'idx-cta-desc': { zh: '下载安装包，接上 STM32，发一行 <code>[plot,ch1,25.3]</code>，即刻看到波形。', en: 'Download the installer, connect your STM32, send <code>[plot,ch1,25.3]</code>, and see the waveform instantly.' },
    'idx-cta-heading': { zh: '准备好开始了吗？', en: 'Ready to Get Started?' },
    'idx-design-card1-desc': { zh: '仿 VS Code Dark+ 配色，21 色 <code>DynamicResource</code> 原地变色不闪烁。从 VS Code 切过来零学习成本——深色背景、蓝色强调、绿色状态灯，肌肉记忆完全复用。', en: 'Modeled after VS Code Dark+ palette, 21-color <code>DynamicResource</code> in-place theme switch, no flicker. Zero learning curve coming from VS Code — dark background, blue accents, green status indicators, muscle memory fully reusable.' },
    'idx-design-card1-img-alt': { zh: '暗色模式', en: 'Dark mode' },
    'idx-design-card1-title': { zh: '🌙 VS Code 暗色主题', en: '🌙 VS Code Dark Theme' },
    'idx-design-card2-desc': { zh: '~550 条翻译全覆盖，中英一键瞬切。ComboBox、系统日志、协议预览、错误提示——全线英文覆盖。GitHub README 双语，STM32 C 库文档双语。', en: '~550 translations, full coverage. CN/EN instant toggle. ComboBox, system log, protocol preview, error messages — full English coverage. GitHub README bilingual, STM32 C library docs bilingual.' },
    'idx-design-card2-img-alt': { zh: '英文界面', en: 'English interface' },
    'idx-design-card2-title': { zh: '🌐 国际化，国际友人能用', en: '🌐 Internationalization — Global Users Welcome' },
    'idx-design-card3-desc': { zh: '左侧图标栏（Activity Bar）→ 侧面板 220px → 主内容区。标签页点图标切换、侧栏内容跟随图标走、选中态左竖线指示器——和 VS Code 一模一样的操作逻辑。', en: 'Left icon bar (Activity Bar) → side panel 220px → main content area. Click icons to switch tabs, sidebar content follows the icon, selected state left-bar indicator — identical interaction logic to VS Code.' },
    'idx-design-card3-title': { zh: '三栏布局，VS Code 同款交互', en: 'Three-Column Layout, VS Code-Style UX' },
    'idx-design-card4-desc': { zh: '加新面板只需一行 <code>RegisterTab</code>——图标动效、标签切换、编辑态退出、弹窗主题全部自动处理。复制反馈、Popup 定位、透明按钮模板——框架统一兜底，忘写也不会出 bug。72 条测试 29ms 全绿。', en: 'Add a new panel with a single <code>RegisterTab</code> line — icon animations, tab switching, edit-mode exit, popup theming all auto-handled. Copy feedback, Popup placement, transparent button templates — the framework provides unified fallback; forgetting to code them won\'t cause bugs. 72 tests, 29ms, all green.' },
    'idx-design-card4-title': { zh: '一处注册，自动跟', en: 'Register Once, Everything Follows' },
    'idx-design-heading': { zh: '为开发者设计', en: 'Designed for Developers' },
    'idx-design-intro': { zh: 'VS Code 风格三栏布局、暗色主题、扁平化设计。嵌入式开发者一眼就能上手。', en: 'VS Code-style three-column layout, dark theme, flat design. Embedded developers feel at home instantly.' },
    'idx-engineering-card1-desc': { zh: '状态卡每 500ms 发一次心跳。PC 端 2 秒没收到 → 自动绿变红、online → offline。固件挂了不需要发离线消息——"死人不会打电话报丧"。', en: 'Status cards send heartbeat every 500ms. PC detects no update in 2s → auto green-to-red, online→offline. Dead firmware doesn\'t need to send an offline message — "A dead man doesn\'t call to report his death".' },
    'idx-engineering-card1-title': { zh: '死人不报丧', en: 'Dead Men Tell No Tales' },
    'idx-engineering-card2-desc': { zh: '8 类卡片，每种长得不一样。温度有波形、湿度有进度条、气压 PC 端自算百分比、开关是 iOS 胶囊滑块。同排混搭，WrapPanel 自然折行。', en: '8 card types, each with a unique look. Temperature has a mini waveform, humidity has a progress bar, pressure auto-calculates percentage on PC, switches are iOS-style capsule toggles. Mixed in one row, WrapPanel wraps naturally.' },
    'idx-engineering-card2-title': { zh: 'Cluster 思维', en: 'Cluster Thinking' },
    'idx-engineering-card3-desc': { zh: '卡片间虚线 <code>[+]</code> 悬停弹性展开 8→16px 不挤卡片。F2 改名跳过删卡重选。删组二次确认 3 秒倒计时——不靠弹窗打断。', en: 'Dashed <code>[+]</code> between cards expands elastically 8→16px on hover without pushing cards. F2 rename skips delete-and-recreate. Group delete has a 3-second countdown confirmation — no intrusive popups.' },
    'idx-engineering-card3-title': { zh: '编辑模式', en: 'Edit Mode' },
    'idx-engineering-card4-desc': { zh: 'PC 画板拖图形时每次只发 <strong>1 帧</strong>——<code>[draw,set,id,...]</code>。MCU 端本地维护图形数组，收到后本地全量重绘。不把整张画布几十条命令重发一遍。', en: 'Dragging a shape on PC Canvas sends only <strong>1 frame</strong> per update — <code>[draw,set,id,...]</code>. MCU maintains a local shape array and does a full redraw locally. Not re-sending dozens of commands for the entire canvas.' },
    'idx-engineering-card4-title': { zh: 'F5 增量同步', en: 'F5 Incremental Sync' },
    'idx-engineering-card5-desc': { zh: '拖滑杆时 <code>InvalidatePlot</code> 走 <code>DispatcherPriority.Background</code>——WPF 优先处理 MouseMove，空闲才渲染波形。拖拽永远流畅，松手瞬间恢复 30Hz。', en: 'When dragging a slider, <code>InvalidatePlot</code> uses <code>DispatcherPriority.Background</code> — WPF prioritizes MouseMove, renders waveform when idle. Dragging stays smooth, instant 30Hz recovery on release.' },
    'idx-engineering-card5-title': { zh: '调度法', en: 'Scheduler Method' },
    'idx-engineering-card6-desc': { zh: '72 条测试 29ms 全绿。CubeMX 重新生成后 6 个必补坑全写清——栈 1K→4K、<code>-u _printf_float</code>、TIM6_DAC_IRQHandler 永远不生成……嵌入式开发者一看就知道踩过坑。', en: '72 tests, 29ms, all green. 6 must-fix pitfalls after CubeMX regeneration fully documented — stack 1K→4K, <code>-u _printf_float</code>, TIM6_DAC_IRQHandler never generated... Embedded developers recognize these scars on sight.' },
    'idx-engineering-card6-title': { zh: '文档不写"略"', en: 'Docs Never Say "Omitted"' },
    'idx-engineering-card7-desc': { zh: '1px 轮廓线保持峰谷细节 + 30% 透明填充给视觉重量。加粗线条 2px+ 会在 140px 宽画布上抹平相邻点差异——面积图方案不碰线宽。', en: '1px outline preserves peak/valley detail + 30% transparent fill adds visual weight. Thicker lines 2px+ would flatten adjacent point differences on a 140px-wide canvas — the area chart approach never touches line width.' },
    'idx-engineering-card7-title': { zh: '迷你波形不糊不单薄', en: 'Mini Waveform: Crisp, Not Thin' },
    'idx-engineering-card8-desc': { zh: '传感器读回异常？中段留空双逗号 <code>[sensor,temp,温度,,45]</code> → 主值显示 <code>--</code> 不进波形，辅助参数照常显示。不报错、不丢数据、不污染图表。', en: 'Sensor readback anomaly? Leave a blank with double commas <code>[sensor,temp,temp,,45]</code> → main value shows <code>--</code>, excluded from waveform; auxiliary params display normally. No errors, no data loss, no chart pollution.' },
    'idx-engineering-card8-title': { zh: '优雅降级', en: 'Graceful Degradation' },
    'idx-engineering-heading': { zh: '工程细节', en: 'Engineering Details' },
    'idx-engineering-intro': { zh: '不是"能跑就行"——每个交互都认真想过。', en: 'Not "good enough to run" — every interaction was carefully considered.' },
    'idx-features-card1-desc': { zh: '收发区、波形图、FFT 频谱、传感面板、按键、滑杆、摇杆、OLED 绘图——一个软件全搞定。', en: 'Serial Transceiver, Waveform Plot, FFT Spectrum, Sensor Panel, Keys, Sliders, Joystick, OLED Drawing — all in one app.' },
    'idx-features-card1-title': { zh: '9 个功能面板', en: '9 Functional Panels' },
    'idx-features-card2-desc': { zh: 'STM32 发一行 <code>[sensor,temp,芯片,42.5]</code>，PC 端自动建卡。协议自动路由，通道名即图例。', en: 'Send <code>[sensor,temp,chip,42.5]</code> from STM32, and the PC auto-creates a card. Protocol auto-routing, channel name becomes legend.' },
    'idx-features-card2-title': { zh: '零配置，即插即显', en: 'Zero Config, Plug and Display' },
    'idx-features-card3-desc': { zh: 'PC 端拖滑杆、点开关、画图形 → 协议实时发回 MCU → 操作硬件。不是单向监视器。', en: 'Drag sliders, toggle switches, draw graphics on PC → protocol sent back to MCU in real time → control hardware. Not just a one-way monitor.' },
    'idx-features-card3-title': { zh: '双向控制闭环', en: 'Bidirectional Control Loop' },
    'idx-features-card4-desc': { zh: '仅 2 个文件（<code>Serial.c</code> + <code>Serial.h</code>），零依赖零 malloc，扔进 CubeMX 工程即可。', en: 'Just 2 files (<code>Serial.c</code> + <code>Serial.h</code>), zero dependencies, zero malloc. Drop into any CubeMX project.' },
    'idx-features-card4-title': { zh: 'STM32 端开箱即用', en: 'STM32 Side: Drop-in Ready' },
    'idx-features-card5-desc': { zh: '~550 条翻译一键瞬切。21 色 DynamicResource 原地变色不闪烁。', en: '~550 translations, instant toggle. 21-color DynamicResource in-place theme switch, no flicker.' },
    'idx-features-card5-title': { zh: '中英双语 + 亮暗双模', en: 'Bilingual CN/EN + Light/Dark Mode' },
    'idx-features-card6-desc': { zh: '三个致命路径全 try-catch 保护。崩溃日志自动写入。USB 热插拔自动检测 + 自动重连。', en: 'Three critical paths fully try-catch protected. Crash logs auto-written. USB hot-plug auto-detect + auto-reconnect.' },
    'idx-features-card6-title': { zh: '健壮容错', en: 'Robust Fault Tolerance' },
    'idx-features-heading': { zh: '为什么选择 Serial Monitor V2？', en: 'Why Serial Monitor V2?' },
    'idx-footer-c-lib-docs': { zh: 'C 库文档', en: 'C Library Docs' },
    'idx-footer-col1-heading': { zh: '项目', en: 'Project' },
    'idx-footer-col2-heading': { zh: '文档', en: 'Docs' },
    'idx-footer-col3-heading': { zh: '配套资源', en: 'Resources' },
    'idx-footer-copyright': { zh: '© 2026 冯毅力 (Encaron)  ·  基于 WPF + OxyPlot + AvalonEdit 构建  ·  STM32 配套 C 库开箱即用', en: '© 2026 Encaron (Feng Yili)  ·  Built with WPF + OxyPlot + AvalonEdit  ·  STM32 companion C library, drop-in ready' },
    'idx-footer-demo-project': { zh: '演示工程', en: 'Demo Project' },
    'idx-footer-download-release': { zh: '下载 Release', en: 'Download Release' },
    'idx-footer-issues': { zh: '问题反馈', en: 'Report Issues' },
    'idx-footer-manual': { zh: '使用说明', en: 'User Manual' },
    'idx-footer-mcu': { zh: 'MCU 对接', en: 'MCU Integration' },
    'idx-footer-oled-protocol': { zh: 'OLED 绘图协议', en: 'OLED Drawing Protocol' },
    'idx-footer-protocol': { zh: '协议速查', en: 'Protocol Reference' },
    'idx-footer-stm32-c-lib': { zh: 'STM32 C 库', en: 'STM32 C Library' },
    'idx-hero-btn-download': { zh: '⬇ 下载软件', en: '⬇ Download' },
    'idx-hero-btn-manual': { zh: '📖 使用说明', en: '📖 User Manual' },
    'idx-hero-screenshot-alt': { zh: 'Serial Monitor V2 主界面', en: 'Serial Monitor V2 main window' },
    'idx-hero-scroll-hint': { zh: '▼ 向下滚动了解详情', en: '▼ Scroll down to learn more' },
    'idx-hero-subtitle-line1': { zh: '基于 WPF + OxyPlot + AvalonEdit 的串口调试工具<br>STM32 端配套 C 库，扔进工程即用', en: 'A serial debug tool built on WPF + OxyPlot + AvalonEdit<br>Companion C library for STM32 — drop into your project and go' },
    'idx-hero-version-meta': { zh: '作者：<strong style="color:var(--accent)">冯毅力 (Encaron)</strong> &nbsp;·&nbsp; v2.6.5 &nbsp;·&nbsp; Windows 10+ x64 &nbsp;·&nbsp; .NET 8 &nbsp;·&nbsp; MIT 开源', en: 'Author: <strong style="color:var(--accent)">Encaron (Feng Yili)</strong> &nbsp;·&nbsp; v2.6.5 &nbsp;·&nbsp; Windows 10+ x64 &nbsp;·&nbsp; .NET 8 &nbsp;·&nbsp; MIT Open Source' },
    'idx-nav-faq': { zh: '常见问题', en: 'FAQ' },
    'idx-nav-manual': { zh: '使用说明', en: 'User Manual' },
    'idx-nav-mcu': { zh: 'MCU 对接', en: 'MCU Integration' },
    'idx-nav-protocol': { zh: '协议速查', en: 'Protocol Reference' },
    'idx-page-title': { zh: 'Serial Monitor V2 — 串口调试工具', en: 'Serial Monitor V2 — Serial Debug Tool' },
    'idx-panels-card1-desc': { zh: 'AvalonEdit 虚拟化渲染<br>三色日志 + HEX/文本双模', en: 'AvalonEdit virtualized rendering<br>Tri-color log + HEX/Text dual mode' },
    'idx-panels-card1-title': { zh: '收发区', en: 'Serial Transceiver' },
    'idx-panels-card2-desc': { zh: 'OxyPlot 实时曲线<br>时域/频域一键切换', en: 'OxyPlot real-time curves<br>Time/Frequency domain one-click toggle' },
    'idx-panels-card2-title': { zh: '波形图', en: 'Waveform Plot' },
    'idx-panels-card3-desc': { zh: 'PC 端自动 FFT<br>STM32 端零改动', en: 'PC-side auto FFT<br>Zero changes on STM32 side' },
    'idx-panels-card3-title': { zh: 'FFT 频谱', en: 'FFT Spectrum' },
    'idx-panels-card4-desc': { zh: '8 类卡片零配置建卡<br>双向控制 + 心跳检测', en: '8 card types, zero-config creation<br>Bidirectional control + heartbeat detection' },
    'idx-panels-card4-title': { zh: '传感面板', en: 'Sensor Panel' },
    'idx-panels-card5-desc': { zh: '6 种键盘布局<br>40 色自定义', en: '6 keyboard layouts<br>40-color customization' },
    'idx-panels-card5-title': { zh: '按键', en: 'Keys' },
    'idx-panels-card6-desc': { zh: '拖拽节流发送<br>自定义颜色轨道', en: 'Drag-throttled send<br>Custom color tracks' },
    'idx-panels-card6-title': { zh: '滑杆', en: 'Sliders' },
    'idx-panels-card7-desc': { zh: '双轴控制<br>3 种风格 + 自定义素材', en: 'Dual-axis control<br>3 styles + custom assets' },
    'idx-panels-card7-title': { zh: '摇杆', en: 'Joystick' },
    'idx-panels-card8-desc': { zh: '11 种绘图指令<br>F5 增量同步', en: '11 drawing commands<br>F5 incremental sync' },
    'idx-panels-card8-title': { zh: 'OLED 绘图', en: 'OLED Drawing' },
    'idx-panels-heading': { zh: '10 个面板，一个软件', en: '10 Panels, One App' },
    'idx-panels-intro': { zh: '协议格式统一为 <code>[type,参数...]</code>。PC 端根据 type 自动路由到对应面板，无需任何配置。', en: 'Unified protocol format: <code>[type,params...]</code>. PC auto-routes by type to the correct panel — no configuration needed.' },
    'idx-showcase-fig1-alt': { zh: '传感面板', en: 'Sensor Panel' },
    'idx-showcase-fig1-caption': { zh: '传感面板 — 8 类卡片火力全开', en: 'Sensor Panel — all 8 card types in action' },
    'idx-showcase-fig2-alt': { zh: '波形图', en: 'Waveform Plot' },
    'idx-showcase-fig2-caption': { zh: '波形图 — 多通道实时曲线', en: 'Waveform Plot — multi-channel real-time curves' },
    'idx-showcase-fig3-alt': { zh: 'FFT 频谱', en: 'FFT Spectrum' },
    'idx-showcase-fig3-caption': { zh: 'FFT 频谱 — 时域/频域一键切换', en: 'FFT Spectrum — time/frequency domain one-click toggle' },
    'idx-showcase-fig4-alt': { zh: 'PC 画板', en: 'PC Canvas' },
    'idx-showcase-fig4-caption': { zh: 'PC 画板 — 拖拽绘制 + 实时同步 MCU', en: 'PC Canvas — drag to draw + real-time sync to MCU' },
    'idx-showcase-heading': { zh: '看看实际效果', en: 'See It in Action' },

    /* Manual Page (m-*) */
    'ch3-tbl-type': { zh: 'Type', en: 'Type' },
    'm-btn-back-to-top': { zh: '↑', en: '↑' },
    'm-faq-a1': { zh: '99% 是没在 CubeMX 里开串口中断。NVIC 页签 → <code>USARTx global interrupt</code> → 勾上 ✅ → 重新编译。', en: '99% of the time, the serial interrupt is not enabled in CubeMX. NVIC tab → <code>USARTx global interrupt</code> → check ✅ → rebuild.' },
    'm-faq-a2': { zh: '确认设备已连接、驱动已安装。部分 USB 转串口芯片（CH340/CP2102）需手动安装驱动。', en: 'Confirm the device is connected and drivers are installed. Some USB-to-serial chips (CH340/CP2102) require manual driver installation.' },
    'm-faq-a3': { zh: '几乎都是 MCU 端发送太快。常规数据（传感/波形/滑杆/LED）请保持 <strong>50ms</strong> 以上间隔，PC 画板绘图 <strong>5ms</strong> 以上。8 张传感卡片 × 每 5ms 发一次 = 串口瞬间塞爆，PC 端渲染跟不上。宁可慢一点，求稳不求快。', en: 'Almost certainly the MCU is sending too fast. Keep regular data (sensor/waveform/slider/LED) at <strong>50ms+</strong> intervals, PC Canvas drawing at <strong>5ms+</strong>. 8 sensor cards × every 5ms = serial port instantly flooded; PC rendering can\'t keep up. Better slower and stable than faster and broken.' },
    'm-faq-a4': { zh: '波特率太低——115200 是推荐起点。不要在 <code>while(1)</code> 里放 <code>HAL_Delay(500)</code> 然后发大量数据。', en: 'Baud rate too low — 115200 is the recommended starting point. Don\'t put <code>HAL_Delay(500)</code> in <code>while(1)</code> and then send large amounts of data.' },
    'm-faq-a5': { zh: '检查协议格式——必须是 <code>[sensor,类型,卡片名,值]</code>。值不能为空（除非主动留空用双逗号 <code>,,</code>）。状态卡 2 秒无心跳自动标红。', en: 'Check the protocol format — must be <code>[sensor,type,card name,value]</code>. Value cannot be empty (unless intentionally left blank with double commas <code>,,</code>). Status cards auto-red after 2s without heartbeat.' },
    'm-faq-a6': { zh: '只要用 HAL 库就支持。已在 STM32F407 和 STM32H743 上验证，F1/G0/L4 理论兼容。', en: 'Any STM32 using the HAL library is supported. Verified on STM32F407 and STM32H743; F1/G0/L4 theoretically compatible.' },
    'm-faq-a7': { zh: '请在 <a href="https://github.com/Encaron/SerialMonitor/issues" target="_blank">GitHub Issues</a> 提交，附上串口参数、操作步骤和截图。', en: 'Please submit at <a href="https://github.com/Encaron/SerialMonitor/issues" target="_blank">GitHub Issues</a> with serial parameters, steps to reproduce, and screenshots.' },
    'm-faq-heading': { zh: '常见问题', en: 'FAQ' },
    'm-faq-q1': { zh: 'Q: 接收不到数据，但发送正常？', en: 'Q: Not receiving data, but sending works?' },
    'm-faq-q2': { zh: 'Q: 串口列表为空？', en: 'Q: COM port list is empty?' },
    'm-faq-q3': { zh: 'Q: 软件卡死/响应迟钝？', en: 'Q: App freezes / becomes sluggish?' },
    'm-faq-q4': { zh: 'Q: 波形图卡顿/数据丢失？', en: 'Q: Waveform Plot stutters / data loss?' },
    'm-faq-q5': { zh: 'Q: 传感面板卡片不更新？', en: 'Q: Sensor Panel cards not updating?' },
    'm-faq-q6': { zh: 'Q: 支持哪些 STM32 系列？', en: 'Q: Which STM32 families are supported?' },
    'm-faq-q7': { zh: 'Q: 如何反馈问题？', en: 'Q: How do I report an issue?' },
    'm-img-alt-cubemx-nvic': { zh: 'CubeMX NVIC 中断设置', en: 'CubeMX NVIC Interrupt Settings' },
    'm-img-alt-cubemx-usart': { zh: 'CubeMX USART 配置', en: 'CubeMX USART Configuration' },
    'm-img-alt-fft': { zh: 'FFT 频谱', en: 'FFT Spectrum' },
    'm-img-alt-fft-opts': { zh: 'FFT 侧栏设置', en: 'FFT Sidebar Settings' },
    'm-img-alt-joystick': { zh: '摇杆面板', en: 'Joystick Panel' },
    'm-img-alt-keys': { zh: '按键面板', en: 'Keys Panel' },
    'm-img-alt-keys-edit': { zh: '按键编辑', en: 'Keys Edit' },
    'm-img-alt-oled': { zh: 'PC 画板', en: 'PC Canvas' },
    'm-img-alt-plot': { zh: '波形图', en: 'Waveform Plot' },
    'm-img-alt-sensor': { zh: '传感面板', en: 'Sensor Panel' },
    'm-img-alt-sensor-edit': { zh: '传感面板编辑', en: 'Sensor Panel Edit' },
    'm-img-alt-serial': { zh: '收发区', en: 'Serial Transceiver' },
    'm-img-alt-settings': { zh: '设置页', en: 'Settings' },
    'm-img-alt-signal': { zh: '信号分析', en: 'Signal Analysis' },
    'm-img-alt-sliders': { zh: '滑杆面板', en: 'Sliders Panel' },
    'm-img-alt-sliders-edit': { zh: '滑杆编辑', en: 'Sliders Edit' },
    'm-img-alt-tuning': { zh: '调参工作台', en: 'Tuning Workbench' },
    'm-joystick-desc': { zh: '双轴摇杆控件 → <code>[joystick,id,x1,y1,x2,y2]</code> → MCU 端控制（如小车/云台）。', en: 'Dual-axis joystick control → <code>[joystick,id,x1,y1,x2,y2]</code> → MCU control (e.g., car/gimbal).' },
    'm-joystick-heading': { zh: '🕹️ 摇杆面板', en: '🕹️ Joystick Panel' },
    'm-joystick-sidebar': { zh: '<strong>侧栏设置：</strong>发送间隔(ms)。⟲ 全部回中。摇杆反馈——实时显示当前坐标值 + 协议预览 <code>[joystick,id,x1,y1,x2,y2]</code>。', en: '<strong>Sidebar Settings:</strong> Send interval (ms). ⟲ Re-center all. Joystick feedback — real-time current coordinate display + protocol preview <code>[joystick,id,x1,y1,x2,y2]</code>.' },
    'm-joystick-style': { zh: '<strong>风格：</strong>3 种内置风格（手柄/极简/经典）+ 自定义图片素材。顶栏「底板 ▾」「拇指 ▾」分别切换。自定义 PNG 放入素材文件夹即可自动识别。', en: '<strong>Style:</strong> 3 built-in styles (Gamepad/Minimal/Classic) + custom image assets. Top bar "Base ▾" and "Thumb ▾" switch independently. Drop custom PNGs into the assets folder for auto-recognition.' },
    'm-joystick-sub-heading-protocol': { zh: '协议格式', en: 'Protocol Format' },
    'm-keys-desc': { zh: '虚拟按键面板，PC 端按下 → 发送 <code>[key,name,state]</code> → MCU 端响应。', en: 'Virtual key panel. Press on PC → sends <code>[key,name,state]</code> → MCU responds.' },
    'm-keys-edit-mode': { zh: '<strong>编辑模式：</strong>点"编辑"进入——+ 添加按键、⌨ 键盘布局（6 种预设）、🗑 清空全部。选中按键后侧栏编辑属性：按键名、自锁开关、按下/松开模式（null / 键名 / up / down / on / off）及对应发送值、宽度/高度、颜色（40 色 Material Design 色板 + hex 自定义）。支持批量选中编辑 + 🗑 删除。', en: '<strong>Edit Mode:</strong> Click "Edit" to enter — + Add key, ⌨ Keyboard layout (6 presets), 🗑 Clear all. Select a key to edit in sidebar: key name, latching switch, press/release mode (null / key name / up / down / on / off) and corresponding send value, width/height, color (40-color Material Design palette + hex custom). Batch select editing + 🗑 Delete supported.' },
    'm-keys-heading': { zh: '🔘 按键面板', en: '🔘 Keys Panel' },
    'm-keys-mcu-example': { zh: 'MCU 端接收示例', en: 'MCU Receive Example' },
    'm-keys-normal-mode': { zh: '<strong>普通模式：</strong>点按键 → 侧栏显示最后操作反馈（按下/松开协议预览，可复制）。', en: '<strong>Normal Mode:</strong> Press a key → sidebar shows last action feedback (press/release protocol preview, copyable).' },
    'm-keys-sub-heading-protocol': { zh: '协议格式', en: 'Protocol Format' },
    'm-mcu-callout-body': { zh: ' <a href="https://github.com/Encaron/SerialMonitor/tree/master/Serial_C_Language/example" target="_blank">GitHub 上的 STM32F407 CMake 工程</a>，含绘图解析（OLED/LCD 双屏）+ F5 增量同步 + 双滑杆 PWM + 传感面板 + 9 种波形 + 物理按键。', en: ' <a href="https://github.com/Encaron/SerialMonitor/tree/master/Serial_C_Language/example" target="_blank">STM32F407 CMake project on GitHub</a>, including drawing parsing (OLED/LCD dual screen) + F5 incremental sync + dual slider PWM + Sensor Panel + 9 waveforms + physical keys.' },
    'm-mcu-callout-title': { zh: '📁 完整演示工程：', en: '📁 Complete Demo Project:' },
    'm-mcu-func-array': { zh: '发字节数组', en: 'Send byte array' },
    'm-mcu-func-byte': { zh: '发一个字节', en: 'Send one byte' },
    'm-mcu-func-number': { zh: '发定长数字', en: 'Send fixed-length number' },
    'm-mcu-func-packet': { zh: '发数据包', en: 'Send data packet' },
    'm-mcu-func-printf': { zh: '格式化发送 ⭐', en: 'Formatted send ⭐' },
    'm-mcu-func-string': { zh: '发字符串', en: 'Send string' },
    'm-mcu-heading': { zh: 'MCU 对接', en: 'MCU Integration' },
    'm-mcu-intro': { zh: 'MCU 端只需 <code>Serial.c</code> + <code>Serial.h</code> 两个文件，扔进 CubeMX 工程即可。<strong>零依赖、零 malloc。</strong>', en: 'The MCU side only needs two files: <code>Serial.c</code> + <code>Serial.h</code>. Drop them into any CubeMX project. <strong>Zero dependencies, zero malloc.</strong>' },
    'm-mcu-select-uart': { zh: '在 <code>Serial.h</code> 顶部选择启用哪些串口：', en: 'At the top of <code>Serial.h</code>, select which UARTs to enable:' },
    'm-mcu-step1': { zh: '① 导入文件', en: '① Import Files' },
    'm-mcu-step1-desc': { zh: '将 <code>Serial.c</code> 和 <code>Serial.h</code> 复制到 CubeMX 工程的 <code>Core/Src/</code> 和 <code>Core/Inc/</code>。', en: 'Copy <code>Serial.c</code> and <code>Serial.h</code> to your CubeMX project\'s <code>Core/Src/</code> and <code>Core/Inc/</code> directories.' },
    'm-mcu-step2': { zh: '② ⚠️ 开启串口中断（必须）', en: '② ⚠️ Enable Serial Interrupt (Required)' },
    'm-mcu-step2-desc': { zh: '在 CubeMX 中，对每个要用的 USART：<strong>NVIC Settings</strong> → 勾选 <code>USARTx global interrupt</code> ✅。不勾中断 → 接收功能静默失效。', en: 'In CubeMX, for each USART you use: <strong>NVIC Settings</strong> → check <code>USARTx global interrupt</code> ✅. Without the interrupt checked → receive silently fails.' },
    'm-mcu-step3': { zh: '③ 三行初始化', en: '③ Three-Line Initialization' },
    'm-mcu-step4': { zh: '④ 发送数据到 PC（6 个函数）', en: '④ Send Data to PC (6 Functions)' },
    'm-mcu-step5': { zh: '⑤ 接收 PC 指令', en: '⑤ Receive PC Commands' },
    'm-mcu-step5-desc': { zh: '主循环中轮询接收，用 <code>GetField</code> 拆字段，按 <code>type</code> 路由：', en: 'Poll in main loop, split fields with <code>GetField</code>, route by <code>type</code>:' },
    'm-mcu-step6': { zh: '⑥ 传感数据上报', en: '⑥ Sensor Data Reporting' },
    'm-mcu-step6-desc': { zh: '定时发送传感器读数，PC 端自动建卡。心跳状态卡保持 500ms 间隔：', en: 'Periodically send sensor readings; PC auto-creates cards. Heartbeat status cards at 500ms intervals:' },
    'm-mcu-th-desc': { zh: '说明', en: 'Description' },
    'm-mcu-th-example': { zh: '示例', en: 'Example' },
    'm-mcu-th-func': { zh: '函数', en: 'Function' },
    'm-nav-back-home': { zh: '← 返回首页', en: '← Back to Home' },
    'm-nav-download': { zh: '下载', en: 'Download' },
    'm-oled-11-commands': { zh: '11 种绘图指令', en: '11 Drawing Commands' },
    'm-oled-cmd-arc': { zh: '弧线', en: 'Arc' },
    'm-oled-cmd-circle': { zh: '圆', en: 'Circle' },
    'm-oled-cmd-clear': { zh: '清屏', en: 'Clear Screen' },
    'm-oled-cmd-ellipse': { zh: '椭圆', en: 'Ellipse' },
    'm-oled-cmd-f5': { zh: 'F5 增量同步', en: 'F5 Incremental Sync' },
    'm-oled-cmd-line': { zh: '线', en: 'Line' },
    'm-oled-cmd-point': { zh: '点', en: 'Point' },
    'm-oled-cmd-rect': { zh: '矩形', en: 'Rectangle' },
    'm-oled-cmd-rrect': { zh: '圆角矩形', en: 'Rounded Rectangle' },
    'm-oled-cmd-text': { zh: '文字', en: 'Text' },
    'm-oled-cmd-triangle': { zh: '三角形', en: 'Triangle' },
    'm-oled-cta': { zh: '👆 点击查看完整 MCU 端解析代码 —— GetField · 路由 · F5 · 旋转 · 双屏', en: '👆 Click for complete MCU parsing code — GetField · Routing · F5 · Rotation · Dual Screen' },
    'm-oled-desc': { zh: 'PC 端鼠标绘制图形 → 实时转换为 <code>[draw,...]</code> 协议 → STM32 接收后渲染到物理 OLED/LCD 屏幕。', en: 'Draw shapes with mouse on PC → real-time conversion to <code>[draw,...]</code> protocol → STM32 receives and renders to physical OLED/LCD screen.' },
    'm-oled-features': { zh: '旋转支持：矩形/椭圆支持旋转，<code>a&lt;angle&gt;</code> 协议后缀。F5 增量同步：拖动图形时每次只发 1 帧，MCU 本地维护图形数组。双屏支持：OLED 128×64 + LCD 240×280，注释一行代码即可切换。PC 画板：类似画图软件，鼠标拖拽绘制 → 实时同步到 STM32 物理屏幕。', en: 'Rotation support: rectangles/ellipses support rotation via <code>a&lt;angle&gt;</code> protocol suffix. F5 incremental sync: dragging a shape sends only 1 frame per update; MCU maintains a local shape array. Dual screen: OLED 128×64 + LCD 240×280, switch by commenting one line of code. PC Canvas: like a paint app, drag to draw → real-time sync to STM32 physical screen.' },
    'm-oled-heading': { zh: '🎨 OLED 绘图 & PC 画板', en: '🎨 OLED Drawing & PC Canvas' },
    'm-oled-table-cmd': { zh: '指令', en: 'Command' },
    'm-oled-table-format': { zh: '格式', en: 'Format' },
    'm-panels-heading': { zh: '面板详情', en: 'Panel Details' },
    'm-panels-intro': { zh: '协议格式统一：<code>[type,参数...]</code>，PC 端根据 <code>type</code> 字段自动路由到对应面板。', en: 'Unified protocol format: <code>[type,params...]</code>. PC auto-routes by the <code>type</code> field to the corresponding panel.' },
    'm-plot-desc': { zh: '同一标签页内切换三种视图：时域波形 → 调参工作台（底部抽屉）→ FFT 频谱（⏱/📶 按钮）。通道名自动成为图例，颜色自动分配。', en: 'Three views switchable within one tab: Time Domain Waveform → Tuning Workbench (bottom drawer) → FFT Spectrum (⏱/📶 buttons). Channel names auto-become legends, colors auto-assigned.' },
    'm-plot-fft-desc': { zh: '点 ⏱/📶 按钮一键切换到频域。PC 端自动对 <code>[plot,...]</code> 数据滑窗 FFT，STM32 端零改动。也可由 STM32 端 CMSIS-DSP 计算后直发：', en: 'Click ⏱/📶 buttons to switch to frequency domain in one click. PC auto-performs sliding-window FFT on <code>[plot,...]</code> data; zero changes needed on STM32. Can also be computed by STM32 via CMSIS-DSP and sent directly:' },
    'm-plot-fft-details': { zh: '窗函数：汉宁 / 矩形 / 汉明 / 布莱克曼。频域指标：基频 / 幅度 / THD / SNR / DC 偏置。采样率输入 → X 轴 Bin → Hz 显示。', en: 'Window functions: Hanning / Rectangular / Hamming / Blackman. Frequency metrics: Fundamental / Amplitude / THD / SNR / DC Offset. Sample rate input → X-axis Bin → Hz display.' },
    'm-plot-heading': { zh: '📈 波形图', en: '📈 Waveform Plot' },
    'm-plot-sidebar': { zh: '<strong>侧栏设置：</strong>显示模式（滚动/扫描）、数据点数、Y 轴自动/手动（含上下限）、标点 / 连线 / 数值 HUD 独立开关。🗑 清除全部曲线、📥 导出 CSV。滚轮缩放、右键拖拽平移、悬停查看数值。', en: '<strong>Sidebar Settings:</strong> Display mode (Scroll/Sweep), data point count, Y-axis auto/manual (with upper/lower limits), markers / lines / value HUD independent toggles. 🗑 Clear all curves, 📥 Export CSV. Scroll-wheel zoom, right-drag pan, hover to see values.' },
    'm-plot-signal-analysis': { zh: '<strong>暂停后 📊 详细：</strong>点击 📊 进入信号分析面板——Vpp / Vmax / Vmin / 平均值 / RMS、频率 / 周期 / 占空比 / 高电平时间 / 低电平时间 / 上升时间 / 下降时间、波形类型识别（正弦/方波/三角/锯齿/AM/混合/噪声/阻尼/脉冲）。📄 复制数据、📊 复制统计、📋 全部复制。', en: '<strong>After Pause, 📊 Details:</strong> Click 📊 to enter Signal Analysis panel — Vpp / Vmax / Vmin / Average / RMS, Frequency / Period / Duty Cycle / High Time / Low Time / Rise Time / Fall Time, waveform type identification (Sine/Square/Triangle/Sawtooth/AM/Mixed/Noise/Damped/Pulse). 📄 Copy Data, 📊 Copy Stats, 📋 Copy All.' },
    'm-plot-sub-heading': { zh: '时域波形', en: 'Time Domain Waveform' },
    'm-plot-tuning-desc': { zh: '波形图底部可伸缩抽屉。拖动滑杆调整 PID 参数，同时实时观察波形变化。', en: 'Resizable drawer at the bottom of Waveform Plot. Drag sliders to adjust PID parameters while watching waveform changes in real time.' },
    'm-plot-tuning-heading': { zh: '🎛️ 调参工作台', en: '🎛️ Tuning Workbench' },
    'm-plot-tuning-vm': { zh: '滑杆与 Sliders 面板共享 VM，步长/颜色/范围双向即时生效。+/- 按钮精调 PID。', en: 'Sliders share VM with the Sliders panel; step/color/range changes take effect both ways instantly. +/- buttons for fine PID adjustment.' },
    'm-proto-desc-ctrl': { zh: '开关卡/滑杆卡双向控制', en: 'Switch card / slider card bidirectional control' },
    'm-proto-desc-display': { zh: '旧协议，建议用 <code>[draw,text,...]</code>', en: 'Legacy protocol; recommend using <code>[draw,text,...]</code>' },
    'm-proto-desc-draw': { zh: '11 种绘图指令 + F5 增量同步', en: '11 drawing commands + F5 incremental sync' },
    'm-proto-desc-draw-del': { zh: 'F5 删除图形', en: 'F5 delete shape' },
    'm-proto-desc-draw-set': { zh: 'F5 增量同步——只发变化的图形', en: 'F5 incremental sync — only sends the changed shape' },
    'm-proto-desc-fft': { zh: 'STM32 端 CMSIS-DSP FFT 后发送', en: 'Sent after STM32-side CMSIS-DSP FFT computation' },
    'm-proto-desc-joystick': { zh: '坐标 -100~100，中心=0', en: 'Coordinates -100~100, center=0' },
    'm-proto-desc-key': { zh: '状态="on"/"off"（字符串）', en: 'state="on"/"off" (string)' },
    'm-proto-desc-plot': { zh: '通道名自动成为图例，多通道一条消息', en: 'Channel name auto-becomes legend; multiple channels in one message' },
    'm-proto-desc-sensor': { zh: '8 种卡片类型，零配置自动建卡', en: '8 card types, zero-config auto card creation' },
    'm-proto-desc-slider': { zh: '拖动节流发送，松手发最终值', en: 'Throttled send while dragging, final value on release' },
    'm-proto-heading': { zh: '协议速查', en: 'Protocol Reference' },
    'm-proto-intro': { zh: '全部协议格式、参数、方向、示例。  📤 = MCU→PC   📥 = PC→MCU   🔄 = 双向。', en: 'All protocol formats, parameters, directions, and examples.  📤 = MCU→PC   📥 = PC→MCU   🔄 = Bidirectional.' },
    'm-proto-rule-1': { zh: '方括号 <code>[]</code> 包裹每条消息，逗号分隔字段', en: 'Square brackets <code>[]</code> wrap each message; commas separate fields' },
    'm-proto-rule-2': { zh: '一条消息可含多组方括号：<code>[plot,a,1][plot,b,2]\\r\\n</code>', en: 'One message can contain multiple bracket groups: <code>[plot,a,1][plot,b,2]\\r\\n</code>' },
    'm-proto-rule-3': { zh: '消息以 <code>\\r\\n</code> 结尾（<code>Serial_Printf</code> 自动追加）', en: 'Messages end with <code>\\r\\n</code> (auto-appended by <code>Serial_Printf</code>)' },
    'm-proto-rule-4': { zh: '小数点 <code>.</code> 不是分隔符——<code>42.5</code> 不会被拆断', en: 'The decimal point <code>.</code> is not a separator — <code>42.5</code> won\'t be split' },
    'm-proto-rule-5': { zh: '字段留空用双逗号：<code>[sensor,temp,温度,,85]</code> → 值显示 <code>--</code>', en: 'Empty fields use double commas: <code>[sensor,temp,temp,,85]</code> → value shows <code>--</code>' },
    'm-proto-rules-title': { zh: '📌 协议通用规则：', en: '📌 General Protocol Rules:' },
    'm-proto-th-desc': { zh: '说明', en: 'Description' },
    'm-proto-th-dir': { zh: '方向', en: 'Direction' },
    'm-proto-th-example': { zh: '示例', en: 'Example' },
    'm-proto-th-format': { zh: '格式', en: 'Format' },
    'm-qs-callout-body': { zh: ' 到 <a href="https://github.com/Encaron/SerialMonitor/tree/master/Serial_C_Language" target="_blank">GitHub 下载 STM32 C 库</a>（<code>Serial.c</code> + <code>Serial.h</code>），扔进 CubeMX 工程。配套演示工程在 <code>example/</code> 目录，烧录到 STM32F407 即可看到所有面板的演示数据。', en: ' Download the <a href="https://github.com/Encaron/SerialMonitor/tree/master/Serial_C_Language" target="_blank">STM32 C library from GitHub</a> (<code>Serial.c</code> + <code>Serial.h</code>), drop into your CubeMX project. The companion demo project is in the <code>example/</code> directory — flash to STM32F407 to see demo data on all panels.' },
    'm-qs-callout-q': { zh: '💡 第一次用？', en: '💡 First time?' },
    'm-qs-heading': { zh: '快速入门', en: 'Quick Start' },
    'm-qs-intro': { zh: '打开软件后，三步即可看到第一条数据：', en: 'After opening the app, three steps to see your first data:' },
    'm-qs-no-config': { zh: '无需任何配置。', en: 'No configuration needed.' },
    'm-qs-step1-desc': { zh: ' — 顶栏下拉菜单自动扫描可用串口', en: ' — top-bar dropdown auto-scans available ports' },
    'm-qs-step1-label': { zh: '选择串口', en: 'Select COM Port' },
    'm-qs-step2-desc': { zh: ' — 波特率默认 115200，通常不需要改', en: ' — baud rate defaults to 115200, usually no need to change' },
    'm-qs-step2-label': { zh: '点击连接', en: 'Click Connect' },
    'm-qs-step3-desc': { zh: ' — 数据会自动路由到正确的面板', en: ' — data auto-routes to the correct panel' },
    'm-qs-step3-label': { zh: '切换到对应标签页', en: 'Switch to the Target Tab' },
    'm-qs-stm32-send': { zh: 'STM32 端发一行 <code>[plot,ch1,25.3]</code>，波形图面板即刻显示曲线。', en: 'Send <code>[plot,ch1,25.3]</code> from STM32, and the Waveform Plot panel shows the curve instantly.' },
    'm-qs-warn-body': { zh: ' 这是最容易踩的坑——传感数据、波形、滑杆等常规传输建议 <strong>50ms</strong> 以上间隔，PC 画板绘图建议 <strong>5ms</strong> 以上。想象一下：8 张传感卡片 × 每条 ~40 字节 × 每 5ms 发一次 = 每秒 64KB 塞进串口。PC 端来不及解析渲染，软件直接卡死。<strong>数据不是越快越好——是稳定才好。</strong>', en: ' This is the #1 pitfall — regular data (sensor, waveform, slider) should be sent at <strong>50ms+</strong> intervals; PC Canvas drawing at <strong>5ms+</strong>. Imagine: 8 sensor cards × ~40 bytes each × every 5ms = 64KB/sec flooding the serial port. The PC can\'t parse and render fast enough, and the app freezes. <strong>Data is not about speed — it\'s about stability.</strong>' },
    'm-qs-warn-title': { zh: '⚠️ 发送间隔不能太小！', en: '⚠️ Send Interval Must Not Be Too Small!' },
    'm-sb-brand-docs': { zh: '文档', en: 'Docs' },
    'm-sb-footer-home': { zh: '← 返回首页', en: '← Back to Home' },
    'm-sb-link-faq': { zh: '常见问题', en: 'FAQ' },
    'm-sb-link-fft': { zh: 'FFT 频谱', en: 'FFT Spectrum' },
    'm-sb-link-joystick': { zh: '摇杆面板', en: 'Joystick Panel' },
    'm-sb-link-keys': { zh: '按键面板', en: 'Keys Panel' },
    'm-sb-link-mcu': { zh: 'MCU 对接', en: 'MCU Integration' },
    'm-sb-link-oled': { zh: 'OLED 绘图 & PC 画板', en: 'OLED Drawing & PC Canvas' },
    'm-sb-link-oled-doc': { zh: '→ 完整 MCU 解析文档', en: '→ Full MCU Parsing Reference' },
    'm-sb-link-plot': { zh: '波形图', en: 'Waveform Plot' },
    'm-sb-link-protocol': { zh: '协议速查', en: 'Protocol Reference' },
    'm-sb-link-quickstart': { zh: '快速入门', en: 'Quick Start' },
    'm-sb-link-recv': { zh: '接收区', en: 'Receive Area' },
    'm-sb-link-send': { zh: '发送区', en: 'Send Area' },
    'm-sb-link-sensor': { zh: '传感面板', en: 'Sensor Panel' },
    'm-sb-link-serial': { zh: '收发区', en: 'Serial Transceiver' },
    'm-sb-link-settings': { zh: '设置页', en: 'Settings' },
    'm-sb-link-sliders': { zh: '滑杆面板', en: 'Sliders Panel' },
    'm-sb-link-time': { zh: '时域波形', en: 'Time Domain Waveform' },
    'm-sb-link-tuning': { zh: '调参工作台', en: 'Tuning Workbench' },
    'm-sb-sect-panels': { zh: '面板详情', en: 'Panel Details' },
    'm-sb-sect-ref': { zh: '参考', en: 'Reference' },
    'm-sb-sect-start': { zh: '入门', en: 'Getting Started' },
    'm-sensor-card-battery': { zh: '电池', en: 'Battery' },
    'm-sensor-card-ctrl': { zh: '开关', en: 'Switch' },
    'm-sensor-card-humidity': { zh: '湿度', en: 'Humidity' },
    'm-sensor-card-motor': { zh: '电机', en: 'Motor' },
    'm-sensor-card-pressure': { zh: '气压', en: 'Pressure' },
    'm-sensor-card-slider': { zh: '滑杆', en: 'Slider' },
    'm-sensor-card-status': { zh: '状态', en: 'Status' },
    'm-sensor-card-temp': { zh: '温度', en: 'Temperature' },
    'm-sensor-edit-mode': { zh: '<strong>编辑模式（卡片管理）：</strong>点"编辑"进入——拖拽排序卡片、增删改卡片、分组布局。侧栏切换分组，每组可独立管理。', en: '<strong>Edit Mode (Card Management):</strong> Click "Edit" to enter — drag to reorder cards, add/delete/modify cards, group layout. Switch groups in sidebar; each group independently managed.' },
    'm-sensor-empty-behavior': { zh: '值留空时卡片显示 <code>--</code>，不进迷你波形（不污染图表），辅助参数照常显示。传感器异常不用写额外处理逻辑。', en: 'When value is blank, the card shows <code>--</code>, excluded from the mini waveform (no chart pollution). Auxiliary parameters display normally. No extra handling logic needed for sensor anomalies.' },
    'm-sensor-empty-field': { zh: '中间参数如果读不到，用双逗号留空即可——后面的参数不受影响：', en: 'If a middle parameter is unreadable, leave it blank with double commas — downstream parameters are unaffected:' },
    'm-sensor-heading': { zh: '📡 传感面板', en: '📡 Sensor Panel' },
    'm-sensor-heartbeat-body': { zh: ' 状态卡每次收到 <code>[sensor,status,名,online]</code> 重置 2s 倒计时。超时自动绿变红、online→offline。固件挂了不需要发 offline——"死人不会打电话报丧"。', en: ' Each time a status card receives <code>[sensor,status,name,online]</code>, the 2s countdown resets. Timeout auto green→red, online→offline. Dead firmware doesn\'t need to send offline — "A dead man doesn\'t call to report his death".' },
    'm-sensor-heartbeat-title': { zh: '💡 心跳机制：', en: '💡 Heartbeat Mechanism:' },
    'm-sensor-intro': { zh: 'STM32 发一行 <code>[sensor,temp,芯片温度,42.5]</code>，PC 端自动建卡——零配置，即插即显。', en: 'Send <code>[sensor,temp,chip temp,42.5]</code> from STM32, and the PC auto-creates a card — zero config, plug and display.' },
    'm-sensor-normal-mode': { zh: '<strong>普通模式：</strong>卡片概览。开关卡点一下 → <code>[ctrl,led,名,on]</code> 发回 MCU。滑杆卡拖拽实时回控。数值卡内嵌 30 点迷你面积图，鼠标悬停查看数据点。状态卡 2 秒心跳超时自动绿变红、online→offline。', en: '<strong>Normal Mode:</strong> Card overview. Click a switch card → <code>[ctrl,led,name,on]</code> sent back to MCU. Drag a slider card for real-time control. Value cards embed a 30-point mini area chart; hover to see data points. Status cards auto green→red, online→offline after 2-second heartbeat timeout.' },
    'm-sensor-note-body': { zh: '图中波形由 MCU 端随机数模拟生成，不代表真实传感器数据。实际使用时波形由你的传感器读数决定。', en: ' Waveforms shown are simulated by MCU random number generation and do not represent real sensor data. In actual use, waveforms are determined by your sensor readings.' },
    'm-sensor-note-title': { zh: '💡 注意：', en: '💡 Note:' },
    'm-sensor-sub-heading': { zh: '协议格式', en: 'Protocol Format' },
    'm-sensor-sub-heading-8': { zh: '8 类卡片', en: '8 Card Types' },
    'm-sensor-table-example': { zh: '示例', en: 'Example' },
    'm-sensor-table-format': { zh: '格式', en: 'Format' },
    'm-sensor-table-type': { zh: '类型', en: 'Type' },
    'm-serial-desc': { zh: '串口通信的主标签页，上半部分接收数据，下半部分发送数据。', en: 'The main tab for serial communication. Top half receives data, bottom half sends data.' },
    'm-serial-heading': { zh: '收发区', en: 'Serial Transceiver' },
    'm-serial-recv-desc': { zh: '所有串口数据在此显示。AvalonEdit 虚拟化渲染，高频数据不掉帧。', en: 'All serial data displayed here. AvalonEdit virtualized rendering, no frame drops on high-frequency data.' },
    'm-serial-recv-heading': { zh: '📥 接收区', en: '📥 Receive Area' },
    'm-serial-recv-sidebar': { zh: '<strong>侧栏设置：</strong>时间戳格式（无/HH:mm:ss/HH:mm:ss.fff/yyyy-MM-dd HH:mm:ss）、消息回显、行号显示、系统消息独立显示（三色日志）。换行符可选（\\r\\n / \\n / \\r / 无）。定时发送可设间隔（ms），可选发送后自动清空发送区。接收/发送编码（UTF-8 / GBK）、HEX/文本双模式独立切换。', en: '<strong>Sidebar Settings:</strong> Timestamp format (None/HH:mm:ss/HH:mm:ss.fff/yyyy-MM-dd HH:mm:ss), message echo, line numbers, system messages separate display (tri-color log). Newline optional (\\r\\n / \\n / \\r / None). Timed send with configurable interval (ms), optional auto-clear send area after send. Receive/Send encoding (UTF-8 / GBK), HEX/Text dual mode independently switchable.' },
    'm-serial-recv-toolbar': { zh: '<strong>工具栏：</strong>⏸ 暂停 / 导出日志 / 清空接收区 / 🔍 搜索（Ctrl+F，支持关键字/正则/大小写）/ 📡 筛选（按协议类型过滤显示内容）。', en: '<strong>Toolbar:</strong> ⏸ Pause / Export Log / Clear Receive / 🔍 Search (Ctrl+F, supports keyword/regex/case-sensitive) / 📡 Filter (filter display by protocol type).' },
    'm-serial-send-desc': { zh: '快捷发送 chip 按钮 + 右键编辑删除、发送历史（最近 20 条去重，▾ 下拉）、HEX 实时格式化提醒。Enter = 发送 / Shift+Enter = 换行。', en: 'Quick-send chip buttons + right-click edit/delete, send history (last 20 unique, ▾ dropdown), HEX real-time format hint. Enter = Send / Shift+Enter = Newline.' },
    'm-serial-send-heading': { zh: '📤 发送区', en: '📤 Send Area' },
    'm-settings-about': { zh: 'ℹ 关于', en: 'ℹ About' },
    'm-settings-about-body': { zh: '版本号、作者（冯毅力）、技术栈（.NET 8 / WPF / OxyPlot / AvalonEdit / CommunityToolkit.Mvvm）、.NET 运行时版本。GitHub 仓库地址、用户数据路径、Issue 反馈链接均支持一键复制或浏览器打开。', en: 'Version number, author (Feng Yili), tech stack (.NET 8 / WPF / OxyPlot / AvalonEdit / CommunityToolkit.Mvvm), .NET runtime version. GitHub repository URL, user data path, and Issue feedback link all support one-click copy or browser open.' },
    'm-settings-assets': { zh: '🎨 素材自定义', en: '🎨 Asset Customization' },
    'm-settings-assets-body': { zh: '滑杆轨道和拇指支持自定义颜色。摇杆底板和拇指支持自定义 PNG 图片素材——放入素材文件夹，软件下拉菜单自动识别。删除 PNG 后重启软件，菜单中自动消失。内置风格（默认/极简/经典）始终可用。页面显示当前素材文件夹路径，点一下即可复制。', en: 'Slider tracks and thumbs support custom colors. Joystick base and thumb support custom PNG image assets — drop them into the assets folder for auto-recognition in the software dropdown. Delete a PNG and restart the app; it disappears from the menu automatically. Built-in styles (Default/Minimal/Classic) are always available. The page shows the current assets folder path; click to copy.' },
    'm-settings-desc': { zh: '点击标签栏最右侧的 ⚙ 进入设置页。左侧为导航，右侧为子页面。所有设置实时生效，无需重启。', en: 'Click the ⚙ at the rightmost tab bar to enter Settings. Left side is navigation, right side is sub-pages. All settings take effect immediately, no restart needed.' },
    'm-settings-examples': { zh: '📖 使用示例', en: '📖 Usage Examples' },
    'm-settings-examples-body': { zh: '各面板的协议格式示例，和 MCU 端 <code>Serial_Printf</code> 模板一致。新手可从这里复制粘贴到 STM32 工程验证。', en: 'Protocol format examples for each panel, matching the MCU-side <code>Serial_Printf</code> templates. Beginners can copy-paste from here into their STM32 project for verification.' },
    'm-settings-func-clear-recv': { zh: '清空接收区', en: 'Clear Receive Area' },
    'm-settings-func-clear-send': { zh: '清空发送区', en: 'Clear Send Area' },
    'm-settings-func-newline': { zh: '换行', en: 'Newline' },
    'm-settings-func-open': { zh: '打开 / 关闭串口', en: 'Open / Close Serial Port' },
    'm-settings-func-pause': { zh: '暂停 / 继续显示', en: 'Pause / Resume Display' },
    'm-settings-func-search': { zh: '呼出接收区搜索栏', en: 'Open Receive Area Search Bar' },
    'm-settings-func-send': { zh: '发送', en: 'Send' },
    'm-settings-group-global': { zh: '全局', en: 'Global' },
    'm-settings-group-send': { zh: '发送区', en: 'Send Area' },
    'm-settings-heading': { zh: '设置页', en: 'Settings' },
    'm-settings-i18n-theme': { zh: '🌐 中英双语 + 🌙 主题切换', en: '🌐 Bilingual CN/EN + 🌙 Theme Toggle' },
    'm-settings-i18n-theme-body': { zh: '设置页底部常驻 <strong>中/EN</strong> 和 <strong>☀/🌙</strong> 按钮，和顶栏按钮等价。~550 条翻译全覆盖，21 色 DynamicResource 原地变色。', en: 'Settings page bottom has persistent <strong>CN/EN</strong> and <strong>☀/🌙</strong> buttons, equivalent to the top-bar buttons. ~550 translations with full coverage, 21-color DynamicResource in-place theme switch.' },
    'm-settings-serial': { zh: '📡 串口配置', en: '📡 Serial Configuration' },
    'm-settings-serial-body': { zh: '数据位（5~8）、停止位（1/1.5/2）、校验位（None/Odd/Even）、流控（None/RTS/XOnXOff）。DTR / RTS 控制信号可手动开关。自动重连——串口意外断开后自动尝试重新连接。流量计数保持——串口重开时不重置 TX/RX 字节计数。', en: 'Data bits (5~8), stop bits (1/1.5/2), parity (None/Odd/Even), flow control (None/RTS/XOnXOff). DTR / RTS control signals can be toggled manually. Auto-reconnect — automatically retry connection when serial port unexpectedly disconnects. Traffic count persistence — TX/RX byte counts are not reset when reopening the serial port.' },
    'm-settings-shortcuts': { zh: '⌨ 快捷键提示', en: '⌨ Keyboard Shortcuts' },
    'm-settings-th-func': { zh: '功能', en: 'Function' },
    'm-settings-th-group': { zh: '分组', en: 'Group' },
    'm-settings-th-key': { zh: '快捷键', en: 'Shortcut' },
    'm-sliders-desc': { zh: '拖拽滑杆 → 节流发送 <code>[slider,name,val]</code> → MCU 端实时接收数值。', en: 'Drag sliders → throttled send <code>[slider,name,val]</code> → MCU receives values in real time.' },
    'm-sliders-edit-mode': { zh: '<strong>编辑模式：</strong>点"编辑"进入——+ 添加滑杆、🗑 清空全部。选中滑杆后侧栏编辑：名称、最小值/最大值、步长、发送间隔(ms)、颜色、轨道风格（默认/极简 + 自定义 PNG）、拇指风格（默认/极简 + 自定义 PNG）。🗑 删除此滑杆。', en: '<strong>Edit Mode:</strong> Click "Edit" to enter — + Add slider, 🗑 Clear all. Select a slider to edit in sidebar: name, min/max value, step, send interval (ms), color, track style (Default/Minimal + custom PNG), thumb style (Default/Minimal + custom PNG). 🗑 Delete this slider.' },
    'm-sliders-heading': { zh: '🎚️ 滑杆面板', en: '🎚️ Sliders Panel' },
    'm-sliders-mcu-example': { zh: 'MCU 端接收示例（PWM 调光）', en: 'MCU Receive Example (PWM Dimming)' },
    'm-sliders-normal-mode': { zh: '<strong>普通模式：</strong>侧栏快捷预设（全部归零/置中/全部最大）、当前值/范围/步长/发送值实时显示、协议预览可复制。', en: '<strong>Normal Mode:</strong> Sidebar quick presets (All Zero / Center / All Max), current value/range/step/send value real-time display, protocol preview copyable.' },
    'm-sliders-sub-heading-protocol': { zh: '协议格式', en: 'Protocol Format' },
    'm-title-manual': { zh: '使用说明 — Serial Monitor V2', en: 'User Manual — Serial Monitor V2' },

    /* OLED Page (oled-*) */
    'oled-btt-title': { zh: '↑', en: '↑' },
    'oled-dual-desc': { zh: '演示工程同时支持 OLED 128×64 和 LCD 240×280。切换只需一行：', en: 'The demo project supports both OLED 128×64 and LCD 240×280 simultaneously. Switching takes just one line:' },
    'oled-dual-heading': { zh: 'LCD 端显示', en: 'LCD Display' },
    'oled-dual-img-alt': { zh: 'STM32 LCD 实物', en: 'STM32 LCD hardware' },
    'oled-dual-tbl-color': { zh: '颜色', en: 'Color' },
    'oled-dual-tbl-f5-bg': { zh: 'F5 背景', en: 'F5 Background' },
    'oled-dual-tbl-font-map': { zh: '字号映射', en: 'Font Size Mapping' },
    'oled-dual-tbl-framebuf': { zh: '帧缓冲', en: 'Frame Buffer' },
    'oled-dual-tbl-protocol-prefix': { zh: '协议前缀', en: 'Protocol Prefix' },
    'oled-dual-tbl-resolution': { zh: '分辨率', en: 'Resolution' },
    'oled-dual-val-lcd-color': { zh: 'RGB565（16 位色）', en: 'RGB565 (16-bit color)' },
    'oled-dual-val-lcd-f5bg': { zh: '跟踪 <code>f5_bg</code>（可自定义）', en: 'Tracks <code>f5_bg</code> (customizable)' },
    'oled-dual-val-lcd-font': { zh: '≤12→12，≤16→16，≤24→24，>24→32', en: '≤12→12, ≤16→16, ≤24→24, >24→32' },
    'oled-dual-val-lcd-framebuf': { zh: '无缓冲，直接绘制', en: 'No buffer, direct draw' },
    'oled-dual-val-oled-color': { zh: '单色（1 bit/pixel）', en: 'Monochrome (1 bit/pixel)' },
    'oled-dual-val-oled-f5bg': { zh: '固定黑色', en: 'Fixed black' },
    'oled-dual-val-oled-font': { zh: '≤12→6×8，>12→8×16', en: '≤12→6×8, >12→8×16' },
    'oled-eraser-desc': { zh: 'OLED 是单色的，无法"画个白色矩形"来模拟橡皮擦。PC 画板的橡皮擦工具发送特定暗色——MCU 端识别这些颜色后，对 line 执行清除逻辑，对其他形状跳过绘制。', en: 'OLED is monochrome, so you can\'t "draw a white rectangle" to simulate an eraser. The PC Canvas eraser tool sends specific dark colors — the MCU recognizes these colors and executes clear logic for lines, while skipping drawing for other shapes.' },
    'oled-eraser-heading': { zh: '橡皮擦颜色', en: 'Eraser Color' },
    'oled-f5-code-heading': { zh: 'F5 增量同步 — 完整代码', en: 'F5 Incremental Sync — Full Code' },
    'oled-f5-desc-add': { zh: '存入 f5_cmds[0]，重绘全部', en: 'Stored in f5_cmds[0]; redraw all' },
    'oled-f5-desc-delete': { zh: '移除 f5_cmds[0]，前移数组，重绘全部', en: 'Remove f5_cmds[0]; shift array forward; redraw all' },
    'oled-f5-desc-drag': { zh: '每次只更新 1 条 → 1 次全量重绘', en: 'Only 1 update per frame → 1 full redraw' },
    'oled-f5-desc-update': { zh: '同 ID 覆盖（半径 20→25），重绘全部', en: 'Same ID overwrite (radius 20→25); redraw all' },
    'oled-f5-overview-desc': { zh: 'PC 画板上拖动一个图形时，如果每次都发全量重绘（几十条 draw 命令），串口带宽吃不消。F5 机制：<strong>PC 只发变化的那个图形，MCU 端本地维护一份图形数组，收到新数据后本地全量重绘。</strong>', en: 'When dragging a shape on PC Canvas, sending a full redraw (dozens of draw commands) every time would overwhelm the serial bandwidth. F5 mechanism: <strong>PC only sends the changed shape; MCU maintains a local shape array and does a full local redraw upon receiving new data.</strong>' },
    'oled-f5-overview-heading': { zh: 'F5 增量同步 — 原理', en: 'F5 Incremental Sync — Principle' },
    'oled-f5-overview-key': { zh: '关键：每帧串口只需传 <strong>1 条</strong> set/del 消息，不需要把整个画布的所有图形重发一遍。', en: 'Key point: each frame only needs to send <strong>1</strong> set/del message over serial; no need to re-send every shape on the entire canvas.' },
    'oled-f5-step-add': { zh: '新增图形', en: 'Add Shape' },
    'oled-f5-step-delete': { zh: '删除图形', en: 'Delete Shape' },
    'oled-f5-step-drag': { zh: '拖动过程', en: 'Dragging' },
    'oled-f5-step-update': { zh: '修改图形', en: 'Update Shape' },
    'oled-f5-tbl-mcu-action': { zh: 'MCU 动作', en: 'MCU Action' },
    'oled-f5-tbl-pc-send': { zh: 'PC 发送', en: 'PC Sends' },
    'oled-f5-tbl-step': { zh: '步骤', en: 'Step' },
    'oled-mcu-field-desc': { zh: '所有 draw 协议解析的第一步：从逗号串中拆出字段。下面是可以直接复制的完整实现。', en: 'The first step of all draw protocol parsing: splitting fields from a comma-separated string. Below is a complete implementation you can copy directly.' },
    'oled-mcu-field-example-heading': { zh: '解析示例', en: 'Parsing Example' },
    'oled-mcu-field-heading': { zh: 'MCU 端解析 — GetField', en: 'MCU Parsing — GetField' },
    'oled-mcu-route-desc': { zh: '收到一条方括号消息后，先取第 0 个字段判断是哪种图形，再交给对应 handler。', en: 'After receiving a bracket message, extract field 0 to determine which shape type, then dispatch to the corresponding handler.' },
    'oled-mcu-route-heading': { zh: 'MCU 端解析 — type 路由', en: 'MCU Parsing — type Routing' },
    'oled-mcu-shapes-callout-body': { zh: 'LCD 驱动不提供这些函数，handler 里从零实现了 Bresenham 圆角矩形（~60 行）、中点椭圆算法（~30 行）、atan2f 弧线角度过滤（~40 行）。完整源码见 <a href="https://github.com/Encaron/SerialMonitor/blob/master/Serial_C_Language/example/SerialTest/DrawTest/lcd_draw_test.c" target="_blank">lcd_draw_test.c</a>。OLED 驱动自带全部复杂图形函数，handler 就是简单转发。', en: ' The LCD driver doesn\'t provide these functions; the handler implements them from scratch — Bresenham rounded rectangle (~60 lines), midpoint ellipse algorithm (~30 lines), atan2f arc angle filtering (~40 lines). Full source at <a href="https://github.com/Encaron/SerialMonitor/blob/master/Serial_C_Language/example/SerialTest/DrawTest/lcd_draw_test.c" target="_blank">lcd_draw_test.c</a>. The OLED driver comes with all complex shape functions built-in; handlers are simple pass-throughs.' },
    'oled-mcu-shapes-callout-label': { zh: '📌 复杂图形（椭圆 / 圆角矩形 / 弧线）：', en: '📌 Complex Shapes (Ellipse / Rounded Rectangle / Arc):' },
    'oled-mcu-shapes-circle-tri-heading': { zh: '圆 / 三角形', en: 'Circle / Triangle' },
    'oled-mcu-shapes-clamp-heading': { zh: '坐标钳位 + fill 检测', en: 'Coordinate Clamping + Fill Detection' },
    'oled-mcu-shapes-color-heading': { zh: '颜色解析（LCD 专用）', en: 'Color Parsing (LCD Only)' },
    'oled-mcu-shapes-desc': { zh: '以下以 <strong>LCD（彩色）</strong>为基准——带颜色解析、坐标钳位、旋转检测。OLED 单色版更简单，差异处已标注。', en: 'The following uses <strong>LCD (color)</strong> as the baseline — with color parsing, coordinate clamping, and rotation detection. The OLED monochrome version is simpler; differences are noted.' },
    'oled-mcu-shapes-heading': { zh: 'MCU 端解析 — 图形处理（完整代码）', en: 'MCU Parsing — Shape Handling (Full Code)' },
    'oled-mcu-shapes-pointline-heading': { zh: '点 / 线', en: 'Point / Line' },
    'oled-mcu-shapes-rect-heading': { zh: '矩形（含旋转检测）', en: 'Rectangle (with Rotation Detection)' },
    'oled-mcu-shapes-text-clear-heading': { zh: '文字 / 清屏', en: 'Text / Clear Screen' },
    'oled-nav-dual-screen': { zh: 'LCD 端显示', en: 'LCD Display' },
    'oled-nav-eraser': { zh: '橡皮擦颜色', en: 'Eraser Color' },
    'oled-nav-f5-code': { zh: '完整代码', en: 'Full Code' },
    'oled-nav-f5-overview': { zh: '原理', en: 'Principle' },
    'oled-nav-mcu-field': { zh: 'GetField 拆字段', en: 'GetField Field Splitting' },
    'oled-nav-mcu-route': { zh: 'type 路由', en: 'type Routing' },
    'oled-nav-mcu-shapes': { zh: '图形处理', en: 'Shape Handling' },
    'oled-nav-overview': { zh: '系统架构', en: 'System Architecture' },
    'oled-nav-protocol': { zh: '全部指令', en: 'All Commands' },
    'oled-nav-protocol-display': { zh: 'display 协议', en: 'display Protocol' },
    'oled-nav-protocol-extra': { zh: '可选参数', en: 'Optional Parameters' },
    'oled-nav-quick': { zh: '快速上手', en: 'Quick Start' },
    'oled-nav-rotation': { zh: '旋转支持', en: 'Rotation Support' },
    'oled-overview-desc': { zh: 'PC 端画板用鼠标绘制图形 → 实时转为 <code>[draw,...]</code> 协议通过串口发送 → STM32 端解析 → 渲染到物理 OLED/LCD 屏幕。', en: 'Draw shapes with mouse on PC Canvas → real-time conversion to <code>[draw,...]</code> protocol sent over serial → STM32 parses → renders to physical OLED/LCD screen.' },
    'oled-overview-heading': { zh: '系统架构', en: 'System Architecture' },
    'oled-overview-row1-desc': { zh: 'PC 画板绘制的图形 → MCU 解析 → 驱动 OLED/LCD', en: 'Shapes drawn on PC Canvas → MCU parses → drives OLED/LCD' },
    'oled-overview-row2-desc': { zh: 'MCU 端生成图形 → PC 端 OLED 虚拟屏显示', en: 'MCU generates shapes → displayed on PC OLED virtual screen' },
    'oled-overview-row3-desc': { zh: '旧协议，纯文本渲染（建议迁移到 draw,text）', en: 'Legacy protocol, plain text rendering (migrate to draw,text recommended)' },
    'oled-overview-source-generator': { zh: 'MCU 生成器', en: 'MCU Generator' },
    'oled-overview-source-label': { zh: '源码位置：', en: 'Source Code Locations:' },
    'oled-overview-source-lcd-drv': { zh: 'LCD 驱动', en: 'LCD Driver' },
    'oled-overview-source-oled-drv': { zh: 'OLED 驱动', en: 'OLED Driver' },
    'oled-overview-source-parser': { zh: 'MCU 解析器', en: 'MCU Parser' },
    'oled-overview-source-vscreen': { zh: '虚拟屏 UI', en: 'Virtual Screen UI' },
    'oled-overview-tbl-dir': { zh: '方向', en: 'Direction' },
    'oled-overview-tbl-protocol': { zh: '协议', en: 'Protocol' },
    'oled-overview-tbl-purpose': { zh: '用途', en: 'Purpose' },
    'oled-protocol-callout-body': { zh: '新项目用 <code>[draw,text,...]</code> 替代 <code>[display,...]</code>——display 协议保留仅为兼容旧固件。', en: ' For new projects, use <code>[draw,text,...]</code> instead of <code>[display,...]</code> — the display protocol is retained only for legacy firmware compatibility.' },
    'oled-protocol-callout-label': { zh: '📌 建议：', en: '📌 Recommendation:' },
    'oled-protocol-desc': { zh: '协议外壳：<code>[draw,指令,参数...]</code>。所有坐标以像素为单位。', en: 'Protocol wrapper: <code>[draw,command,params...]</code>. All coordinates in pixels.' },
    'oled-protocol-desc-arc': { zh: 'start,end=角度（度）', en: 'start,end=angles (degrees)' },
    'oled-protocol-desc-circle': { zh: 'cx,cy=圆心', en: 'cx,cy=center' },
    'oled-protocol-desc-clear': { zh: '可选背景色', en: 'Optional background color' },
    'oled-protocol-desc-ellipse': { zh: 'rx,ry=半轴', en: 'rx,ry=semi-axes' },
    'oled-protocol-desc-fill': { zh: '纯色填充（LCD 专用）', en: 'Solid color fill (LCD only)' },
    'oled-protocol-desc-line': { zh: '线段，w=线宽（px）', en: 'Line segment, w=line width (px)' },
    'oled-protocol-desc-point': { zh: '单像素点', en: 'Single pixel' },
    'oled-protocol-desc-rect': { zh: 'x,y=左上角；fill="fill" 则填充', en: 'x,y=top-left corner; fill="fill" to fill' },
    'oled-protocol-desc-rrect': { zh: 'r=圆角半径', en: 'r=corner radius' },
    'oled-protocol-desc-text': { zh: 'size 映射 OLED 字号', en: 'size maps to OLED font size' },
    'oled-protocol-desc-triangle': { zh: '三个顶点', en: 'Three vertices' },
    'oled-protocol-display-heading': { zh: 'display 协议（旧版文本渲染）', en: 'display Protocol (Legacy Text Rendering)' },
    'oled-protocol-display-row1': { zh: '在 PC 端虚拟 OLED 上显示文字', en: 'Display text on PC virtual OLED' },
    'oled-protocol-display-row2': { zh: '带颜色', en: 'With color' },
    'oled-protocol-display-row3': { zh: '清空虚拟屏', en: 'Clear virtual screen' },
    'oled-protocol-display-tbl-format': { zh: '格式', en: 'Format' },
    'oled-protocol-display-tbl-note': { zh: '说明', en: 'Description' },
    'oled-protocol-extra-desc': { zh: '<code>w</code>（线宽）和 <code>fill</code> 均为可选。解析器在两个可能的字段位置检查 <code>"fill"</code> 字符串：', en: '<code>w</code> (line width) and <code>fill</code> are both optional. The parser checks the <code>"fill"</code> string at two possible field positions:' },
    'oled-protocol-extra-heading': { zh: '可选参数规则', en: 'Optional Parameter Rules' },
    'oled-protocol-heading': { zh: '全部绘图指令', en: 'All Drawing Commands' },
    'oled-protocol-name-arc': { zh: '弧线', en: 'Arc' },
    'oled-protocol-name-circle': { zh: '圆', en: 'Circle' },
    'oled-protocol-name-clear': { zh: '清屏', en: 'Clear Screen' },
    'oled-protocol-name-ellipse': { zh: '椭圆', en: 'Ellipse' },
    'oled-protocol-name-fill': { zh: '填充矩形', en: 'Fill Rectangle' },
    'oled-protocol-name-line': { zh: '线', en: 'Line' },
    'oled-protocol-name-point': { zh: '点', en: 'Point' },
    'oled-protocol-name-rect': { zh: '矩形', en: 'Rectangle' },
    'oled-protocol-name-rrect': { zh: '圆角矩形', en: 'Rounded Rectangle' },
    'oled-protocol-name-text': { zh: '文字', en: 'Text' },
    'oled-protocol-name-triangle': { zh: '三角形', en: 'Triangle' },
    'oled-protocol-tbl-cmd': { zh: '指令', en: 'Command' },
    'oled-protocol-tbl-format': { zh: '格式', en: 'Format' },
    'oled-protocol-tbl-note': { zh: '说明', en: 'Description' },
    'oled-quick-callout': { zh: '<strong>PC 端操作：</strong>切换到 OLED 绘图标签页 → 选圆形工具 → 在画布上拖一个圆 → MCU OLED 上实时显示。', en: '<strong>PC Operation:</strong> Switch to the OLED Drawing tab → select the circle tool → drag a circle on the canvas → it appears on the MCU OLED in real time.' },
    'oled-quick-desc': { zh: '最简单的验证：PC 画板画个圆，MCU OLED 上显示出来。', en: 'Simplest verification: draw a circle on PC Canvas, and it appears on the MCU OLED.' },
    'oled-quick-heading': { zh: '快速上手', en: 'Quick Start' },
    'oled-quick-img-alt': { zh: 'PC 画板', en: 'PC Canvas' },
    'oled-quick-mcu-init-title': { zh: 'MCU 端（三行初始化）', en: 'MCU Side (Three-Line Init)' },
    'oled-rotation-desc': { zh: '矩形、圆角矩形、椭圆支持旋转。协议末尾加 <code>a&lt;angle&gt;</code>（角度制）：', en: 'Rectangles, rounded rectangles, and ellipses support rotation. Append <code>a&lt;angle&gt;</code> (in degrees) to the end of the protocol:' },
    'oled-rotation-ellipse-note': { zh: '旋转椭圆的实现类似——对每个扫描线点应用旋转矩阵后填充或描边。', en: 'Rotated ellipse implementation is similar — apply the rotation matrix to each scanline point, then fill or stroke.' },
    'oled-rotation-heading': { zh: '旋转支持', en: 'Rotation Support' },
    'oled-rotation-matrix-desc': { zh: 'MCU 端用旋转矩阵计算四个角点：', en: 'MCU side uses rotation matrix to calculate the four corner points:' },
    'oled-sidebar-brand-ver': { zh: '绘图', en: 'Drawing' },
    'oled-sidebar-footer-index': { zh: '返回首页', en: 'Back to Home' },
    'oled-sidebar-footer-manual': { zh: '← 使用说明', en: '← User Manual' },
    'oled-sidebar-sect-advanced': { zh: '高级', en: 'Advanced' },
    'oled-sidebar-sect-f5': { zh: 'F5 增量同步', en: 'F5 Incremental Sync' },
    'oled-sidebar-sect-mcu': { zh: 'MCU 解析', en: 'MCU Parsing' },
    'oled-sidebar-sect-overview': { zh: '概述', en: 'Overview' },
    'oled-sidebar-sect-protocol': { zh: '协议参考', en: 'Protocol Reference' },
    'oled-title': { zh: 'OLED 绘图 & PC 画板 — Serial Monitor V2', en: 'OLED Drawing & PC Canvas — Serial Monitor V2' },
    'oled-topnav-back': { zh: '← 返回使用说明', en: '← Back to User Manual' },

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
            /* Only on desktop — sidebar has its own scrollbar there.
               On mobile the sidebar is inline, scrolling it scrolls the whole page. */
            if (window.innerWidth > 860) {
                activeLink.scrollIntoView({ block: 'nearest', behavior: 'smooth' });
            }
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
