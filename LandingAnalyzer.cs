using System.Globalization;

namespace FlightDataRecorder;

public sealed record ProcessedTelemetryLines(string FullCsvLine, string MlCsvLine);

public sealed class LandingAnalyzer
{
    private const double UninitPrevOnGround = -1.0;

    private double _prevRadioAltFt;
    private double _prevOnGround = UninitPrevOnGround;
    private double _lastVerticalSpeedFpm;
    private bool _approachLatched;
    private double _radioAltOffset;
    private bool _radioAltCalibrated;
    private int _featureSamples;
    private double _sumVerticalSpeed;
    private double _sumPitch;
    private double _sumAbsLocalizer;
    private double _sumAbsGlideSlope;
    private double _sumAbsSpeedDeviation;
    private double _sumAbsCrosswind;
    private double _sumWeight;
    private double _maxAbsVerticalSpeed;
    private double _maxAbsBank;
    private bool _touchdownCaptured;
    private double _touchdownVsFpm;
    private double _touchdownFlaps;
    private double _touchdownThrust;
    private double _touchdownSpoilers;
    private double _touchdownAutobrake;
    private double _lastLandingScore;

    public void Reset()
    {
        _prevOnGround = UninitPrevOnGround;
        _prevRadioAltFt = 0.0;
        _lastVerticalSpeedFpm = 0.0;
        _approachLatched = false;
        _radioAltOffset = 0.0;
        _radioAltCalibrated = false;
        _featureSamples = 0;
        _sumVerticalSpeed = 0.0;
        _sumPitch = 0.0;
        _sumAbsLocalizer = 0.0;
        _sumAbsGlideSlope = 0.0;
        _sumAbsSpeedDeviation = 0.0;
        _sumAbsCrosswind = 0.0;
        _sumWeight = 0.0;
        _maxAbsVerticalSpeed = 0.0;
        _maxAbsBank = 0.0;
        _touchdownCaptured = false;
        _touchdownVsFpm = 0.0;
        _touchdownFlaps = 0.0;
        _touchdownThrust = 0.0;
        _touchdownSpoilers = 0.0;
        _touchdownAutobrake = 0.0;
        _lastLandingScore = 0.0;
    }

