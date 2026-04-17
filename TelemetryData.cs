using System.Runtime.InteropServices;

namespace FlightDataRecorder;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct TelemetryData
{
    public double Altitude;
    public double RadioAltitude;
    public double Airspeed;
    public double VerticalSpeed;
    public double Pitch;
    public double Bank;
    public double GearPosition;
    public double FlapsIndex;
    public double TotalWeight;
    public double TouchdownVelocity;
    public double GlideSlopeError;
    public double ElevatorPosition;
    public double Latitude;
    public double Longitude;
    public double Heading;
    public double N1Engine1;
    public double N1Engine2;
    public double FuelTotal;
    public double OnGround;
    public double DistanceToRunway;
    public double LocalizerError;
    public double TargetAirspeed;
    public double ThrustLeverPosition;
    public double ReverseThrustState;
    public double GroundSpoilersArmed;
    public double GroundSpoilersPosition;
    public double AutoBrakeSetting;
    public double WindSpeed;
    public double WindDirection;
    public double AutopilotStatus;
    public double AileronPosition;
    public double RudderPosition;
    public double BtvActive;
    public double BrakeLeftPosition;
    public double BrakeRightPosition;
    public double OutsideTemperature;
    public double BarometricPressureQnh;
    public double RunwayCondition;
    public double FmaModeLand;
    public double GForce;
    public double LocalizerCaptured;
    public double GlideSlopeCaptured;
}

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
public struct AircraftInfoData
{
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string Title;
}
