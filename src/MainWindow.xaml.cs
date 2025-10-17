using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using NAudio.Wave;
using System.Windows.Threading;

namespace LofiVisualiser
{
    public partial class MainWindow : Window
    {
        private WaveInEvent waveIn;
        private WasapiLoopbackCapture loopbackCapture;
        private AudioFileReader audioFile;
        private WaveOutEvent outputDevice;
        private DispatcherTimer renderTimer;
        private DispatcherTimer progressTimer;
        private float[] audioData;

        private string visualStyle = "Bars";
        private Color baseColor = Color.FromArgb(255, 100, 200, 255);
        private double speed = 1.0;
        private double density = 1.0;
        private Random random = new Random();
        private bool isSeeking = false;
        private bool isFullscreen = false;
        private WindowState previousWindowState;
        private double previousWidth;
        private double previousHeight;
        private double previousTop;
        private double previousLeft;

        public MainWindow()
        {
            InitializeComponent();

            // Сохраняем начальные размеры и позицию
            previousWidth = Width;
            previousHeight = Height;
            previousTop = Top;
            previousLeft = Left;
            previousWindowState = WindowState;

            // Setup event handlers
            this.MouseLeftButtonDown += (s, e) => DragMove();
            CloseBtn.Click += (s, e) => Close();
            MinimizeBtn.Click += (s, e) => WindowState = WindowState.Minimized;
            MaximizeBtn.Click += (s, e) => ToggleFullscreen();

            LoadBtn.Click += LoadAudio;
            PlayBtn.Click += PlayAudio;
            PauseBtn.Click += PauseAudio;
            StopBtn.Click += StopAudio;
            StyleCombo.SelectionChanged += StyleChanged;

            // Volume control
            VolumeSlider.ValueChanged += VolumeSlider_ValueChanged;

            // Time slider events
            TimeSlider.AddHandler(MouseLeftButtonDownEvent, new MouseButtonEventHandler(TimeSlider_MouseLeftButtonDown), true);
            TimeSlider.AddHandler(MouseLeftButtonUpEvent, new MouseButtonEventHandler(TimeSlider_MouseLeftButtonUp), true);
            TimeSlider.ValueChanged += TimeSlider_ValueChanged;

            InitializeAudio();
            InitializeVisualization();
            InitializeProgressTimer();
        }

        private void ToggleFullscreen()
        {
            if (!isFullscreen)
            {
                // Переход в полноэкранный режим
                previousWindowState = WindowState;
                previousWidth = Width;
                previousHeight = Height;
                previousTop = Top;
                previousLeft = Left;

                // Скрываем границы и разворачиваем на весь экран
                WindowState = WindowState.Maximized;
                MaximizeBtn.Content = "🗗"; // Иконка восстановления
                isFullscreen = true;
            }
            else
            {
                // Возврат к обычному режиму
                WindowState = previousWindowState;
                Width = previousWidth;
                Height = previousHeight;
                Top = previousTop;
                Left = previousLeft;

                MaximizeBtn.Content = "🗖"; // Иконка разворачивания
                isFullscreen = false;
            }
        }

        private void InitializeAudio()
        {
            try
            {
                audioData = new float[128];
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Audio init: {ex.Message}");
            }
        }

        private void InitializeVisualization()
        {
            renderTimer = new DispatcherTimer();
            renderTimer.Interval = TimeSpan.FromMilliseconds(50);
            renderTimer.Tick += RenderFrame;
            renderTimer.Start();
        }

        private void InitializeProgressTimer()
        {
            progressTimer = new DispatcherTimer();
            progressTimer.Interval = TimeSpan.FromMilliseconds(100);
            progressTimer.Tick += UpdateProgress;
        }

        private void UpdateProgress(object sender, EventArgs e)
        {
            if (audioFile != null && outputDevice != null && outputDevice.PlaybackState == PlaybackState.Playing && !isSeeking)
            {
                TimeSpan currentTime = audioFile.CurrentTime;
                TimeSpan totalTime = audioFile.TotalTime;

                CurrentTimeText.Text = currentTime.ToString(@"mm\:ss");
                TotalTimeText.Text = totalTime.ToString(@"mm\:ss");

                TimeSlider.Maximum = totalTime.TotalSeconds;
                TimeSlider.Value = currentTime.TotalSeconds;
            }
        }

        private void TimeSlider_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isSeeking = true;
        }