    public ProcessedTelemetryLines? BuildCsvLines(TelemetryData t, long timestampMs, double raThreshold, bool approachOnlyFile)
    {
        if (_prevOnGround < -0.5)
        {
            _prevOnGround = t.OnGround;
            _prevRadioAltFt = t.RadioAltitude;
            _lastVerticalSpeedFpm = t.VerticalSpeed;
            return null;
        }

        bool airborne = t.OnGround < 0.5;
        if (!_approachLatched && airborne && _prevRadioAltFt > raThreshold && t.RadioAltitude <= raThreshold)
        {
            _approachLatched = true;
        }

        double touchdownVsEvent = 0.0;
        if (_approachLatched && _prevOnGround < 0.5 && t.OnGround >= 0.5)
        {
            touchdownVsEvent = _lastVerticalSpeedFpm;
            _touchdownCaptured = true;
            _touchdownVsFpm = touchdownVsEvent;
            _touchdownFlaps = t.FlapsIndex;
            _touchdownThrust = t.ThrustLeverPosition;
            _touchdownSpoilers = t.GroundSpoilersArmed;
            _touchdownAutobrake = t.AutoBrakeSetting;
        }

        if (!_radioAltCalibrated && _prevOnGround < 0.5 && t.OnGround >= 0.5)
        {
            _radioAltOffset = t.RadioAltitude;
            _radioAltCalibrated = true;
        }

        double correctedRa = Math.Max(0.0, t.RadioAltitude - _radioAltOffset);
        bool isFlare = (t.OnGround < 0.5) && correctedRa < 50.0;

        int landingPhase = 0;
        if (_approachLatched)
        {
            landingPhase = t.OnGround >= 0.5 ? 3 : (isFlare ? 2 : 1);
        }

        if (approachOnlyFile && !_approachLatched)
        {
            _prevRadioAltFt = t.RadioAltitude;
            _lastVerticalSpeedFpm = t.VerticalSpeed;
            _prevOnGround = t.OnGround;
            return null;
        }

        double speedDeviation = t.Airspeed - t.TargetAirspeed;
        double relativeWindRad = (t.WindDirection - t.Heading) * Math.PI / 180.0;
        double crosswindComponent = t.WindSpeed * Math.Sin(relativeWindRad);
        double landingScore = CalculateLandingScore(t, speedDeviation, crosswindComponent);
        _lastLandingScore = landingScore;
        UpdateFeatureAggregation(t, speedDeviation, crosswindComponent);

        double brakeMax = t.BrakeLeftPosition > t.BrakeRightPosition ? t.BrakeLeftPosition : t.BrakeRightPosition;
        double manualBrakingApplied = brakeMax > 0.01 ? 1.0 : 0.0;

        string fullLine = FormattableString.Invariant(
            $"{timestampMs},{t.Altitude:F2},{correctedRa:F2},{t.Airspeed:F2},{t.VerticalSpeed:F2},{t.Pitch:F4},{t.Bank:F4},{t.GearPosition:F0},{t.FlapsIndex:F0},{t.TotalWeight:F1},{t.TouchdownVelocity:F4},{t.GlideSlopeError:F4},{t.ElevatorPosition:F4},{t.Latitude:F6},{t.Longitude:F6},{t.Heading:F2},{t.N1Engine1:F2},{t.N1Engine2:F2},{t.FuelTotal:F1},{t.OnGround:F0},{t.DistanceToRunway:F1},{t.LocalizerError:F2},{t.TargetAirspeed:F2},{t.ThrustLeverPosition:F2},{t.ReverseThrustState:F2},{t.GroundSpoilersArmed:F0},{t.GroundSpoilersPosition:F4},{t.AutoBrakeSetting:F0},{t.WindSpeed:F2},{t.WindDirection:F2},{t.AutopilotStatus:F0},{t.AileronPosition:F4},{t.RudderPosition:F4},{t.BtvActive:F0},{manualBrakingApplied:F0},{t.OutsideTemperature:F2},{t.BarometricPressureQnh:F2},{t.RunwayCondition:F0},{t.FmaModeLand:F0},{t.GForce:F3},{t.LocalizerCaptured:F0},{t.GlideSlopeCaptured:F0},{(_approachLatched ? 1 : 0):F0},{landingPhase:F0},{touchdownVsEvent:F2},{landingScore:F1}"
        );

        string mlLine = FormattableString.Invariant(
            $"{t.VerticalSpeed:F2},{t.Pitch:F4},{t.Bank:F4},{t.LocalizerError:F2},{t.GlideSlopeError:F4},{t.Airspeed:F2},{t.TargetAirspeed:F2},{t.WindSpeed:F2},{t.WindDirection:F2},{t.Heading:F2},{speedDeviation:F2},{crosswindComponent:F2},{t.TotalWeight:F1},{t.FlapsIndex:F0},{t.ThrustLeverPosition:F2},{t.GroundSpoilersArmed:F0},{t.AutoBrakeSetting:F0},{landingScore:F1}"
        );

        _prevRadioAltFt = t.RadioAltitude;
        _lastVerticalSpeedFpm = t.VerticalSpeed;
        _prevOnGround = t.OnGround;

        return new ProcessedTelemetryLines(fullLine, mlLine);
    }

