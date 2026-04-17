using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using Microsoft.FlightSimulator.SimConnect;

namespace FlightDataRecorder
{
    public partial class MainWindow : Window
    {
        private readonly SimConnectService simConnectService = new();
        private readonly DataLogger fullDataLogger = new();
        private readonly DataLogger mlDataLogger = new();
        private readonly DataLogger landingFeaturesLogger = new();
        private readonly LandingAnalyzer landingAnalyzer = new();

        private bool isRecording;
        private bool autoStopTriggered;
        private long startTime;
        private string aircraftTitle = "UnknownAircraft";

        private const string FullCsvHeader = "Timestamp_ms,Altitude_ft,RadioAlt_ft,Airspeed_kts,VerticalSpeed_fpm,Pitch_deg,Bank_deg,Gear,Flaps,Weight_kg,Touchdown_fps,GlideSlope_deg,Elevator_pos,Latitude,Longitude,Heading_deg,N1_Eng1_pct,N1_Eng2_pct,Fuel_gal,OnGround,DistRunway_m,Localizer_NAV1_CDI,TargetAirspeed_kts,ThrustLever1_pct,ReverseNozzle1_pct,SpoilersArmed,SpoilersLeft_pos,AutoBrakeSwitch,Wind_kts,Wind_dir_deg,AutopilotMaster,Aileron_pos,Rudder_pos,BTV_autobrakeActive_proxy,ManualBraking_applied,OAT_C,QNH_mb,RunwaySurfaceCondition,FMA_Land_apprActive_proxy,GForce,LocalizerCaptured_NAV1Lock,GlideSlopeCaptured,ApproachLatched,LandingPhase,Touchdown_Vsfpm_Event,LandingScore_0_100";
        private const string MlCsvHeader = "VerticalSpeed_fpm,Pitch_deg,Bank_deg,Localizer_NAV1_CDI,GlideSlope_deg,Airspeed_kts,TargetAirspeed_kts,Wind_kts,Wind_dir_deg,Heading_deg,Speed_Deviation_kts,Crosswind_Component_kts,Weight_kg,Flaps,ThrustLever1_pct,SpoilersArmed,AutoBrakeSwitch,LandingScore_0_100";
        private const string LandingFeaturesHeader = "AircraftTitle,RecordDuration_ms,SampleCount,VerticalSpeedAbsMax_fpm,VerticalSpeedMean_fpm,PitchMean_deg,BankAbsMax_deg,LocalizerAbsMean,GlideSlopeAbsMean_deg,SpeedDeviationAbsMean_kts,CrosswindAbsMean_kts,WeightMean_kg,TouchdownFlaps,TouchdownThrust_pct,TouchdownSpoilersArmed,TouchdownAutobrake,TouchdownVs_fpm,LandingScore_0_100";

        public MainWindow()
        {
            InitializeComponent();
            simConnectService.TelemetryReceived += Simconnect_OnTelemetryReceived;
            simConnectService.SimulatorDisconnected += Simconnect_OnSimulatorDisconnected;
            simConnectService.AircraftTitleReceived += title => aircraftTitle = SanitizeFilePart(title);
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            if (PresentationSource.FromVisual(this) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == simConnectService.MessageId && simConnectService.IsConnected)
            {
                simConnectService.ReceiveMessage();
                handled = true;
            }
            return IntPtr.Zero;
        }