        private void TimeSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (audioFile != null && isSeeking)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(TimeSlider.Value);
                isSeeking = false;
            }
        }

        private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // This will be handled by mouse up event
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (outputDevice != null)
            {
                outputDevice.Volume = (float)VolumeSlider.Value;
            }
        }

        private void RenderFrame(object sender, EventArgs e)
        {
            if (VisualCanvas == null || VisualCanvas.ActualWidth == 0)
                return;

            VisualCanvas.Children.Clear();
            GenerateTestData();

            switch (visualStyle)
            {
                case "Bars": DrawBars(); break;
                case "Dots": DrawDots(); break;
                case "Grid": DrawGrid(); break;
            }
        }

        private void GenerateTestData()
        {
            if (audioData == null) audioData = new float[128];

            double time = DateTime.Now.TimeOfDay.TotalSeconds * speed;

            for (int i = 0; i < audioData.Length; i++)
            {
                double x = i / (double)audioData.Length;
                audioData[i] = (float)(
                    Math.Sin(x * 20 + time * 3) * 0.4 +
                    Math.Sin(x * 45 + time * 2) * 0.3 +
                    Math.Sin(x * 80 + time * 4) * 0.2 +
                    (random.NextDouble() - 0.5) * 0.1
                );
            }
        }

        private void DrawBars()
        {
            int barCount = 32;
            double barWidth = VisualCanvas.ActualWidth / barCount;
            double maxHeight = VisualCanvas.ActualHeight * 0.7;

            for (int i = 0; i < barCount; i++)
            {
                int dataIndex = i * audioData.Length / barCount;
                double amplitude = Math.Abs(audioData[dataIndex % audioData.Length]);
                double height = amplitude * maxHeight;

                var bar = new Rectangle
                {
                    Width = barWidth * 0.7,
                    Height = Math.Max(3, height),
                    Fill = new SolidColorBrush(Color.FromArgb(180, baseColor.R, baseColor.G, baseColor.B)),
                    StrokeThickness = 0,
                    RadiusX = 2,
                    RadiusY = 2
                };

                Canvas.SetLeft(bar, i * barWidth + barWidth * 0.15);
                Canvas.SetBottom(bar, 10);
                VisualCanvas.Children.Add(bar);
            }
        }

        private void DrawDots()
        {
            int dotCount = 80;
            double time = DateTime.Now.TimeOfDay.TotalSeconds;

            for (int i = 0; i < dotCount; i++)
            {
                double x = (i / (double)dotCount) * VisualCanvas.ActualWidth;
                int dataIndex = i * audioData.Length / dotCount;
                double amplitude = Math.Abs(audioData[dataIndex % audioData.Length]);
                double y = 20 + amplitude * VisualCanvas.ActualHeight * 0.6;

                double pulse = (Math.Sin(time * 2 + i * 0.2) + 1) * 0.5;
                double size = 3 + amplitude * 10 + pulse * 4;

                var dot = new Ellipse
                {
                    Width = size,
                    Height = size,
                    Fill = new SolidColorBrush(Color.FromArgb(200, baseColor.R, baseColor.G, baseColor.B)),
                    StrokeThickness = 0
                };

                Canvas.SetLeft(dot, x - size / 2);
                Canvas.SetBottom(dot, y);
                VisualCanvas.Children.Add(dot);
            }
        }

        private void DrawGrid()
        {
            int gridSize = 12;
            double cellSize = VisualCanvas.ActualWidth / gridSize;
            double time = DateTime.Now.TimeOfDay.TotalSeconds;

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    double xPos = x * cellSize;
                    double yPos = y * cellSize;

                    double influence = (Math.Abs(audioData[(x * gridSize + y) % audioData.Length]) +
                                      Math.Abs(audioData[(y * gridSize + x) % audioData.Length])) * 0.5;

                    double animation = Math.Sin(time * 3 + x * 0.4 + y * 0.4);
                    double size = cellSize * 0.4 + influence * cellSize * 0.3 + animation * 1.5;

                    var cell = new Rectangle
                    {
                        Width = Math.Max(2, size),
                        Height = Math.Max(2, size),
                        Fill = new SolidColorBrush(Color.FromArgb(150, baseColor.R, baseColor.G, baseColor.B)),
                        StrokeThickness = 0,
                        RadiusX = 1,
                        RadiusY = 1
                    };

                    Canvas.SetLeft(cell, xPos + (cellSize - size) / 2);
                    Canvas.SetTop(cell, yPos + (cellSize - size) / 2);
                    VisualCanvas.Children.Add(cell);
                }
            }
        }

        private void LoadAudio(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog();
            dialog.Filter = "Audio Files|*.mp3;*.wav;*.aac;*.wma;*.m4a";

            if (dialog.ShowDialog() == true)
            {
                try
                {
                    audioFile?.Dispose();
                    outputDevice?.Dispose();

                    audioFile = new AudioFileReader(dialog.FileName);
                    outputDevice = new WaveOutEvent();
                    outputDevice.Init(audioFile);

                    // Set initial volume
                    outputDevice.Volume = (float)VolumeSlider.Value;

                    // Setup progress tracking
                    TimeSlider.Maximum = audioFile.TotalTime.TotalSeconds;
                    TotalTimeText.Text = audioFile.TotalTime.ToString(@"mm\:ss");

                    progressTimer.Start();

                    MessageBox.Show("Audio loaded: " + System.IO.Path.GetFileName(dialog.FileName));
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            }
        }

        private void PlayAudio(object sender, RoutedEventArgs e)
        {
            outputDevice?.Play();
            progressTimer.Start();
        }

        private void PauseAudio(object sender, RoutedEventArgs e)
        {
            outputDevice?.Pause();
            progressTimer.Stop();
        }

        private void StopAudio(object sender, RoutedEventArgs e)
        {
            outputDevice?.Stop();
            audioFile?.Seek(0, System.IO.SeekOrigin.Begin);
            progressTimer.Stop();

            if (audioFile != null)
            {
                CurrentTimeText.Text = "0:00";
                TimeSlider.Value = 0;
            }
        }

        private void StyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (StyleCombo.SelectedItem is ComboBoxItem item)
            {
                visualStyle = item.Content.ToString();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            renderTimer?.Stop();
            progressTimer?.Stop();
            waveIn?.Dispose();
            loopbackCapture?.Dispose();
            audioFile?.Dispose();
            outputDevice?.Dispose();
            base.OnClosed(e);
        }
    }
}