    public string? BuildLandingFeaturesCsvLine(string aircraftTitle, long durationMs)
    {
        if (_featureSamples == 0 || !_touchdownCaptured)
        {
            return null;
        }

        double meanVerticalSpeed = _sumVerticalSpeed / _featureSamples;
        double meanPitch = _sumPitch / _featureSamples;
        double meanAbsLocalizer = _sumAbsLocalizer / _featureSamples;
        double meanAbsGlideSlope = _sumAbsGlideSlope / _featureSamples;
        double meanAbsSpeedDeviation = _sumAbsSpeedDeviation / _featureSamples;
        double meanAbsCrosswind = _sumAbsCrosswind / _featureSamples;
        double meanWeight = _sumWeight / _featureSamples;

        return FormattableString.Invariant(
            $"{aircraftTitle},{durationMs},{_featureSamples:F0},{_maxAbsVerticalSpeed:F2},{meanVerticalSpeed:F2},{meanPitch:F4},{_maxAbsBank:F4},{meanAbsLocalizer:F3},{meanAbsGlideSlope:F4},{meanAbsSpeedDeviation:F3},{meanAbsCrosswind:F3},{meanWeight:F1},{_touchdownFlaps:F0},{_touchdownThrust:F2},{_touchdownSpoilers:F0},{_touchdownAutobrake:F0},{_touchdownVsFpm:F2},{_lastLandingScore:F1}"
        );
    }

    private void UpdateFeatureAggregation(TelemetryData t, double speedDeviation, double crosswindComponent)
    {
        _featureSamples++;
        _sumVerticalSpeed += t.VerticalSpeed;
        _sumPitch += t.Pitch;
        _sumAbsLocalizer += Math.Abs(t.LocalizerError);
        _sumAbsGlideSlope += Math.Abs(t.GlideSlopeError);
        _sumAbsSpeedDeviation += Math.Abs(speedDeviation);
        _sumAbsCrosswind += Math.Abs(crosswindComponent);
        _sumWeight += t.TotalWeight;
        _maxAbsVerticalSpeed = Math.Max(_maxAbsVerticalSpeed, Math.Abs(t.VerticalSpeed));
        _maxAbsBank = Math.Max(_maxAbsBank, Math.Abs(t.Bank));
    }

    private static double CalculateLandingScore(TelemetryData t, double speedDeviation, double crosswindComponent)
    {
        double score = 100.0;

        // Core touchdown stability penalties.
        score -= PenaltyFromRange(Math.Abs(t.VerticalSpeed), 120.0, 900.0, 35.0);
        score -= PenaltyFromRange(Math.Abs(t.Pitch - 3.0), 2.0, 10.0, 20.0);
        score -= PenaltyFromRange(Math.Abs(t.Bank), 1.5, 8.0, 20.0);
        score -= PenaltyFromRange(Math.Abs(t.LocalizerError), 2.0, 35.0, 12.0);
        score -= PenaltyFromRange(Math.Abs(t.GlideSlopeError), 0.3, 2.5, 12.0);

        // Energy / environment / SOP penalties.
        score -= PenaltyFromRange(Math.Abs(speedDeviation), 3.0, 20.0, 15.0);
        score -= PenaltyFromRange(Math.Abs(t.ThrustLeverPosition - 20.0), 8.0, 45.0, 12.0);

        if (t.GroundSpoilersArmed < 0.5)
        {
            score -= 5.0;
        }

        if (t.AutoBrakeSetting < 0.5)
        {
            score -= 1.0;
        }

        return Math.Clamp(score, 0.0, 100.0);
    }

    private static double PenaltyFromRange(double value, double noPenaltyLimit, double maxPenaltyLimit, double maxPenalty)
    {
        if (value <= noPenaltyLimit)
        {
            return 0.0;
        }

        if (value >= maxPenaltyLimit)
        {
            return maxPenalty;
        }

        double normalized = (value - noPenaltyLimit) / (maxPenaltyLimit - noPenaltyLimit);
        return normalized * maxPenalty;
    }

    public static double ParseRaThresholdOrDefault(string text, double defaultValue = 3000.0)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsedRa)
            && parsedRa > 50.0
            && parsedRa < 20000.0)
        {
            return parsedRa;
        }

        return defaultValue;
    }
}