        private void BtnConnect_Click(object sender, RoutedEventArgs e)
        {
            if (!simConnectService.IsConnected)
            {
                try
                {
                    IntPtr handle = new WindowInteropHelper(this).Handle;
                    simConnectService.Connect(handle);
                    simConnectService.RequestAircraftTitle();

                    StatusText.Text = "Status: Połączono z MSFS!";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                    BtnStart.IsEnabled = true;
                }
                catch (COMException)
                {
                    MessageBox.Show("Uruchom najpierw symulator!", "Błąd połączenia", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            if (!simConnectService.IsConnected)
            {
                MessageBox.Show("Najpierw połącz się z MSFS!", "Błąd", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            isRecording = true;
            autoStopTriggered = false;
            startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            landingAnalyzer.Reset();

            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string fullFileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"Lot_{aircraftTitle}_{timestamp}_FULL.csv"
            );
            string mlFileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"Lot_{aircraftTitle}_{timestamp}_ML.csv"
            );
            string featuresFileName = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                $"Lot_{aircraftTitle}_{timestamp}_LANDINGS_FEATURES.csv"
            );

            fullDataLogger.Start(fullFileName, FullCsvHeader);
            mlDataLogger.Start(mlFileName, MlCsvHeader);
            landingFeaturesLogger.Start(featuresFileName, LandingFeaturesHeader);
            simConnectService.StartTelemetry();

            StatusText.Text = "Status: Nagrywanie...";
            StatusText.Foreground = System.Windows.Media.Brushes.OrangeRed;
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
        }

        private void Simconnect_OnTelemetryReceived(TelemetryData t)
        {
            if (!isRecording)
            {
                return;
            }

            double raTh = LandingAnalyzer.ParseRaThresholdOrDefault(TxtRaThresholdFt.Text);
            bool approachOnlyFile = true;
            long ts = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;

            ProcessedTelemetryLines? lines = landingAnalyzer.BuildCsvLines(t, ts, raTh, approachOnlyFile);
            if (lines is not null)
            {
                fullDataLogger.Enqueue(lines.FullCsvLine);
                mlDataLogger.Enqueue(lines.MlCsvLine);
            }

            if (!autoStopTriggered && t.OnGround >= 0.5 && t.Airspeed < 40.0)
            {
                autoStopTriggered = true;
                Dispatcher.InvokeAsync(async () =>
                {
                    await StopRecordingAsync();
                    StatusText.Text = "Status: Auto-stop (poniżej 40 kts) - zapisano CSV!";
                    StatusText.Foreground = System.Windows.Media.Brushes.Green;
                    BtnStart.IsEnabled = true;
                    BtnStop.IsEnabled = false;
                });
            }
        }

        private async void BtnStop_Click(object sender, RoutedEventArgs e)
        {
            await StopRecordingAsync();
            StatusText.Text = "Status: Zapisano do CSV!";
            StatusText.Foreground = System.Windows.Media.Brushes.Green;
            BtnStart.IsEnabled = true;
            BtnStop.IsEnabled = false;
        }

        private async Task StopRecordingAsync()
        {
            isRecording = false;
            autoStopTriggered = false;
            long durationMs = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;
            string? featureLine = landingAnalyzer.BuildLandingFeaturesCsvLine(aircraftTitle, durationMs);
            if (featureLine is not null)
            {
                landingFeaturesLogger.Enqueue(featureLine);
            }

            simConnectService.StopTelemetry();
            await fullDataLogger.StopAsync();
            await mlDataLogger.StopAsync();
            await landingFeaturesLogger.StopAsync();
            landingAnalyzer.Reset();
        }

        private void Simconnect_OnSimulatorDisconnected()
        {
            Dispatcher.Invoke(() =>
            {
                if (isRecording)
                {
                    StopRecordingAsync().GetAwaiter().GetResult();
                }
                StatusText.Text = "Status: Symulator rozłączony";
                StatusText.Foreground = System.Windows.Media.Brushes.Red;
                BtnStart.IsEnabled = false;
                BtnStop.IsEnabled = false;
            });
        }

        private static string SanitizeFilePart(string rawTitle)
        {
            if (string.IsNullOrWhiteSpace(rawTitle))
            {
                return "UnknownAircraft";
            }

            StringBuilder sb = new(rawTitle.Length);
            foreach (char c in rawTitle.Trim())
            {
                sb.Append(Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            }

            return sb.Length == 0 ? "UnknownAircraft" : sb.ToString().Replace(' ', '_');
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            simConnectService.Dispose();
            fullDataLogger.Dispose();
            mlDataLogger.Dispose();
            landingFeaturesLogger.Dispose();
        }
    }
}