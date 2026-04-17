using System.Runtime.InteropServices;
using Microsoft.FlightSimulator.SimConnect;

namespace FlightDataRecorder;

public sealed class SimConnectService : IDisposable
{
    private const int WmUserSimconnect = 0x0402;

    private SimConnect? _simconnect;

    private enum Definitions
    {
        TelemetryData,
        AircraftInfo,
    }

    private enum Requests
    {
        TelemetryRequest,
        AircraftInfoRequest,
    }

    public event Action<TelemetryData>? TelemetryReceived;
    public event Action? SimulatorDisconnected;
    public event Action<string>? AircraftTitleReceived;

    public bool IsConnected => _simconnect is not null;
    public int MessageId => WmUserSimconnect;

    public void Connect(IntPtr handle)
    {
        if (_simconnect is not null)
        {
            return;
        }

        _simconnect = new SimConnect("FlightDataRecorder", handle, WmUserSimconnect, null, 0);
        RegisterDefinitions(_simconnect);

        _simconnect.OnRecvSimobjectData += OnRecvSimobjectData;
        _simconnect.OnRecvQuit += (_, _) =>
        {
            StopTelemetry();
            SimulatorDisconnected?.Invoke();
            DisposeSimConnectOnly();
        };
    }

    public void ReceiveMessage()
    {
        _simconnect?.ReceiveMessage();
    }

    public void StartTelemetry()
    {
        _simconnect?.RequestDataOnSimObject(
            Requests.TelemetryRequest,
            Definitions.TelemetryData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.SIM_FRAME,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
    }

    public void StopTelemetry()
    {
        _simconnect?.RequestDataOnSimObject(
            Requests.TelemetryRequest,
            Definitions.TelemetryData,
            SimConnect.SIMCONNECT_OBJECT_ID_USER,
            SIMCONNECT_PERIOD.NEVER,
            SIMCONNECT_DATA_REQUEST_FLAG.DEFAULT,
            0,
            0,
            0);
    }

    public void RequestAircraftTitle()
    {
        _simconnect?.RequestDataOnSimObjectType(
            Requests.AircraftInfoRequest,
            Definitions.AircraftInfo,
            0,
            SIMCONNECT_SIMOBJECT_TYPE.USER);
    }

    private void OnRecvSimobjectData(SimConnect _, SIMCONNECT_RECV_SIMOBJECT_DATA data)
    {
        if (data.dwRequestID == (uint)Requests.TelemetryRequest)
        {
            TelemetryData telemetry = (TelemetryData)data.dwData[0];
            TelemetryReceived?.Invoke(telemetry);
            return;
        }

        if (data.dwRequestID == (uint)Requests.AircraftInfoRequest)
        {
            AircraftInfoData info = (AircraftInfoData)data.dwData[0];
            string title = string.IsNullOrWhiteSpace(info.Title) ? "UnknownAircraft" : info.Title;
            AircraftTitleReceived?.Invoke(title);
        }
    }

    private static void RegisterDefinitions(SimConnect simconnect)
    {
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE ALTITUDE", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "RADIO HEIGHT", "feet", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AIRSPEED INDICATED", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "VERTICAL SPEED", "feet per minute", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE PITCH DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE BANK DEGREES", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "GEAR HANDLE POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "FLAPS HANDLE INDEX", "numbers", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "TOTAL WEIGHT", "kg", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE TOUCHDOWN NORMAL VELOCITY", "feet per second", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "NAV GLIDE SLOPE ERROR", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "ELEVATOR POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE LATITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE LONGITUDE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "PLANE HEADING DEGREES TRUE", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "TURB ENG N1:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "TURB ENG N1:2", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "FUEL TOTAL QUANTITY", "gallons", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "SIM ON GROUND", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "ATC RUNWAY START DISTANCE", "meters", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "NAV CDI:1", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOPILOT AIRSPEED HOLD VAR", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "GENERAL ENG THROTTLE LEVER POSITION:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "TURB ENG REVERSE NOZZLE PERCENT:1", "percent", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "SPOILERS ARMED", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "SPOILERS LEFT POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTO BRAKE SWITCH CB", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AMBIENT WIND VELOCITY", "knots", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AMBIENT WIND DIRECTION", "degrees", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOPILOT MASTER", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AILERON POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "RUDDER POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOBRAKES ACTIVE", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "BRAKE LEFT POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "BRAKE RIGHT POSITION", "position", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AMBIENT TEMPERATURE", "celsius", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "KOHLSMAN SETTING MB:1", "millibars", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "SURFACE CONDITION", "number", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOPILOT APPROACH ACTIVE", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "G FORCE", "gforce", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOPILOT NAV1 LOCK", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);
        simconnect.AddToDataDefinition(Definitions.TelemetryData, "AUTOPILOT GLIDESLOPE ACTIVE", "bool", SIMCONNECT_DATATYPE.FLOAT64, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        simconnect.AddToDataDefinition(Definitions.AircraftInfo, "TITLE", null, SIMCONNECT_DATATYPE.STRING256, 0.0f, SimConnect.SIMCONNECT_UNUSED);

        simconnect.RegisterDataDefineStruct<TelemetryData>(Definitions.TelemetryData);
        simconnect.RegisterDataDefineStruct<AircraftInfoData>(Definitions.AircraftInfo);
    }

    private void DisposeSimConnectOnly()
    {
        if (_simconnect is null)
        {
            return;
        }

        _simconnect.Dispose();
        _simconnect = null;
    }

    public void Dispose()
    {
        StopTelemetry();
        DisposeSimConnectOnly();
    }
}
