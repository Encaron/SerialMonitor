using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Windows.Threading;

namespace 串口助手
{
    public enum PlotDisplayMode { Scroll, Sweep }

    /// <summary>
    /// 波形图 ViewModel —— Phase 3 实现。
    /// </summary>
    public class PlotViewModel
    {
        /// <summary>时域波形 Model（[plot,...] 数据）</summary>
        public PlotModel TimeModel { get; private set; }
        /// <summary>频域频谱 Model（[fft,...] 数据）</summary>
        public PlotModel FreqModel { get; private set; }
        /// <summary>向后兼容：指向 TimeModel，老代码无需改动</summary>
        public PlotModel Model => TimeModel;
        public bool IsPaused { get; set; }
        public bool IsActive { get; set; } = true;
        /// <summary>#8 调度法：同屏拖拽滑杆时用 Background 优先级渲染，让路给 MouseMove</summary>
        public bool UseBackgroundRender { get; set; } = false;
        private bool _backgroundRenderScheduled = false;
        public bool ShowValueHud { get; set; } = true;
        public bool XAxisAutoRange { get; set; } = true;
        public bool YAxisAutoRange { get; set; } = true;
        public int MaxDataPoints { get; set; } = 200;
        public PlotDisplayMode DisplayMode { get; set; } = PlotDisplayMode.Scroll;

        // 扫描模式：固定 X 轴窗口起始时间
        private double _sweepStartX;
        public double YMin { get; set; } = 0;
        public double YMax { get; set; } = 100;
        public bool ShowMarkers { get; set; }
        public bool ShowLines { get; set; } = true;
        public Dictionary<string, double> LatestValues { get; } = new Dictionary<string, double>();

        private readonly Dictionary<string, LineSeries> _series = new Dictionary<string, LineSeries>();
        private readonly DateTimeAxis _xAxis;
        private readonly LinearAxis _yAxis;
        // 频域 FreqModel 内部字段
        private readonly LinearAxis _freqXAxis;
        private readonly LinearAxis _freqYAxis;
        private readonly LineSeries _freqSeries;
        private double[] _freqBins = Array.Empty<double>();
        private int _freqBinCount;
        // PC 端 FFT：从 [plot,...] 历史数据滑窗计算频谱
        private double[] _fftBuffer = Array.Empty<double>();
        private string _fftSourceChannel;          // 从哪条曲线取数据做 FFT（null=未选）
        private string _fftDirectSource;            // 命名的 [fft,...] 源（非 null 时优先，不自动覆盖）
        private int _fftWindowSize = 128;           // FFT 点数，默认 128
        private int _fftWindowType;                  // 0=Hanning, 1=Rectangular, 2=Hamming, 3=Blackman
        private double _fftSampleRate;               // 采样率 Hz（0=未设置，X 轴用 Bin 索引）
        private Complex[] _fftComplex;               // 复用避免每帧分配
        // 命名的 FFT 数据存储：key="fft:name" → (count, bins)
        private readonly Dictionary<string, (int count, double[] bins)> _namedFftData = new();
        private bool _isDark;

        // 刷新限流：30Hz（≈33ms 间隔）
        private DateTime _lastRefresh = DateTime.MinValue;
        private bool _dirty;

        private const double SecPerDay = 86400.0;

        public PlotViewModel()
        {
            // ── 时域 Model ──
            TimeModel = new PlotModel
            {
                Title = null,
                PlotAreaBorderThickness = new OxyThickness(0),
                IsLegendVisible = true,
            };
            TimeModel.Legends.Add(new Legend
            {
                LegendPosition = LegendPosition.LeftTop,
                LegendOrientation = LegendOrientation.Vertical,
            });

            _xAxis = new DateTimeAxis
            {
                Position = AxisPosition.Bottom,
                StringFormat = "HH:mm:ss",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
            };
            TimeModel.Axes.Add(_xAxis);

            _yAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
                Minimum = YMin,
                Maximum = YMax,
            };
            TimeModel.Axes.Add(_yAxis);

            // ── 频域 Model ──
            FreqModel = new PlotModel
            {
                Title = null,
                PlotAreaBorderThickness = new OxyThickness(0),
                IsLegendVisible = false,
            };

            _freqXAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = "Bin",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
                Minimum = 0,
            };
            FreqModel.Axes.Add(_freqXAxis);

            _freqYAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Magnitude",
                MajorGridlineStyle = LineStyle.Dash,
                MinorGridlineStyle = LineStyle.None,
                Minimum = 0,
                Maximum = 1,
            };
            FreqModel.Axes.Add(_freqYAxis);

            _freqSeries = new LineSeries
            {
                Title = "FFT",
                StrokeThickness = 1.5,
                Color = OxyColor.FromRgb(0x0E, 0x63, 0x9C),
                LineStyle = LineStyle.Solid,
                MarkerType = MarkerType.None,
            };
            FreqModel.Series.Add(_freqSeries);

            UpdateThemeColors(false);
        }

        /// <summary>
        /// 扫描所有曲线的数据点，自动计算 Y 轴范围（留 10% 边距）。
        /// 在 Y 轴自动模式下调用。
        /// </summary>
        public void RecalcYAxis()
        {
            if (!YAxisAutoRange) return;

            double min = double.MaxValue, max = double.MinValue;
            // 只扫描可见窗口（最后 MaxDataPoints 个点），避免每帧遍历全部 5000 点
            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls) || ls.Points.Count == 0) continue;
                int startIdx = Math.Max(0, ls.Points.Count - MaxDataPoints);
                for (int i = startIdx; i < ls.Points.Count; i++)
                {
                    double y = ls.Points[i].Y;
                    if (y < min) min = y;
                    if (y > max) max = y;
                }
            }
            if (min < double.MaxValue)
            {
                double margin = (max - min) * 0.1;
                if (margin < 0.01) margin = 1;
                _yAxis.Zoom(min - margin, max + margin);
            }
            else
            {
                _yAxis.Zoom(YMin, YMax);
            }
            // InvalidatePlot 由调用方统一执行
        }

        /// <summary>
        /// 主题切换时更新 OxyPlot 颜色（OxyPlot 不支持 DynamicResource，需手动切换）。
        /// </summary>
        public void UpdateThemeColors(bool isDark)
        {
            _isDark = isDark;

            // 亮色主题：深色文字，浅色网格线
            // 暗色主题：浅色文字，暗色网格线
            var textColor = isDark
                ? OxyColor.FromRgb(0xD4, 0xD4, 0xD4)
                : OxyColor.FromRgb(0x2D, 0x2D, 0x2D);
            var gridColor = isDark
                ? OxyColor.FromRgb(0x3E, 0x3E, 0x42)
                : OxyColor.FromRgb(0xE0, 0xE0, 0xE0);
            var tickColor = isDark
                ? OxyColor.FromRgb(0x5A, 0x5A, 0x5A)
                : OxyColor.FromRgb(0xBB, 0xBB, 0xBB);
            var legendTextColor = isDark
                ? OxyColor.FromRgb(0xD4, 0xD4, 0xD4)
                : OxyColor.FromRgb(0x2D, 0x2D, 0x2D);

            // 时域 Model
            TimeModel.TextColor = textColor;
            TimeModel.PlotAreaBackground = isDark
                ? OxyColor.FromRgb(0x1A, 0x1A, 0x1C)
                : OxyColor.FromRgb(0xFA, 0xFA, 0xFA);

            foreach (var axis in TimeModel.Axes)
            {
                axis.TextColor = textColor;
                axis.TicklineColor = tickColor;
                axis.MajorGridlineColor = gridColor;
            }

            if (TimeModel.Legends.Count > 0)
                TimeModel.Legends[0].LegendTextColor = legendTextColor;

            // 频域 Model（同样色系）
            FreqModel.TextColor = textColor;
            FreqModel.PlotAreaBackground = isDark
                ? OxyColor.FromRgb(0x1A, 0x1A, 0x1C)
                : OxyColor.FromRgb(0xFA, 0xFA, 0xFA);

            foreach (var axis in FreqModel.Axes)
            {
                axis.TextColor = textColor;
                axis.TicklineColor = tickColor;
                axis.MajorGridlineColor = gridColor;
            }

            TimeModel.InvalidatePlot(true);
            FreqModel.InvalidatePlot(true);
        }

        public void OnPlotMessage(string name, double value, DateTime timestamp)
        {
            if (IsPaused) return;

            if (!_series.TryGetValue(name, out var series))
            {
                series = CreateSeries(name);
                _series[name] = series;
                Model.Series.Add(series);
            }

            series.Points.Add(new DataPoint(DateTimeAxis.ToDouble(timestamp), value));
            LatestValues[name] = value;

            // #10 FFT：喂入滑窗 buffer（频域自动频谱）
            FeedPlotToFft(name, value);

            // 内存安全
            if (series.Points.Count > 5000)
                series.Points.RemoveAt(0);

            // 扫描模式：数据点超出窗口 → 清空重画
            if (DisplayMode == PlotDisplayMode.Sweep)
            {
                double sweepSec = MaxDataPoints * 0.05; // N点 × 假设20Hz间隔
                double sweepWidth = DateTimeAxis.ToDouble(_xAxis.ConvertToDateTime(_sweepStartX).AddSeconds(sweepSec)) - _sweepStartX;
                double currentX = DateTimeAxis.ToDouble(timestamp);
                if (_sweepStartX == 0) _sweepStartX = currentX;
                if (currentX - _sweepStartX > sweepWidth)
                {
                    foreach (var s in _series.Values) s.Points.Clear();
                    foreach (var o in Model.Series)
                        if (o is LineSeries l) l.Points.Clear();
                    _sweepStartX = currentX;
                    series = _series[name]; // 重新获取引用
                    series.Points.Add(new DataPoint(currentX, value));
                }
            }

            // 限流 30Hz 刷新（不可见时跳过渲染，数据照存——防切页拖拽卡顿）
            _dirty = true;
            if (IsActive)
            {
                var now = DateTime.Now;
                if ((now - _lastRefresh).TotalMilliseconds >= 33)
                {
                    _lastRefresh = now;
                    _dirty = false;
                    if (DisplayMode == PlotDisplayMode.Scroll)
                        ApplyXAxisWindow();
                    else
                        ApplySweepWindow();
                    RecalcYAxis();
                    if (UseBackgroundRender)
                    {
                        if (!_backgroundRenderScheduled)
                        {
                            _backgroundRenderScheduled = true;
                            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                                DispatcherPriority.Background,
                                new Action(() =>
                                {
                                    _backgroundRenderScheduled = false;
                                    Model.InvalidatePlot(false);
                                }));
                        }
                    }
                    else
                    {
                        Model.InvalidatePlot(true);
                    }
                }
            }
        }

        /// <summary>恢复后重置节流计时器，确保首个数据点立即触发刷新</summary>
        public void OnResumeDrawing()
        {
            _lastRefresh = DateTime.MinValue;
        }

        /// <summary>扫描模式窗口：固定宽度，从 _sweepStartX 开始</summary>
        private void ApplySweepWindow()
        {
            if (_sweepStartX == 0) return;
            double sweepSec = MaxDataPoints * 0.05;
            double endX = DateTimeAxis.ToDouble(_xAxis.ConvertToDateTime(_sweepStartX).AddSeconds(sweepSec));
            _xAxis.Zoom(_sweepStartX, endX);
        }

        /// <summary>
        /// 滚动模式：点索引基线（稳定滚动），时间上下界防抖动 + 防暂停 gap 压扁。
        /// </summary>
        public void ApplyXAxisWindow()
        {
            double windowStart = double.MaxValue;
            double windowEnd = double.MinValue;
            int totalPoints = 0;

            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls) || ls.Points.Count == 0) continue;
                totalPoints += ls.Points.Count;

                // 窗口起点：第 (Count - MaxDataPoints) 个点（原始逻辑，保证滚动稳定）
                int startIdx = ls.Points.Count - MaxDataPoints;
                if (startIdx < 0) startIdx = 0;
                double startX = ls.Points[startIdx].X;
                if (startX < windowStart) windowStart = startX;
                // 窗口终点：最后一个点
                double endX = ls.Points[ls.Points.Count - 1].X;
                if (endX > windowEnd) windowEnd = endX;
            }

            if (totalPoints > 0 && windowStart < windowEnd)
            {
                double windowRange = windowEnd - windowStart;
                // 至少 3 秒宽：防止暂停恢复后窗口只剩几个新点 → 波形平缓
                double minRange = 3.0 / SecPerDay;
                if (windowRange < minRange)
                {
                    windowStart = windowEnd - minRange;
                    windowRange = minRange;
                }
                // 最多 60 秒宽：防止含 gap 时窗口过宽 → 波形被压扁
                double maxRange = 60.0 / SecPerDay;
                if (windowRange > maxRange)
                {
                    windowStart = windowEnd - maxRange;
                    windowRange = maxRange;
                }
                double margin = windowRange * 0.02;
                _xAxis.Zoom(windowStart - margin, windowEnd + margin);
                // InvalidatePlot 由调用方统一执行
            }
        }

        /// <summary>用户手动设置 Y 轴范围（关掉自动时调用）</summary>
        public void SetYRange(double min, double max)
        {
            YMin = min;
            YMax = max;
            _yAxis.Zoom(min, max);
            Model.InvalidatePlot(true);
        }

        /// <summary>强制刷新（暂停/清除/切换模式时调用）</summary>
        public void Flush()
        {
            if (_dirty)
            {
                _dirty = false;
                _lastRefresh = DateTime.Now;
                ApplyXAxisWindow();
                RecalcYAxis();
                Model.InvalidatePlot(true);
            }
        }

        /// <summary>清空频域频谱数据</summary>
        public void ClearFreq()
        {
            _freqSeries.Points.Clear();
            _freqBins = Array.Empty<double>();
            _freqBinCount = 0;
            _fftBuffer = Array.Empty<double>();
            _fftDirectSource = null;
            _namedFftData.Clear();
            FreqModel.InvalidatePlot(true);
        }

        /// <summary>重置频域视图（X/Y 轴回到默认范围）</summary>
        public void ResetFreqView()
        {
            _freqXAxis.Reset();
            _freqYAxis.Reset();
            FreqModel.InvalidatePlot(true);
        }

        /// <summary>
        /// #10 FFT 数据入口：收到 [fft,...] 消息。name=null 为旧格式
        /// </summary>
        public void OnFftMessage(string name, int binCount, double[] bins)
        {
            if (IsPaused) return;

            // 命名 FFT：存储到字典，数据源更新时再画
            if (name != null)
            {
                _namedFftData[name] = (binCount, bins);
                // 如果当前选中的就是这个 fft 源，立即刷新
                if (_fftDirectSource == name)
                    ShowNamedFft(name);
                return;
            }

            // 旧格式无名字：直接覆盖频谱
            _fftDirectSource = null;
            UpdateFreqSpectrum(bins);
        }

        private void UpdateFreqSpectrum(double[] bins)
        {
            _freqBins = bins;
            _freqBinCount = bins.Length;

            _freqSeries.Points.Clear();
            double hzPerBin = _fftSampleRate > 0 ? _fftSampleRate / _fftWindowSize : 1.0;
            for (int i = 0; i < bins.Length; i++)
                _freqSeries.Points.Add(new DataPoint(i * hzPerBin, bins[i]));

            // X 轴范围
            double xMax = bins.Length - 1;
            if (_fftSampleRate > 0)
            {
                _freqXAxis.Title = "Frequency (Hz)";
                xMax = (bins.Length - 1) * hzPerBin;
            }
            else
            {
                _freqXAxis.Title = "Bin";
            }
            _freqXAxis.Zoom(-0.5 * hzPerBin, xMax + 0.5 * hzPerBin);

            double max = bins.Length > 0 ? bins.Max() : 1.0;
            _freqYAxis.Zoom(0, Math.Max(max * 1.15, 0.1));
            FreqModel.InvalidatePlot(true);
        }

        private void ShowNamedFft(string name)
        {
            if (_namedFftData.TryGetValue(name, out var data))
                UpdateFreqSpectrum(data.bins);
        }

        // ══════ 频域分析指标 ══════

        /// <summary>获取当前频谱数据（外部只读）</summary>
        public double[] GetFreqBins() => _freqBins;

        /// <summary>DC 偏置 = bin0 幅度</summary>
        public double GetFreqDcBias()
        {
            if (_freqBins.Length == 0) return 0;
            return _freqBins[0];
        }

        /// <summary>基频峰（bin 索引 + 幅度），排除 DC 和底噪</summary>
        public (int bin, double mag) GetFreqFundamental()
        {
            if (_freqBins.Length < 2) return (0, 0);
            var nonDc = new double[_freqBins.Length - 1];
            Array.Copy(_freqBins, 1, nonDc, 0, nonDc.Length);
            Array.Sort(nonDc);
            double median = nonDc[nonDc.Length / 2];
            double threshold = median * 2;

            int bestBin = 0;
            double bestMag = 0;
            for (int i = 1; i < _freqBins.Length; i++)
            {
                if (_freqBins[i] > threshold && _freqBins[i] > bestMag)
                {
                    bestMag = _freqBins[i];
                    bestBin = i;
                }
            }
            return (bestBin, bestMag);
        }

        /// <summary>基频对应的 Hz 值（需采样率）</summary>
        public double GetFreqFundamentalHz()
        {
            if (_fftSampleRate <= 0) return 0;
            return GetFreqFundamental().bin * _fftSampleRate / _fftWindowSize;
        }

        /// <summary>频率分辨率 Hz/bin</summary>
        public double GetFreqResolution() => _fftSampleRate > 0 ? _fftSampleRate / _fftWindowSize : 0;

        /// <summary>THD = 谐波总功率 / 基频功率（0~1 比例）</summary>
        public double GetFreqTHD()
        {
            var (fundBin, fundMag) = GetFreqFundamental();
            if (fundBin == 0 || fundMag < 0.001) return 0;
            double harmonicPower = 0;
            for (int h = 2; h * fundBin < _freqBins.Length; h++)
            {
                double hMag = _freqBins[h * fundBin];
                harmonicPower += hMag * hMag;
            }
            double fundPower = fundMag * fundMag;
            return harmonicPower / fundPower;
        }

        /// <summary>信噪比 SNR (dB) = 基频功率 / 其余功率</summary>
        public double GetFreqSNR()
        {
            var (fundBin, fundMag) = GetFreqFundamental();
            if (fundBin == 0) return 0;
            double signalPower = fundMag * fundMag;
            double totalPower = 0;
            for (int i = 1; i < _freqBins.Length; i++)
                totalPower += _freqBins[i] * _freqBins[i];
            double noisePower = totalPower - signalPower;
            if (noisePower <= 0) return 99;
            return 10 * Math.Log10(signalPower / noisePower);
        }

        // ══════ PC 端 FFT：从 [plot,...] 历史数据计算频谱 ══════

        /// <summary>设置 FFT 数据源（"plot:chName" 或 "fft:fftName" 或 null=不选）</summary>
        public void SetFftSource(string key)
        {
            _fftDirectSource = null;
            _fftSourceChannel = null;
            if (key == null) return;
            if (key.StartsWith("fft:"))
            {
                _fftDirectSource = key.Substring(4);
                ShowNamedFft(_fftDirectSource);
            }
            else if (key.StartsWith("plot:"))
            {
                _fftSourceChannel = key.Substring(5);
                // 只在首次或换通道时重建 buffer，不丢已攒数据
                if (_fftBuffer.Length != _fftWindowSize)
                    _fftBuffer = new double[_fftWindowSize];
            }
        }

        public string GetFftSourceKey()
        {
            if (_fftDirectSource != null) return $"fft:{_fftDirectSource}";
            if (_fftSourceChannel != null) return $"plot:{_fftSourceChannel}";
            return null;
        }

        public string GetFftChannel() => _fftSourceChannel;

        /// <summary>获取所有命名 FFT 源</summary>
        public List<string> GetNamedFftKeys() => _namedFftData.Keys.ToList();

        /// <summary>设置 FFT 窗口大小（必须是 2 的幂）</summary>
        public void SetFftWindowSize(int size)
        {
            if (size < 16 || size > 4096) return;
            _fftWindowSize = size;
            _fftBuffer = new double[size];
            _fftComplex = null; // 下次重分配
        }
        public int GetFftWindowSize() => _fftWindowSize;

        /// <summary>设置窗函数类型（0=Hanning, 1=Rectangular, 2=Hamming, 3=Blackman）</summary>
        public void SetFftWindowType(int type) { _fftWindowType = type; }
        /// <summary>设置采样率（Hz），0=未设置</summary>
        public void SetFftSampleRate(double hz) { _fftSampleRate = hz; }
        public double GetFftSampleRate() => _fftSampleRate;

        /// <summary>[plot,...] 数据同时喂入 FFT 滑窗 buffer</summary>
        public void FeedPlotToFft(string channelName, double value)
        {
            if (_fftSourceChannel == null || channelName != _fftSourceChannel) return;
            // 确保 buffer 大小匹配
            if (_fftBuffer.Length != _fftWindowSize)
                _fftBuffer = new double[_fftWindowSize];
            // 滑窗：左侧移出，新值追加到末尾
            Array.Copy(_fftBuffer, 1, _fftBuffer, 0, _fftBuffer.Length - 1);
            _fftBuffer[_fftBuffer.Length - 1] = value;
        }

        /// <summary>对滑窗 buffer 执行 FFT，更新 FreqModel 频谱（仅 plot 源有效）</summary>
        public void RecomputeFft()
        {
            // 命名 fft 源优先，不自动覆盖
            if (_fftDirectSource != null) return;
            int n = _fftBuffer.Length;
            if (n == 0) return;

            // 复用 Complex 数组避免每帧 GC 分配
            if (_fftComplex == null || _fftComplex.Length != n)
                _fftComplex = new Complex[n];

            for (int i = 0; i < n; i++)
            {
                double w = _fftWindowType switch
                {
                    1 /*Rectangular*/ => 1.0,
                    2 /*Hamming*/     => 0.54 - 0.46 * Math.Cos(2 * Math.PI * i / (n - 1)),
                    3 /*Blackman*/    => 0.42 - 0.5 * Math.Cos(2 * Math.PI * i / (n - 1)) + 0.08 * Math.Cos(4 * Math.PI * i / (n - 1)),
                    _ /*Hanning*/     => 0.5 * (1 - Math.Cos(2 * Math.PI * i / (n - 1))),
                };
                _fftComplex[i] = new Complex(_fftBuffer[i] * w, 0);
            }

            Fft(_fftComplex);

            // 只取前半（对称性），归一化幅度
            int half = n / 2;
            var mags = new double[half];
            double maxMag = 0;
            for (int i = 0; i < half; i++)
            {
                mags[i] = _fftComplex[i].Magnitude / n;
                if (mags[i] > maxMag) maxMag = mags[i];
            }

            // 更新 FreqModel 数据
            _freqBins = mags;
            _freqBinCount = half;
            _freqSeries.Points.Clear();
            for (int i = 0; i < half; i++)
                _freqSeries.Points.Add(new DataPoint(i, mags[i]));

            _freqXAxis.Zoom(-0.5, half - 0.5);
            _freqYAxis.Zoom(0, Math.Max(maxMag * 1.15, 0.1));
            FreqModel.InvalidatePlot(true);
        }

        /// <summary>Radix-2 Cooley–Tukey FFT（就地）</summary>
        private static void Fft(Complex[] data)
        {
            int n = data.Length;
            // 位反转
            for (int i = 1, j = 0; i < n; i++)
            {
                int bit = n >> 1;
                for (; j >= bit; bit >>= 1) j -= bit;
                j += bit;
                if (i < j) (data[i], data[j]) = (data[j], data[i]);
            }
            // 蝶形
            for (int len = 2; len <= n; len <<= 1)
            {
                double angle = -2 * Math.PI / len;
                Complex wlen = new Complex(Math.Cos(angle), Math.Sin(angle));
                for (int i = 0; i < n; i += len)
                {
                    Complex w = Complex.One;
                    for (int j = 0; j < len / 2; j++)
                    {
                        Complex u = data[i + j];
                        Complex v = data[i + j + len / 2] * w;
                        data[i + j] = u + v;
                        data[i + j + len / 2] = u - v;
                        w *= wlen;
                    }
                }
            }
        }

        /// <summary>频域标点显示切换</summary>
        public void SetFreqMarkers(bool show)
        {
            _freqSeries.MarkerType = show ? MarkerType.Circle : MarkerType.None;
            _freqSeries.MarkerSize = show ? 3 : 0;
            FreqModel.InvalidatePlot(true);
        }

        /// <summary>频域连线显示切换</summary>
        public void SetFreqLines(bool show)
        {
            _freqSeries.LineStyle = show ? LineStyle.Solid : LineStyle.None;
            FreqModel.InvalidatePlot(true);
        }

        /// <summary>导出频域频谱 CSV（Bin, Magnitude）</summary>
        public string ExportFreqCsv()
        {
            if (_freqSeries.Points.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Bin,Magnitude");
            foreach (var pt in _freqSeries.Points)
                sb.AppendLine($"{pt.X:F0},{pt.Y:F6}");
            return sb.ToString();
        }

        public void Clear()
        {
            _dirty = false;
            _series.Clear();
            Model.Series.Clear();
            LatestValues.Clear();
            _sweepStartX = 0;
            Model.InvalidatePlot(true);
        }

        public void ResetView()
        {
            _xAxis.Reset();

            // 扫描所有数据点计算 Y 轴范围
            double min = double.MaxValue, max = double.MinValue;
            foreach (var s in Model.Series)
            {
                if (!(s is LineSeries ls)) continue;
                foreach (var pt in ls.Points)
                {
                    if (pt.Y < min) min = pt.Y;
                    if (pt.Y > max) max = pt.Y;
                }
            }
            if (min < double.MaxValue)
            {
                double margin = (max - min) * 0.1;
                if (margin < 0.01) margin = 1;
                _yAxis.Zoom(min - margin, max + margin);
            }

            Model.InvalidatePlot(true);
        }

        public string ExportCsv()
        {
            var sb = new System.Text.StringBuilder();
            var names = _series.Keys.ToList();
            sb.Append("Timestamp");
            foreach (var name in names) sb.Append("," + name);
            sb.AppendLine();

            int maxCount = 0;
            foreach (var s in _series.Values)
                if (s.Points.Count > maxCount) maxCount = s.Points.Count;

            for (int i = 0; i < maxCount; i++)
            {
                string ts = "";
                var parts = new List<string>();
                foreach (var name in names)
                {
                    var s = _series[name];
                    if (i < s.Points.Count)
                    {
                        var pt = s.Points[i];
                        if (string.IsNullOrEmpty(ts))
                            ts = _xAxis.ConvertToDateTime(pt.X).ToString("yyyy-MM-dd HH:mm:ss.fff");
                        parts.Add(pt.Y.ToString("F4"));
                    }
                    else parts.Add("");
                }
                sb.AppendLine(ts + "," + string.Join(",", parts));
            }

            return sb.ToString();
        }

        public void TogglePause()
        {
            IsPaused = !IsPaused;
            if (IsPaused) Flush();  // 暂停时刷新残留数据
        }

        /// <summary>
        /// 获取某条曲线的原始数据点（用于统计分析）。
        /// </summary>
        public List<DataPoint> GetChannelData(string channelName)
        {
            if (_series.TryGetValue(channelName, out var ls))
                return new List<DataPoint>(ls.Points);
            // 也搜索未注册到 _series 的曲线
            foreach (var s in Model.Series)
            {
                if (s is LineSeries l && l.Title == channelName)
                    return new List<DataPoint>(l.Points);
            }
            return new List<DataPoint>();
        }

        /// <summary>
        /// 获取所有已注册曲线名。
        /// </summary>
        public List<string> GetChannelNames()
        {
            return _series.Keys.ToList();
        }

        /// <summary>
        /// 获取图例显示名（Title）。
        /// </summary>
        public string GetLegendTitle(string channelName)
        {
            return channelName;
        }

        /// <summary>
        /// 暂停/继续切换，同时返回新状态。
        /// Pause → 冻结波形；Resume → 清空冻结标记。
        /// </summary>
        public bool TogglePauseAndReturnState()
        {
            TogglePause();
            return IsPaused;
        }

        public void SetMarkers(bool show)
        {
            ShowMarkers = show;
            foreach (var s in _series.Values)
            {
                s.MarkerType = show ? MarkerType.Circle : MarkerType.None;
                s.MarkerSize = show ? 3 : 0;
            }
            Model.InvalidatePlot(true);
        }

        public void SetLines(bool show)
        {
            ShowLines = show;
            foreach (var s in _series.Values)
            {
                s.LineStyle = show ? LineStyle.Solid : LineStyle.None;
            }
            Model.InvalidatePlot(true);
        }

        private LineSeries CreateSeries(string name)
        {
            var colorIndex = _series.Count % 8;
            OxyColor color;
            switch (colorIndex)
            {
                case 0: color = OxyColor.FromRgb(0x0E, 0x63, 0x9C); break; // 蓝
                case 1: color = OxyColor.FromRgb(0xD4, 0x4E, 0x2A); break; // 橙红
                case 2: color = OxyColor.FromRgb(0x38, 0x9A, 0x38); break; // 绿
                case 3: color = OxyColor.FromRgb(0xC4, 0x7D, 0x2A); break; // 棕
                case 4: color = OxyColor.FromRgb(0x8E, 0x3B, 0x9E); break; // 紫
                case 5: color = OxyColor.FromRgb(0x2A, 0x9D, 0xC4); break; // 青
                case 6: color = OxyColor.FromRgb(0xC4, 0x2A, 0x7A); break; // 粉
                default: color = OxyColor.FromRgb(0xAA, 0xAA, 0xAA); break; // 灰
            }

            return new LineSeries
            {
                Title = name,
                StrokeThickness = 2,
                Color = color,
                MarkerType = ShowMarkers ? MarkerType.Circle : MarkerType.None,
                MarkerSize = ShowMarkers ? 3 : 0,
                MarkerFill = color,
                MarkerStroke = color,
                LineStyle = ShowLines ? LineStyle.Solid : LineStyle.None,
                CanTrackerInterpolatePoints = true,
            };
        }
    }
}
